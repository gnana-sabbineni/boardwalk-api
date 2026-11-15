using BoardWalk.Api.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace BoardWalk.Api.Data
{
    /// <summary>
    /// Makes sure the six computer-player accounts exist. They are ordinary rows in the Users table so lobbies,
    /// friend lists and game seats treat them like anyone else; they cannot log in (login rejects IsBot users).
    /// Runs on every startup and only inserts what is missing, so it is safe to run repeatedly and on several servers
    /// (the unique email index turns a rare simultaneous insert into a harmless failed attempt).
    /// </summary>
    public static class BotSeeder
    {
        // Fixed ids so bots are identical in every environment.
        private static readonly (Guid Id, string Name)[] Bots =
        {
            (new Guid("b0000000-0000-0000-0000-000000000001"), "Marina"),
            (new Guid("b0000000-0000-0000-0000-000000000002"), "Otto"),
            (new Guid("b0000000-0000-0000-0000-000000000003"), "Juniper"),
            (new Guid("b0000000-0000-0000-0000-000000000004"), "Basil"),
            (new Guid("b0000000-0000-0000-0000-000000000005"), "Coral"),
            (new Guid("b0000000-0000-0000-0000-000000000006"), "Finn"),
        };

        public static async Task EnsureAsync(IServiceProvider services, ILogger logger)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            List<(Guid Id, string Name)> missing;
            try
            {
                var existing = await db.Users.Where(u => u.IsBot).Select(u => u.Id).ToListAsync();
                missing = Bots.Where(b => !existing.Contains(b.Id)).ToList();
            }
            catch (Exception ex)
            {
                // Most likely the database has not been migrated yet (no IsBot column). Don't crash startup over it.
                logger.LogError(ex, "Could not check bot users. Have the EF migrations been applied?");
                return;
            }

            if (missing.Count == 0) return;

            foreach (var (id, name) in missing)
            {
                db.Users.Add(new User
                {
                    Id = id,
                    FirstName = name,
                    LastName = "Bot",
                    Email = $"{name.ToLowerInvariant()}@bots.boardwalk.invalid",
                    PasswordHash = "!bot-account-cannot-log-in",
                    Salt = "!",
                    IsBot = true,
                    CreatedAt = DateTime.UtcNow
                });
            }

            try
            {
                await db.SaveChangesAsync();
                logger.LogInformation("Seeded {Count} bot users", missing.Count);
            }
            catch (DbUpdateException ex)
            {
                // Another server seeded at the same moment — fine, the bots exist.
                logger.LogWarning(ex, "Bot seeding raced with another instance");
            }
        }
    }
}
