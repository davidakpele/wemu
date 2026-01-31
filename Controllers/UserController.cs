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