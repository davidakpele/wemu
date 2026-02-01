// src/Services/MediaServer.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace wenu.Services
{
    public class MediaServer
    {
        private readonly ConcurrentDictionary<string, MediaRoom> _mediaRooms = new();
        private readonly ILogger<MediaServer> _logger;

        public MediaServer(ILogger<MediaServer> logger)
        {
            _logger = logger;
        }

        public MediaRoom GetOrCreateRoom(string roomId)
        {
            return _mediaRooms.GetOrAdd(roomId, id => new MediaRoom
            {
                RoomId = id,
                CreatedAt = DateTime.UtcNow
            });
        }

        public bool RemoveRoom(string roomId)
        {
            return _mediaRooms.TryRemove(roomId, out _);
        }

        public void AddProducer(string roomId, string userId, MediaProducer producer)
        {
            var room = GetOrCreateRoom(roomId);
            room.Producers[userId] = producer;
            _logger.LogInformation("Producer added for user {UserId} in room {RoomId}", userId, roomId);
        }

        public void RemoveProducer(string roomId, string userId)
        {
            if (_mediaRooms.TryGetValue(roomId, out var room))
            {
                room.Producers.TryRemove(userId, out _);
                _logger.LogInformation("Producer removed for user {UserId} in room {RoomId}", userId, roomId);
            }
        }

        public void AddConsumer(string roomId, string userId, string producerId, MediaConsumer consumer)
        {
            var room = GetOrCreateRoom(roomId);
            var key = $"{userId}:{producerId}";
            room.Consumers[key] = consumer;
            _logger.LogInformation("Consumer added for user {UserId} consuming {ProducerId} in room {RoomId}", 
                userId, producerId, roomId);
        }

        public void RemoveConsumer(string roomId, string userId, string producerId)
        {
            if (_mediaRooms.TryGetValue(roomId, out var room))
            {
                var key = $"{userId}:{producerId}";
                room.Consumers.TryRemove(key, out _);
            }
        }

        public void RemoveAllConsumersForUser(string roomId, string userId)
        {
            if (_mediaRooms.TryGetValue(roomId, out var room))
            {
                var keysToRemove = room.Consumers.Keys
                    .Where(k => k.StartsWith($"{userId}:"))
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    room.Consumers.TryRemove(key, out _);
                }
            }
        }

        public List<MediaProducer> GetProducersInRoom(string roomId, string excludeUserId = null)
        {
            if (_mediaRooms.TryGetValue(roomId, out var room))
            {
                return room.Producers.Values
                    .Where(p => excludeUserId == null || p.UserId != excludeUserId)
                    .ToList();
            }
            return new List<MediaProducer>();
        }

        public MediaProducer GetProducer(string roomId, string userId)
        {
            if (_mediaRooms.TryGetValue(roomId, out var room))
            {
                room.Producers.TryGetValue(userId, out var producer);
                return producer;
            }
            return null;
        }
    }

    public class MediaRoom
    {
        public string RoomId { get; set; }
        public DateTime CreatedAt { get; set; }
        public ConcurrentDictionary<string, MediaProducer> Producers { get; set; } = new();
        public ConcurrentDictionary<string, MediaConsumer> Consumers { get; set; } = new();
    }

    public class MediaProducer
    {
        public string UserId { get; set; }
        public string ProducerId { get; set; }
        public string Kind { get; set; } // "audio" or "video"
        public RTCSessionDescriptionInit Offer { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
        public MediaTrackSettings TrackSettings { get; set; }
    }

    public class MediaConsumer
    {
        public string ConsumerId { get; set; }
        public string UserId { get; set; }
        public string ProducerId { get; set; }
        public string Kind { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
    }

    public class MediaTrackSettings
    {
        public string TrackId { get; set; }
        public bool Enabled { get; set; }
        public int? MaxBitrate { get; set; }
        public string Resolution { get; set; }
        public int? FrameRate { get; set; }
    }
}