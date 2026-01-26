using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace wenu.Entities
{
    public class LiveStreamParticipant
    {
        [Key]
        public Guid Id { get; set; }
        
        [Required]
        public Guid LiveStreamId { get; set; }
        
        [ForeignKey("LiveStreamId")]
        public virtual LiveStream LiveStream { get; set; }
        
        [Required]
        public int UserId { get; set; } 
        
        [ForeignKey("UserId")]
        public virtual Users User { get; set; }
        
        public DateTime JoinedAt { get; set; }
        
        public DateTime? LeftAt { get; set; }
        
        public bool IsCurrentlyWatching { get; set; }
        
        // Connection info for SignalR
        public string ConnectionId { get; set; }
        
        public LiveStreamParticipant()
        {
            Id = Guid.NewGuid();
            JoinedAt = DateTime.UtcNow;
            IsCurrentlyWatching = true;
        }
    }
}