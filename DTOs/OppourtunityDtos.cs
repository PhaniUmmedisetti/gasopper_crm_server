using System.ComponentModel.DataAnnotations;

namespace gasopper_crm_server.DTOs
{
    public class CreateOpportunityDto
    {
        [Required]
        [MaxLength(100)]
        public string OwnerName { get; set; } = string.Empty;

        [Required]
        public string OwnerAddress { get; set; } = string.Empty;

        public int? AssignedTo { get; set; }
    }

    public class UpdateOpportunityDto
    {
        [MaxLength(100)]
        public string? OwnerName { get; set; }

        public string? OwnerAddress { get; set; }

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

    // ✅ COMPLETE OpportunityResponseDto - ALL fields your service expects
    public class OpportunityResponseDto
    {
        public int OpportunityId { get; set; }
        public int LeadId { get; set; }
        public string LeadName { get; set; } = string.Empty;
        public string LeadEmail { get; set; } = string.Empty;
        public string LeadPhone { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string OwnerAddress { get; set; } = string.Empty;

        // Status information
        public int StatusId { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public string StatusDescription { get; set; } = string.Empty;

        // Assignment information
        public int AssignedTo { get; set; }
        public string AssignedToName { get; set; } = string.Empty;
        public int CreatedBy { get; set; }
        public string CreatedByName { get; set; } = string.Empty;

        // ✅ GAS STATION FIELDS - Required by your service
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

    // ✅ FIXED OpportunityListDto - Added missing StatusId property
    public class OpportunityListDto
    {
        public int OpportunityId { get; set; }
        public string LeadName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public int StatusId { get; set; } // ✅ ADDED: This was missing and causing the error
        public string StatusName { get; set; } = string.Empty;
        public string AssignedToName { get; set; } = string.Empty;

        // ✅ GAS STATION FIELDS - Required by your service
        public int TotalStations { get; set; }
        public int CompleteStations { get; set; }
        public double CompletionPercentage { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    // ✅ COMPLETE OpportunityStatsDto - ALL fields your service expects
    public class OpportunityStatsDto
    {
        public int TotalOpportunities { get; set; }
        public int ActiveOpportunities { get; set; }
        public int CompleteOpportunities { get; set; }
        public double CompletionRate { get; set; }

        // ✅ GAS STATION METRICS - Required by your service
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

    // ✅ COMPLETE OpportunityStationDto - ALL fields your service expects
    public class OpportunityStationDto
    {
        public int StationId { get; set; }
        public string StationName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string? PocName { get; set; }
        public string? PocPhone { get; set; }
        public string? PocEmail { get; set; }
        public int? NumberOfPumps { get; set; }
        public int? NumberOfEmployees { get; set; }
        public string? StationTypeName { get; set; }
        public bool IsComplete { get; set; }
        public List<string> MissingFields { get; set; } = new List<string>();
        public DateTime CreatedAt { get; set; }

        // Also include status fields for compatibility
        public int StatusId { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}