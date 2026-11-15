using BoardWalk.Monopoly.Models;
using Microsoft.EntityFrameworkCore;

namespace BoardWalk.Monopoly.Data
{
    /// <summary>
    /// Maps this module's tables into the host's DbContext. The host calls modelBuilder.ApplyMonopolyModel() in
    /// OnModelCreating. UserId / RoomId columns deliberately have NO foreign keys to the host's tables: the module
    /// stays independent of how the host stores users and lobbies.
    /// </summary>
    public static class MonopolyModel
    {
        public static ModelBuilder ApplyMonopolyModel(this ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Game>(entity =>
            {
                entity.ToTable("Games");
                entity.Property(g => g.ChanceDeckOrder).HasMaxLength(200);
                entity.HasIndex(g => g.RoomId);
                // The turn timer scans in-progress games by deadline.
                entity.HasIndex(g => new { g.Status, g.TurnDeadlineAt });
            });

            modelBuilder.Entity<GamePlayer>(entity =>
            {
                entity.ToTable("GamePlayers");
                entity.HasOne(p => p.Game).WithMany(g => g.Players).HasForeignKey(p => p.GameId).OnDelete(DeleteBehavior.Cascade);
                entity.Property(p => p.FirstName).HasMaxLength(50);
                entity.Property(p => p.LastName).HasMaxLength(50);
                entity.HasIndex(p => new { p.GameId, p.UserId }).IsUnique();
                entity.HasIndex(p => p.UserId);
            });

            modelBuilder.Entity<GameProperty>(entity =>
            {
                entity.ToTable("GameProperties");
                entity.HasOne(p => p.Game).WithMany(g => g.Properties).HasForeignKey(p => p.GameId).OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(p => new { p.GameId, p.TileIndex }).IsUnique();
            });

            modelBuilder.Entity<GameEvent>(entity =>
            {
                entity.ToTable("GameEvents");
                entity.HasOne(e => e.Game).WithMany().HasForeignKey(e => e.GameId).OnDelete(DeleteBehavior.Cascade);
                // The history endpoint pages by (game, sequence).
                entity.HasIndex(e => new { e.GameId, e.Sequence }).IsUnique();
            });

            modelBuilder.Entity<GameSnapshot>(entity =>
            {
                entity.ToTable("GameSnapshots");
                entity.HasOne(s => s.Game).WithMany().HasForeignKey(s => s.GameId).OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(s => new { s.GameId, s.Turn, s.Seat }).IsUnique();
            });

            modelBuilder.Entity<Trade>(entity =>
            {
                entity.ToTable("Trades");
                entity.HasOne(t => t.Game).WithMany(g => g.Trades).HasForeignKey(t => t.GameId).OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(t => new { t.GameId, t.Status });
            });

            modelBuilder.Entity<TradeItem>(entity =>
            {
                entity.ToTable("TradeItems");
                entity.HasOne(i => i.Trade).WithMany(t => t.Items).HasForeignKey(i => i.TradeId).OnDelete(DeleteBehavior.Cascade);
            });

            return modelBuilder;
        }
    }
}
