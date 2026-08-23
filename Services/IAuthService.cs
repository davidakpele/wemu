using wenu.Entities;
using wenu.Models;
using wenu.Resopones;

namespace wenu.Services
{
    public interface IAuthService
    {
        Task<UserResponseDto> RegisterAsync(UsersDTO request);

        Task<ApiResponse<AuthResponseDTO>> LoginAsync(LoginRequestDTO request);
    }
}
