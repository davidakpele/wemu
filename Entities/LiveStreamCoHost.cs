using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace wenu.Entities
{
    public class LiveStreamCoHost
    {
        [Key]
        public Guid Id { get; set; }
        
        [Required]
        public Guid LiveStreamId { get; set; }
        
        [ForeignKey("LiveStreamId")]
        public virtual LiveStream LiveStream { get; set; }
        
        [Required]
        public int UserId { get; set; } // Changed from Guid to int
        
        [ForeignKey("UserId")]
        public virtual Users User { get; set; }
        
        public DateTime InvitedAt { get; set; }
        
        public DateTime? AcceptedAt { get; set; }
        
        public DateTime? RemovedAt { get; set; }
        
        public bool IsActive { get; set; }
        
        // Permissions
        public bool CanSpeak { get; set; }
        
        public bool CanEnableVideo { get; set; }
        
        public bool CanRemoveParticipants { get; set; }
        
        // Connection info
        public string ConnectionId { get; set; }
        
        public LiveStreamCoHost()
        {
            Id = Guid.NewGuid();
            InvitedAt = DateTime.UtcNow;
            IsActive = false;
            CanSpeak = true;
            CanEnableVideo = true;
            CanRemoveParticipants = false;
        }
    }
}