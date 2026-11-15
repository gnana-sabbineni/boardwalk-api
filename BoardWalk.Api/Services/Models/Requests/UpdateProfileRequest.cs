using System.ComponentModel.DataAnnotations;

namespace BoardWalk.Api.Services.Models.Requests
{
    /// <summary>
    /// Request body for updating the current user's profile. Name and email are always required.
    /// Password fields are optional — include both NewPassword and CurrentPassword only if the
    /// user wants to change their password as part of this update.
    /// </summary>
    public class UpdateProfileRequest
    {
        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Required only if NewPassword is provided — verifies the request is really
        /// coming from the account owner before allowing a password change.
        /// </summary>
        public string? CurrentPassword { get; set; }

        /// <summary>
        /// Optional. If provided, CurrentPassword must also be provided and correct.
        /// </summary>
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
        public string? NewPassword { get; set; }
    }
}