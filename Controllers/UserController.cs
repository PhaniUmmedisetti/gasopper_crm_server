using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using gasopper_crm_server.DTOs;
using gasopper_crm_server.Services;

namespace gasopper_crm_server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // ALL endpoints require authentication
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        // ✅ ALL AUTHENTICATED USERS can view users (role-based filtering in service)
        // Salesperson sees only self, Manager sees self+team, Admin sees all
        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            var users = await _userService.GetUsersAsync(currentUserId, currentUserRole);
            
            return Ok(new { data = users, count = users.Count });
        }

        // ✅ ALL AUTHENTICATED USERS can view accessible user details
        [HttpGet("{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            var user = await _userService.GetUserByIdAsync(id, currentUserId, currentUserRole);
            
            if (user == null)
                return NotFound(new { message = "User not found or access denied" });
            
            return Ok(user);
        }

        // 🔒 ONLY Admin/Manager can create users (Salespeople CANNOT create other users)
        [HttpPost]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserDto createUserDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            var user = await _userService.CreateUserAsync(createUserDto, currentUserId, currentUserRole);
            
            if (user == null)
                return BadRequest(new { message = "Unable to create user. Check role permissions and data validity." });
            
            return CreatedAtAction(nameof(GetUser), new { id = user.UserId }, user);
        }

        // ✅ ALL AUTHENTICATED USERS can update accessible users (self or managed users)
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDto updateUserDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            var user = await _userService.UpdateUserAsync(id, updateUserDto, currentUserId, currentUserRole);
            
            if (user == null)
                return NotFound(new { message = "User not found or access denied" });
            
            return Ok(user);
        }

        // 🔒 ONLY Admin can delete/deactivate users
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            var success = await _userService.DeleteUserAsync(id, currentUserId, currentUserRole);
            
            if (!success)
                return BadRequest(new { message = "Unable to delete user. Cannot delete self or user not found." });
            
            return Ok(new { message = "User deactivated successfully" });
        }

        // 🔒 ONLY Manager/Admin can view team members
        [HttpGet("my-team")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> GetMyTeam()
        {
            var (currentUserId, _) = GetCurrentUserInfo();
            var teamMembers = await _userService.GetMyTeamAsync(currentUserId);
            
            return Ok(new { data = teamMembers, count = teamMembers.Count });
        }

        // ✅ ALL AUTHENTICATED USERS can change their own password
        [HttpPost("{id}/change-password")]
        public async Task<IActionResult> ChangePassword(int id, [FromBody] ChangePasswordDto changePasswordDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var (currentUserId, _) = GetCurrentUserInfo();
            
            if (id != currentUserId)
                return Forbid("You can only change your own password");

            var success = await _userService.ChangePasswordAsync(id, changePasswordDto, currentUserId);
            
            if (!success)
                return BadRequest(new { message = "Password change failed. Check current password." });
            
            return Ok(new { message = "Password changed successfully" });
        }

        // ✅ ALL AUTHENTICATED USERS can view available roles
        [HttpGet("roles")]
        public async Task<IActionResult> GetRoles()
        {
            var roles = await _userService.GetRolesAsync();
            return Ok(roles);
        }

        private (int userId, int roleId) GetCurrentUserInfo()
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            var userId = int.Parse(identity?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var roleId = int.Parse(identity?.FindFirst("role_id")?.Value ?? "0");
            
            return (userId, roleId);
        }
    }
}