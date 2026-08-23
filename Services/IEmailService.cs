using wenu.Models;

namespace wenu.Services
{
    public interface IEmailService
    {
        Task SendContactEmailAsync(ContactRequestDTO request);
    }
}
