using BoardWalk.Api.Data.Models;
using BoardWalk.Api.Data.Repositories;
using BoardWalk.Api.Services.Common;
using BoardWalk.Api.Services.Interfaces;
using BoardWalk.Api.Services.Models.Requests;
using BoardWalk.Api.Services.Models.Responses;

namespace BoardWalk.Api.Services.Implementations
{
    public class UserService : IUserService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UserService> _logger;
        private readonly IUserRepository _userRepository;
        private readonly JwtTokenGenerator _tokenGenerator;
        private readonly IEmailSender _emailSender;
        private readonly IConfiguration _config;


        public UserService(IUnitOfWork unitOfWork, ILogger<UserService> logger, JwtTokenGenerator tokenGenerator , IEmailSender emailSender, IConfiguration config)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _userRepository = unitOfWork.Users;
            _tokenGenerator = tokenGenerator;
            _emailSender = emailSender;
            _config = config;
        }


        /// <summary>
        /// Registers a new user with the provided details. It checks if the email is already registered, generates a salt and hashes the password, creates a new user entity, and saves it to the repository.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<LoginResponse?> RegisterAsync(RegisterUserRequest request)
        {
            var existingUser = await _userRepository.GetByEmailAsync(request.Email);
            if (existingUser != null)
            {
                return null;
            }

            var salt = PasswordHasher.GenerateSalt();
            var hashedPassword = PasswordHasher.HashPassword(request.Password, salt);
            var newUser = new User
            {
                Id = Guid.NewGuid(),
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                PasswordHash = hashedPassword,
                Salt = salt,
                CreatedAt = DateTime.UtcNow
            };

            await _userRepository.AddAsync(newUser);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("New user registered with email: {Email}", request.Email);

            return new LoginResponse
            {
                Token = _tokenGenerator.GenerateToken(newUser),
                User = new UserResponse
                {
                    Id = newUser.Id,
                    FirstName = newUser.FirstName,
                    LastName = newUser.LastName,
                    Email = newUser.Email
                }
            };
        }

        /// <summary>
        /// Authenticates a user by verifying the provided email and password. It retrieves the user by email, checks if the password is valid using the stored salt and hash, and returns a UserResponse object with user details if authentication is successful.
        /// </summary>
        /// <param name="email"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            var user = await _userRepository.GetByEmailAsync(request.Email);
            if (user == null)
            {
                throw new InvalidOperationException("Invalid email or password.");
            }
            bool isPasswordValid = PasswordHasher.VerifyPassword(request.Password, user.Salt, user.PasswordHash);
            if (!isPasswordValid)
            {
                throw new InvalidOperationException("Invalid email or password.");
            }
            return new LoginResponse
            {
                Token = _tokenGenerator.GenerateToken(user), 
                User = new UserResponse
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email
                }
            };
        }

        /// <summary>
        /// Updates the profile of an existing user. It checks for email uniqueness, validates password change requests, and updates the user's first name, last name, email, and password hash if applicable. Finally, it saves the changes to the repository.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                throw new InvalidOperationException("User not found.");

            // --- Email uniqueness check (only matters if it's actually changing) ---
            if (!string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase))
            {
                var existing = await _userRepository.GetByEmailAsync(request.Email);
                if (existing != null)
                    throw new InvalidOperationException("That email is already in use.");
            }

            // --- Password change is optional, but must be requested consistently ---
            bool wantsPasswordChange = !string.IsNullOrWhiteSpace(request.NewPassword);
            bool suppliedCurrentPassword = !string.IsNullOrWhiteSpace(request.CurrentPassword);

            if (wantsPasswordChange && !suppliedCurrentPassword)
                throw new InvalidOperationException("CurrentPassword is required to set a new password.");

            if (suppliedCurrentPassword && !wantsPasswordChange)
                throw new InvalidOperationException("NewPassword is required when CurrentPassword is provided.");

            if (wantsPasswordChange)
            {
                bool isCurrentValid = PasswordHasher.VerifyPassword(request.CurrentPassword!, user.Salt, user.PasswordHash);
                if (!isCurrentValid)
                    throw new InvalidOperationException("Current password is incorrect.");

                var newSalt = PasswordHasher.GenerateSalt();
                user.Salt = newSalt;
                user.PasswordHash = PasswordHasher.HashPassword(request.NewPassword!, newSalt);
            }

            // --- Apply name/email changes (always happens) ---
            user.FirstName = request.FirstName;
            user.LastName = request.LastName;
            user.Email = request.Email;

            _userRepository.Update(user);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Profile updated for user {UserId} (password changed: {PasswordChanged})", userId, wantsPasswordChange);
        }

        public async Task ForgotPasswordAsync(ForgotPasswordRequest request)
        {
            var user = await _userRepository.GetByEmailAsync(request.Email);

            if (user != null)
            {
                var rawToken = PasswordResetTokenHelper.GenerateRawToken();

                var resetToken = new PasswordResetToken
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    TokenHash = PasswordResetTokenHelper.Hash(rawToken),
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.PasswordResetTokens.AddAsync(resetToken);
                await _unitOfWork.SaveChangesAsync();

                var frontendBaseUrl = _config["Frontend:BaseUrl"]; // e.g. "http://localhost:5173"
                var resetLink = $"{frontendBaseUrl}/reset-password?token={rawToken}";

                await _emailSender.SendPasswordResetEmailAsync(user.Email, resetLink);

                _logger.LogInformation("Password reset requested for user {UserId}", user.Id);
            }
            else
            {
                // Deliberately no-op — same response either way, see explanation above.
                _logger.LogInformation("Password reset requested for unknown email {Email}", request.Email);
            }

            // No return value either way — the controller sends an identical response
            // regardless of what happened here.
        }

        public async Task ResetPasswordAsync(ResetPasswordRequest request)
        {
            var tokenHash = PasswordResetTokenHelper.Hash(request.Token);
            var resetToken = await _unitOfWork.PasswordResetTokens.GetByTokenHashAsync(tokenHash);

            if (resetToken == null)
                throw new InvalidOperationException("Invalid or expired reset link.");

            if (resetToken.UsedAt != null)
                throw new InvalidOperationException("This reset link has already been used.");

            if (resetToken.ExpiresAt < DateTime.UtcNow)
                throw new InvalidOperationException("This reset link has expired.");

            var user = resetToken.User;
            var newSalt = PasswordHasher.GenerateSalt();
            user.Salt = newSalt;
            user.PasswordHash = PasswordHasher.HashPassword(request.NewPassword, newSalt);
            _userRepository.Update(user);

            resetToken.UsedAt = DateTime.UtcNow;
            _unitOfWork.PasswordResetTokens.Update(resetToken);

            await _unitOfWork.SaveChangesAsync(); // password change + token consumption committed together

            _logger.LogInformation("Password reset completed for user {UserId}", user.Id);
        }

    }
}
