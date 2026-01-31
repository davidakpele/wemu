using wenu.Entities;
using wenu.Models;
using wenu.Resopones;
using wenu.Services;
using wenu.Configs;
using Microsoft.AspNetCore.Identity;

namespace wenu.Services
{
    public class AuthService : IAuthService
    {
        private readonly InMemoryUserStore _store;
        private readonly JwtService _jwtService;
        private readonly IPasswordHasher<Users> _passwordHasher;

        public AuthService(InMemoryUserStore store, JwtService jwtService, IPasswordHasher<Users> passwordHasher)
        {
            _store = store;
            _jwtService = jwtService;
            _passwordHasher = passwordHasher;
        }

        public async Task<bool> IsEmailExistsAsync(string email)
        {
            return await Task.Run(() => _store.ExistsEmail(email));
        }

        public async Task<bool> IsUsernameExistsAsync(string username)
        {
            return await Task.Run(() => _store.ExistsUsername(username));
        }

        public async Task<UserResponseDto> RegisterAsync(UsersDTO request)
        {
            if (await IsUsernameExistsAsync(request.Username))
                throw new InvalidOperationException("Username already exists");

            if (await IsEmailExistsAsync(request.Email))
                throw new InvalidOperationException("Email already exists");

            var user = new Users
            {
                Id = _store.GenerateId(),
                UserName = request.Username,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                Roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "User" }
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

            if (!_store.TryAdd(user))
                throw new InvalidOperationException("User registration failed: username or email conflict.");

            await _jwtService.GenerateToken(user);

            return new UserResponseDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Username = user.UserName,
                Email = user.Email,
            };
        }

        public async Task<ApiResponse<AuthResponseDTO>> LoginAsync(LoginRequestDTO request)
        {
            var user = await Task.Run(() => _store.FindByUsername(request.Username));

            if (user == null || !user.IsActive)
                throw new UnauthorizedAccessException("Invalid username or password");

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

            if (result == PasswordVerificationResult.Failed)
                throw new UnauthorizedAccessException("Invalid username or password");

            user.LastLoginAt = DateTime.UtcNow;
            _store.Update(user);

            var token = await _jwtService.GenerateToken(user);

            return new ApiResponse<AuthResponseDTO>
            {
                Success = true,
                Message = "Login successful",
                Data = new AuthResponseDTO
                {
                    Id = user.Id,
                    Token = token,
                    UserName = user.UserName,
                    FirstName = user.FirstName,
                    LastName = user.LastName
                }
            };
        }
    }
}