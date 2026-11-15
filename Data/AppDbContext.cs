using BoardWalk.Api.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace BoardWalk.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<FriendRequest> FriendRequests => Set<FriendRequest>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
        public DbSet<Game> Games => Set<Game>();
        public DbSet<GamePlayer> GamePlayers => Set<GamePlayer>();
        public DbSet<GameProperty> GameProperties => Set<GameProperty>();
        public DbSet<GameEvent> GameEvents => Set<GameEvent>();
        public DbSet<Lobby> Lobbies => Set<Lobby>();
        public DbSet<LobbyMember> LobbyMembers => Set<LobbyMember>();
        public DbSet<LobbyInvite> LobbyInvites => Set<LobbyInvite>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.Email)
                      .IsUnique();

                entity.HasOne(u => u.CurrentLobby)
                      .WithMany()
                      .HasForeignKey(u => u.CurrentLobbyId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<FriendRequest>(entity =>
            {
                // A FriendRequest has TWO separate relationships to the SAME table (User).
                // We must tell EF Core explicitly which FK goes with which navigation,
                // and set DeleteBehavior.Restrict on both — otherwise EF Core throws an
                // error about "multiple cascade paths" (deleting one User could cascade-
                // delete a FriendRequest through two different routes at once, which is
                // ambiguous and Postgres/EF Core refuses to allow by default).
                entity.HasOne(fr => fr.Requester)
                      .WithMany()
                      .HasForeignKey(fr => fr.RequesterId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(fr => fr.Addressee)
                      .WithMany()
                      .HasForeignKey(fr => fr.AddresseeId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasOne(n => n.Recipient)
                      .WithMany()
                      .HasForeignKey(n => n.RecipientUserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(n => n.Actor)
                      .WithMany()
                      .HasForeignKey(n => n.ActorUserId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Speeds up "get all notifications for this user" — run every notification-bar load.
                entity.HasIndex(n => n.RecipientUserId);
            });

            modelBuilder.Entity<PasswordResetToken>(entity =>
            {
                entity.HasOne(t => t.User)
                      .WithMany()
                      .HasForeignKey(t => t.UserId)
                      .OnDelete(DeleteBehavior.Cascade); // if a User is ever deleted, their old reset tokens go with them

                entity.HasIndex(t => t.TokenHash); // fast lookup when the user submits their token back
            });

            modelBuilder.Entity<Lobby>(entity =>
            {
                entity.HasOne(l => l.Host)
                      .WithMany()
                      .HasForeignKey(l => l.HostUserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<LobbyMember>(entity =>
            {
                entity.HasOne(m => m.Lobby)
                      .WithMany(l => l.Members)
                      .HasForeignKey(m => m.LobbyId)
                      .OnDelete(DeleteBehavior.Cascade); // deleting a Lobby cleans up its members automatically

                entity.HasOne(m => m.User)
                      .WithMany()
                      .HasForeignKey(m => m.UserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(m => m.LobbyId);
            });

            modelBuilder.Entity<LobbyInvite>(entity =>
            {
                entity.HasOne(i => i.Lobby)
                      .WithMany()
                      .HasForeignKey(i => i.LobbyId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(i => i.Inviter)
                      .WithMany()
                      .HasForeignKey(i => i.InviterUserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(i => i.Invitee)
                      .WithMany()
                      .HasForeignKey(i => i.InviteeUserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Game>(entity =>
            {
                entity.HasOne(g => g.Lobby)
                      .WithMany()
                      .HasForeignKey(g => g.LobbyId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.Property(g => g.ChanceDeckOrder).HasMaxLength(200);
                entity.HasIndex(g => g.LobbyId);
            });

            modelBuilder.Entity<GamePlayer>(entity =>
            {
                entity.HasOne(p => p.Game)
                      .WithMany(g => g.Players)
                      .HasForeignKey(p => p.GameId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(p => p.User)
                      .WithMany()
                      .HasForeignKey(p => p.UserId)
                      .OnDelete(DeleteBehavior.Restrict);

                // A user appears at most once per game.
                entity.HasIndex(p => new { p.GameId, p.UserId }).IsUnique();
                entity.HasIndex(p => p.UserId);
            });

            modelBuilder.Entity<GameProperty>(entity =>
            {
                entity.HasOne(p => p.Game)
                      .WithMany(g => g.Properties)
                      .HasForeignKey(p => p.GameId)
                      .OnDelete(DeleteBehavior.Cascade);

                // One row per tile per game.
                entity.HasIndex(p => new { p.GameId, p.TileIndex }).IsUnique();
            });

            modelBuilder.Entity<GameEvent>(entity =>
            {
                entity.HasOne(e => e.Game)
                      .WithMany(g => g.Events)
                      .HasForeignKey(e => e.GameId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.Message).HasMaxLength(500);
                entity.HasIndex(e => new { e.GameId, e.Sequence });
            });
        }

    }
}
