using Microsoft.EntityFrameworkCore;
using gasopper_crm_server.Data;
using gasopper_crm_server.DTOs;
using gasopper_crm_server.Models;

namespace gasopper_crm_server.Services
{
    public interface IUserService
    {
        Task<UserResponseDto?> CreateUserAsync(CreateUserDto createUserDto, int currentUserId, int currentUserRole);
        Task<UserResponseDto?> GetUserByIdAsync(int userId, int currentUserId, int currentUserRole);
        Task<List<UserListResponseDto>> GetUsersAsync(int currentUserId, int currentUserRole);
        Task<List<UserListResponseDto>> GetFilteredUsersAsync(int currentUserId, int currentUserRole, UserFilterDto filters);
        Task<UserResponseDto?> UpdateUserAsync(int userId, UpdateUserDto updateUserDto, int currentUserId, int currentUserRole);
        Task<bool> DeleteUserAsync(int userId, int currentUserId, int currentUserRole);
        Task<List<UserListResponseDto>> GetMyTeamAsync(int managerId);
        Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto changePasswordDto, int currentUserId);
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

        // ENHANCED: Default password generation
        private string GenerateDefaultPassword()
        {
            var random = new Random();
            var digits = random.Next(1000, 9999);
            return $"Gas{digits}!";
        }

        public async Task<UserResponseDto?> CreateUserAsync(CreateUserDto createUserDto, int currentUserId, int currentUserRole)
        {
            try
            {
                // Validation: Only Admin can create any user type, Manager can only create Salesperson
                if (currentUserRole == 3) // Salesperson cannot create users
                    return null;
                
                if (currentUserRole == 2 && createUserDto.RoleId != 3) // Manager can only create Salesperson
                    return null;

                // Check if email or employee ID already exists
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.email == createUserDto.Email || u.employee_id == createUserDto.EmployeeId);
                
                if (existingUser != null)
                    return null;

                // ENHANCED: Auto-assign manager for Manager role creating Salesperson
                var assignedManagerId = createUserDto.ManagerId;
                if (currentUserRole == 2 && createUserDto.RoleId == 3)
                {
                    // If manager is creating salesperson, auto-assign to themselves if no manager specified
                    assignedManagerId = createUserDto.ManagerId ?? currentUserId;
                }

                // If manager is creating, validate they can only assign to themselves or valid managers
                if (currentUserRole == 2 && assignedManagerId != null && assignedManagerId != currentUserId)
                {
                    var targetManager = await _context.Users
                        .FirstOrDefaultAsync(u => u.user_id == assignedManagerId && u.role_id == 2);
                    if (targetManager == null)
                        return null;
                }

                // ENHANCED: Use provided password or generate default
                var password = !string.IsNullOrEmpty(createUserDto.Password) 
                    ? createUserDto.Password 
                    : GenerateDefaultPassword();

                var user = new User
                {
                    employee_id = createUserDto.EmployeeId,
                    email = createUserDto.Email,
                    phone_number = createUserDto.PhoneNumber,
                    address = createUserDto.Address,
                    first_name = createUserDto.FirstName,
                    last_name = createUserDto.LastName,
                    role_id = createUserDto.RoleId,
                    manager_id = assignedManagerId,
                    password_hash = BCrypt.Net.BCrypt.HashPassword(password),
                    is_active = true,
                    requires_password_reset = string.IsNullOrEmpty(createUserDto.Password), // ENHANCED: Flag for default password
                    iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    exp = DateTimeOffset.UtcNow.AddYears(1).ToUnixTimeSeconds()
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                return await GetUserByIdAsync(user.user_id, currentUserId, currentUserRole);
            }
            catch (Exception)
            {
                return null;
            }
        }

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
                    RequiresPasswordReset = user.requires_password_reset, // ENHANCED
                    Iat = user.iat,
                    Exp = user.exp
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ENHANCED: Include inactive users, remove filtering
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
                        RequiresPasswordReset = u.requires_password_reset // ENHANCED
                    })
                    .ToListAsync();

                return users;
            }
            catch (Exception)
            {
                return new List<UserListResponseDto>();
            }
        }

        // ENHANCED: New method for filtered users
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

        // ENHANCED: Manager reassignment permissions
        public async Task<UserResponseDto?> UpdateUserAsync(int userId, UpdateUserDto updateUserDto, int currentUserId, int currentUserRole)
        {
            try
            {
                // Get the user with role-based access check
                var user = await GetEditableUserAsync(userId, currentUserId, currentUserRole);
                if (user == null)
                    return null;

                // Update only provided fields
                if (!string.IsNullOrEmpty(updateUserDto.EmployeeId))
                    user.employee_id = updateUserDto.EmployeeId;
                
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

                // ENHANCED: Role changes - Admin only
                if (currentUserRole == 1 && updateUserDto.RoleId.HasValue)
                    user.role_id = updateUserDto.RoleId.Value;

                // ENHANCED: Manager assignment - Admin can assign anyone, Manager can reassign their team
                if (updateUserDto.ManagerId.HasValue)
                {
                    if (currentUserRole == 1) // Admin can assign anyone
                    {
                        user.manager_id = updateUserDto.ManagerId.Value;
                    }
                    else if (currentUserRole == 2) // Manager can reassign their team members
                    {
                        // Verify the user being updated is in their team
                        var isTeamMember = await _context.Users
                            .AnyAsync(u => u.user_id == userId && u.manager_id == currentUserId);
                        
                        if (isTeamMember)
                            user.manager_id = updateUserDto.ManagerId.Value;
                    }
                }

                // ENHANCED: Active status - Admin only
                if (currentUserRole == 1 && updateUserDto.IsActive.HasValue)
                    user.is_active = updateUserDto.IsActive.Value;

                user.last_updated = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return await GetUserByIdAsync(userId, currentUserId, currentUserRole);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<bool> DeleteUserAsync(int userId, int currentUserId, int currentUserRole)
        {
            try
            {
                // Only Admin can delete users
                if (currentUserRole != 1)
                    return false;

                // Cannot delete yourself
                if (userId == currentUserId)
                    return false;

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return false;

                user.is_active = false;
                user.jwt_token = null; // Invalidate token
                user.last_updated = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
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
                        RequiresPasswordReset = u.requires_password_reset // ENHANCED
                    })
                    .ToListAsync();

                return teamMembers;
            }
            catch (Exception)
            {
                return new List<UserListResponseDto>();
            }
        }

        // ENHANCED: Clear password reset flag on successful change
        public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto changePasswordDto, int currentUserId)
        {
            try
            {
                // Users can only change their own password
                if (userId != currentUserId)
                    return false;

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return false;

                if (!BCrypt.Net.BCrypt.Verify(changePasswordDto.CurrentPassword, user.password_hash))
                    return false;

                user.password_hash = BCrypt.Net.BCrypt.HashPassword(changePasswordDto.NewPassword);
                user.requires_password_reset = false; // ENHANCED: Clear reset flag
                user.last_updated = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
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

        // ENHANCED: Get available managers for assignment
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