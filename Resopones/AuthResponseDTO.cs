using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace wenu.Resopones
{
    public class AuthResponseDTO
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty; 
    }
}