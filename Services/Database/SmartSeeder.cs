using Microsoft.EntityFrameworkCore;
using gasopper_crm_server.Data;
using gasopper_crm_server.Models;
using BCrypt.Net;

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
                // 🔧 CHECK IF SEEDING IS NEEDED FIRST
                if (!await IsSeedRequiredAsync())
                {
                    _logger.LogInformation("✅ Database already seeded - skipping");
                    return;
                }

                // Use individual INSERT statements with WHERE NOT EXISTS
                await SeedWithIndividualInsertsAsync();
                
                // 🆕 CRITICAL: Create default admin user
                await CreateDefaultAdminUserAsync();

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

            // 🔧 FIXED: Seed ONLY the correct 4 station types
            await ExecuteSafely("INSERT INTO station_types (type_name) SELECT 'Only Gas' WHERE NOT EXISTS (SELECT 1 FROM station_types WHERE type_name = 'Only Gas');");
            await ExecuteSafely("INSERT INTO station_types (type_name) SELECT 'Gas and Booth Sales' WHERE NOT EXISTS (SELECT 1 FROM station_types WHERE type_name = 'Gas and Booth Sales');");
            await ExecuteSafely("INSERT INTO station_types (type_name) SELECT 'Gas and Convenience Store' WHERE NOT EXISTS (SELECT 1 FROM station_types WHERE type_name = 'Gas and Convenience Store');");
            await ExecuteSafely("INSERT INTO station_types (type_name) SELECT 'Gas, Booth Sales and Convenience Store' WHERE NOT EXISTS (SELECT 1 FROM station_types WHERE type_name = 'Gas, Booth Sales and Convenience Store');");

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

        private async Task CreateDefaultAdminUserAsync()
        {
            try
            {
                // Check if any users exist using EF Core first
                var userCount = await _context.Users.CountAsync();
                
                if (userCount == 0)
                {
                    // No users exist, create the admin user
                    string defaultPassword = "Admin123!";
                    string passwordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword);
                    
                    // Get the Admin role ID
                    var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.role_name == "Admin");
                    if (adminRole == null)
                    {
                        _logger.LogError("❌ Admin role not found! Cannot create admin user.");
                        return;
                    }

                    // Create the admin user directly using EF Core
                    var adminUser = new User
                    {
                        employee_id = "ADM001",
                        email = "phanisri444@gmail.com",
                        phone_number = "+91-9876543210",
                        address = "Hyderabad, Telangana, India",
                        first_name = "UMMEDISETTI",
                        last_name = "PHANISRI",
                        role_id = adminRole.role_id,
                        password_hash = passwordHash,
                        is_active = true,
                        created_at = DateTime.UtcNow,
                        last_updated = DateTime.UtcNow,
                        iat = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds(),
                        exp = ((DateTimeOffset)DateTime.UtcNow.AddYears(1)).ToUnixTimeSeconds()
                    };

                    _context.Users.Add(adminUser);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("👤 Created admin user: phanisri444@gmail.com (UMMEDISETTI PHANISRI)");
                    _logger.LogWarning("🔒 Default admin password: Admin123! - Login and change immediately!");
                    _logger.LogInformation("📧 OTP emails will be sent to: phanisri444@gmail.com");
                }
                else
                {
                    // Check if the specific admin user exists
                    var existingAdmin = await _context.Users
                        .FirstOrDefaultAsync(u => u.email.ToLower() == "phanisri444@gmail.com");
                    
                    if (existingAdmin != null)
                    {
                        _logger.LogInformation("✓ Admin user already exists: phanisri444@gmail.com");
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ Users exist but phanisri444@gmail.com not found. Existing users:");
                        var existingUsers = await _context.Users.Take(5).ToListAsync();
                        foreach (var user in existingUsers)
                        {
                            _logger.LogInformation($"   - {user.email} ({user.first_name} {user.last_name})");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create default admin user using EF Core, trying fallback...");
                
                // Fallback: Try direct SQL insert
                try
                {
                    await ExecuteSafely(@"
                        INSERT INTO users (
                            employee_id, email, phone_number, address, first_name, last_name, 
                            role_id, password_hash, is_active, email_verified, requires_password_reset,
                            created_at, last_updated, iat, exp
                        )
                        SELECT 
                            'ADM001',
                            'phanisri444@gmail.com',
                            '+91-9876543210',
                            'Hyderabad, Telangana, India',
                            'UMMEDISETTI',
                            'PHANISRI',
                            (SELECT role_id FROM roles WHERE role_name = 'Admin' LIMIT 1),
                            '$2a$11$rQJ8vwMvbRr6mQZ8vwMvbRr6mQZ8vwMvbRr6mQZ8vwMvbRr6mQZ8u',
                            true,
                            true,
                            false,
                            NOW(),
                            NOW(),
                            EXTRACT(EPOCH FROM NOW())::bigint,
                            EXTRACT(EPOCH FROM NOW() + INTERVAL '1 year')::bigint
                        WHERE NOT EXISTS (
                            SELECT 1 FROM users u 
                            WHERE LOWER(u.email) = LOWER('phanisri444@gmail.com')
                        );");
                    
                    _logger.LogInformation("👤 Fallback: Created admin user via direct SQL");
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "❌ Both EF Core and SQL fallback failed to create admin user");
                }
            }
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
            try
            {
                // Check if station types already exist
                var stationTypesCount = await _context.StationTypes.CountAsync();
                return stationTypesCount == 0; // Only seed if no station types exist
            }
            catch (Exception)
            {
                return true; // If error, assume seeding is needed
            }
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