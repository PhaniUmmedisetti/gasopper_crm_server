using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using gasopper_crm_server.Data;
using gasopper_crm_server.Services.Database;

namespace gasopper_crm_server.Services.Database
{
    public class DatabaseHealthService : IHealthCheck
    {
        private readonly GasopperDbContext _context;
        private readonly IDatabaseSeeder _seeder;
        private readonly ILogger<DatabaseHealthService> _logger;

        public DatabaseHealthService(
            GasopperDbContext context, 
            IDatabaseSeeder seeder,
            ILogger<DatabaseHealthService> logger)
        {
            _context = context;
            _seeder = seeder;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("🔍 Starting database health check...");

                var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
                if (!canConnect)
                {
                    return HealthCheckResult.Unhealthy("Cannot connect to database");
                }

                var seederStatus = await _seeder.GetSeederStatusAsync();
                
                var data = new Dictionary<string, object>
                {
                    ["CanConnect"] = canConnect,
                    ["IsSeeded"] = seederStatus.IsSeeded,
                    ["RolesCount"] = seederStatus.RolesCount,
                    ["StationTypesCount"] = seederStatus.StationTypesCount,
                    ["LeadStatusesCount"] = seederStatus.LeadStatusesCount,
                    ["OpportunityStatusesCount"] = seederStatus.OpportunityStatusesCount,
                    ["HasAdminUser"] = seederStatus.HasAdminUser,
                    ["Status"] = seederStatus.Status
                };

                if (!seederStatus.IsSeeded)
                {
                    return HealthCheckResult.Degraded(
                        "Database connected but not properly seeded", 
                        data: data);
                }

                if (!seederStatus.HasAdminUser)
                {
                    return HealthCheckResult.Degraded(
                        "Database seeded but no admin user found", 
                        data: data);
                }

                _logger.LogDebug("✅ Database health check passed");
                return HealthCheckResult.Healthy("Database is healthy and fully seeded", data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Database health check failed");
                return HealthCheckResult.Unhealthy($"Database health check failed: {ex.Message}");
            }
        }
    }
}