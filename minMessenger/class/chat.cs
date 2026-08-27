using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace minMessenger{
    public class Chat
    {
        public int Id { get; set; }
        public string Type { get; set; }          // "private" / "group"
        public string Title { get; set; }
        public int CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }

        public User CreatedByUser { get; set; }
        public List<ChatMember> Members { get; set; } = new List<ChatMember>();
        public List<Message> Messages { get; set; } = new List<Message>();
    }
}
