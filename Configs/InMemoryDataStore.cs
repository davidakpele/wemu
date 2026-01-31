using System.Collections.Concurrent;
using wenu.Entities;

namespace wenu.Configs
{
    public class InMemoryDataStore
    {
        public ConcurrentDictionary<int, LiveStream> LiveStreams { get; } = new();
        public ConcurrentDictionary<int, LiveStreamParticipant> LiveStreamParticipants { get; } = new();
        public ConcurrentDictionary<int, LiveStreamCoHost> LiveStreamCoHosts { get; } = new();
        public ConcurrentDictionary<int, LiveStreamMessage> LiveStreamMessages { get; } = new();
        public ConcurrentDictionary<int, BlockedParticipant> BlockedParticipants { get; } = new();
        public ConcurrentDictionary<int, Message> Messages { get; } = new();
        public ConcurrentDictionary<int, UserRecord> UserRecords { get; } = new();
        
        private int _nextLiveStreamId;
        private int _nextParticipantId;
        private int _nextCoHostId;
        private int _nextMessageId;
        private int _nextLiveStreamMessageId;
        private int _nextBlockedParticipantId;
        private int _nextUserRecordId;

        public int NextLiveStreamId() => Interlocked.Increment(ref _nextLiveStreamId);
        public int NextParticipantId() => Interlocked.Increment(ref _nextParticipantId);
        public int NextCoHostId() => Interlocked.Increment(ref _nextCoHostId);
        public int NextMessageId() => Interlocked.Increment(ref _nextMessageId);
        public int NextLiveStreamMessageId() => Interlocked.Increment(ref _nextLiveStreamMessageId);
        public int NextBlockedParticipantId() => Interlocked.Increment(ref _nextBlockedParticipantId);
        public int NextUserRecordId() => Interlocked.Increment(ref _nextUserRecordId);
    }
}