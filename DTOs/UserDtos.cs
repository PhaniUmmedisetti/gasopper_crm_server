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

        // ENHANCED: Made password optional for auto-generation
        [MinLength(8)]
        public string? Password { get; set; } = null;
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

    // ENHANCED: Added password reset tracking
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
        
        // ENHANCED: Password reset tracking
        public bool RequiresPasswordReset { get; set; } = false;
        
        // JWT session info
        public long Iat { get; set; }
        public long Exp { get; set; }
    }

    // ENHANCED: Added password reset tracking
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
        
        // ENHANCED: Password reset tracking
        public bool RequiresPasswordReset { get; set; } = false;
    }

    public class ChangePasswordDto
    {
        [Required]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        [MinLength(8)]
        public string NewPassword { get; set; } = string.Empty;
    }

    // ENHANCED: New DTO for filtering users
    public class UserFilterDto
    {
        public string? Status { get; set; } // "active", "inactive", null (all)
        public string? Role { get; set; } // "Admin", "Manager", "Salesperson", null (all)
        public int? ManagerId { get; set; } // Filter by specific manager
        public DateTime? CreatedAfter { get; set; } // Filter by creation date
        public DateTime? CreatedBefore { get; set; } // Filter by creation date
    }
}