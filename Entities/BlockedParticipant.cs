using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;


namespace wenu.Entities
{
    public class BlockedParticipant
    {
        [Key]
        public int Id { get; set; } 
        
        [Required]
        public Guid LiveStreamId { get; set; }
        
        public virtual LiveStream LiveStream { get; set; } 
        
        [Required]
        public int BlockedUserId { get; set; } 
        
        public virtual Users BlockedUser { get; set; } 
        
        [Required]
        public int BlockedByUserId { get; set; } 
        
        public virtual Users BlockedByUser { get; set; } // Remove [ForeignKey] attribute
        
        public DateTime BlockedAt { get; set; }
        
        public string Reason { get; set; }
        
        public BlockedParticipant()
        {
            BlockedAt = DateTime.UtcNow;
        }
    }
}