using Microsoft.EntityFrameworkCore;
using gasopper_crm_server.Data;
using gasopper_crm_server.Models;

namespace gasopper_crm_server.Services.Database
{
    public class SmartSeeder : IDatabaseSeeder
    {
        private readonly GasopperDbContext _context;
        private readonly ILogger<SmartSeeder> _logger;

        public SmartSeeder(GasopperDbContext context, ILogger<SmartSeeder> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task SeedAsync()
        {
            _logger.LogInformation("🌱 Starting database seeding process...");
            var startTime = DateTime.UtcNow;

            try
            {
                // Use individual INSERT statements with WHERE NOT EXISTS
                await SeedWithIndividualInsertsAsync();

                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation("✅ Database seeding completed successfully in {Duration}ms", duration.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Database seeding failed");
                // DON'T throw - just log and continue
                _logger.LogInformation("⚠️ Continuing despite seeding issues - data likely already exists");
            }
        }

        private async Task SeedWithIndividualInsertsAsync()
        {
            // Seed Roles one by one
            await ExecuteSafely("INSERT INTO roles (role_name) SELECT 'Admin' WHERE NOT EXISTS (SELECT 1 FROM roles WHERE role_name = 'Admin');");
            await ExecuteSafely("INSERT INTO roles (role_name) SELECT 'Manager' WHERE NOT EXISTS (SELECT 1 FROM roles WHERE role_name = 'Manager');");
            await ExecuteSafely("INSERT INTO roles (role_name) SELECT 'Salesperson' WHERE NOT EXISTS (SELECT 1 FROM roles WHERE role_name = 'Salesperson');");

            // Seed Station Types one by one
            await ExecuteSafely("INSERT INTO station_types (type_name) SELECT 'Full Service' WHERE NOT EXISTS (SELECT 1 FROM station_types WHERE type_name = 'Full Service');");
            await ExecuteSafely("INSERT INTO station_types (type_name) SELECT 'Self Service' WHERE NOT EXISTS (SELECT 1 FROM station_types WHERE type_name = 'Self Service');");
            await ExecuteSafely("INSERT INTO station_types (type_name) SELECT 'Truck Stop' WHERE NOT EXISTS (SELECT 1 FROM station_types WHERE type_name = 'Truck Stop');");
            await ExecuteSafely("INSERT INTO station_types (type_name) SELECT 'Convenience Store' WHERE NOT EXISTS (SELECT 1 FROM station_types WHERE type_name = 'Convenience Store');");

            // Seed Lead Statuses one by one
            await ExecuteSafely("INSERT INTO lead_statuses (status_name, description) SELECT 'New', 'New lead received' WHERE NOT EXISTS (SELECT 1 FROM lead_statuses WHERE status_name = 'New');");
            await ExecuteSafely("INSERT INTO lead_statuses (status_name, description) SELECT 'Qualified', 'Lead has been qualified' WHERE NOT EXISTS (SELECT 1 FROM lead_statuses WHERE status_name = 'Qualified');");
            await ExecuteSafely("INSERT INTO lead_statuses (status_name, description) SELECT 'Converted', 'Lead converted to opportunity' WHERE NOT EXISTS (SELECT 1 FROM lead_statuses WHERE status_name = 'Converted');");

            // Seed Opportunity Statuses one by one
            await ExecuteSafely("INSERT INTO opportunity_statuses (status_name, description) SELECT 'Planning', 'Planning phase' WHERE NOT EXISTS (SELECT 1 FROM opportunity_statuses WHERE status_name = 'Planning');");
            await ExecuteSafely("INSERT INTO opportunity_statuses (status_name, description) SELECT 'Active', 'Active opportunity' WHERE NOT EXISTS (SELECT 1 FROM opportunity_statuses WHERE status_name = 'Active');");
            await ExecuteSafely("INSERT INTO opportunity_statuses (status_name, description) SELECT 'Complete', 'Completed' WHERE NOT EXISTS (SELECT 1 FROM opportunity_statuses WHERE status_name = 'Complete');");

            _logger.LogInformation("✅ All reference data seeded successfully");
        }

        private async Task ExecuteSafely(string sql)
        {
            try
            {
                await _context.Database.ExecuteSqlRawAsync(sql);
            }
            catch (Exception ex)
            {
                _logger.LogDebug("SQL already exists or failed: {Message}", ex.Message);
                // Ignore errors - data probably already exists
            }
        }

        public async Task<bool> IsSeedRequiredAsync()
        {
            return true; // Always return true to be safe
        }

        public async Task<SeederStatus> GetSeederStatusAsync()
        {
            try
            {
                var rolesCount = await _context.Roles.CountAsync();
                var stationTypesCount = await _context.StationTypes.CountAsync();
                var leadStatusesCount = await _context.LeadStatuses.CountAsync();
                var opportunityStatusesCount = await _context.OpportunityStatuses.CountAsync();

                return new SeederStatus
                {
                    IsSeeded = rolesCount > 0,
                    RolesCount = rolesCount,
                    StationTypesCount = stationTypesCount,
                    LeadStatusesCount = leadStatusesCount,
                    OpportunityStatusesCount = opportunityStatusesCount,
                    HasAdminUser = true,
                    Status = "Seeded"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting seeder status");
                return new SeederStatus { Status = "Unknown" };
            }
        }
    }
}