// REPLACE your entire GasStationDtos.cs with this version
// CHANGED: Using stationTypeId instead of stationTypeName in responses

using System.ComponentModel.DataAnnotations;

namespace gasopper_crm_server.DTOs
{
    public class CreateGasStationDto
    {
        [Required]
        [MaxLength(100)]
        public string StationName { get; set; } = string.Empty;

        [Required]
        public string Address { get; set; } = string.Empty;

        // POC information (all optional)
        [MaxLength(100)]
        public string? PocName { get; set; }

        [MaxLength(20)]
        public string? PocPhone { get; set; }

        [EmailAddress]
        [MaxLength(320)]
        public string? PocEmail { get; set; }

        // Station details (all optional)
        [Range(1, int.MaxValue, ErrorMessage = "Number of pumps must be greater than 0")]
        public int? NumberOfPumps { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Number of employees must be greater than 0")]
        public int? NumberOfEmployees { get; set; }

        [Range(1, 4, ErrorMessage = "Station type ID must be between 1-4")]
        public int? StationTypeId { get; set; }

        public string? Notes { get; set; }
        
        // NOTE: StationCode is NOT included here - it's auto-generated
    }

    public class UpdateGasStationDto
    {
        [MaxLength(100)]
        public string? StationName { get; set; }

        public string? Address { get; set; }

        // POC information (all optional)
        [MaxLength(100)]
        public string? PocName { get; set; }

        [MaxLength(20)]
        public string? PocPhone { get; set; }

        [EmailAddress]
        [MaxLength(320)]
        public string? PocEmail { get; set; }

        // Station details (all optional)
        [Range(1, int.MaxValue, ErrorMessage = "Number of pumps must be greater than 0")]
        public int? NumberOfPumps { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Number of employees must be greater than 0")]
        public int? NumberOfEmployees { get; set; }

        [Range(1, 4, ErrorMessage = "Station type ID must be between 1-4")]
        public int? StationTypeId { get; set; }

        public string? Notes { get; set; }
        
        // NOTE: StationCode is NOT included here - it's immutable after creation
    }

    public class GasStationResponseDto
    {
        public int StationId { get; set; }
        public int OpportunityId { get; set; }
        public string StationName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        
        // AUTO-GENERATED STATION CODE
        public string StationCode { get; set; } = string.Empty;

        // POC information
        public string? PocName { get; set; }
        public string? PocPhone { get; set; }
        public string? PocEmail { get; set; }

        // Station details
        public int? NumberOfPumps { get; set; }
        public int? NumberOfEmployees { get; set; }
        
        // CHANGED: Return stationTypeId instead of stationTypeName
        public int? StationTypeId { get; set; }
        public string? Notes { get; set; }

        // Completion tracking
        public bool IsComplete { get; set; }
        public double CompletionPercentage { get; set; }

        // Audit information
        public int CreatedBy { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdated { get; set; }

        // Opportunity information
        public string OpportunityLeadName { get; set; } = string.Empty;
        public string OpportunityOwnerName { get; set; } = string.Empty;
    }

    // UPDATED: Using stationTypeId for faster frontend referencing
    public class GasStationListResponseDto
    {
        public int StationId { get; set; }
        public int OpportunityId { get; set; }
        public string StationName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        
        // AUTO-GENERATED STATION CODE
        public string StationCode { get; set; } = string.Empty;
        
        // POC information
        public string? PocName { get; set; }
        public string? PocPhone { get; set; }
        public string? PocEmail { get; set; }
        
        // Station details
        public int? NumberOfPumps { get; set; }
        public int? NumberOfEmployees { get; set; }
        
        // CHANGED: Return stationTypeId instead of stationTypeName for faster referencing
        public int? StationTypeId { get; set; }
        
        public bool IsComplete { get; set; }
        public double CompletionPercentage { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string OpportunityLeadName { get; set; } = string.Empty;
    }

    public class GasStationSummaryDto
    {
        public int StationId { get; set; }
        public string StationName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        
        // AUTO-GENERATED STATION CODE
        public string StationCode { get; set; } = string.Empty;
        
        // CHANGED: Use stationTypeId for consistency
        public int? StationTypeId { get; set; }
        public bool IsComplete { get; set; }
        public double CompletionPercentage { get; set; }
    }

    public class StationTypeDto
    {
        public int StationTypeId { get; set; }
        public string TypeName { get; set; } = string.Empty;
    }

    public class GasStationStatsDto
    {
        public int TotalStations { get; set; }
        public int CompleteStations { get; set; }
        public int IncompleteStations { get; set; }
        public double CompletionRate { get; set; }
        public int AverageStationsPerOpportunity { get; set; }
        public Dictionary<string, int> StationTypeBreakdown { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> CompletionBreakdown { get; set; } = new Dictionary<string, int>();
    }

    public class OpportunityStationSummaryDto
    {
        public int OpportunityId { get; set; }
        public string LeadName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public int TotalStations { get; set; }
        public int CompleteStations { get; set; }
        public int IncompleteStations { get; set; }
        public double CompletionPercentage { get; set; }
    }
}