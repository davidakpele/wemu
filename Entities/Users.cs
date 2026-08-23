using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace wenu.Entities
{
    public class Users : IdentityUser<int> 
    {
        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;
        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;
        public string ProfilePictureUrl { get; set; }= string.Empty;
        public string Bio { get; set; }= string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
        public bool IsActive { get; set; } = true;

        public virtual UserRecord? UserRecord { get; set; }

        public virtual ICollection<LiveStream> HostedStreams { get; set; }
        
        public virtual ICollection<LiveStreamParticipant> ParticipatedStreams { get; set; }
        
        public virtual ICollection<LiveStreamCoHost> CoHostedStreams { get; set; }
    }
}
