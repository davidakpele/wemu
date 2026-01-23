using System.ComponentModel.DataAnnotations;

namespace wenu.Models
{
    public class UsersDTO
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters long.")]
        public string Password { get; set; } = string.Empty;
    }
}
