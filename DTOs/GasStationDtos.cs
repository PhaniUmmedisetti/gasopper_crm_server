// DTOs/GasStationDtos.cs
// UPDATED: Added pagination DTOs for Gas Stations

using System.ComponentModel.DataAnnotations;

namespace gasopper_crm_server.DTOs
{
    // EXISTING DTOs (unchanged)
    public class CreateGasStationDto
    {
        [Required]
        [MaxLength(100)]
        public string StationName { get; set; } = string.Empty;

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

        [MaxLength(100)]
        public string? PocName { get; set; }

        [MaxLength(20)]
        public string? PocPhone { get; set; }

        [EmailAddress]
        [MaxLength(320)]
        public string? PocEmail { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Number of pumps must be greater than 0")]
        public int? NumberOfPumps { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Number of employees must be greater than 0")]
        public int? NumberOfEmployees { get; set; }

        [Range(1, 4, ErrorMessage = "Station type ID must be between 1-4")]
        public int? StationTypeId { get; set; }

        public string? Notes { get; set; }
    }

    public class UpdateGasStationDto
    {
        [MaxLength(100)]
        public string? StationName { get; set; }

        [MaxLength(100)]
        public string? PocName { get; set; }

        [MaxLength(20)]
        public string? PocPhone { get; set; }

        [EmailAddress]
        [MaxLength(320)]
        public string? PocEmail { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Number of pumps must be greater than 0")]
        public int? NumberOfPumps { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Number of employees must be greater than 0")]
        public int? NumberOfEmployees { get; set; }

        [Range(1, 4, ErrorMessage = "Station type ID must be between 1-4")]
        public int? StationTypeId { get; set; }

        public string? Notes { get; set; }
    }

    // NEW: Pagination DTO for Gas Stations
    public class GasStationPaginationDto
    {
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }
        public int PageSize { get; set; }
    }

    // NEW: Paginated Gas Station Response DTO
    public class PaginatedGasStationResponseDto
    {
        public List<GasStationListResponseDto> Data { get; set; } = new List<GasStationListResponseDto>();
        public GasStationPaginationDto Pagination { get; set; } = new GasStationPaginationDto();
    }

    // EXISTING: Gas Station Response DTOs (unchanged)
    public class GasStationResponseDto
    {
        public int StationId { get; set; }
        public int OpportunityId { get; set; }
        public string StationName { get; set; } = string.Empty;
        
        public string AddressLine1 { get; set; } = string.Empty;
        public string? AddressLine2 { get; set; }
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        
        public string Address { get; set; } = string.Empty;
        public string StationCode { get; set; } = string.Empty;

        public string? PocName { get; set; }
        public string? PocPhone { get; set; }
        public string? PocEmail { get; set; }

        public int? NumberOfPumps { get; set; }
        public int? NumberOfEmployees { get; set; }
        public int? StationTypeId { get; set; }
        public string? Notes { get; set; }

        public bool IsComplete { get; set; }
        public double CompletionPercentage { get; set; }

        public bool IsSignedOff { get; set; }
        public DateTime? SignedOffAt { get; set; }
        public bool CanSignOff { get; set; }
        public bool CanEdit { get; set; }

        public int CreatedBy { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdated { get; set; }

        public string OpportunityLeadName { get; set; } = string.Empty;
        public string OpportunityOwnerName { get; set; } = string.Empty;
    }

    public class GasStationListResponseDto
    {
        public int StationId { get; set; }
        public int OpportunityId { get; set; }
        public string StationName { get; set; } = string.Empty;
        
        public string AddressLine1 { get; set; } = string.Empty;
        public string? AddressLine2 { get; set; }
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        
        public string Address { get; set; } = string.Empty;
        public string StationCode { get; set; } = string.Empty;
        
        public string? PocName { get; set; }
        public string? PocPhone { get; set; }
        public string? PocEmail { get; set; }
        
        public int? NumberOfPumps { get; set; }
        public int? NumberOfEmployees { get; set; }
        public int? StationTypeId { get; set; }
        
        public bool IsComplete { get; set; }
        public double CompletionPercentage { get; set; }
        
        public bool IsSignedOff { get; set; }
        public DateTime? SignedOffAt { get; set; }
        public bool CanSignOff { get; set; }
        public bool CanEdit { get; set; }
        
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string OpportunityLeadName { get; set; } = string.Empty;
    }

    // EXISTING: Other DTOs (unchanged)
    public class GasStationSummaryDto
    {
        public int StationId { get; set; }
        public string StationName { get; set; } = string.Empty;
        
        public string AddressLine1 { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        
        public string Address { get; set; } = string.Empty;
        public string StationCode { get; set; } = string.Empty;
        
        public int? StationTypeId { get; set; }
        public bool IsComplete { get; set; }
        public double CompletionPercentage { get; set; }
        
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
        
        public int SignedOffStations { get; set; }
        public int PendingSignOffStations { get; set; }
        public double SignOffRate { get; set; }
        
        public Dictionary<string, int> StationTypeBreakdown { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> CompletionBreakdown { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> SignOffBreakdown { get; set; } = new Dictionary<string, int>();
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
        
        public int SignedOffStations { get; set; }
        public bool AllStationsSignedOff { get; set; }
    }

    public class SignOffStationDto
    {
        [Required]
        public bool ConfirmSignOff { get; set; }
    }
}