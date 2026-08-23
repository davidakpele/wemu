using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace wenu.Resopones
{
    public class UserResponseDto
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
    }
}