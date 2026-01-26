using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;
using wenu.Entities;

namespace wumo.Configs
{
    public class AppDbContext : IdentityDbContext<Users, IdentityRole<int>, int>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        public DbSet<BlockedParticipant> BlockedParticipant {get; set;}
        public DbSet<LiveStream> LiveStreams {get; set;}
        public DbSet<LiveStreamCoHost> LiveStreamCoHost {get; set;}
        public DbSet<LiveStreamMessage> LiveStreamMessage {get; set;}
        public DbSet<LiveStreamParticipant> LiveStreamParticipant {get; set;}
        public DbSet<Message> Messages {get; set;}
        public DbSet<UserRecord> UserRecords { get; set; }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Rename Identity tables
            modelBuilder.Entity<Users>().ToTable("Users");
            modelBuilder.Entity<IdentityRole<int>>().ToTable("Roles");
            modelBuilder.Entity<IdentityUserRole<int>>().ToTable("UserRoles");
            modelBuilder.Entity<IdentityUserClaim<int>>().ToTable("UserClaims");
            modelBuilder.Entity<IdentityUserLogin<int>>().ToTable("UserLogins");
            modelBuilder.Entity<IdentityRoleClaim<int>>().ToTable("RoleClaims");
            modelBuilder.Entity<IdentityUserToken<int>>().ToTable("UserTokens");

            modelBuilder.Entity<Users>(entity =>
            {
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.UserName).IsUnique();
            });

            modelBuilder.Entity<LiveStream>(entity =>
            {
                entity.HasIndex(e => e.HostUserId);
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.StartedAt);
                
                entity.HasOne(e => e.HostUser)
                    .WithMany(u => u.HostedStreams)
                    .HasForeignKey(e => e.HostUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<LiveStreamParticipant>(entity =>
            {
                entity.HasIndex(e => new { e.LiveStreamId, e.UserId });
                entity.HasIndex(e => e.IsCurrentlyWatching);
                
                entity.HasOne(e => e.LiveStream)
                    .WithMany(ls => ls.Participants)
                    .HasForeignKey(e => e.LiveStreamId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(e => e.User)
                    .WithMany(u => u.ParticipatedStreams)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<LiveStreamCoHost>(entity =>
            {
                entity.HasIndex(e => new { e.LiveStreamId, e.UserId });
                entity.HasIndex(e => e.IsActive);
                
                entity.HasOne(e => e.LiveStream)
                    .WithMany(ls => ls.CoHosts)
                    .HasForeignKey(e => e.LiveStreamId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(e => e.User)
                    .WithMany(u => u.CoHostedStreams)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<LiveStreamMessage>(entity =>
            {
                entity.HasIndex(e => e.LiveStreamId);
                entity.HasIndex(e => e.SentAt);
                
                entity.HasOne(e => e.LiveStream)
                    .WithMany(ls => ls.Messages)
                    .HasForeignKey(e => e.LiveStreamId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<BlockedParticipant>(entity =>
            {
                entity.HasIndex(e => new { e.LiveStreamId, e.BlockedUserId });
                
                entity.HasOne(e => e.LiveStream)
                    .WithMany(ls => ls.BlockedParticipants)
                    .HasForeignKey(e => e.LiveStreamId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(e => e.BlockedUser)
                    .WithMany()
                    .HasForeignKey(e => e.BlockedUserId)
                    .OnDelete(DeleteBehavior.Restrict);
                
                entity.HasOne(e => e.BlockedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.BlockedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Participant)
                .WithMany()
                .HasForeignKey(m => m.ParticipantId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
