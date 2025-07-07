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

        // Filtered users endpoint
        [HttpGet("filtered")]
        public async Task<IActionResult> GetFilteredUsers([FromQuery] string? status = null, [FromQuery] string? role = null, [FromQuery] DateTime? createdAfter = null, [FromQuery] DateTime? createdBefore = null)
        {
            var (currentUserId, currentUserRole) = GetCurrentUserInfo();

            var filters = new UserFilterDto
            {
                Status = status,
                Role = role,
                CreatedAfter = createdAfter,
                CreatedBefore = createdBefore
            };

            var users = await _userService.GetFilteredUsersAsync(currentUserId, currentUserRole, filters);

            return Ok(new
            {
                data = users,
                count = users.Count,
                filters = new
                {
                    status = filters.Status,
                    role = filters.Role,
                    createdAfter = filters.CreatedAfter,
                    createdBefore = filters.CreatedBefore
                }
            });
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
            {
                // ENHANCED: Return detailed validation errors
                var errors = ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray()
                    );

                return BadRequest(new
                {
                    message = "Validation failed",
                    errorCode = "VALIDATION_ERROR",
                    errors = errors
                });
            }

            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            var result = await _userService.CreateUserAsync(createUserDto, currentUserId, currentUserRole);

            if (!result.Success)
            {
                // ENHANCED: Return specific error details from service
                return BadRequest(new
                {
                    message = result.ErrorMessage,
                    errorCode = result.ErrorCode
                });
            }

            return CreatedAtAction(nameof(GetUser), new { id = result.Data!.UserId }, result.Data);
        }

        // ✅ ALL AUTHENTICATED USERS can update accessible users (self or managed users)
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDto updateUserDto)
        {
            if (!ModelState.IsValid)
            {
                // ENHANCED: Return detailed validation errors
                var errors = ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray()
                    );

                return BadRequest(new
                {
                    message = "Validation failed",
                    errorCode = "VALIDATION_ERROR",
                    errors = errors
                });
            }

            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            var result = await _userService.UpdateUserAsync(id, updateUserDto, currentUserId, currentUserRole);

            if (!result.Success)
            {
                // ENHANCED: Return specific error details from service
                if (result.ErrorCode == "USER_NOT_FOUND_OR_ACCESS_DENIED")
                {
                    return NotFound(new
                    {
                        message = result.ErrorMessage,
                        errorCode = result.ErrorCode
                    });
                }

                return BadRequest(new
                {
                    message = result.ErrorMessage,
                    errorCode = result.ErrorCode
                });
            }

            return Ok(result.Data);
        }

        // 🔒 ONLY Admin can delete/deactivate users
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            var result = await _userService.DeleteUserAsync(id, currentUserId, currentUserRole);

            if (!result.Success)
            {
                // ENHANCED: Return specific error details from service
                if (result.ErrorCode == "USER_NOT_FOUND")
                {
                    return NotFound(new
                    {
                        message = result.ErrorMessage,
                        errorCode = result.ErrorCode
                    });
                }

                if (result.ErrorCode == "INSUFFICIENT_PERMISSIONS" ||
                    result.ErrorCode == "SELF_DELETION_FORBIDDEN")
                {
                    return Forbid(result.ErrorMessage);
                }

                return BadRequest(new
                {
                    message = result.ErrorMessage,
                    errorCode = result.ErrorCode
                });
            }

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

        // COMMENTED OUT: Change password endpoint for OTP-only authentication
        /*
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
        */

        // ✅ ALL AUTHENTICATED USERS can view available roles
        [HttpGet("roles")]
        public async Task<IActionResult> GetRoles()
        {
            try
            {
                var roles = await _userService.GetRolesAsync();
                return Ok(roles);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Error fetching roles",
                    errorCode = "INTERNAL_ERROR"
                });
            }
        }

        // Get available managers for assignment dropdown
        [HttpGet("managers")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> GetAvailableManagers()
        {
            try
            {
                var (currentUserId, currentUserRole) = GetCurrentUserInfo();
                var managers = await _userService.GetAvailableManagersAsync(currentUserId, currentUserRole);

                return Ok(new { data = managers, count = managers.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Error fetching available managers",
                    errorCode = "INTERNAL_ERROR"
                });
            }
        }

        // User statistics with filtering
        [HttpGet("stats")]
        public async Task<IActionResult> GetUserStats([FromQuery] string? status = null, [FromQuery] string? role = null)
        {
            try
            {
                var (currentUserId, currentUserRole) = GetCurrentUserInfo();
                var users = await _userService.GetUsersAsync(currentUserId, currentUserRole);

                // Apply filters if provided
                if (!string.IsNullOrEmpty(status))
                {
                    bool isActive = status.ToLower() == "active";
                    users = users.Where(u => u.IsActive == isActive).ToList();
                }

                if (!string.IsNullOrEmpty(role))
                {
                    users = users.Where(u => u.RoleName.Equals(role, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                var activeUsers = users.Count(u => u.IsActive);
                var inactiveUsers = users.Count(u => !u.IsActive);
                var requiresPasswordReset = users.Count(u => u.RequiresPasswordReset);

                // Role breakdown
                var roleBreakdown = users.GroupBy(u => u.RoleName)
                    .Select(g => new { role = g.Key, count = g.Count() })
                    .ToList();

                var response = new
                {
                    total = users.Count,
                    active = activeUsers,
                    inactive = inactiveUsers,
                    requiresPasswordReset = requiresPasswordReset,
                    roleBreakdown = roleBreakdown,
                    filters = new
                    {
                        status = status,
                        role = role
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Error fetching user statistics",
                    errorCode = "INTERNAL_ERROR"
                });
            }
        }

        // Force password reset for a user (Admin only) - Can be repurposed for email verification later
        [HttpPost("{id}/force-password-reset")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ForcePasswordReset(int id)
        {
            try
            {
                var (currentUserId, currentUserRole) = GetCurrentUserInfo();

                // Cannot force reset on yourself
                if (id == currentUserId)
                    return BadRequest(new
                    {
                        message = "Cannot force password reset on yourself",
                        errorCode = "SELF_ACTION_FORBIDDEN"
                    });

                var updateDto = new UpdateUserDto();
                var result = await _userService.UpdateUserAsync(id, updateDto, currentUserId, currentUserRole);

                if (!result.Success)
                {
                    if (result.ErrorCode == "USER_NOT_FOUND_OR_ACCESS_DENIED")
                    {
                        return NotFound(new
                        {
                            message = result.ErrorMessage,
                            errorCode = result.ErrorCode
                        });
                    }

                    return BadRequest(new
                    {
                        message = result.ErrorMessage,
                        errorCode = result.ErrorCode
                    });
                }

                return Ok(new { message = "User will be required to reset password on next login" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Error forcing password reset",
                    errorCode = "INTERNAL_ERROR"
                });
            }
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