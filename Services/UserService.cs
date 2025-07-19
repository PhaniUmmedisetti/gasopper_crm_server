using Microsoft.EntityFrameworkCore;
using gasopper_crm_server.Data;
using gasopper_crm_server.DTOs;
using gasopper_crm_server.Models;
using gasopper_crm_server.Helpers;

namespace gasopper_crm_server.Services
{
    // UPDATED: ServiceResult class with success message support
    public class ServiceResult<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ErrorCode { get; set; }
        public string? SuccessMessage { get; set; } // NEW: For detailed success messages

        public static ServiceResult<T> SuccessResult(T data, string? message = null)
        {
            return new ServiceResult<T>
            {
                Success = true,
                Data = data,
                SuccessMessage = message
            };
        }

        public static ServiceResult<T> ErrorResult(string message, string? code = null)
        {
            return new ServiceResult<T>
            {
                Success = false,
                ErrorMessage = message,
                ErrorCode = code
            };
        }
    }

    public interface IUserService
    {
        // UPDATED: Return ServiceResult for better error handling
        Task<ServiceResult<UserResponseDto>> CreateUserAsync(CreateUserDto createUserDto, int currentUserId, int currentUserRole);
        Task<UserResponseDto?> GetUserByIdAsync(int userId, int currentUserId, int currentUserRole);
        Task<List<UserListResponseDto>> GetUsersAsync(int currentUserId, int currentUserRole);
        Task<List<UserListResponseDto>> GetFilteredUsersAsync(int currentUserId, int currentUserRole, UserFilterDto filters);
        Task<ServiceResult<UserResponseDto>> UpdateUserAsync(int userId, UpdateUserDto updateUserDto, int currentUserId, int currentUserRole);
        Task<ServiceResult<bool>> DeleteUserAsync(int userId, int currentUserId, int currentUserRole);
        Task<List<UserListResponseDto>> GetMyTeamAsync(int managerId);

        // COMMENTED OUT: Password change method for OTP-only authentication
        // Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto changePasswordDto, int currentUserId);

        Task<List<object>> GetRolesAsync();
        Task<List<UserListResponseDto>> GetAvailableManagersAsync(int currentUserId, int currentUserRole);
    }

    public class UserService : IUserService
    {
        private readonly GasopperDbContext _context;

        public UserService(GasopperDbContext context)
        {
            _context = context;
        }

        // COMMENTED OUT: Default password generation for OTP-only authentication
        /*
        private string GenerateDefaultPassword()
        {
            var random = new Random();
            var digits = random.Next(1000, 9999);
            return $"Gas{digits}!";
        }
        */

        public async Task<ServiceResult<UserResponseDto>> CreateUserAsync(CreateUserDto createUserDto, int currentUserId, int currentUserRole)
        {
            try
            {
                // ENHANCED: Detailed role-based validation with specific error messages
                if (currentUserRole == 3) // Salesperson cannot create users
                {
                    return ServiceResult<UserResponseDto>.ErrorResult(
                        "Access denied. Salespeople cannot create other users.",
                        "INSUFFICIENT_PERMISSIONS"
                    );
                }

                if (currentUserRole == 2 && createUserDto.RoleId != 3) // Manager can only create Salesperson
                {
                    return ServiceResult<UserResponseDto>.ErrorResult(
                        "Access denied. Managers can only create Salesperson accounts.",
                        "ROLE_RESTRICTION"
                    );
                }

                // ENHANCED: Check for existing email with detailed message
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.email == createUserDto.Email);

                if (existingUser != null)
                {
                    return ServiceResult<UserResponseDto>.ErrorResult(
                        $"A user with email '{createUserDto.Email}' already exists in the system.",
                        "EMAIL_ALREADY_EXISTS"
                    );
                }

                // ENHANCED: Validate role exists
                var roleExists = await _context.Roles
                    .AnyAsync(r => r.role_id == createUserDto.RoleId);

                if (!roleExists)
                {
                    return ServiceResult<UserResponseDto>.ErrorResult(
                        $"Invalid role ID '{createUserDto.RoleId}'. Please select a valid role.",
                        "INVALID_ROLE"
                    );
                }

                // Auto-generate Employee ID based on role
                var generatedEmployeeId = await EmployeeIdGenerator.GenerateEmployeeIdAsync(_context, createUserDto.RoleId);

                // ENHANCED: Manager assignment validation with detailed errors
                var assignedManagerId = createUserDto.ManagerId;
                if (currentUserRole == 2 && createUserDto.RoleId == 3)
                {
                    // If manager is creating salesperson, auto-assign to themselves if no manager specified
                    assignedManagerId = createUserDto.ManagerId ?? currentUserId;
                }

                // Validate manager assignment
                if (assignedManagerId.HasValue)
                {
                    var targetManager = await _context.Users
                        .Include(u => u.Role)
                        .FirstOrDefaultAsync(u => u.user_id == assignedManagerId.Value);

                    if (targetManager == null)
                    {
                        return ServiceResult<UserResponseDto>.ErrorResult(
                            $"Manager with ID '{assignedManagerId}' not found.",
                            "MANAGER_NOT_FOUND"
                        );
                    }

                    if (targetManager.role_id != 2) // Must be a Manager role
                    {
                        return ServiceResult<UserResponseDto>.ErrorResult(
                            $"Selected user '{targetManager.first_name} {targetManager.last_name}' is not a Manager and cannot be assigned as a manager.",
                            "INVALID_MANAGER_ROLE"
                        );
                    }

                    if (!targetManager.is_active)
                    {
                        return ServiceResult<UserResponseDto>.ErrorResult(
                            $"Manager '{targetManager.first_name} {targetManager.last_name}' is inactive and cannot be assigned.",
                            "INACTIVE_MANAGER"
                        );
                    }

                    // Manager permission check
                    if (currentUserRole == 2 && assignedManagerId != currentUserId)
                    {
                        return ServiceResult<UserResponseDto>.ErrorResult(
                            "Access denied. Managers can only assign users to themselves.",
                            "MANAGER_ASSIGNMENT_RESTRICTION"
                        );
                    }
                }

                // ENHANCED: Check for Employee ID collision (very rare but possible)
                var employeeIdExists = await _context.Users
                    .AnyAsync(u => u.employee_id == generatedEmployeeId);

                if (employeeIdExists)
                {
                    return ServiceResult<UserResponseDto>.ErrorResult(
                        "Employee ID generation conflict. Please try again.",
                        "EMPLOYEE_ID_CONFLICT"
                    );
                }

                var user = new User
                {
                    employee_id = generatedEmployeeId,
                    email = createUserDto.Email,
                    phone_number = createUserDto.PhoneNumber,
                    address = createUserDto.Address,
                    first_name = createUserDto.FirstName,
                    last_name = createUserDto.LastName,
                    role_id = createUserDto.RoleId,
                    manager_id = assignedManagerId,
                    password_hash = null, // NULL for OTP-only authentication
                    is_active = true,
                    requires_password_reset = false,
                    iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    exp = DateTimeOffset.UtcNow.AddYears(1).ToUnixTimeSeconds()
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                var createdUser = await GetUserByIdAsync(user.user_id, currentUserId, currentUserRole);

                if (createdUser == null)
                {
                    return ServiceResult<UserResponseDto>.ErrorResult(
                        "User was created but could not be retrieved. Please refresh and try again.",
                        "POST_CREATION_ERROR"
                    );
                }

                return ServiceResult<UserResponseDto>.SuccessResult(createdUser);
            }
            catch (DbUpdateException dbEx)
            {
                // Database-specific errors
                if (dbEx.InnerException?.Message.Contains("UNIQUE") == true ||
                    dbEx.InnerException?.Message.Contains("duplicate") == true)
                {
                    return ServiceResult<UserResponseDto>.ErrorResult(
                        "A user with this information already exists. Please check email and employee details.",
                        "DUPLICATE_ENTRY"
                    );
                }

                return ServiceResult<UserResponseDto>.ErrorResult(
                    "Database error occurred while creating user. Please try again.",
                    "DATABASE_ERROR"
                );
            }
            catch (Exception)
            {
                // Log the actual exception for debugging (you should use proper logging)
                // _logger.LogError(ex, "Error creating user");

                return ServiceResult<UserResponseDto>.ErrorResult(
                    "An unexpected error occurred while creating the user. Please try again.",
                    "INTERNAL_ERROR"
                );
            }
        }

        public async Task<ServiceResult<UserResponseDto>> UpdateUserAsync(int userId, UpdateUserDto updateUserDto, int currentUserId, int currentUserRole)
        {
            try
            {
                // Get the user with role-based access check
                var user = await GetEditableUserAsync(userId, currentUserId, currentUserRole);
                if (user == null)
                {
                    return ServiceResult<UserResponseDto>.ErrorResult(
                        "User not found or you don't have permission to edit this user.",
                        "USER_NOT_FOUND_OR_ACCESS_DENIED"
                    );
                }

                // ENHANCED: Email uniqueness check for updates
                if (!string.IsNullOrEmpty(updateUserDto.Email) && updateUserDto.Email != user.email)
                {
                    var emailExists = await _context.Users
                        .AnyAsync(u => u.email == updateUserDto.Email && u.user_id != userId);

                    if (emailExists)
                    {
                        return ServiceResult<UserResponseDto>.ErrorResult(
                            $"Email '{updateUserDto.Email}' is already in use by another user.",
                            "EMAIL_ALREADY_EXISTS"
                        );
                    }
                }

                // ENHANCED: Role change validation
                if (updateUserDto.RoleId.HasValue && updateUserDto.RoleId.Value != user.role_id)
                {
                    if (currentUserRole != 1) // Only Admin can change roles
                    {
                        return ServiceResult<UserResponseDto>.ErrorResult(
                            "Access denied. Only Administrators can change user roles.",
                            "INSUFFICIENT_PERMISSIONS"
                        );
                    }

                    var roleExists = await _context.Roles
                        .AnyAsync(r => r.role_id == updateUserDto.RoleId.Value);

                    if (!roleExists)
                    {
                        return ServiceResult<UserResponseDto>.ErrorResult(
                            $"Invalid role ID '{updateUserDto.RoleId.Value}'. Please select a valid role.",
                            "INVALID_ROLE"
                        );
                    }
                }

                // ENHANCED: Manager assignment validation for updates
                if (updateUserDto.ManagerId.HasValue)
                {
                    var targetManager = await _context.Users
                        .Include(u => u.Role)
                        .FirstOrDefaultAsync(u => u.user_id == updateUserDto.ManagerId.Value);

                    if (targetManager == null)
                    {
                        return ServiceResult<UserResponseDto>.ErrorResult(
                            $"Manager with ID '{updateUserDto.ManagerId.Value}' not found.",
                            "MANAGER_NOT_FOUND"
                        );
                    }

                    if (targetManager.role_id != 2)
                    {
                        return ServiceResult<UserResponseDto>.ErrorResult(
                            $"Selected user '{targetManager.first_name} {targetManager.last_name}' is not a Manager.",
                            "INVALID_MANAGER_ROLE"
                        );
                    }

                    if (!targetManager.is_active)
                    {
                        return ServiceResult<UserResponseDto>.ErrorResult(
                            $"Manager '{targetManager.first_name} {targetManager.last_name}' is inactive.",
                            "INACTIVE_MANAGER"
                        );
                    }

                    // Prevent circular manager assignment
                    if (updateUserDto.ManagerId.Value == userId)
                    {
                        return ServiceResult<UserResponseDto>.ErrorResult(
                            "A user cannot be assigned as their own manager.",
                            "CIRCULAR_MANAGER_ASSIGNMENT"
                        );
                    }
                }

                // Apply updates
                if (!string.IsNullOrEmpty(updateUserDto.Email))
                    user.email = updateUserDto.Email;

                if (!string.IsNullOrEmpty(updateUserDto.PhoneNumber))
                    user.phone_number = updateUserDto.PhoneNumber;

                if (!string.IsNullOrEmpty(updateUserDto.Address))
                    user.address = updateUserDto.Address;

                if (!string.IsNullOrEmpty(updateUserDto.FirstName))
                    user.first_name = updateUserDto.FirstName;

                if (!string.IsNullOrEmpty(updateUserDto.LastName))
                    user.last_name = updateUserDto.LastName;

                // Role changes - Admin only
                if (currentUserRole == 1 && updateUserDto.RoleId.HasValue)
                    user.role_id = updateUserDto.RoleId.Value;

                // Manager assignment
                if (updateUserDto.ManagerId.HasValue)
                {
                    if (currentUserRole == 1) // Admin can assign anyone
                    {
                        user.manager_id = updateUserDto.ManagerId.Value;
                    }
                    else if (currentUserRole == 2) // Manager can reassign their team
                    {
                        var isTeamMember = await _context.Users
                            .AnyAsync(u => u.user_id == userId && u.manager_id == currentUserId);

                        if (isTeamMember)
                            user.manager_id = updateUserDto.ManagerId.Value;
                        else
                        {
                            return ServiceResult<UserResponseDto>.ErrorResult(
                                "You can only reassign users from your own team.",
                                "TEAM_ASSIGNMENT_RESTRICTION"
                            );
                        }
                    }
                }

                // Active status - Admin only
                if (currentUserRole == 1 && updateUserDto.IsActive.HasValue)
                    user.is_active = updateUserDto.IsActive.Value;

                user.last_updated = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                var updatedUser = await GetUserByIdAsync(userId, currentUserId, currentUserRole);

                if (updatedUser == null)
                {
                    return ServiceResult<UserResponseDto>.ErrorResult(
                        "User was updated but could not be retrieved. Please refresh and try again.",
                        "POST_UPDATE_ERROR"
                    );
                }

                return ServiceResult<UserResponseDto>.SuccessResult(updatedUser);
            }
            catch (DbUpdateException dbEx)
            {
                if (dbEx.InnerException?.Message.Contains("UNIQUE") == true ||
                    dbEx.InnerException?.Message.Contains("duplicate") == true)
                {
                    return ServiceResult<UserResponseDto>.ErrorResult(
                        "The updated information conflicts with existing data. Please check email and other details.",
                        "DUPLICATE_ENTRY"
                    );
                }

                return ServiceResult<UserResponseDto>.ErrorResult(
                    "Database error occurred while updating user. Please try again.",
                    "DATABASE_ERROR"
                );
            }
            catch (Exception)
            {
                return ServiceResult<UserResponseDto>.ErrorResult(
                    "An unexpected error occurred while updating the user. Please try again.",
                    "INTERNAL_ERROR"
                );
            }
        }

        public async Task<ServiceResult<bool>> DeleteUserAsync(int userId, int currentUserId, int currentUserRole)
        {
            try
            {
                // Only Admin can delete users
                if (currentUserRole != 1)
                {
                    return ServiceResult<bool>.ErrorResult(
                        "Access denied. Only Administrators can deactivate users.",
                        "INSUFFICIENT_PERMISSIONS"
                    );
                }

                // Cannot delete yourself
                if (userId == currentUserId)
                {
                    return ServiceResult<bool>.ErrorResult(
                        "You cannot deactivate your own account.",
                        "SELF_DELETION_FORBIDDEN"
                    );
                }

                var user = await _context.Users
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.user_id == userId);

                if (user == null)
                {
                    return ServiceResult<bool>.ErrorResult(
                        $"User with ID '{userId}' not found.",
                        "USER_NOT_FOUND"
                    );
                }

                if (!user.is_active)
                {
                    return ServiceResult<bool>.ErrorResult(
                        $"User '{user.first_name} {user.last_name}' is already inactive.",
                        "USER_ALREADY_INACTIVE"
                    );
                }

                // SMART AUTO-REASSIGNMENT: Handle team members before deactivation
                var teamMembers = await _context.Users
                    .Where(u => u.manager_id == userId && u.is_active)
                    .ToListAsync();

                string reassignmentMessage = "";

                if (teamMembers.Any())
                {
                    // Find the best reassignment target
                    User? newManager = null;
                    string reassignmentType = "";

                    // Option 1: Try to find another active manager at the same level
                    var otherManagers = await _context.Users
                        .Where(u => u.role_id == 2 && u.is_active && u.user_id != userId)
                        .ToListAsync();

                    if (otherManagers.Any())
                    {
                        newManager = otherManagers.First(); // Take the first available manager
                        reassignmentType = "active manager";
                    }
                    else
                    {
                        // Option 2: Try to find the user's own manager (move team up one level)
                        if (user.manager_id.HasValue)
                        {
                            newManager = await _context.Users
                                .FirstOrDefaultAsync(u => u.user_id == user.manager_id.Value && u.is_active);

                            if (newManager != null)
                            {
                                reassignmentType = "upper-level manager";
                            }
                        }
                    }

                    // Option 3: Find any active admin
                    if (newManager == null)
                    {
                        newManager = await _context.Users
                            .Where(u => u.role_id == 1 && u.is_active && u.user_id != userId)
                            .FirstOrDefaultAsync();

                        if (newManager != null)
                        {
                            reassignmentType = "admin";
                        }
                    }

                    // Perform reassignment
                    if (newManager != null)
                    {
                        foreach (var member in teamMembers)
                        {
                            member.manager_id = newManager.user_id;
                            member.last_updated = DateTime.UtcNow;
                        }

                        reassignmentMessage = $" {teamMembers.Count} team member(s) reassigned to {newManager.first_name} {newManager.last_name} ({reassignmentType}).";
                    }
                    else
                    {
                        // Last resort: Orphan the team members (set manager to null)
                        foreach (var member in teamMembers)
                        {
                            member.manager_id = null;
                            member.last_updated = DateTime.UtcNow;
                        }

                        reassignmentMessage = $" {teamMembers.Count} team member(s) set as unassigned (no available managers found).";
                    }
                }

                // Deactivate the user
                user.is_active = false;
                user.jwt_token = null; // Invalidate token
                user.last_updated = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Return success with detailed message
                string successMessage = $"User '{user.first_name} {user.last_name}' deactivated successfully.{reassignmentMessage}";

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception)
            {
                return ServiceResult<bool>.ErrorResult(
                    "An unexpected error occurred while deactivating the user. Please try again.",
                    "INTERNAL_ERROR"
                );
            }
        }

        // Keep existing methods unchanged for backward compatibility
        public async Task<UserResponseDto?> GetUserByIdAsync(int userId, int currentUserId, int currentUserRole)
        {
            try
            {
                var query = _context.Users
                    .Include(u => u.Role)
                    .Include(u => u.Manager)
                        .ThenInclude(m => m!.Role)
                    .Where(u => u.user_id == userId);

                // Apply role-based filtering with MATERIALIZED team member IDs
                if (currentUserRole == 3) // Salesperson can only see themselves
                {
                    query = query.Where(u => u.user_id == currentUserId);
                }
                else if (currentUserRole == 2) // Manager can see self and team
                {
                    var teamMemberIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId)
                        .Select(u => u.user_id)
                        .ToListAsync();

                    teamMemberIds.Add(currentUserId); // Add manager's own ID
                    query = query.Where(u => teamMemberIds.Contains(u.user_id));
                }
                // Admin can see all (no filter)

                var user = await query.FirstOrDefaultAsync();

                if (user == null)
                    return null;

                return new UserResponseDto
                {
                    UserId = user.user_id,
                    EmployeeId = user.employee_id,
                    Email = user.email,
                    PhoneNumber = user.phone_number,
                    Address = user.address,
                    FirstName = user.first_name,
                    LastName = user.last_name,
                    RoleId = user.role_id,
                    RoleName = user.Role?.role_name ?? "",
                    ManagerId = user.manager_id,
                    ManagerName = user.Manager != null ? $"{user.Manager.first_name} {user.Manager.last_name}" : null,
                    ManagerRoleId = user.Manager?.role_id,
                    ManagerRoleName = user.Manager?.Role?.role_name,
                    IsActive = user.is_active,
                    LastLogin = user.last_login,
                    CreatedAt = user.created_at,
                    LastUpdated = user.last_updated,
                    RequiresPasswordReset = user.requires_password_reset,
                    Iat = user.iat,
                    Exp = user.exp
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Keep all other existing methods unchanged...
        public async Task<List<UserListResponseDto>> GetUsersAsync(int currentUserId, int currentUserRole)
        {
            try
            {
                var query = _context.Users
                    .Include(u => u.Role)
                    .Include(u => u.Manager)
                    .AsQueryable();

                // Apply role-based filtering with MATERIALIZED team member IDs
                if (currentUserRole == 3) // Salesperson
                {
                    query = query.Where(u => u.user_id == currentUserId);
                }
                else if (currentUserRole == 2) // Manager
                {
                    var teamMemberIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId)
                        .Select(u => u.user_id)
                        .ToListAsync();

                    teamMemberIds.Add(currentUserId); // Add manager's own ID
                    query = query.Where(u => teamMemberIds.Contains(u.user_id));
                }
                // Admin sees all

                var users = await query
                    .OrderBy(u => u.created_at)
                    .Select(u => new UserListResponseDto
                    {
                        UserId = u.user_id,
                        EmployeeId = u.employee_id,
                        FirstName = u.first_name,
                        LastName = u.last_name,
                        Email = u.email,
                        PhoneNumber = u.phone_number,
                        RoleName = u.Role!.role_name,
                        ManagerName = u.Manager != null ? $"{u.Manager.first_name} {u.Manager.last_name}" : null,
                        IsActive = u.is_active,
                        CreatedAt = u.created_at,
                        RequiresPasswordReset = u.requires_password_reset
                    })
                    .ToListAsync();

                return users;
            }
            catch (Exception)
            {
                return new List<UserListResponseDto>();
            }
        }

        public async Task<List<UserListResponseDto>> GetFilteredUsersAsync(int currentUserId, int currentUserRole, UserFilterDto filters)
        {
            try
            {
                var users = await GetUsersAsync(currentUserId, currentUserRole);

                // Apply filters
                if (!string.IsNullOrEmpty(filters.Status))
                {
                    bool isActive = filters.Status.ToLower() == "active";
                    users = users.Where(u => u.IsActive == isActive).ToList();
                }

                if (!string.IsNullOrEmpty(filters.Role))
                {
                    users = users.Where(u => u.RoleName.Equals(filters.Role, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                if (filters.CreatedAfter.HasValue)
                {
                    users = users.Where(u => u.CreatedAt >= filters.CreatedAfter.Value).ToList();
                }

                if (filters.CreatedBefore.HasValue)
                {
                    users = users.Where(u => u.CreatedAt <= filters.CreatedBefore.Value).ToList();
                }

                return users;
            }
            catch (Exception)
            {
                return new List<UserListResponseDto>();
            }
        }

        public async Task<List<UserListResponseDto>> GetMyTeamAsync(int managerId)
        {
            try
            {
                var teamMembers = await _context.Users
                    .Include(u => u.Role)
                    .Where(u => u.manager_id == managerId)
                    .OrderBy(u => u.first_name)
                    .Select(u => new UserListResponseDto
                    {
                        UserId = u.user_id,
                        EmployeeId = u.employee_id,
                        FirstName = u.first_name,
                        LastName = u.last_name,
                        Email = u.email,
                        PhoneNumber = u.phone_number,
                        RoleName = u.Role!.role_name,
                        IsActive = u.is_active,
                        CreatedAt = u.created_at,
                        RequiresPasswordReset = u.requires_password_reset
                    })
                    .ToListAsync();

                return teamMembers;
            }
            catch (Exception)
            {
                return new List<UserListResponseDto>();
            }
        }

        public async Task<List<object>> GetRolesAsync()
        {
            try
            {
                var roles = await _context.Roles
                    .OrderBy(r => r.role_id)
                    .Select(r => new
                    {
                        roleId = r.role_id,
                        roleName = r.role_name
                    })
                    .ToListAsync<object>();

                return roles;
            }
            catch (Exception)
            {
                return new List<object>();
            }
        }

        public async Task<List<UserListResponseDto>> GetAvailableManagersAsync(int currentUserId, int currentUserRole)
        {
            try
            {
                var query = _context.Users
                    .Include(u => u.Role)
                    .Where(u => u.role_id == 2 && u.is_active); // Only active managers

                // Apply role-based filtering
                if (currentUserRole == 2) // Manager can see other managers at same level
                {
                    // Can assign to themselves or other managers
                    query = query.Where(u => u.role_id == 2);
                }
                // Admin can see all managers

                var managers = await query
                    .OrderBy(u => u.first_name)
                    .Select(u => new UserListResponseDto
                    {
                        UserId = u.user_id,
                        EmployeeId = u.employee_id,
                        FirstName = u.first_name,
                        LastName = u.last_name,
                        Email = u.email,
                        PhoneNumber = u.phone_number,
                        RoleName = u.Role!.role_name,
                        IsActive = u.is_active,
                        CreatedAt = u.created_at
                    })
                    .ToListAsync();

                return managers;
            }
            catch (Exception)
            {
                return new List<UserListResponseDto>();
            }
        }

        private async Task<User?> GetEditableUserAsync(int userId, int currentUserId, int currentUserRole)
        {
            try
            {
                var query = _context.Users.AsQueryable();

                // Apply role-based filtering with MATERIALIZED team member IDs
                if (currentUserRole == 3) // Salesperson can only edit themselves
                {
                    query = query.Where(u => u.user_id == currentUserId);
                }
                else if (currentUserRole == 2) // Manager can edit self and team
                {
                    var teamMemberIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId)
                        .Select(u => u.user_id)
                        .ToListAsync();

                    teamMemberIds.Add(currentUserId); // Add manager's own ID
                    query = query.Where(u => teamMemberIds.Contains(u.user_id));
                }
                // Admin can edit all

                return await query.FirstOrDefaultAsync(u => u.user_id == userId);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}