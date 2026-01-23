using wenu.Entities;
using wenu.Models;
using wenu.Resopones;

namespace wenu.Services
{
    public interface IAuthService
    {
        Task<UserResponseDto> RegisterAsync(UsersDTO request);

        Task<AuthResponseDTO> LoginAsync(LoginRequestDTO request);
    }
}
