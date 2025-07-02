using System.ComponentModel.DataAnnotations;

namespace gasopper_crm_server.DTOs
{
    public class CreateGasStationDto
    {
        [Required]
        [MaxLength(100)]
        public string StationName { get; set; } = string.Empty;

        // UPDATED: Split address fields instead of single Address
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
        
        // NOTE: StationCode is NOT included here - it's auto-generated from PostalCode
    }

    public class UpdateGasStationDto
    {
        [MaxLength(100)]
        public string? StationName { get; set; }

        // REMOVED: Address fields cannot be updated to prevent station code conflicts
        // Address fields are immutable after creation since station code depends on postal code

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
        // NOTE: Address fields are NOT included here - they're immutable to prevent station code conflicts
    }

    public class GasStationResponseDto
    {
        public int StationId { get; set; }
        public int OpportunityId { get; set; }
        public string StationName { get; set; } = string.Empty;
        
        // UPDATED: Split address fields
        public string AddressLine1 { get; set; } = string.Empty;
        public string? AddressLine2 { get; set; }
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        
        // Computed full address for backward compatibility
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
        public int? StationTypeId { get; set; }
        public string? Notes { get; set; }

        // Completion tracking
        public bool IsComplete { get; set; }
        public double CompletionPercentage { get; set; }

        // NEW: Sign-off information
        public bool IsSignedOff { get; set; }
        public DateTime? SignedOffAt { get; set; }
        public bool CanSignOff { get; set; }  // Helper field for frontend
        public bool CanEdit { get; set; }     // Helper field for frontend

        // Audit information
        public int CreatedBy { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdated { get; set; }

        // Opportunity information
        public string OpportunityLeadName { get; set; } = string.Empty;
        public string OpportunityOwnerName { get; set; } = string.Empty;
    }

    public class GasStationListResponseDto
    {
        public int StationId { get; set; }
        public int OpportunityId { get; set; }
        public string StationName { get; set; } = string.Empty;
        
        // UPDATED: Split address fields
        public string AddressLine1 { get; set; } = string.Empty;
        public string? AddressLine2 { get; set; }
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        
        // Computed full address for backward compatibility
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
        public int? StationTypeId { get; set; }
        
        public bool IsComplete { get; set; }
        public double CompletionPercentage { get; set; }
        
        // NEW: Sign-off information
        public bool IsSignedOff { get; set; }
        public DateTime? SignedOffAt { get; set; }
        public bool CanSignOff { get; set; }  // Helper field for frontend
        public bool CanEdit { get; set; }     // Helper field for frontend
        
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string OpportunityLeadName { get; set; } = string.Empty;
    }

    public class GasStationSummaryDto
    {
        public int StationId { get; set; }
        public string StationName { get; set; } = string.Empty;
        
        // UPDATED: Split address fields
        public string AddressLine1 { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        
        // Computed full address for backward compatibility
        public string Address { get; set; } = string.Empty;
        
        // AUTO-GENERATED STATION CODE
        public string StationCode { get; set; } = string.Empty;
        
        public int? StationTypeId { get; set; }
        public bool IsComplete { get; set; }
        public double CompletionPercentage { get; set; }
        
        // NEW: Sign-off information
        public bool IsSignedOff { get; set; }
        public DateTime? SignedOffAt { get; set; }
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
        
        // NEW: Sign-off statistics
        public int SignedOffStations { get; set; }
        public int PendingSignOffStations { get; set; } // Complete but not signed off
        public double SignOffRate { get; set; }
        
        public Dictionary<string, int> StationTypeBreakdown { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> CompletionBreakdown { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> SignOffBreakdown { get; set; } = new Dictionary<string, int>(); // NEW
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
        
        // NEW: Sign-off information
        public int SignedOffStations { get; set; }
        public bool AllStationsSignedOff { get; set; }
    }

    // NEW: Sign-off specific DTO
    public class SignOffStationDto
    {
        [Required]
        public bool ConfirmSignOff { get; set; }
    }
}