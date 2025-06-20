using System.ComponentModel.DataAnnotations;

namespace gasopper_crm_server.DTOs
{
    public class CreateLeadDto
    {
        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(320)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Address { get; set; } = string.Empty;

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Expected stations must be greater than 0")]
        public int ExpectedStations { get; set; }

        // Referral information (all optional)
        [MaxLength(100)]
        public string? ReferralName { get; set; }

        [EmailAddress]
        [MaxLength(320)]
        public string? ReferralEmail { get; set; }

        [MaxLength(20)]
        public string? ReferralPhone { get; set; }

        public string? ReferralAddress { get; set; }

        // Assignment (optional - defaults to current user if not specified)
        public int? AssignedTo { get; set; }
    }

    public class UpdateLeadDto
    {
        [MaxLength(150)]
        public string? Name { get; set; }

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        [EmailAddress]
        [MaxLength(320)]
        public string? Email { get; set; }

        public string? Address { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Expected stations must be greater than 0")]
        public int? ExpectedStations { get; set; }

        // Referral information (all optional)
        [MaxLength(100)]
        public string? ReferralName { get; set; }

        [EmailAddress]
        [MaxLength(320)]
        public string? ReferralEmail { get; set; }

        [MaxLength(20)]
        public string? ReferralPhone { get; set; }

        public string? ReferralAddress { get; set; }

        // Assignment (Admin/Manager only)
        public int? AssignedTo { get; set; }
    }

    // SIMPLIFIED - Only 2 statuses: New (1) or Converted (2)
    public class UpdateLeadStatusDto
    {
        [Required]
        [Range(1, 2, ErrorMessage = "Status ID must be 1 (New) or 2 (Converted)")]
        public int StatusId { get; set; }
    }

    public class AssignLeadDto
    {
        [Required]
        public int AssignedTo { get; set; }
    }

    // CLEANED - Removed all color/order/final status references
    public class LeadResponseDto
    {
        public int LeadId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public int ExpectedStations { get; set; }

        // Referral information
        public string? ReferralName { get; set; }
        public string? ReferralEmail { get; set; }
        public string? ReferralPhone { get; set; }
        public string? ReferralAddress { get; set; }

        // SIMPLIFIED Status information - removed color_code, is_final
        public int? StatusId { get; set; }
        public string? StatusName { get; set; }

        // Assignment information
        public int AssignedTo { get; set; }
        public string AssignedToName { get; set; } = string.Empty;
        public int CreatedBy { get; set; }
        public string CreatedByName { get; set; } = string.Empty;

        // Timestamps
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdated { get; set; }

        // Opportunity information (if converted)
        public int? OpportunityId { get; set; }
        public string? OpportunityStatus { get; set; } // Updated from Stage to Status

        // Soft delete status
        public bool IsDeleted { get; set; }
    }

    // CLEANED - Removed color references
    public class LeadListResponseDto
    {
        public int LeadId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public int ExpectedStations { get; set; }
        public string? StatusName { get; set; }
        public string AssignedToName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdated { get; set; }
        public bool HasOpportunity { get; set; }
        public bool IsDeleted { get; set; }
    }

    public class ConvertLeadToOpportunityDto
    {
        [Required]
        [MaxLength(100)]
        public string OwnerName { get; set; } = string.Empty;

        [Required]
        public string OwnerAddress { get; set; } = string.Empty;

        // Assignment (optional - defaults to current user if not specified)
        public int? AssignedTo { get; set; }
    }

    // SIMPLIFIED - Only New vs Converted stats
    public class LeadStatsDto
    {
        public int TotalLeads { get; set; }
        public int NewLeads { get; set; }           // Status 1 (New)
        public int ConvertedLeads { get; set; }     // Status 2 (Converted)
        public double ConversionRate { get; set; }
        public int AverageDaysToConvert { get; set; }
        public Dictionary<string, int> StatusBreakdown { get; set; } = new Dictionary<string, int>();
        // StatusBreakdown will only contain "New" and "Converted"
    }
}