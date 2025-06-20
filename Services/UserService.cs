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
        Task<UserResponseDto?> UpdateUserAsync(int userId, UpdateUserDto updateUserDto, int currentUserId, int currentUserRole);
        Task<bool> DeleteUserAsync(int userId, int currentUserId, int currentUserRole);
        Task<List<UserListResponseDto>> GetMyTeamAsync(int managerId);
        Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto changePasswordDto, int currentUserId);
        Task<List<object>> GetRolesAsync();
    }

    public class UserService : IUserService
    {
        private readonly GasopperDbContext _context;

        public UserService(GasopperDbContext context)
        {
            _context = context;
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

                // If manager is creating, validate they can only assign to themselves
                if (currentUserRole == 2 && createUserDto.ManagerId != null && createUserDto.ManagerId != currentUserId)
                    return null;

                var user = new User
                {
                    employee_id = createUserDto.EmployeeId,
                    email = createUserDto.Email,
                    phone_number = createUserDto.PhoneNumber,
                    address = createUserDto.Address,
                    first_name = createUserDto.FirstName,
                    last_name = createUserDto.LastName,
                    role_id = createUserDto.RoleId,
                    manager_id = createUserDto.ManagerId,
                    password_hash = BCrypt.Net.BCrypt.HashPassword(createUserDto.Password),
                    is_active = true,
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

                // FIXED: Apply role-based filtering with MATERIALIZED team member IDs
                if (currentUserRole == 3) // Salesperson can only see themselves
                {
                    query = query.Where(u => u.user_id == currentUserId);
                }
                else if (currentUserRole == 2) // Manager can see self and team
                {
                    // FIXED: Materialize team member IDs first
                    var teamMemberIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId && u.is_active)
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
                    Iat = user.iat,
                    Exp = user.exp
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        // FIXED: This MUST return empty list for salespeople if they can't see other users
        public async Task<List<UserListResponseDto>> GetUsersAsync(int currentUserId, int currentUserRole)
        {
            try
            {
                var query = _context.Users
                    .Include(u => u.Role)
                    .Include(u => u.Manager)
                    .AsQueryable();

                // FIXED: Apply role-based filtering with MATERIALIZED team member IDs
                if (currentUserRole == 3) // Salesperson
                {
                    query = query.Where(u => u.user_id == currentUserId);
                }
                else if (currentUserRole == 2) // Manager
                {
                    // FIXED: Materialize team member IDs first
                    var teamMemberIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId && u.is_active)
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
                        CreatedAt = u.created_at
                    })
                    .ToListAsync();

                return users;
            }
            catch (Exception)
            {
                // FIXED: Return empty list on error, not null
                return new List<UserListResponseDto>();
            }
        }

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

                // Only Admin can change roles and managers
                if (currentUserRole == 1)
                {
                    if (updateUserDto.RoleId.HasValue)
                        user.role_id = updateUserDto.RoleId.Value;
                    
                    if (updateUserDto.ManagerId.HasValue)
                        user.manager_id = updateUserDto.ManagerId.Value;
                    
                    if (updateUserDto.IsActive.HasValue)
                        user.is_active = updateUserDto.IsActive.Value;
                }

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
                    .Where(u => u.manager_id == managerId && u.is_active)
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

                return teamMembers;
            }
            catch (Exception)
            {
                return new List<UserListResponseDto>();
            }
        }

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

        private async Task<User?> GetEditableUserAsync(int userId, int currentUserId, int currentUserRole)
        {
            try
            {
                var query = _context.Users.AsQueryable();

                // FIXED: Apply role-based filtering with MATERIALIZED team member IDs
                if (currentUserRole == 3) // Salesperson can only edit themselves
                {
                    query = query.Where(u => u.user_id == currentUserId);
                }
                else if (currentUserRole == 2) // Manager can edit self and team
                {
                    // FIXED: Materialize team member IDs first
                    var teamMemberIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId && u.is_active)
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