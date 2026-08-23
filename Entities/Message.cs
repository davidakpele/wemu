using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace wenu.Entities
{
    public class Message
    {
        public int Id { get; set; }
        public int SenderId { get; set; }
        public int ParticipantId { get; set; }
        
        public string? Content { get; set; } 
        public DateTime CreatedDate { get; set; }
        public bool IsRead { get; set; }

        public Users? Sender { get; set; }
        public Users? Participant { get; set; }
    }
}