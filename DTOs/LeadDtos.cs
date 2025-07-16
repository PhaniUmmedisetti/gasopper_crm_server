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

        [MaxLength(100)]
        public string? ReferralName { get; set; }

        [EmailAddress]
        [MaxLength(320)]
        public string? ReferralEmail { get; set; }

        [MaxLength(20)]
        public string? ReferralPhone { get; set; }

        public string? ReferralAddress { get; set; }

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

        [MaxLength(100)]
        public string? ReferralName { get; set; }

        [EmailAddress]
        [MaxLength(320)]
        public string? ReferralEmail { get; set; }

        [MaxLength(20)]
        public string? ReferralPhone { get; set; }

        public string? ReferralAddress { get; set; }

        public int? AssignedTo { get; set; }
    }

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

    public class LeadResponseDto
    {
        public int LeadId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public int ExpectedStations { get; set; }

        public string? ReferralName { get; set; }
        public string? ReferralEmail { get; set; }
        public string? ReferralPhone { get; set; }
        public string? ReferralAddress { get; set; }

        public int? StatusId { get; set; }
        public string? StatusName { get; set; }

        public int AssignedTo { get; set; }
        public string AssignedToName { get; set; } = string.Empty;
        public int CreatedBy { get; set; }
        public string CreatedByName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdated { get; set; }

        public int? OpportunityId { get; set; }
        public bool HasOpportunity { get; set; }
        public string? OpportunityStatus { get; set; }

        public bool IsDeleted { get; set; }
    }

    public class LeadListResponseDto
    {
        public int LeadId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;  
        public int ExpectedStations { get; set; }
        public string? StatusName { get; set; }
        public string AssignedToName { get; set; } = string.Empty;
        public string CreatedByName { get; set; } = string.Empty;
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
        [MaxLength(200)]
        public string AddressLine1 { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? AddressLine2 { get; set; }

        [Required]
        [MaxLength(100)]
        public string City { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string State { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string PostalCode { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Country { get; set; } = "United States";

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Actual stations must be greater than 0")]
        public int ActualStations { get; set; }

        public int? AssignedTo { get; set; }
    }

    public class LeadStatsDto
    {
        public int TotalLeads { get; set; }
        public int NewLeads { get; set; }
        public int ConvertedLeads { get; set; }
        public double ConversionRate { get; set; }
        public int AverageDaysToConvert { get; set; }
        public Dictionary<string, int> StatusBreakdown { get; set; } = new Dictionary<string, int>();
    }

    // REMOVED: Duplicate PaginationDto - Now using shared version from PaginationDtos.cs
    // Use shared PaginatedResponseDto<T> instead of custom pagination response
    public class PaginatedLeadResponseDto : PaginatedResponseDto<LeadListResponseDto>
    {
        // Inherits Data and Pagination properties from base class
    }

    // Lead-specific filters extending the base
    public class LeadFilters : BaseFilters
    {
        // Add any lead-specific filter properties here if needed
    }
}