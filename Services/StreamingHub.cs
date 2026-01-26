using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace wenu.Services
{
    public class CallHub : Hub
    {
        private static readonly ConcurrentDictionary<string, CallRoom> _rooms = new();
        private static readonly ConcurrentDictionary<string, UserConnection> _connections = new();
        private static readonly ConcurrentDictionary<string, MediaStream> _streams = new();
        private static readonly ConcurrentDictionary<string, StreamBuffer> _buffers = new();
        private readonly ILogger<CallHub> _logger;

        public CallHub(ILogger<CallHub> logger)
        {
            _logger = logger;
        }

        public async Task JoinCall(string roomId, string userId, string username)
        {
            var connectionId = Context.ConnectionId;

            if (!_rooms.ContainsKey(roomId))
            {
                _rooms[roomId] = new CallRoom 
                { 
                    RoomId = roomId, 
                    Participants = new List<string>(),
                    CreatedAt = DateTime.UtcNow,
                    QualitySettings = new QualitySettings
                    {
                        MaxBitrate = 2500,
                        MinBitrate = 256,
                        AdaptiveBitrate = true,
                        BufferSizeMs = 2000,
                        JitterBufferMs = 200
                    }
                };
            }

            var room = _rooms[roomId];

            var connection = new UserConnection
            {
                ConnectionId = connectionId,
                UserId = userId,
                Username = username,
                RoomId = roomId,
                JoinedAt = DateTime.UtcNow,
                NetworkQuality = new NetworkQuality
                {
                    Bandwidth = 2000,
                    Latency = 50,
                    PacketLoss = 0,
                    Jitter = 10
                },
                CurrentBitrate = room.QualitySettings.MaxBitrate
            };

            _connections[connectionId] = connection;
            room.Participants.Add(connectionId);
            await Groups.AddToGroupAsync(connectionId, roomId);

            // Initialize stream buffer for this connection
            _buffers[connectionId] = new StreamBuffer
            {
                BufferSizeMs = room.QualitySettings.BufferSizeMs,
                JitterBufferMs = room.QualitySettings.JitterBufferMs
            };

            // Start quality monitoring for this connection
            _ = MonitorConnectionQuality(connectionId, roomId);

            var existingParticipants = room.Participants
                .Where(p => p != connectionId && _connections.ContainsKey(p))
                .Select(p => new
                {
                    connectionId = p,
                    userId = _connections[p].UserId,
                    username = _connections[p].Username,
                    quality = _connections[p].NetworkQuality,
                    streams = _streams.Values.Where(s => s.OwnerConnectionId == p).Select(s => new
                    {
                        streamId = s.StreamId,
                        type = s.Type,
                        bitrate = s.CurrentBitrate,
                        hasRedundancy = s.RedundantPaths.Any()
                    })
                })
                .ToList();

            await Clients.Caller.SendAsync("ExistingParticipants", new
            {
                participants = existingParticipants,
                roomSettings = room.QualitySettings,
                bufferSettings = new
                {
                    bufferSizeMs = room.QualitySettings.BufferSizeMs,
                    jitterBufferMs = room.QualitySettings.JitterBufferMs
                }
            });

            await Clients.OthersInGroup(roomId).SendAsync("UserJoined", new 
            { 
                connectionId, 
                userId, 
                username,
                quality = connection.NetworkQuality
            });

            _logger.LogInformation("User {UserId} joined room {RoomId} with {ParticipantCount} participants", 
                userId, roomId, room.Participants.Count);
        }

        public async Task PublishStream(string streamId, string type, int initialBitrate)
        {
            var connectionId = Context.ConnectionId;
            if (!_connections.TryGetValue(connectionId, out var connection))
                return;

            var stream = new MediaStream
            {
                StreamId = streamId,
                OwnerConnectionId = connectionId,
                OwnerUserId = connection.UserId,
                Type = type,
                CurrentBitrate = initialBitrate,
                TargetBitrate = initialBitrate,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastPacketTime = DateTime.UtcNow,
                RedundantPaths = new List<RedundantPath>(),
                Subscribers = new List<string>()
            };

            _streams[streamId] = stream;

            if (_rooms.TryGetValue(connection.RoomId, out var room))
            {
                // Create redundant path for reliability
                var redundantPath = new RedundantPath
                {
                    PathId = $"{streamId}_redundant_1",
                    IsActive = true,
                    Priority = 1,
                    LastUsedAt = DateTime.UtcNow
                };
                stream.RedundantPaths.Add(redundantPath);

                await Clients.OthersInGroup(connection.RoomId).SendAsync("NewStreamAvailable", new
                {
                    streamId,
                    ownerConnectionId = connectionId,
                    ownerUserId = connection.UserId,
                    type,
                    bitrate = initialBitrate,
                    redundantPaths = stream.RedundantPaths.Select(p => p.PathId)
                });

                // Start stream health monitoring
                _ = MonitorStreamHealth(streamId, connection.RoomId);
            }

            _logger.LogInformation("Stream {StreamId} published by user {UserId} with bitrate {Bitrate}kbps", 
                streamId, connection.UserId, initialBitrate);
        }

        public async Task SubscribeToStream(string streamId)
        {
            var connectionId = Context.ConnectionId;
            if (!_connections.TryGetValue(connectionId, out var connection))
                return;

            if (!_streams.TryGetValue(streamId, out var stream))
                return;

            if (!stream.Subscribers.Contains(connectionId))
            {
                stream.Subscribers.Add(connectionId);

                // Adjust bitrate based on subscriber's network quality
                var optimalBitrate = CalculateOptimalBitrate(connection.NetworkQuality);
                
                await Clients.Caller.SendAsync("StreamSubscribed", new
                {
                    streamId,
                    ownerConnectionId = stream.OwnerConnectionId,
                    recommendedBitrate = optimalBitrate,
                    bufferSizeMs = _buffers.TryGetValue(connectionId, out var buffer) ? buffer.BufferSizeMs : 2000,
                    redundantPaths = stream.RedundantPaths.Where(p => p.IsActive).Select(p => p.PathId)
                });

                _logger.LogInformation("User {UserId} subscribed to stream {StreamId} with bitrate {Bitrate}kbps", 
                    connection.UserId, streamId, optimalBitrate);
            }
        }

        public async Task UnsubscribeFromStream(string streamId)
        {
            var connectionId = Context.ConnectionId;
            if (_streams.TryGetValue(streamId, out var stream))
            {
                stream.Subscribers.Remove(connectionId);
                await Clients.Caller.SendAsync("StreamUnsubscribed", new { streamId });
            }
        }

        public async Task UpdateNetworkStats(int bandwidth, int latency, int packetLoss, int jitter)
        {
            var connectionId = Context.ConnectionId;
            if (!_connections.TryGetValue(connectionId, out var connection))
                return;

            connection.NetworkQuality.Bandwidth = bandwidth;
            connection.NetworkQuality.Latency = latency;
            connection.NetworkQuality.PacketLoss = packetLoss;
            connection.NetworkQuality.Jitter = jitter;
            connection.NetworkQuality.LastUpdated = DateTime.UtcNow;

            // Adjust bitrate for all streams this user is subscribed to
            var subscribedStreams = _streams.Values.Where(s => s.Subscribers.Contains(connectionId));
            foreach (var stream in subscribedStreams)
            {
                var newBitrate = CalculateOptimalBitrate(connection.NetworkQuality);
                if (Math.Abs(connection.CurrentBitrate - newBitrate) > 200)
                {
                    connection.CurrentBitrate = newBitrate;
                    await Clients.Caller.SendAsync("BitrateAdjusted", new
                    {
                        streamId = stream.StreamId,
                        newBitrate,
                        reason = "network_condition_change"
                    });
                }
            }

            // Check if quality is degraded and needs redundant path switch
            if (packetLoss > 5 || latency > 200)
            {
                await HandleDegradedConnection(connectionId);
            }
        }

        public async Task ReportStreamHealth(string streamId, int packetLoss, int jitter, long lastPacketTimestamp)
        {
            if (!_streams.TryGetValue(streamId, out var stream))
                return;

            stream.PacketLoss = packetLoss;
            stream.Jitter = jitter;
            stream.LastPacketTime = DateTime.UtcNow;

            // If stream health is poor, trigger recovery
            if (packetLoss > 10 || jitter > 100)
            {
                await RecoverStream(streamId);
            }
        }

        public async Task RequestRedundantPath(string streamId)
        {
            var connectionId = Context.ConnectionId;
            if (!_connections.TryGetValue(connectionId, out var connection))
                return;

            if (!_streams.TryGetValue(streamId, out var stream))
                return;

            // Find or create an available redundant path
            var redundantPath = stream.RedundantPaths.FirstOrDefault(p => p.IsActive) 
                ?? new RedundantPath
                {
                    PathId = $"{streamId}_redundant_{stream.RedundantPaths.Count + 1}",
                    IsActive = true,
                    Priority = stream.RedundantPaths.Count + 1,
                    LastUsedAt = DateTime.UtcNow
                };

            if (!stream.RedundantPaths.Contains(redundantPath))
            {
                stream.RedundantPaths.Add(redundantPath);
            }

            await Clients.Caller.SendAsync("RedundantPathReady", new
            {
                streamId,
                pathId = redundantPath.PathId,
                priority = redundantPath.Priority
            });

            _logger.LogInformation("Redundant path {PathId} provided for stream {StreamId} to user {UserId}", 
                redundantPath.PathId, streamId, connection.UserId);
        }

        public async Task SwitchToRedundantPath(string streamId, string pathId)
        {
            var connectionId = Context.ConnectionId;
            if (!_streams.TryGetValue(streamId, out var stream))
                return;

            var path = stream.RedundantPaths.FirstOrDefault(p => p.PathId == pathId);
            if (path != null)
            {
                path.LastUsedAt = DateTime.UtcNow;
                await Clients.Caller.SendAsync("RedundantPathActivated", new { streamId, pathId });
                
                _logger.LogInformation("Switched to redundant path {PathId} for stream {StreamId}", pathId, streamId);
            }
        }

        public async Task AdjustBuffer(int bufferSizeMs)
        {
            var connectionId = Context.ConnectionId;
            if (_buffers.TryGetValue(connectionId, out var buffer))
            {
                buffer.BufferSizeMs = Math.Clamp(bufferSizeMs, 500, 5000);
                await Clients.Caller.SendAsync("BufferAdjusted", new { bufferSizeMs = buffer.BufferSizeMs });
            }
        }

        public async Task SendOffer(string targetConnectionId, object offer, string mediaType)
        {
            await Clients.Client(targetConnectionId).SendAsync("ReceiveOffer", new 
            { 
                senderConnectionId = Context.ConnectionId, 
                offer, 
                mediaType 
            });
        }

        public async Task SendAnswer(string targetConnectionId, object answer, string mediaType)
        {
            await Clients.Client(targetConnectionId).SendAsync("ReceiveAnswer", new 
            { 
                senderConnectionId = Context.ConnectionId, 
                answer, 
                mediaType 
            });
        }

        public async Task SendIceCandidate(string targetConnectionId, object candidate)
        {
            await Clients.Client(targetConnectionId).SendAsync("ReceiveIceCandidate", new 
            { 
                senderConnectionId = Context.ConnectionId, 
                candidate 
            });
        }

        public async Task ToggleMedia(string mediaType, bool enabled)
        {
            var connectionId = Context.ConnectionId;
            if (_connections.TryGetValue(connectionId, out var userConnection))
            {
                await Clients.OthersInGroup(userConnection.RoomId).SendAsync("MediaToggled", new 
                { 
                    connectionId, 
                    mediaType, 
                    enabled 
                });
            }
        }

        public async Task StartScreenShare()
        {
            var connectionId = Context.ConnectionId;
            if (_connections.TryGetValue(connectionId, out var userConnection))
            {
                await Clients.OthersInGroup(userConnection.RoomId).SendAsync("ScreenShareStarted", new 
                { 
                    connectionId 
                });
            }
        }

        public async Task StopScreenShare()
        {
            var connectionId = Context.ConnectionId;
            if (_connections.TryGetValue(connectionId, out var userConnection))
            {
                await Clients.OthersInGroup(userConnection.RoomId).SendAsync("ScreenShareStopped", new 
                { 
                    connectionId 
                });
            }
        }

        public async Task SendMessage(string message)
        {
            var connectionId = Context.ConnectionId;
            if (_connections.TryGetValue(connectionId, out var userConnection))
            {
                await Clients.Group(userConnection.RoomId).SendAsync("ReceiveMessage", new
                {
                    userId = userConnection.UserId,
                    username = userConnection.Username,
                    message
                });
            }
        }

        public async Task LeaveCall()
        {
            var connectionId = Context.ConnectionId;
            if (_connections.TryRemove(connectionId, out var userConnection))
            {
                var roomId = userConnection.RoomId;

                // Clean up streams owned by this user
                var ownedStreams = _streams.Values.Where(s => s.OwnerConnectionId == connectionId).ToList();
                foreach (var stream in ownedStreams)
                {
                    stream.IsActive = false;
                    _streams.TryRemove(stream.StreamId, out _);
                }

                // Remove from subscriptions
                foreach (var stream in _streams.Values)
                {
                    stream.Subscribers.Remove(connectionId);
                }

                // Clean up buffer
                _buffers.TryRemove(connectionId, out _);

                if (_rooms.TryGetValue(roomId, out var room))
                {
                    room.Participants.Remove(connectionId);
                    if (room.Participants.Count == 0)
                    {
                        _rooms.TryRemove(roomId, out _);
                    }
                }

                await Clients.OthersInGroup(roomId).SendAsync("UserLeft", new 
                { 
                    connectionId, 
                    userId = userConnection.UserId 
                });

                await Groups.RemoveFromGroupAsync(connectionId, roomId);

                _logger.LogInformation("User {UserId} left room {RoomId}", userConnection.UserId, roomId);
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await LeaveCall();
            await base.OnDisconnectedAsync(exception);
        }

        private async Task MonitorConnectionQuality(string connectionId, string roomId)
        {
            while (_connections.ContainsKey(connectionId))
            {
                await Task.Delay(5000);

                if (!_connections.TryGetValue(connectionId, out var connection))
                    break;

                if (DateTime.UtcNow - connection.NetworkQuality.LastUpdated > TimeSpan.FromSeconds(15))
                {
                    await Clients.Client(connectionId).SendAsync("RequestNetworkStats");
                }
            }
        }

        private async Task MonitorStreamHealth(string streamId, string roomId)
        {
            while (_streams.ContainsKey(streamId))
            {
                await Task.Delay(3000);

                if (!_streams.TryGetValue(streamId, out var stream))
                    break;

                if (!stream.IsActive)
                    break;

                var timeSinceLastPacket = DateTime.UtcNow - stream.LastPacketTime;
                if (timeSinceLastPacket.TotalSeconds > 5)
                {
                    await RecoverStream(streamId);
                }
            }
        }

        private async Task HandleDegradedConnection(string connectionId)
        {
            if (!_connections.TryGetValue(connectionId, out var connection))
                return;

            var subscribedStreams = _streams.Values.Where(s => s.Subscribers.Contains(connectionId));
            
            foreach (var stream in subscribedStreams)
            {
                var redundantPath = stream.RedundantPaths.FirstOrDefault(p => p.IsActive);
                if (redundantPath != null)
                {
                    await Clients.Client(connectionId).SendAsync("SwitchToRedundantPath", new
                    {
                        streamId = stream.StreamId,
                        pathId = redundantPath.PathId,
                        reason = "degraded_connection"
                    });
                }
            }

            _logger.LogWarning("Degraded connection detected for user {UserId}, switching to redundant paths", 
                connection.UserId);
        }

        private async Task RecoverStream(string streamId)
        {
            if (!_streams.TryGetValue(streamId, out var stream))
                return;

            if (_rooms.TryGetValue(_connections[stream.OwnerConnectionId].RoomId, out var room))
            {
                await Clients.Group(room.RoomId).SendAsync("StreamRecovering", new
                {
                    streamId,
                    ownerConnectionId = stream.OwnerConnectionId
                });

                await Clients.Client(stream.OwnerConnectionId).SendAsync("RecoverStream", new
                {
                    streamId,
                    reason = "packet_loss_or_timeout"
                });
            }

            _logger.LogWarning("Stream recovery initiated for stream {StreamId}", streamId);
        }

        private int CalculateOptimalBitrate(NetworkQuality quality)
        {
            var baseBitrate = 2000;

            if (quality.Bandwidth < 500)
                baseBitrate = 256;
            else if (quality.Bandwidth < 1000)
                baseBitrate = 512;
            else if (quality.Bandwidth < 1500)
                baseBitrate = 1000;
            else if (quality.Bandwidth < 2000)
                baseBitrate = 1500;

            if (quality.PacketLoss > 5)
                baseBitrate = (int)(baseBitrate * 0.7);

            if (quality.Latency > 150)
                baseBitrate = (int)(baseBitrate * 0.8);

            return Math.Clamp(baseBitrate, 256, 2500);
        }
    }

    public class CallRoom
    {
        public string RoomId { get; set; } = string.Empty;
        public List<string> Participants { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public QualitySettings QualitySettings { get; set; } = new();
    }

    public class UserConnection
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string RoomId { get; set; } = string.Empty;
        public DateTime JoinedAt { get; set; }
        public NetworkQuality NetworkQuality { get; set; } = new();
        public int CurrentBitrate { get; set; }
    }

    public class MediaStream
    {
        public string StreamId { get; set; } = string.Empty;
        public string OwnerConnectionId { get; set; } = string.Empty;
        public string OwnerUserId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int CurrentBitrate { get; set; }
        public int TargetBitrate { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastPacketTime { get; set; }
        public int PacketLoss { get; set; }
        public int Jitter { get; set; }
        public List<RedundantPath> RedundantPaths { get; set; } = new();
        public List<string> Subscribers { get; set; } = new();
    }

    public class RedundantPath
    {
        public string PathId { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int Priority { get; set; }
        public DateTime LastUsedAt { get; set; }
    }

    public class NetworkQuality
    {
        public int Bandwidth { get; set; }
        public int Latency { get; set; }
        public int PacketLoss { get; set; }
        public int Jitter { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    public class QualitySettings
    {
        public int MaxBitrate { get; set; }
        public int MinBitrate { get; set; }
        public bool AdaptiveBitrate { get; set; }
        public int BufferSizeMs { get; set; }
        public int JitterBufferMs { get; set; }
    }

    public class StreamBuffer
    {
        public int BufferSizeMs { get; set; }
        public int JitterBufferMs { get; set; }
        public Queue<byte[]> Buffer { get; set; } = new();
    }
}