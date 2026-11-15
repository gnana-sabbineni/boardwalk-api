using System.ComponentModel.DataAnnotations;

namespace BoardWalk.Api.Services.Models.Requests
{
    public class RespondToNotificationRequest
    {
        /// <summary>Must be exactly "accept" or "decline".</summary>
        [Required]
        public string Action { get; set; } = string.Empty;
    }
}