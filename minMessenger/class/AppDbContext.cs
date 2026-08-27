using Microsoft.EntityFrameworkCore;
using minMessenger;

namespace MinMessenger
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Chat> Chats { get; set; }
        public DbSet<ChatMember> ChatMembers { get; set; }
        public DbSet<Message> Messages { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(
                @"Data Source=DESKTOP-JDAG33F\SQLEXPRESS;
                  Initial Catalog=MinMessenger;
                  Integrated Security=True;
                  Connect Timeout=30;
                  Encrypt=True;
                  TrustServerCertificate=True;
                  ApplicationIntent=ReadWrite;
                  MultiSubnetFailover=False");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ChatMember>()
                .HasIndex(x => new { x.ChatId, x.UserId })
                .IsUnique();
        }
    }
}