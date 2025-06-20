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
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            if (identity == null)
                return Unauthorized();

            var userId = int.Parse(identity.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            
            if (userId == 0)
                return Unauthorized();

            var userInfo = await _authService.GetUserInfoAsync(userId);
            
            if (userInfo == null)
                return Unauthorized();

            return Ok(userInfo);
        }

        [HttpGet("test")]
        public IActionResult Test()
        {
            return Ok(new { 
                message = "Auth controller is working!", 
                timestamp = DateTime.UtcNow,
                server = "GasopperCRM API",
                version = "1.0"
            });
        }

        // Helper endpoint for development - get user claims
        [HttpGet("claims")]
        [Authorize]
        public IActionResult GetClaims()
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            if (identity == null)
                return Unauthorized();

            var claims = identity.Claims.Select(c => new { 
                Type = c.Type, 
                Value = c.Value 
            }).ToList();

            return Ok(new { 
                isAuthenticated = identity.IsAuthenticated,
                name = identity.Name,
                claims = claims
            });
        }
    }
}