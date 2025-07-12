using gasopper_crm_server.Models;

namespace gasopper_crm_server.Services.Database
{
    public interface IDatabaseSeeder
    {
        Task SeedAsync();
        Task<bool> IsSeedRequiredAsync();
        Task<SeederStatus> GetSeederStatusAsync();
    }

    public class SeederStatus
    {
        public bool IsSeeded { get; set; }
        public int RolesCount { get; set; }
        public int StationTypesCount { get; set; }
        public int LeadStatusesCount { get; set; }
        public int OpportunityStatusesCount { get; set; }
        public bool HasAdminUser { get; set; }
        public DateTime? LastSeededAt { get; set; }
        public string Status { get; set; } = "Unknown";
    }
}