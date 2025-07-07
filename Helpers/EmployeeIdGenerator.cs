using Microsoft.EntityFrameworkCore;
using gasopper_crm_server.Data;

namespace gasopper_crm_server.Helpers
{
    public static class EmployeeIdGenerator
    {
        /// <summary>
        /// Generates the next sequential Employee ID based on role
        /// Format: ADM001, MAN001, SAL001, etc.
        /// </summary>
        /// <param name="context">Database context</param>
        /// <param name="roleId">Role ID (1=Admin, 2=Manager, 3=Salesperson)</param>
        /// <returns>Generated Employee ID</returns>
        public static async Task<string> GenerateEmployeeIdAsync(GasopperDbContext context, int roleId)
        {
            try
            {
                // Determine prefix based on role
                var prefix = roleId switch
                {
                    1 => "ADM", // Admin
                    2 => "MAN", // Manager
                    3 => "SAL", // Salesperson
                    _ => "USR"  // Default fallback
                };

                // Get the highest existing number for this role
                var existingIds = await context.Users
                    .Where(u => u.role_id == roleId && u.employee_id.StartsWith(prefix))
                    .Select(u => u.employee_id)
                    .ToListAsync();

                var nextNumber = 1;
                
                if (existingIds.Any())
                {
                    // Extract numbers from existing IDs and find the maximum
                    var existingNumbers = existingIds
                        .Where(id => id.Length >= 6) // Ensure valid format (PREFIX + 3 digits)
                        .Select(id => 
                        {
                            var numberPart = id.Substring(3); // Remove prefix
                            return int.TryParse(numberPart, out var num) ? num : 0;
                        })
                        .Where(num => num > 0);

                    if (existingNumbers.Any())
                    {
                        nextNumber = existingNumbers.Max() + 1;
                    }
                }

                // Format with 3-digit padding: ADM001, MAN001, SAL001
                return $"{prefix}{nextNumber:D3}";
            }
            catch (Exception)
            {
                // Fallback to timestamp-based ID in case of any error
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString().Substring(5);
                var prefix = roleId switch
                {
                    1 => "ADM",
                    2 => "MAN", 
                    3 => "SAL",
                    _ => "USR"
                };
                return $"{prefix}{timestamp}";
            }
        }

        /// <summary>
        /// Validates if an Employee ID follows the correct format
        /// </summary>
        /// <param name="employeeId">Employee ID to validate</param>
        /// <param name="roleId">Expected role ID</param>
        /// <returns>True if valid format</returns>
        public static bool IsValidEmployeeIdFormat(string employeeId, int roleId)
        {
            if (string.IsNullOrWhiteSpace(employeeId) || employeeId.Length != 6)
                return false;

            var expectedPrefix = roleId switch
            {
                1 => "ADM",
                2 => "MAN",
                3 => "SAL",
                _ => "USR"
            };

            return employeeId.StartsWith(expectedPrefix) && 
                   int.TryParse(employeeId.Substring(3), out var number) && 
                   number > 0;
        }
    }
}