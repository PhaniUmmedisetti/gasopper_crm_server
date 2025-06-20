using System.ComponentModel.DataAnnotations;

namespace gasopper_crm_server.DTOs
{
    public class CreateUserDto
    {
        [Required]
        [MaxLength(20)]
        public string EmployeeId { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(320)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        public string Address { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [Range(1, 3, ErrorMessage = "Role ID must be 1 (Admin), 2 (Manager), or 3 (Salesperson)")]
        public int RoleId { get; set; }

        public int? ManagerId { get; set; }

        [Required]
        [MinLength(8)]
        public string Password { get; set; } = string.Empty;
    }

    public class UpdateUserDto
    {
        [MaxLength(20)]
        public string? EmployeeId { get; set; }

        [EmailAddress]
        [MaxLength(320)]
        public string? Email { get; set; }

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        public string? Address { get; set; }

        [MaxLength(50)]
        public string? FirstName { get; set; }

        [MaxLength(50)]
        public string? LastName { get; set; }

        [Range(1, 3, ErrorMessage = "Role ID must be 1 (Admin), 2 (Manager), or 3 (Salesperson)")]
        public int? RoleId { get; set; }

        public int? ManagerId { get; set; }

        public bool? IsActive { get; set; }
    }

    // UPDATED - Complete user data response with manager hierarchy
    public class UserResponseDto
    {
        public int UserId { get; set; }
        public string EmployeeId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        
        // Role information
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        
        // Manager hierarchy information
        public int? ManagerId { get; set; }
        public string? ManagerName { get; set; }
        public int? ManagerRoleId { get; set; }
        public string? ManagerRoleName { get; set; }
        
        // Status and timestamps
        public bool IsActive { get; set; }
        public DateTime? LastLogin { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdated { get; set; }
        
        // JWT session info
        public long Iat { get; set; }
        public long Exp { get; set; }
    }

    // UPDATED - List response with hierarchy info
    public class UserListResponseDto
    {
        public int UserId { get; set; }
        public string EmployeeId { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public string? ManagerName { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ChangePasswordDto
    {
        [Required]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        [MinLength(8)]
        public string NewPassword { get; set; } = string.Empty;
    }
}