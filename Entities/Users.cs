using System.ComponentModel.DataAnnotations;

namespace wenu.Entities
{
    public class Users
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        public string ProfilePictureUrl { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
        public bool IsActive { get; set; } = true;

        public HashSet<string> Roles { get; set; }
            = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public virtual UserRecord? UserRecord { get; set; }

        public virtual ICollection<LiveStream> HostedStreams { get; set; }
            = new List<LiveStream>();

        public virtual ICollection<LiveStreamParticipant> ParticipatedStreams { get; set; }
            = new List<LiveStreamParticipant>();

        public virtual ICollection<LiveStreamCoHost> CoHostedStreams { get; set; }
            = new List<LiveStreamCoHost>();
    }
}
