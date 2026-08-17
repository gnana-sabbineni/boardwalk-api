using System.ComponentModel.DataAnnotations;

namespace BoardWalk.Api.Services.Models.Requests
{
    public class SendFriendRequestRequest
    {
        [Required]
        public Guid TargetUserId { get; set; }
    }
}