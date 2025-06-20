using System.ComponentModel.DataAnnotations;

namespace gasopper_crm_server.DTOs
{
    // 1. UPDATE OPPORTUNITY DTO (no create - opportunities come from lead conversion)
    public class UpdateOpportunityDto
    {
        [Required]
        [StringLength(100, ErrorMessage = "Owner name cannot exceed 100 characters")]
        public string OwnerName { get; set; } = string.Empty;

        [Required]
        [StringLength(500, ErrorMessage = "Owner address cannot exceed 500 characters")]
        public string OwnerAddress { get; set; } = string.Empty;

        // Optional assignment (Admin/Manager can reassign)
        public int? AssignedTo { get; set; }
    }

    // 2. UPDATE OPPORTUNITY STATUS DTO (manual status control)
    public class UpdateOpportunityStatusDto
    {
        [Required]
        [Range(1, 2, ErrorMessage = "Status ID must be 1 (Active) or 2 (Complete)")]
        public int StatusId { get; set; }
    }

    // 3. ASSIGN OPPORTUNITY DTO (Admin/Manager only)
    public class AssignOpportunityDto
    {
        [Required]
        public int AssignedTo { get; set; }
    }

    // 4. FULL OPPORTUNITY RESPONSE DTO (detailed view)
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
        
        // Station completion tracking
        public int TotalStations { get; set; }
        public int CompleteStations { get; set; }
        public int IncompleteStations { get; set; }
        public double CompletionPercentage { get; set; }
        
        // Station details (list of stations for this opportunity)
        public List<OpportunityStationDto> Stations { get; set; } = new List<OpportunityStationDto>();
        
        // Timestamps
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    // 5. OPPORTUNITY LIST DTO (optimized for lists)
    public class OpportunityListDto
    {
        public int OpportunityId { get; set; }
        public string LeadName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string StatusName { get; set; } = string.Empty;
        public string AssignedToName { get; set; } = string.Empty;
        
        // Completion summary
        public int TotalStations { get; set; }
        public int CompleteStations { get; set; }
        public double CompletionPercentage { get; set; }
        
        // Key dates
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    // 6. STATION INFO FOR OPPORTUNITY (nested in opportunity response)
    public class OpportunityStationDto
    {
        public int StationId { get; set; }
        public string StationName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        
        // POC information (from gas_stations table)
        public string? PocName { get; set; }
        public string? PocPhone { get; set; }
        public string? PocEmail { get; set; }
        
        // Station details
        public int? NumberOfPumps { get; set; }
        public int? NumberOfEmployees { get; set; }
        public string? StationTypeName { get; set; }
        
        // Completion status
        public bool IsComplete { get; set; }
        public List<string> MissingFields { get; set; } = new List<string>();
        
        public DateTime CreatedAt { get; set; }
    }

    // 7. OPPORTUNITY STATUS REFERENCE DTO (for dropdowns)
    public class OpportunityStatusDto
    {
        public int StatusId { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    // 8. OPPORTUNITY STATISTICS DTO (for dashboard)
    public class OpportunityStatsDto
    {
        public int TotalOpportunities { get; set; }
        public int ActiveOpportunities { get; set; }       // Status 1 (Active)
        public int CompleteOpportunities { get; set; }     // Status 2 (Complete)
        public double CompletionRate { get; set; }         // Complete / Total * 100
        
        // Station statistics
        public int TotalStations { get; set; }
        public int CompleteStations { get; set; }
        public double StationCompletionRate { get; set; }
        
        // Average metrics
        public double AverageStationsPerOpportunity { get; set; }
        public int AverageDaysToComplete { get; set; }
        
        // Status breakdown (only "Active" and "Complete")
        public Dictionary<string, int> StatusBreakdown { get; set; } = new Dictionary<string, int>();
    }
}