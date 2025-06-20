using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using gasopper_crm_server.DTOs;
using gasopper_crm_server.Services;

namespace gasopper_crm_server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.LoginAsync(loginDto);
            
            if (result == null)
                return Unauthorized(new { success = false, message = "Invalid email or password" });

            return Ok(result);
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            
            if (userId == 0)
                return BadRequest(new { success = false, message = "Invalid user" });

            var success = await _authService.LogoutAsync(userId);
            
            if (!success)
                return BadRequest(new { success = false, message = "Logout failed" });

            return Ok(new { success = true, message = "Logout successful" });
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            Console.WriteLine("🔍 /me endpoint called");
            
            try
            {
                var identity = HttpContext.User.Identity as ClaimsIdentity;
                
                Console.WriteLine($"🔍 Identity authenticated: {identity?.IsAuthenticated}");
                Console.WriteLine($"🔍 Claims count: {identity?.Claims?.Count() ?? 0}");

                if (identity?.Claims != null)
                {
                    foreach (var claim in identity.Claims)
                    {
                        Console.WriteLine($"🔍 Claim: {claim.Type} = {claim.Value}");
                    }
                }

                if (identity == null || !identity.IsAuthenticated)
                {
                    Console.WriteLine("❌ Not authenticated");
                    return Unauthorized(new { message = "Not authenticated" });
                }

                var userIdClaim = identity.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                Console.WriteLine($"🔍 User ID claim: {userIdClaim}");

                var userId = int.Parse(userIdClaim ?? "0");
                
                if (userId == 0)
                {
                    Console.WriteLine("❌ Invalid user ID");
                    return Unauthorized(new { message = "Invalid user ID in token" });
                }

                var userInfo = await _authService.GetUserInfoAsync(userId);
                
                if (userInfo == null)
                {
                    Console.WriteLine("❌ User info not found");
                    return Unauthorized(new { message = "User not found or inactive" });
                }

                Console.WriteLine($"✅ Returning user info for: {userInfo.FirstName} {userInfo.LastName}");
                return Ok(userInfo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Exception in /me: {ex.Message}");
                return Unauthorized(new { message = "Authentication failed", error = ex.Message });
            }
        }

        [HttpGet("test")]
        public IActionResult Test()
        {
            return Ok(new { 
                message = "Auth controller is working!", 
                timestamp = DateTime.UtcNow
            });
        }

        [HttpGet("debug-claims")]
        [Authorize]
        public IActionResult DebugClaims()
        {
            Console.WriteLine("🔍 Debug claims endpoint called");
            
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            if (identity == null)
            {
                Console.WriteLine("❌ No identity");
                return Unauthorized(new { message = "No identity" });
            }

            var claims = identity.Claims.Select(c => new { 
                Type = c.Type, 
                Value = c.Value 
            }).ToList();

            Console.WriteLine($"🔍 Total claims: {claims.Count}");

            return Ok(new { 
                isAuthenticated = identity.IsAuthenticated,
                authenticationType = identity.AuthenticationType,
                name = identity.Name,
                claims = claims
            });
        }
    }
}