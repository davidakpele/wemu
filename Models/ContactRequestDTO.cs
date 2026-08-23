using System.ComponentModel.DataAnnotations;

namespace wenu.Models
{
    public class ContactRequestDTO
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Phone { get; set; } = string.Empty;

        public string Whatsapp { get; set; } = string.Empty;

        [Required]
        public string InquiryType { get; set; } = string.Empty;

        [Required]
        public string Message { get; set; } = string.Empty;
    }
}
