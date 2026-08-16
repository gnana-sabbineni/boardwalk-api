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

        public UserService(IUnitOfWork unitOfWork, ILogger<UserService> logger, JwtTokenGenerator tokenGenerator)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _userRepository = unitOfWork.Users;
            _tokenGenerator = tokenGenerator;
        }


        /// <summary>
        /// Registers a new user with the provided details. It checks if the email is already registered, generates a salt and hashes the password, creates a new user entity, and saves it to the repository.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<Guid?> RegisterAsync(RegisterUserRequest request)
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
            return newUser.Id;
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

    }
}
