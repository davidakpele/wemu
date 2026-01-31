using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;


namespace wenu.Entities
{
    public class LiveStream
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public string Title { get; set; }

        public string Description { get; set; }

        [Required]
        public int HostUserId { get; set; }

        [ForeignKey("HostUserId")]
        public virtual Users HostUser { get; set; }

        public DateTime StartedAt { get; set; }

        public DateTime? EndedAt { get; set; }

        public bool IsActive { get; set; }

        // Stream settings
        public bool IsAudioEnabled { get; set; }

        public bool IsVideoEnabled { get; set; }

        public int ViewerCount { get; set; }

        // For storing stream metadata or connection info
        public string StreamKey { get; set; }

        public string StreamUrl { get; set; }

        // Navigation properties
        public virtual ICollection<LiveStreamParticipant> Participants { get; set; }

        public virtual ICollection<LiveStreamCoHost> CoHosts { get; set; }

        public virtual ICollection<LiveStreamMessage> Messages { get; set; }

        public virtual ICollection<BlockedParticipant> BlockedParticipants { get; set; }

        public LiveStream()
        {
            Id = Guid.NewGuid();
            StartedAt = DateTime.UtcNow;
            IsActive = true;
            IsAudioEnabled = true;
            IsVideoEnabled = true;
            ViewerCount = 0;
            Participants = new HashSet<LiveStreamParticipant>();
            CoHosts = new HashSet<LiveStreamCoHost>();
            Messages = new HashSet<LiveStreamMessage>();
            BlockedParticipants = new HashSet<BlockedParticipant>();
        }
    }
}