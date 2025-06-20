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
            Console.WriteLine($"🔍 Login attempt for: {loginDto.Email}");

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.email == loginDto.Email && u.is_active);

            if (user == null)
            {
                Console.WriteLine($"❌ User not found: {loginDto.Email}");
                return null;
            }

            if (!BCrypt.Net.BCrypt.Verify(loginDto.Password, user.password_hash))
            {
                Console.WriteLine($"❌ Invalid password for: {loginDto.Email}");
                return null;
            }

            Console.WriteLine($"✅ User authenticated: {user.first_name} {user.last_name} ({user.Role?.role_name})");

            // Generate JWT token
            var token = GenerateJwtToken(user);

            // Update user session
            user.jwt_token = token;
            user.last_login = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            Console.WriteLine($"🎫 Token generated for user {user.user_id}");

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
            if (user == null) return false;

            user.jwt_token = null;
            await _context.SaveChangesAsync();
            Console.WriteLine($"🚪 User {userId} logged out");
            return true;
        }

        public async Task<UserInfoDto?> GetUserInfoAsync(int userId)
        {
            Console.WriteLine($"🔍 Getting user info for ID: {userId}");

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.user_id == userId && u.is_active);

            if (user == null)
            {
                Console.WriteLine($"❌ User {userId} not found or inactive");
                return null;
            }

            Console.WriteLine($"✅ User info found: {user.first_name} {user.last_name} ({user.Role?.role_name})");

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
            var jwtKey = _configuration["Jwt:Key"];
            var jwtIssuer = _configuration["Jwt:Issuer"];
            var jwtAudience = _configuration["Jwt:Audience"];

            Console.WriteLine($"🔍 JWT Generation Debug:");
            Console.WriteLine($"   User ID: {user.user_id}");
            Console.WriteLine($"   Role: {user.Role?.role_name} (ID: {user.role_id})");
            Console.WriteLine($"   Key Length: {jwtKey?.Length}");

            if (string.IsNullOrEmpty(jwtKey))
                throw new InvalidOperationException("JWT Key not configured");

            var key = Encoding.UTF8.GetBytes(jwtKey);
            var tokenHandler = new JwtSecurityTokenHandler();

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.user_id.ToString()),
                new Claim(ClaimTypes.Email, user.email),
                new Claim(ClaimTypes.Name, $"{user.first_name} {user.last_name}"),
                new Claim(ClaimTypes.Role, user.Role?.role_name ?? ""),
                new Claim("role_id", user.role_id.ToString()),
                new Claim("employee_id", user.employee_id)
            };

            Console.WriteLine($"🎫 Token claims:");
            foreach (var claim in claims)
            {
                Console.WriteLine($"   {claim.Type}: {claim.Value}");
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(24),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256),
                Issuer = jwtIssuer,
                Audience = jwtAudience
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            Console.WriteLine($"🎫 Generated token: {tokenString.Substring(0, 50)}...");
            
            return tokenString;
        }
    }
}