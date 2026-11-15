namespace BoardWalk.Api.Services.Models.Responses
{
    public class NotificationResponse
    {
        public Guid Id { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; }

        /// <summary>null while pending; "Accepted" or "Declined" once actioned.</summary>
        public string? Outcome { get; set; }

        /// <summary>Empty once Outcome is set — frontend renders a badge/plain text instead of buttons.</summary>
        public List<string> Actions { get; set; } = new();

        public DateTime CreatedAt { get; set; }
    }
}