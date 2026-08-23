using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace wenu.Entities
{
    public class LiveStreamMessage
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
        
        [Required]
        [MaxLength(1000)]
        public string Message { get; set; }
        
        public DateTime SentAt { get; set; }
        
        public bool IsSystemMessage { get; set; }
        
        public MessageType Type { get; set; }
        
        public LiveStreamMessage()
        {
            Id = Guid.NewGuid();
            SentAt = DateTime.UtcNow;
            IsSystemMessage = false;
            Type = MessageType.UserMessage;
        }
    }
    
    public enum MessageType
    {
        UserMessage,
        UserJoined,
        UserLeft,
        UserBlocked,
        UserRemoved,
        CoHostInvited,
        CoHostAccepted,
        CoHostRemoved,
        StreamStarted,
        StreamEnded
    }
}