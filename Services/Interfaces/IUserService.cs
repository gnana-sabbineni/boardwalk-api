using BoardWalk.Api.Services.Models.Requests;
using BoardWalk.Api.Services.Models.Responses;

namespace BoardWalk.Api.Services.Interfaces
{
    public interface IUserService
    {
        Task<Guid?> RegisterAsync(RegisterUserRequest request);
        Task<LoginResponse> LoginAsync(LoginRequest request);

        /// <summary>
        /// Updates the current user's first name, last name, and email. If NewPassword is
        /// supplied in the request, also verifies CurrentPassword and updates the password
        /// (generating a new salt) in the same operation.
        /// </summary>
        /// <param name="userId">The authenticated user's ID, taken from their JWT — never trust a client-supplied ID here.</param>
        /// <param name="request">The new profile values, and optionally a password change.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the user doesn't exist, the new email is already taken by another account,
        /// NewPassword was given without a matching correct CurrentPassword, or CurrentPassword
        /// was given without a NewPassword.
        /// </exception>
        Task UpdateProfileAsync(Guid userId, UpdateProfileRequest request);
    }
}
