using System.ComponentModel.DataAnnotations;

namespace BoardWalk.Api.Data.Models
{
    public class User
    {
        public Guid Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public string Salt { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Guid? CurrentLobbyId { get; set; }
        public Lobby? CurrentLobby { get; set; }

        // Optimistic concurrency token — EF Core includes this in the WHERE clause of every
        // UPDATE, so a stale write (based on data read before someone else changed it) fails
        // instead of silently overwriting a concurrent change. This is what closes the
        // double-accept race (Scenario 4) on CurrentLobbyId specifically.
        [Timestamp]
        public byte[] RowVersion { get; set; } = null!;
    }
}
