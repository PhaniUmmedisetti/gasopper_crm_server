using System.ComponentModel.DataAnnotations;

namespace gasopper_crm_server.DTOs
{
    public class CreateOpportunityDto
    {
        [Required]
        [MaxLength(100)]
        public string OwnerName { get; set; } = string.Empty;

        // Split address fields to match the new Opportunity model
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

        // ADDED: Actual number of stations field
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Actual stations must be greater than 0")]
        public int ActualStations { get; set; }

        public int? AssignedTo { get; set; }
    }

    public class UpdateOpportunityDto
    {
        [MaxLength(100)]
        public string? OwnerName { get; set; }

        // REMOVED: OwnerAddress field - no longer supported
        // Use split address fields instead

        // Split address fields
        [MaxLength(200)]
        public string? AddressLine1 { get; set; }

        [MaxLength(200)]
        public string? AddressLine2 { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(100)]
        public string? State { get; set; }

        [MaxLength(20)]
        public string? PostalCode { get; set; }

        [MaxLength(100)]
        public string? Country { get; set; }

        // ADDED: Actual number of stations field (optional for updates)
        [Range(1, int.MaxValue, ErrorMessage = "Actual stations must be greater than 0")]
        public int? ActualStations { get; set; }

        public int? AssignedTo { get; set; }
    }

    public class UpdateOpportunityStatusDto
    {
        [Required]
        [Range(1, 2, ErrorMessage = "Status ID must be 1 (Active) or 2 (Complete)")]
        public int StatusId { get; set; }
    }

    public class AssignOpportunityDto
    {
        [Required]
        public int AssignedTo { get; set; }
    }

    // OpportunityResponseDto with proper address fields
    public class OpportunityResponseDto
    {
        public int OpportunityId { get; set; }
        public int LeadId { get; set; }
        public string LeadName { get; set; } = string.Empty;
        public string LeadEmail { get; set; } = string.Empty;
        public string LeadPhone { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;

        // Split address fields to match new model
        public string AddressLine1 { get; set; } = string.Empty;
        public string? AddressLine2 { get; set; }
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;

        // LEGACY: Combined address for backward compatibility (computed from split fields)
        public string OwnerAddress { get; set; } = string.Empty;

        // ADDED: Actual number of stations (user input)
        public int ActualStations { get; set; }

        // Status information
        public int StatusId { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public string StatusDescription { get; set; } = string.Empty;

        // Assignment information
        public int AssignedTo { get; set; }
        public string AssignedToName { get; set; } = string.Empty;
        public int CreatedBy { get; set; }
        public string CreatedByName { get; set; } = string.Empty;

        // Gas station fields
        public List<OpportunityStationDto> Stations { get; set; } = new List<OpportunityStationDto>();
        public int TotalStations { get; set; }
        public int CompleteStations { get; set; }
        public int IncompleteStations { get; set; }
        public double CompletionPercentage { get; set; }

        // Timestamps
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdated { get; set; }
        public bool IsDeleted { get; set; }
    }

    // OpportunityListDto with all required fields
    public class OpportunityListDto
    {
        public int OpportunityId { get; set; }
        public string LeadName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public int StatusId { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public string AssignedToName { get; set; } = string.Empty;

        // ADDED: Actual number of stations (user input)
        public int ActualStations { get; set; }

        // Gas station fields
        public int TotalStations { get; set; }
        public int CompleteStations { get; set; }
        public double CompletionPercentage { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    // OpportunityStatsDto
    public class OpportunityStatsDto
    {
        public int TotalOpportunities { get; set; }
        public int ActiveOpportunities { get; set; }
        public int CompleteOpportunities { get; set; }
        public double CompletionRate { get; set; }

        // Gas station metrics
        public int TotalStations { get; set; }
        public int CompleteStations { get; set; }
        public double StationCompletionRate { get; set; }
        public double AverageStationsPerOpportunity { get; set; }

        public int AverageDaysToComplete { get; set; }
        public Dictionary<string, int> StatusBreakdown { get; set; } = new Dictionary<string, int>();
    }

    public class OpportunityStatusDto
    {
        public int StatusId { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    // OpportunityStationDto with StationCode
    public class OpportunityStationDto
    {
        public int StationId { get; set; }
        public string StationName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string StationCode { get; set; } = string.Empty;
        public string? PocName { get; set; }
        public string? PocPhone { get; set; }
        public string? PocEmail { get; set; }
        public int? NumberOfPumps { get; set; }
        public int? NumberOfEmployees { get; set; }
        public string? StationTypeName { get; set; }
        public bool IsComplete { get; set; }
        public double CompletionPercentage { get; set; }  // ← ADD THIS LINE
        public List<string> MissingFields { get; set; } = new List<string>();
        public DateTime CreatedAt { get; set; }
        public int StatusId { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}