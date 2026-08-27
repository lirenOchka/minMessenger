using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace minMessenger {
    public class ChatMember
    {
        public int Id { get; set; }
        public int ChatId { get; set; }
        public int UserId { get; set; }
        public string Role { get; set; } = "member";
        public DateTime JoinedAt { get; set; }

        public Chat Chat { get; set; }
        public User User { get; set; }
    }
}
