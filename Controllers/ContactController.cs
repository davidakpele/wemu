using Microsoft.AspNetCore.Mvc;
using wenu.Models;
using wenu.Services;

namespace wenu.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ContactController : ControllerBase
    {
        private readonly IEmailService _emailService;

        public ContactController(IEmailService emailService)
        {
            _emailService = emailService;
        }

        /// <summary>
        /// Sends a contact/inquiry email to the provided address.
        /// </summary>
        [HttpPost("send")]
        public async Task<IActionResult> SendEmail([FromBody] ContactRequestDTO request)
        {
            await _emailService.SendContactEmailAsync(request);

            return Ok(new
            {
                success = true,
                message = "Your message has been sent successfully."
            });
        }
    }
}
