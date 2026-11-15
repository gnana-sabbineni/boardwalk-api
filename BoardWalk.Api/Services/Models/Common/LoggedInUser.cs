namespace BoardWalk.Api.Services.Models.Common
{
    /// <summary>
    /// Represents the currently authenticated user, derived from their validated JWT claims.
    /// Passed around inside the service layer instead of a raw Guid userId, so any service
    /// that needs more than just the ID (email, name) doesn't need an extra database call.
    /// </summary>
    public class LoggedInUser
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
    }
}