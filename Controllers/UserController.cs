using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace wenu.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] 
    public class UserController : ControllerBase
    {
        [HttpGet("hello")]
        [Authorize(Roles = "ADMIN,USER")]
        public IActionResult HelloWorld()
        {
            // Get the username from the JWT token claims
            var username = User.FindFirst(ClaimTypes.Name)?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return Ok(new
            {
                message = "Hello World!",
                user = username,
                timestamp = DateTime.UtcNow
            });
        }

        [HttpGet("profile")]
        public IActionResult GetProfile()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            var email = User.FindFirst(ClaimTypes.Email)?.Value;

            return Ok(new
            {
                userId,
                username,
                email,
                message = "User profile retrieved successfully"
            });
        }
    }
}