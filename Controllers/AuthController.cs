using Microsoft.AspNetCore.Mvc;
using wenu.Models;
using wenu.Services;

namespace wenu.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] UsersDTO request)
        {
            var user = await _authService.RegisterAsync(request);
            return CreatedAtAction(nameof(Register), user);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDTO request)
        {
            var user = await _authService.LoginAsync(request);
            return Ok(user);
        }
    }
}
