using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using gasopper_crm_server.Data;
using gasopper_crm_server.DTOs;
using gasopper_crm_server.Models;

namespace gasopper_crm_server.Services
{
    public interface IAuthService
    {
        Task<LoginResponseDto?> LoginAsync(LoginDto loginDto);
        Task<bool> LogoutAsync(int userId);
        Task<UserInfoDto?> GetUserInfoAsync(int userId);
    }

    public class AuthService : IAuthService
    {
        private readonly GasopperDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthService(GasopperDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<LoginResponseDto?> LoginAsync(LoginDto loginDto)
        {
            // Find user by email
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.email == loginDto.Email);

            if (user == null || !user.is_active)
                return null;

            if (!BCrypt.Net.BCrypt.Verify(loginDto.Password, user.password_hash))
                return null;

            // Generate JWT token
            var token = GenerateJwtToken(user);

            // Update user's token and login time
            user.jwt_token = token;
            user.last_login = DateTime.UtcNow;
            user.iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            user.exp = DateTimeOffset.UtcNow.AddDays(365).ToUnixTimeSeconds(); // 1 year expiration

            await _context.SaveChangesAsync();

            return new LoginResponseDto
            {
                Success = true,
                Message = "Login successful",
                Token = token,
                User = new UserInfoDto
                {
                    UserId = user.user_id,
                    EmployeeId = user.employee_id,
                    Email = user.email,
                    FirstName = user.first_name,
                    LastName = user.last_name,
                    RoleName = user.Role?.role_name ?? ""
                }
            };
        }

        public async Task<bool> LogoutAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return false;

            user.jwt_token = null; // Clear the token
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<UserInfoDto?> GetUserInfoAsync(int userId)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.user_id == userId);

            if (user == null || !user.is_active)
                return null;

            return new UserInfoDto
            {
                UserId = user.user_id,
                EmployeeId = user.employee_id,
                Email = user.email,
                FirstName = user.first_name,
                LastName = user.last_name,
                RoleName = user.Role?.role_name ?? ""
            };
        }

        private string GenerateJwtToken(User user)
        {
            // Get JWT configuration with proper null checking
            var jwtKey = _configuration["Jwt:Key"];
            var jwtIssuer = _configuration["Jwt:Issuer"];
            var jwtAudience = _configuration["Jwt:Audience"];

            // Validate JWT configuration
            if (string.IsNullOrEmpty(jwtKey))
            {
                throw new InvalidOperationException("JWT Key is not configured in appsettings.json. Please check Jwt:Key setting.");
            }

            if (string.IsNullOrEmpty(jwtIssuer))
            {
                throw new InvalidOperationException("JWT Issuer is not configured in appsettings.json. Please check Jwt:Issuer setting.");
            }

            if (string.IsNullOrEmpty(jwtAudience))
            {
                throw new InvalidOperationException("JWT Audience is not configured in appsettings.json. Please check Jwt:Audience setting.");
            }

            // Ensure JWT key is long enough
            if (jwtKey.Length < 32)
            {
                throw new InvalidOperationException("JWT Key must be at least 32 characters long for security reasons.");
            }

            var key = Encoding.UTF8.GetBytes(jwtKey); // Changed from ASCII to UTF8
            var tokenHandler = new JwtSecurityTokenHandler();

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.user_id.ToString()),
                new Claim(ClaimTypes.Email, user.email),
                new Claim(ClaimTypes.Name, $"{user.first_name} {user.last_name}"),
                new Claim(ClaimTypes.Role, user.Role?.role_name ?? ""),
                new Claim("role_id", user.role_id.ToString()),
                new Claim("employee_id", user.employee_id),
                new Claim("first_name", user.first_name),
                new Claim("last_name", user.last_name)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddDays(365), // 1 year
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = jwtIssuer,
                Audience = jwtAudience
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}