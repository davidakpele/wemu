using Microsoft.AspNetCore.Identity;
using wenu.Entities;
using wenu.Models;
using wenu.Resopones;
using wumo.Services;

namespace wenu.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<Users> _userManager;
        private readonly SignInManager<Users> _signInManager;
        private readonly JwtService _jwtService;

        public AuthService(
            UserManager<Users> userManager,
            SignInManager<Users> signInManager,
            JwtService jwtService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _jwtService = jwtService;
        }

        public async Task<bool> IsEmailExistsAsync(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            return user != null;
        }

        public async Task<bool> IsUsernameExistsAsync(string username)
        {
            var user = await _userManager.FindByNameAsync(username);
            return user != null;
        }

        public async Task<UserResponseDto> RegisterAsync(UsersDTO request)
        {
            if (await IsUsernameExistsAsync(request.Username))
                throw new InvalidOperationException("Username already exists");

            if (await IsEmailExistsAsync(request.Email))
                throw new InvalidOperationException("Email already exists");

            var user = new Users
            {
                UserName = request.Username,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            var result = await _userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"User registration failed: {errors}");
            }

            await _userManager.AddToRoleAsync(user, "User");

            var token = await _jwtService.GenerateToken(user);

            return new UserResponseDto
            {
                Username = user.UserName,
                Email = user.Email,
            };
        }

        public async Task<AuthResponseDTO> LoginAsync(LoginRequestDTO request)
        {
            var user = await _userManager.FindByNameAsync(request.Username);

            if (user == null || !user.IsActive)
                throw new UnauthorizedAccessException("Invalid username or password");

            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);

            if (!result.Succeeded)
                throw new UnauthorizedAccessException("Invalid username or password");

            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            var token = await _jwtService.GenerateToken(user);

            return new AuthResponseDTO
            {
                Token = token,
                UserName = user.UserName!,
                FirstName = user.FirstName,
                LastName = user.LastName
            };
        }
    }
}
