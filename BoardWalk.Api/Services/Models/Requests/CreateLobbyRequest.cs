// Services/Models/Requests/CreateLobbyRequest.cs
using System.ComponentModel.DataAnnotations;

namespace BoardWalk.Api.Services.Models.Requests
{
    public class CreateLobbyRequest
    {
        [Required, Range(2, 8)]
        public int MaxPlayers { get; set; }
    }

    public class InviteToLobbyRequest
    {
        [Required]
        public Guid InviteeUserId { get; set; }
    }
}