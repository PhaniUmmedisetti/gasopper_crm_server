using Microsoft.EntityFrameworkCore;
using gasopper_crm_server.Data;

namespace gasopper_crm_server.Services.Database
{
    public class DataMigrationService
    {
        private readonly GasopperDbContext _context;
        private readonly ILogger<DataMigrationService> _logger;
        private readonly IDatabaseSeeder _seeder;

        public DataMigrationService(
            GasopperDbContext context, 
            ILogger<DataMigrationService> logger,
            IDatabaseSeeder seeder)
        {
            _context = context;
            _logger = logger;
            _seeder = seeder;
        }

        public async Task<bool> ApplyDataMigrationsAsync()
        {
            _logger.LogInformation("🔄 Starting data migration process...");

            try
            {
                // Simple approach - just run the seeder
                await _seeder.SeedAsync();
                
                _logger.LogInformation("✅ Data migration completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Data migration failed");
                return false;
            }
        }
    }
}