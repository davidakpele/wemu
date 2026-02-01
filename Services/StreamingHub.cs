// src/Services/StreamingHub.cs

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace wenu.Services
{
    public class StreamingHub : Hub
    {
        private static readonly ConcurrentDictionary<string, StreamRoom> _streamRooms = new();
        private static readonly ConcurrentDictionary<string, StreamConnection> _streamConnections = new();
        private static readonly ConcurrentDictionary<string, CancellationTokenSource> _autoCloseTimers = new();
        private readonly ILogger<StreamingHub> _logger;
        private readonly MediaServer _mediaServer;

        public StreamingHub(ILogger<StreamingHub> logger, MediaServer mediaServer)
        {
            _logger = logger;
            _mediaServer = mediaServer;
        }

        // Static methods to access stream rooms from API controllers
        public static List<StreamRoom> GetActiveStreams()
        {
            return _streamRooms.Values
                .Where(room => room.State == "live")
                .OrderByDescending(room => room.CurrentViewers)
                .ToList();
        }

        public static StreamRoom GetStreamRoom(string roomId)
        {
            _streamRooms.TryGetValue(roomId, out var room);
            return room;
        }

        public async Task StartStream(string username, int userId, string title, string description, string category, string visibility, string type)
        {
            var connectionId = Context.ConnectionId;
            var roomId = GenerateRoomId();

            var streamRoom = new StreamRoom
            {
                RoomId = roomId,
                Title = title,
                Description = description,
                Category = category,
                Visibility = visibility,
                State = "live",
                StartTime = DateTime.UtcNow,
                EndTime = null,
                CurrentViewers = 0,
                Host = new HostInfo
                {
                    Id = userId,
                    Username = username,
                    ConnectionId = connectionId,
                    CoHosts = new List<CoHostInfo>()
                },
                // *** NEW: Store original host details for tracking ***
                OriginalHostId = userId,
                OriginalHostUsername = username,
                IsHostPresent = true,
                Participants = new ParticipantsList
                {
                    UsersList = new List<ParticipantInfo>(),
                    TotalMembers = 0
                },
                MediaSettings = new MediaSettings
                {
                    AudioEnabled = type == "video" || type == "audio",
                    VideoEnabled = type == "video",
                    ScreenShareEnabled = false,
                    RecordingEnabled = false
                },
                Permissions = new StreamPermissions
                {
                    CanChat = true,
                    CanRaiseHand = true,
                    CanShareScreen = false
                },
                MessageRoom = new MessageRoom
                {
                    RoomId = roomId,
                    ChatEnabled = true,
                    MessageRetention = "live_only",
                    Messages = new List<ChatMessage>(),
                    Moderation = new ModerationInfo
                    {
                        MutedUsers = new List<string>(),
                        DeletedMessages = new List<string>()
                    }
                },
                Realtime = new RealtimeInfo
                {
                    ReactionsEnabled = true,
                    Events = new List<StreamEvent>()
                },
                BlockedUsers = new List<int>()
            };

            _streamRooms[roomId] = streamRoom;

            var hostConnection = new StreamConnection
            {
                ConnectionId = connectionId,
                UserId = userId,
                Username = username,
                RoomId = roomId,
                Role = "host",
                JoinedAt = DateTime.UtcNow
            };

            _streamConnections[connectionId] = hostConnection;

            await Groups.AddToGroupAsync(connectionId, roomId);

            _mediaServer.GetOrCreateRoom(roomId);

            var response = new
            {
                data = new
                {
                    stream = new
                    {
                        roomid = streamRoom.RoomId,
                        title = streamRoom.Title,
                        description = streamRoom.Description,
                        category = streamRoom.Category,
                        visibility = streamRoom.Visibility
                    },
                    status = new
                    {
                        state = streamRoom.State,
                        start_time = streamRoom.StartTime,
                        end_time = streamRoom.EndTime,
                        current_viewers = streamRoom.CurrentViewers
                    },
                    userdetails = new
                    {
                        username = username,
                        id = userId,
                        role = "host"
                    },
                    host = new
                    {
                        id = streamRoom.Host.Id,
                        username = streamRoom.Host.Username,
                        coHosts = streamRoom.Host.CoHosts
                    },
                    participants = new
                    {
                        users_list = streamRoom.Participants.UsersList,
                        total_members = streamRoom.Participants.TotalMembers
                    },
                    media_settings = streamRoom.MediaSettings,
                    permissions = streamRoom.Permissions,
                    message_room = streamRoom.MessageRoom,
                    realtime = streamRoom.Realtime
                }
            };

            await Clients.Caller.SendAsync("StreamStarted", response);

            _logger.LogInformation("User {Username} (ID: {UserId}) started stream {RoomId}", username, userId, roomId);
        }

        public async Task JoinStream(string roomId, int userId, string username)
        {
            var connectionId = Context.ConnectionId;

            if (!_streamRooms.TryGetValue(roomId, out var room))
            {
                await Clients.Caller.SendAsync("Error", new { message = "Stream room not found" });
                _logger.LogWarning("User {Username} ({UserId}) attempted to join non-existent room {RoomId}", 
                    username, userId, roomId);
                return;
            }

            // *** Check if user is blocked ***
            if (room.BlockedUsers != null && room.BlockedUsers.Contains(userId))
            {
                await Clients.Caller.SendAsync("Error", new { message = "You have been blocked from this stream" });
                _logger.LogWarning("Blocked user {Username} ({UserId}) attempted to join room {RoomId}", 
                    username, userId, roomId);
                return;
            }

            // *** NEW: Check if this is the original host rejoining ***
            bool isOriginalHost = userId == room.OriginalHostId;
            string userRole = "viewer";

            if (isOriginalHost)
            {
                userRole = "host";
                
                // Cancel any pending auto-close timer
                if (_autoCloseTimers.TryRemove(roomId, out var cts))
                {
                    cts.Cancel();
                    cts.Dispose();
                    _logger.LogInformation("Cancelled auto-close timer for room {RoomId} - host {Username} rejoined", 
                        roomId, username);
                }

                // Update host info
                room.Host.ConnectionId = connectionId;
                room.IsHostPresent = true;

                _logger.LogInformation("Original host {Username} ({UserId}) rejoined room {RoomId}", 
                    username, userId, roomId);
            }

            var participant = new ParticipantInfo
            {
                Id = userId.ToString(),
                Username = username,
                Role = userRole,
                ConnectionId = connectionId
            };

            room.Participants.UsersList.Add(participant);
            room.Participants.TotalMembers = room.Participants.UsersList.Count;
            room.CurrentViewers++;

            var connection = new StreamConnection
            {
                ConnectionId = connectionId,
                UserId = userId,
                Username = username,
                RoomId = roomId,
                Role = userRole,
                JoinedAt = DateTime.UtcNow
            };

            _streamConnections[connectionId] = connection;

            await Groups.AddToGroupAsync(connectionId, roomId);

            // Notify host and co-hosts (if not the host rejoining)
            if (!isOriginalHost && room.Host != null && !string.IsNullOrEmpty(room.Host.ConnectionId))
            {
                await Clients.Client(room.Host.ConnectionId).SendAsync("ParticipantJoined", new
                {
                    userId = userId.ToString(),
                    username = username,
                    roomId = roomId
                });

                foreach (var coHost in room.Host.CoHosts)
                {
                    if (!string.IsNullOrEmpty(coHost.ConnectionId))
                    {
                        await Clients.Client(coHost.ConnectionId).SendAsync("ParticipantJoined", new
                        {
                            userId = userId.ToString(),
                            username = username,
                            roomId = roomId
                        });
                    }
                }
            }

            var joinMessage = new ChatMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                Sender = new MessageSender
                {
                    Id = userId.ToString(),
                    Username = username,
                    Role = userRole
                },
                Type = "system",
                Content = isOriginalHost 
                    ? $"{username} (host) rejoined the room" 
                    : $"{username} just joined the room",
                Timestamp = DateTime.UtcNow
            };

            room.MessageRoom.Messages.Add(joinMessage);

            var updatedParticipantsList = room.Participants.UsersList.Select(p => new
            {
                username = p.Username,
                id = p.Id,
                role = p.Role
            }).ToList();

            await Clients.Group(roomId).SendAsync("UserJoinedStream", new
            {
                username = username,
                userId = userId,
                message = joinMessage.Content,
                current_viewers = room.CurrentViewers,
                total_members = room.Participants.TotalMembers,
                participants = updatedParticipantsList,
                isHostRejoining = isOriginalHost
            });

            var existingProducers = _mediaServer.GetProducersInRoom(roomId, userId.ToString());
            
            var streamData = new
            {
                data = new
                {
                    stream = new
                    {
                        roomid = room.RoomId,
                        title = room.Title,
                        description = room.Description,
                        category = room.Category,
                        visibility = room.Visibility
                    },
                    status = new
                    {
                        state = room.State,
                        start_time = room.StartTime,
                        end_time = room.EndTime,
                        current_viewers = room.CurrentViewers
                    },
                    userdetails = new
                    {
                        username = username,
                        id = userId,
                        role = userRole
                    },
                    host = new
                    {
                        id = room.Host.Id,
                        username = room.Host.Username,
                        coHosts = room.Host.CoHosts.Select(c => new { username = c.Username, id = c.Id }).ToList()
                    },
                    participants = new
                    {
                        users_list = updatedParticipantsList,
                        total_members = room.Participants.TotalMembers
                    },
                    media_settings = room.MediaSettings,
                    permissions = room.Permissions,
                    message_room = room.MessageRoom,
                    realtime = room.Realtime,
                    existing_producers = existingProducers.Select(p => new
                    {
                        userId = p.UserId,
                        producerId = p.ProducerId,
                        kind = p.Kind
                    }).ToList()
                }
            };

            await Clients.Caller.SendAsync("JoinedStream", streamData);

            foreach (var producer in existingProducers)
            {
                await Clients.Caller.SendAsync("NewProducer", new
                {
                    userId = producer.UserId,
                    username = producer.UserId,
                    producerId = producer.ProducerId,
                    kind = producer.Kind
                });
            }

            _logger.LogInformation("User {Username} joined stream {RoomId} as {Role}", username, roomId, userRole);
        }

        public async Task ProduceMedia(string roomId, string kind, RTCSessionDescriptionInit offer)
        {
            var connectionId = Context.ConnectionId;

            if (!_streamConnections.TryGetValue(connectionId, out var connection))
            {
                await Clients.Caller.SendAsync("Error", new { message = "Connection not found" });
                return;
            }

            if (connection.Role != "host" && connection.Role != "co-host")
            {
                await Clients.Caller.SendAsync("Error", new { message = "Only host or co-host can produce media" });
                return;
            }

            if (!_streamRooms.TryGetValue(roomId, out var room))
            {
                await Clients.Caller.SendAsync("Error", new { message = "Stream room not found" });
                return;
            }

            var producerId = Guid.NewGuid().ToString();
            var userId = connection.UserId.ToString();

            var producer = new MediaProducer
            {
                UserId = userId,
                ProducerId = producerId,
                Kind = kind,
                Offer = offer,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                TrackSettings = new MediaTrackSettings
                {
                    TrackId = Guid.NewGuid().ToString(),
                    Enabled = true,
                    MaxBitrate = kind == "video" ? 2500000 : 128000,
                    Resolution = kind == "video" ? "1280x720" : null,
                    FrameRate = kind == "video" ? 30 : null
                }
            };

            _mediaServer.AddProducer(roomId, userId, producer);

            await Clients.Caller.SendAsync("ProducerCreated", new
            {
                producerId = producerId,
                kind = kind
            });

            await Clients.OthersInGroup(roomId).SendAsync("NewProducer", new
            {
                userId = userId,
                username = connection.Username,
                producerId = producerId,
                kind = kind
            });

            _logger.LogInformation("User {Username} started producing {Kind} in room {RoomId}", 
                connection.Username, kind, roomId);
        }

        public async Task ConsumeMedia(string roomId, string producerId)
        {
            var connectionId = Context.ConnectionId;

            if (!_streamConnections.TryGetValue(connectionId, out var connection))
            {
                await Clients.Caller.SendAsync("Error", new { message = "Connection not found" });
                return;
            }

            if (!_streamRooms.TryGetValue(roomId, out var room))
            {
                await Clients.Caller.SendAsync("Error", new { message = "Stream room not found" });
                return;
            }

            var producers = _mediaServer.GetProducersInRoom(roomId);
            var producer = producers.FirstOrDefault(p => p.ProducerId == producerId);

            if (producer == null)
            {
                await Clients.Caller.SendAsync("Error", new { message = "Producer not found" });
                return;
            }

            var consumerId = Guid.NewGuid().ToString();
            var userId = connection.UserId.ToString();

            var consumer = new MediaConsumer
            {
                ConsumerId = consumerId,
                UserId = userId,
                ProducerId = producerId,
                Kind = producer.Kind,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _mediaServer.AddConsumer(roomId, userId, producerId, consumer);

            await Clients.Caller.SendAsync("ConsumerCreated", new
            {
                consumerId = consumerId,
                producerId = producerId,
                producerUserId = producer.UserId,
                kind = producer.Kind
            });

            _logger.LogInformation("User {Username} consuming {Kind} from producer {ProducerId} in room {RoomId}", 
                connection.Username, producer.Kind, producerId, roomId);
        }

        public async Task SendOfferToProducer(string roomId, string producerUserId, RTCSessionDescriptionInit offer)
        {
            var connectionId = Context.ConnectionId;

            if (!_streamConnections.TryGetValue(connectionId, out var connection))
            {
                await Clients.Caller.SendAsync("Error", new { message = "Connection not found" });
                return;
            }

            if (!_streamRooms.TryGetValue(roomId, out var room))
            {
                await Clients.Caller.SendAsync("Error", new { message = "Stream room not found" });
                return;
            }

            var producerConnection = _streamConnections.Values.FirstOrDefault(c => c.UserId.ToString() == producerUserId && c.RoomId == roomId);
            
            if (producerConnection == null)
            {
                await Clients.Caller.SendAsync("Error", new { message = "Producer not found" });
                return;
            }

            await Clients.Client(producerConnection.ConnectionId).SendAsync("ReceiveOfferFromConsumer", new
            {
                consumerUserId = connection.UserId.ToString(),
                consumerConnectionId = connectionId,
                offer = offer
            });

            _logger.LogInformation("Offer sent from consumer {Consumer} to producer {Producer}", 
                connection.Username, producerUserId);
        }

        public async Task SendAnswerToConsumer(string roomId, string consumerConnectionId, RTCSessionDescriptionInit answer)
        {
            var connectionId = Context.ConnectionId;

            if (!_streamConnections.TryGetValue(connectionId, out var connection))
            {
                await Clients.Caller.SendAsync("Error", new { message = "Connection not found" });
                return;
            }

            await Clients.Client(consumerConnectionId).SendAsync("ReceiveAnswerFromProducer", new
            {
                producerUserId = connection.UserId.ToString(),
                answer = answer
            });

            _logger.LogInformation("Answer sent from producer {Producer} to consumer", connection.Username);
        }

        public async Task SendIceCandidateToProducer(string roomId, string producerUserId, RTCIceCandidateInit candidate)
        {
            var connectionId = Context.ConnectionId;

            if (!_streamConnections.TryGetValue(connectionId, out var connection))
                return;

            var producerConnection = _streamConnections.Values.FirstOrDefault(c => c.UserId.ToString() == producerUserId && c.RoomId == roomId);
            
            if (producerConnection == null)
                return;

            await Clients.Client(producerConnection.ConnectionId).SendAsync("ReceiveIceCandidateFromConsumer", new
            {
                consumerConnectionId = connectionId,
                candidate = candidate
            });

            _logger.LogDebug("ICE candidate sent from consumer to producer {Producer}", producerUserId);
        }

        public async Task SendIceCandidateToConsumer(string roomId, string consumerConnectionId, RTCIceCandidateInit candidate)
        {
            var connectionId = Context.ConnectionId;

            if (!_streamConnections.TryGetValue(connectionId, out var connection))
                return;

            await Clients.Client(consumerConnectionId).SendAsync("ReceiveIceCandidateFromProducer", new
            {
                producerUserId = connection.UserId.ToString(),
                candidate = candidate
            });

            _logger.LogDebug("ICE candidate sent from producer {Producer} to consumer", connection.Username);
        }

        public async Task ConsumerAnswer(string roomId, string consumerId, RTCSessionDescriptionInit answer)
        {
            var connectionId = Context.ConnectionId;

            if (!_streamConnections.TryGetValue(connectionId, out var connection))
            {
                await Clients.Caller.SendAsync("Error", new { message = "Connection not found" });
                return;
            }

            await Clients.Caller.SendAsync("ConsumerAnswerReceived", new
            {
                consumerId = consumerId,
                success = true
            });

            _logger.LogDebug("Consumer answer received for {ConsumerId} in room {RoomId}", consumerId, roomId);
        }

        public async Task PauseProducer(string roomId, string producerId)
        {
            var connectionId = Context.ConnectionId;

            if (!_streamConnections.TryGetValue(connectionId, out var connection))
                return;

            var producer = _mediaServer.GetProducer(roomId, connection.UserId.ToString());
            
            if (producer != null && producer.ProducerId == producerId)
            {
                producer.IsActive = false;

                await Clients.Group(roomId).SendAsync("ProducerPaused", new
                {
                    userId = connection.UserId.ToString(),
                    producerId = producerId,
                    kind = producer.Kind
                });

                _logger.LogInformation("Producer {ProducerId} paused in room {RoomId}", producerId, roomId);
            }
        }

        public async Task ResumeProducer(string roomId, string producerId)
        {
            var connectionId = Context.ConnectionId;

            if (!_streamConnections.TryGetValue(connectionId, out var connection))
                return;

            var producer = _mediaServer.GetProducer(roomId, connection.UserId.ToString());
            
            if (producer != null && producer.ProducerId == producerId)
            {
                producer.IsActive = true;

                await Clients.Group(roomId).SendAsync("ProducerResumed", new
                {
                    userId = connection.UserId.ToString(),
                    producerId = producerId,
                    kind = producer.Kind
                });

                _logger.LogInformation("Producer {ProducerId} resumed in room {RoomId}", producerId, roomId);
            }
        }

        public async Task CloseProducer(string roomId, string producerId)
        {
            var connectionId = Context.ConnectionId;

            if (!_streamConnections.TryGetValue(connectionId, out var connection))
                return;

            _mediaServer.RemoveProducer(roomId, connection.UserId.ToString());

            await Clients.Group(roomId).SendAsync("ProducerClosed", new
            {
                userId = connection.UserId.ToString(),
                producerId = producerId
            });

            _logger.LogInformation("Producer {ProducerId} closed in room {RoomId}", producerId, roomId);
        }

        public async Task InviteCoHost(string roomId, string targetUsername, int targetUserId)
        {
            var connectionId = Context.ConnectionId;

            if (!_streamConnections.TryGetValue(connectionId, out var hostConnection) || hostConnection.Role != "host")
            {
                await Clients.Caller.SendAsync("Error", new { message = "Only host can invite co-hosts" });
                return;
            }

            if (!_streamRooms.TryGetValue(roomId, out var room))
            {
                await Clients.Caller.SendAsync("Error", new { message = "Stream room not found" });
                return;
            }

            var targetParticipant = room.Participants.UsersList.FirstOrDefault(p => p.Username == targetUsername);
            if (targetParticipant == null)
            {
                await Clients.Caller.SendAsync("Error", new { message = "User not found in stream" });
                return;
            }

            await Clients.Client(targetParticipant.ConnectionId).SendAsync("CoHostInvite", new
            {
                roomId = roomId,
                hostUsername = hostConnection.Username,
                message = $"{hostConnection.Username} invited you to be a co-host"
            });

            _logger.LogInformation("Host {Host} invited {User} to be co-host in room {RoomId}", 
                hostConnection.Username, targetUsername, roomId);
        }

        public async Task AcceptCoHostInvite(string roomId)
        {
            var connectionId = Context.ConnectionId;

            if (!_streamConnections.TryGetValue(connectionId, out var connection))
            {
                await Clients.Caller.SendAsync("Error", new { message = "Connection not found" });
                return;
            }

            if (!_streamRooms.TryGetValue(roomId, out var room))
            {
                await Clients.Caller.SendAsync("Error", new { message = "Stream room not found" });
                return;
            }

            var participant = room.Participants.UsersList.FirstOrDefault(p => p.ConnectionId == connectionId);
            if (participant == null)
            {
                await Clients.Caller.SendAsync("Error", new { message = "Participant not found" });
                return;
            }

            participant.Role = "co-host";
            connection.Role = "co-host";

            var coHost = new CoHostInfo
            {
                Username = connection.Username,
                Id = connection.UserId.ToString(),
                ConnectionId = connectionId
            };

            room.Host.CoHosts.Add(coHost);

            var message = new ChatMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                Sender = new MessageSender
                {
                    Id = connection.UserId.ToString(),
                    Username = connection.Username,
                    Role = "co-host"
                },
                Type = "system",
                Content = $"{connection.Username} is now a co-host",
                Timestamp = DateTime.UtcNow
            };

            room.MessageRoom.Messages.Add(message);

            await Clients.Group(roomId).SendAsync("CoHostAdded", new
            {
                username = connection.Username,
                userId = connection.UserId,
                message = $"{connection.Username} is now a co-host",
                coHosts = room.Host.CoHosts.Select(c => new { username = c.Username, id = c.Id }),
                participants = room.Participants.UsersList.Select(p => new { 
                    username = p.Username, 
                    id = p.Id, 
                    role = p.Role 
                }).ToList()
            });

            _logger.LogInformation("User {Username} accepted co-host invite in room {RoomId}", 
                connection.Username, roomId);
        }

        public async Task RejectCoHostInvite(string roomId)
        {
            var connectionId = Context.ConnectionId;

            if (!_streamConnections.TryGetValue(connectionId, out var connection))
                return;

            await Clients.Caller.SendAsync("CoHostInviteRejected", new { roomId });

            _logger.LogInformation("User {Username} rejected co-host invite in room {RoomId}", 
                connection.Username, roomId);
        }

        public async Task RemoveCoHost(string roomId, string targetUsername, int targetUserId)
        {
            var connectionId = Context.ConnectionId;

            if (!_streamConnections.TryGetValue(connectionId, out var hostConnection) || hostConnection.Role != "host")
            {
                await Clients.Caller.SendAsync("Error", new { message = "Only host can remove co-hosts" });
                return;
            }

            if (!_streamRooms.TryGetValue(roomId, out var room))
            {
                await Clients.Caller.SendAsync("Error", new { message = "Stream room not found" });
                return;
            }

            var coHost = room.Host.CoHosts.FirstOrDefault(c => c.Username == targetUsername);
            if (coHost == null)
            {
                await Clients.Caller.SendAsync("Error", new { message = "Co-host not found" });
                return;
            }

            room.Host.CoHosts.Remove(coHost);

            var participant = room.Participants.UsersList.FirstOrDefault(p => p.Username == targetUsername);
            if (participant != null)
            {
                participant.Role = "viewer";
            }

            var targetConnection = _streamConnections.Values.FirstOrDefault(c => c.Username == targetUsername && c.RoomId == roomId);
            if (targetConnection != null)
            {
                targetConnection.Role = "viewer";
                _mediaServer.RemoveProducer(roomId, targetConnection.UserId.ToString());
            }

            var message = new ChatMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                Sender = new MessageSender
                {
                    Id = targetUserId.ToString(),
                    Username = targetUsername,
                    Role = "viewer"
                },
                Type = "system",
                Content = $"{targetUsername} is no longer a co-host",
                Timestamp = DateTime.UtcNow
            };

            room.MessageRoom.Messages.Add(message);

            await Clients.Group(roomId).SendAsync("CoHostRemoved", new
            {
                username = targetUsername,
                userId = targetUserId,
                message = $"{targetUsername} is no longer a co-host",
                coHosts = room.Host.CoHosts.Select(c => new { username = c.Username, id = c.Id }),
                participants = room.Participants.UsersList.Select(p => new { 
                    username = p.Username, 
                    id = p.Id, 
                    role = p.Role 
                }).ToList()
            });

            _logger.LogInformation("Host {Host} removed {User} from co-host in room {RoomId}", 
                hostConnection.Username, targetUsername, roomId);
        }

        public async Task LeaveCoHost(string roomId)
        {
            var connectionId = Context.ConnectionId;

            if (!_streamConnections.TryGetValue(connectionId, out var connection) || connection.Role != "co-host")
            {
                await Clients.Caller.SendAsync("Error", new { message = "You are not a co-host" });
                return;
            }

            if (!_streamRooms.TryGetValue(roomId, out var room))
            {
                await Clients.Caller.SendAsync("Error", new { message = "Stream room not found" });
                return;
            }

            var coHost = room.Host.CoHosts.FirstOrDefault(c => c.Username == connection.Username);
            if (coHost != null)
            {
                room.Host.CoHosts.Remove(coHost);
            }

            var participant = room.Participants.UsersList.FirstOrDefault(p => p.ConnectionId == connectionId);
            if (participant != null)
            {
                participant.Role = "viewer";
            }

            connection.Role = "viewer";
            _mediaServer.RemoveProducer(roomId, connection.UserId.ToString());

            var message = new ChatMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                Sender = new MessageSender
                {
                    Id = connection.UserId.ToString(),
                    Username = connection.Username,
                    Role = "viewer"
                },
                Type = "system",
                Content = $"{connection.Username} left co-host and returned to viewer",
                Timestamp = DateTime.UtcNow
            };

            room.MessageRoom.Messages.Add(message);

            await Clients.Group(roomId).SendAsync("CoHostLeft", new
            {
                username = connection.Username,
                userId = connection.UserId,
                message = $"{connection.Username} left co-host and returned to viewer",
                coHosts = room.Host.CoHosts.Select(c => new { username = c.Username, id = c.Id }),
                participants = room.Participants.UsersList.Select(p => new { 
                    username = p.Username, 
                    id = p.Id, 
                    role = p.Role 
                }).ToList()
            });

            _logger.LogInformation("User {Username} left co-host role in room {RoomId}", 
                connection.Username, roomId);
        }

        public async Task LeaveStream(string roomId, bool isHostLeaving = false)
        {
            var connectionId = Context.ConnectionId;

            if (!_streamConnections.TryRemove(connectionId, out var connection))
                return;

            if (!_streamRooms.TryGetValue(roomId, out var room))
                return;

            var participant = room.Participants.UsersList.FirstOrDefault(p => p.ConnectionId == connectionId);
            if (participant != null)
            {
                room.Participants.UsersList.Remove(participant);
                room.Participants.TotalMembers = room.Participants.UsersList.Count;
                room.CurrentViewers--;
            }

            var coHost = room.Host.CoHosts.FirstOrDefault(c => c.ConnectionId == connectionId);
            if (coHost != null)
            {
                room.Host.CoHosts.Remove(coHost);
            }

            _mediaServer.RemoveProducer(roomId, connection.UserId.ToString());
            _mediaServer.RemoveAllConsumersForUser(roomId, connection.UserId.ToString());

            await Groups.RemoveFromGroupAsync(connectionId, roomId);

            bool wasHost = connection.Role == "host";

            // *** NEW: Handle host leaving - start 30-minute auto-close timer ***
            if (wasHost && isHostLeaving)
            {
                room.IsHostPresent = false;
                room.Host.ConnectionId = string.Empty; // Clear connection ID but keep host info
                
                _logger.LogInformation("Host {Username} left stream {RoomId} - starting 30-minute auto-close timer", 
                    connection.Username, roomId);

                // Start 30-minute countdown
                var cts = new CancellationTokenSource();
                _autoCloseTimers[roomId] = cts;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(30), cts.Token);
                        
                        // If we reach here, 30 minutes passed without host rejoining
                        if (_streamRooms.TryGetValue(roomId, out var roomToClose))
                        {
                            _logger.LogInformation("Auto-closing stream {RoomId} - host did not return within 30 minutes", roomId);
                            
                            roomToClose.State = "ended";
                            roomToClose.EndTime = DateTime.UtcNow;
                            
                            _mediaServer.RemoveRoom(roomId);
                            
                            await Clients.Group(roomId).SendAsync("StreamEnded", new
                            {
                                roomId = roomId,
                                message = "Stream ended - host did not return",
                                endTime = roomToClose.EndTime,
                                reason = "host_timeout"
                            });
                            
                            // Clean up all connections
                            var participantConnections = _streamConnections.Values
                                .Where(c => c.RoomId == roomId)
                                .ToList();

                            foreach (var p in participantConnections)
                            {
                                _streamConnections.TryRemove(p.ConnectionId, out _);
                                await Groups.RemoveFromGroupAsync(p.ConnectionId, roomId);
                            }
                            
                            _streamRooms.TryRemove(roomId, out _);
                            _autoCloseTimers.TryRemove(roomId, out _);
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        // Timer was cancelled - host rejoined
                        _logger.LogInformation("Auto-close timer cancelled for room {RoomId}", roomId);
                    }
                    finally
                    {
                        cts.Dispose();
                    }
                });
            }

            var leaveMessage = new ChatMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                Sender = new MessageSender
                {
                    Id = connection.UserId.ToString(),
                    Username = connection.Username,
                    Role = connection.Role
                },
                Type = "system",
                Content = wasHost 
                    ? $"{connection.Username} (host) left the room - stream will end in 30 minutes if host doesn't return"
                    : $"{connection.Username} left the room",
                Timestamp = DateTime.UtcNow
            };

            room.MessageRoom.Messages.Add(leaveMessage);

            var updatedParticipantsList = room.Participants.UsersList.Select(p => new
            {
                username = p.Username,
                id = p.Id,
                role = p.Role
            }).ToList();

            await Clients.Group(roomId).SendAsync("UserLeftStream", new
            {
                username = connection.Username,
                userId = connection.UserId,
                message = leaveMessage.Content,
                current_viewers = room.CurrentViewers,
                total_members = room.Participants.TotalMembers,
                participants = updatedParticipantsList,
                wasHost = wasHost,
                hostRejoined = false
            });

            _logger.LogInformation("User {Username} left stream {RoomId}", connection.Username, roomId);
        }

        public async Task EndStream(string roomId)
        {
            var connectionId = Context.ConnectionId;

            if (!_streamConnections.TryGetValue(connectionId, out var connection) || connection.Role != "host")
            {
                await Clients.Caller.SendAsync("Error", new { message = "Only host can end the stream" });
                return;
            }

            if (!_streamRooms.TryRemove(roomId, out var room))
            {
                await Clients.Caller.SendAsync("Error", new { message = "Stream room not found" });
                return;
            }

            // Cancel any pending auto-close timer
            if (_autoCloseTimers.TryRemove(roomId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }

            room.State = "ended";
            room.EndTime = DateTime.UtcNow;

            _mediaServer.RemoveRoom(roomId);

            await Clients.Group(roomId).SendAsync("StreamEnded", new
            {
                roomId = roomId,
                message = $"{connection.Username} has ended the stream",
                endTime = room.EndTime
            });

            var participantConnections = _streamConnections.Values
                .Where(c => c.RoomId == roomId)
                .ToList();

            foreach (var participant in participantConnections)
            {
                _streamConnections.TryRemove(participant.ConnectionId, out _);
                await Groups.RemoveFromGroupAsync(participant.ConnectionId, roomId);
            }

            _logger.LogInformation("Host {Username} ended stream {RoomId}", connection.Username, roomId);
        }

        public async Task SendStreamMessage(string roomId, string message)
        {
            var connectionId = Context.ConnectionId;

            if (!_streamConnections.TryGetValue(connectionId, out var connection))
                return;

            if (!_streamRooms.TryGetValue(roomId, out var room))
                return;

            var chatMessage = new ChatMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                Sender = new MessageSender
                {
                    Id = connection.UserId.ToString(),
                    Username = connection.Username,
                    Role = connection.Role 
                },
                Type = "text",
                Content = message,
                Timestamp = DateTime.UtcNow
            };

            room.MessageRoom.Messages.Add(chatMessage);

            await Clients.Group(roomId).SendAsync("ReceiveStreamMessage", new
            {
                message_id = chatMessage.MessageId,
                sender = chatMessage.Sender, 
                type = chatMessage.Type,
                content = chatMessage.Content,
                timestamp = chatMessage.Timestamp
            });
        }

        public async Task ToggleStreamMedia(string roomId, string mediaType, bool enabled)
        {
            var connectionId = Context.ConnectionId;

            if (!_streamConnections.TryGetValue(connectionId, out var connection))
                return;

            if (connection.Role != "host" && connection.Role != "co-host")
            {
                await Clients.Caller.SendAsync("Error", new { message = "Only host or co-host can toggle media" });
                return;
            }

            if (!_streamRooms.TryGetValue(roomId, out var room))
                return;

            switch (mediaType.ToLower())
            {
                case "audio":
                    room.MediaSettings.AudioEnabled = enabled;
                    break;
                case "video":
                    room.MediaSettings.VideoEnabled = enabled;
                    break;
                case "screenshare":
                    room.MediaSettings.ScreenShareEnabled = enabled;
                    break;
            }

            await Clients.Group(roomId).SendAsync("MediaToggled", new
            {
                username = connection.Username,
                mediaType = mediaType,
                enabled = enabled
            });
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = Context.ConnectionId;

            if (_streamConnections.TryGetValue(connectionId, out var connection))
            {
                if (connection.Role == "host")
                {
                    // Don't auto-end, just mark host as left
                    await LeaveStream(connection.RoomId, isHostLeaving: true);
                }
                else
                {
                    await LeaveStream(connection.RoomId);
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        private string GenerateRoomId()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 20)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    
        public async Task RemoveUser(string roomId, string targetUsername, int targetUserId)
        {
            var connectionId = Context.ConnectionId;

            if (!_streamConnections.TryGetValue(connectionId, out var hostConnection) || hostConnection.Role != "host")
            {
                await Clients.Caller.SendAsync("Error", new { message = "Only host can remove users" });
                return;
            }

            if (!_streamRooms.TryGetValue(roomId, out var room))
            {
                await Clients.Caller.SendAsync("Error", new { message = "Stream room not found" });
                return;
            }

            var targetParticipant = room.Participants.UsersList.FirstOrDefault(p => p.Username == targetUsername);
            if (targetParticipant == null)
            {
                await Clients.Caller.SendAsync("Error", new { message = "User not found in stream" });
                return;
            }

            room.Participants.UsersList.Remove(targetParticipant);
            room.Participants.TotalMembers = room.Participants.UsersList.Count;
            room.CurrentViewers--;

            _streamConnections.TryRemove(targetParticipant.ConnectionId, out _);

            _mediaServer.RemoveProducer(roomId, targetUserId.ToString());
            _mediaServer.RemoveAllConsumersForUser(roomId, targetUserId.ToString());

            await Groups.RemoveFromGroupAsync(targetParticipant.ConnectionId, roomId);

            var message = new ChatMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                Sender = new MessageSender
                {
                    Id = targetUserId.ToString(),
                    Username = targetUsername,
                    Role = "viewer"
                },
                Type = "system",
                Content = $"{targetUsername} was removed from the stream",
                Timestamp = DateTime.UtcNow
            };

            room.MessageRoom.Messages.Add(message);

            await Clients.Client(targetParticipant.ConnectionId).SendAsync("UserRemoved", new
            {
                userId = targetUserId,
                username = targetUsername,
                message = "You have been removed from the stream"
            });

            await Clients.Group(roomId).SendAsync("UserLeftStream", new
            {
                username = targetUsername,
                userId = targetUserId,
                message = $"{targetUsername} was removed from the stream",
                current_viewers = room.CurrentViewers,
                total_members = room.Participants.TotalMembers,
                participants = room.Participants.UsersList.Select(p => new { username = p.Username, id = p.Id, role = p.Role }).ToList()
            });

            _logger.LogInformation("Host {Host} removed user {User} from room {RoomId}", 
                hostConnection.Username, targetUsername, roomId);
        }

        public async Task BlockUser(string roomId, string targetUsername, int targetUserId)
        {
            var connectionId = Context.ConnectionId;

            if (!_streamConnections.TryGetValue(connectionId, out var hostConnection) || hostConnection.Role != "host")
            {
                await Clients.Caller.SendAsync("Error", new { message = "Only host can block users" });
                return;
            }

            if (!_streamRooms.TryGetValue(roomId, out var room))
            {
                await Clients.Caller.SendAsync("Error", new { message = "Stream room not found" });
                return;
            }

            if (room.BlockedUsers == null)
            {
                room.BlockedUsers = new List<int>();
            }
            
            if (!room.BlockedUsers.Contains(targetUserId))
            {
                room.BlockedUsers.Add(targetUserId);
            }

            var targetParticipant = room.Participants.UsersList.FirstOrDefault(p => p.Username == targetUsername);
            if (targetParticipant != null)
            {
                room.Participants.UsersList.Remove(targetParticipant);
                room.Participants.TotalMembers = room.Participants.UsersList.Count;
                room.CurrentViewers--;

                _streamConnections.TryRemove(targetParticipant.ConnectionId, out _);

                _mediaServer.RemoveProducer(roomId, targetUserId.ToString());
                _mediaServer.RemoveAllConsumersForUser(roomId, targetUserId.ToString());

                await Groups.RemoveFromGroupAsync(targetParticipant.ConnectionId, roomId);

                await Clients.Client(targetParticipant.ConnectionId).SendAsync("UserBlocked", new
                {
                    userId = targetUserId,
                    username = targetUsername,
                    message = "You have been blocked from this stream"
                });
            }

            var message = new ChatMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                Sender = new MessageSender
                {
                    Id = targetUserId.ToString(),
                    Username = targetUsername,
                    Role = "viewer"
                },
                Type = "system",
                Content = $"{targetUsername} was blocked from the stream",
                Timestamp = DateTime.UtcNow
            };

            room.MessageRoom.Messages.Add(message);

            await Clients.Group(roomId).SendAsync("UserLeftStream", new
            {
                username = targetUsername,
                userId = targetUserId,
                message = $"{targetUsername} was blocked from the stream",
                current_viewers = room.CurrentViewers,
                total_members = room.Participants.TotalMembers,
                participants = room.Participants.UsersList.Select(p => new { username = p.Username, id = p.Id, role = p.Role }).ToList()
            });

            _logger.LogInformation("Host {Host} blocked user {User} from room {RoomId}", 
                hostConnection.Username, targetUsername, roomId);
        }
    }

    public class StreamRoom
    {
        public string RoomId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Visibility { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int CurrentViewers { get; set; }
        public HostInfo Host { get; set; } = new();
        // *** NEW: Track original host ***
        public int OriginalHostId { get; set; }
        public string OriginalHostUsername { get; set; } = string.Empty;
        public bool IsHostPresent { get; set; }
        public ParticipantsList Participants { get; set; } = new();
        public MediaSettings MediaSettings { get; set; } = new();
        public StreamPermissions Permissions { get; set; } = new();
        public MessageRoom MessageRoom { get; set; } = new();
        public RealtimeInfo Realtime { get; set; } = new();
        public List<int> BlockedUsers { get; set; } = new List<int>();
    }

    public class StreamConnection
    {
        public string ConnectionId { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string RoomId { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime JoinedAt { get; set; }
    }

    public class HostInfo
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string ConnectionId { get; set; } = string.Empty;
        public List<CoHostInfo> CoHosts { get; set; } = new();
    }

    public class CoHostInfo
    {
        public string Username { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string ConnectionId { get; set; } = string.Empty;
    }

    public class ParticipantsList
    {
        public List<ParticipantInfo> UsersList { get; set; } = new();
        public int TotalMembers { get; set; }
    }

    public class ParticipantInfo
    {
        public string Username { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string ConnectionId { get; set; } = string.Empty;
    }

    public class MediaSettings
    {
        public bool AudioEnabled { get; set; }
        public bool VideoEnabled { get; set; }
        public bool ScreenShareEnabled { get; set; }
        public bool RecordingEnabled { get; set; }
    }

    public class StreamPermissions
    {
        public bool CanChat { get; set; }
        public bool CanRaiseHand { get; set; }
        public bool CanShareScreen { get; set; }
    }

    public class MessageRoom
    {
        public string RoomId { get; set; } = string.Empty;
        public bool ChatEnabled { get; set; }
        public string MessageRetention { get; set; } = string.Empty;
        public List<ChatMessage> Messages { get; set; } = new();
        public ModerationInfo Moderation { get; set; } = new();
    }

    public class ChatMessage
    {
        public string MessageId { get; set; } = string.Empty;
        public MessageSender Sender { get; set; } = new();
        public string Type { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public class MessageSender
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public class ModerationInfo
    {
        public List<string> MutedUsers { get; set; } = new();
        public List<string> DeletedMessages { get; set; } = new();
    }

    public class RealtimeInfo
    {
        public bool ReactionsEnabled { get; set; }
        public List<StreamEvent> Events { get; set; } = new();
    }

    public class StreamEvent
    {
        public string EventId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public class RTCSessionDescriptionInit
    {
        public string Type { get; set; } = string.Empty;
        public string Sdp { get; set; } = string.Empty;
    }

    public class RTCIceCandidateInit
    {
        public string Candidate { get; set; } = string.Empty;
        public string SdpMid { get; set; } = string.Empty;
        public int? SdpMLineIndex { get; set; }
        public string UsernameFragment { get; set; } = string.Empty;
    }
}