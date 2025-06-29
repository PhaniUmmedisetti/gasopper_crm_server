// Helpers/StationCodeGenerator.cs
// Helper utility for generating unique station codes based on postal codes

using Microsoft.EntityFrameworkCore;
using gasopper_crm_server.Data;
using System.Text.RegularExpressions;

namespace gasopper_crm_server.Helpers
{
    public static class StationCodeGenerator
    {
        /// <summary>
        /// Generates a unique station code based on the opportunity's postal code.
        /// Format: {First3DigitsOfPostalCode}{SequentialNumber:D2}
        /// Example: Postal code 09827 → Station codes: 09801, 09802, 09803, etc.
        /// </summary>
        /// <param name="context">Database context</param>
        /// <param name="opportunityId">Opportunity ID to get postal code from</param>
        /// <returns>Unique station code or empty string if failed</returns>
        public static async Task<string> GenerateUniqueStationCodeAsync(GasopperDbContext context, int opportunityId)
        {
            try
            {
                // Get opportunity to extract postal code
                var opportunity = await context.Opportunities
                    .FirstOrDefaultAsync(o => o.opportunity_id == opportunityId && !o.is_deleted);

                if (opportunity == null)
                    return string.Empty;

                // Extract postal code safely
                string postalCode = ExtractPostalCode(opportunity);
                if (string.IsNullOrEmpty(postalCode) || postalCode.Length < 3)
                    return GenerateFallbackCode(opportunityId);

                // Get first 3 digits of postal code
                string prefix = postalCode.Substring(0, 3);

                // Find all existing station codes with this prefix across ALL opportunities
                var existingCodes = await context.GasStations
                    .Where(gs => !gs.is_deleted && gs.station_code.StartsWith(prefix))
                    .Select(gs => gs.station_code)
                    .ToListAsync();

                // Find the next available sequence number
                int nextSequence = FindNextSequence(prefix, existingCodes);

                // Generate the new station code
                return $"{prefix}{nextSequence:D2}";
            }
            catch (Exception ex)
            {
                // Log error and return fallback code
                Console.WriteLine($"[ERROR] Failed to generate station code for opportunity {opportunityId}: {ex.Message}");
                return GenerateFallbackCode(opportunityId);
            }
        }

        /// <summary>
        /// Extracts postal code from opportunity, using postal_code field first, then parsing owner_address
        /// </summary>
        /// <param name="opportunity">Opportunity entity</param>
        /// <returns>5-digit postal code or empty string</returns>
        private static string ExtractPostalCode(Models.Opportunity opportunity)
        {
            // Priority 1: Use postal_code field directly
            if (!string.IsNullOrEmpty(opportunity.postal_code))
            {
                string cleanedPostal = CleanPostalCode(opportunity.postal_code);
                if (IsValidPostalCode(cleanedPostal))
                    return cleanedPostal;
            }

            // // Priority 2: Parse from owner_address as fallback
            // if (!string.IsNullOrEmpty(opportunity.owner_address))
            // {
            //     string extractedPostal = ExtractPostalCodeFromAddress(opportunity.owner_address);
            //     if (IsValidPostalCode(extractedPostal))
            //         return extractedPostal;
            // }

            // Priority 3: Parse from address_line_1 as last resort
            if (!string.IsNullOrEmpty(opportunity.address_line_1))
            {
                string extractedPostal = ExtractPostalCodeFromAddress(opportunity.address_line_1);
                if (IsValidPostalCode(extractedPostal))
                    return extractedPostal;
            }

            return string.Empty;
        }

        /// <summary>
        /// Extracts postal code from address string using regex
        /// </summary>
        /// <param name="address">Address string</param>
        /// <returns>5-digit postal code or empty string</returns>
        private static string ExtractPostalCodeFromAddress(string address)
        {
            if (string.IsNullOrEmpty(address))
                return string.Empty;

            // Match 5-digit postal codes (with optional -4 extension)
            var match = Regex.Match(address, @"\b(\d{5})(?:-\d{4})?\b");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        /// <summary>
        /// Cleans postal code by removing non-digits
        /// </summary>
        /// <param name="postalCode">Raw postal code</param>
        /// <returns>Clean 5-digit postal code</returns>
        private static string CleanPostalCode(string postalCode)
        {
            if (string.IsNullOrEmpty(postalCode))
                return string.Empty;

            // Extract only digits
            string digitsOnly = Regex.Replace(postalCode, @"\D", "");
            
            // Return first 5 digits if available
            return digitsOnly.Length >= 5 ? digitsOnly.Substring(0, 5) : digitsOnly;
        }

        /// <summary>
        /// Validates if postal code is exactly 5 digits
        /// </summary>
        /// <param name="postalCode">Postal code to validate</param>
        /// <returns>True if valid 5-digit postal code</returns>
        private static bool IsValidPostalCode(string postalCode)
        {
            return !string.IsNullOrEmpty(postalCode) && 
                   postalCode.Length == 5 && 
                   postalCode.All(char.IsDigit);
        }

        /// <summary>
        /// Finds the next available sequence number for a given prefix
        /// </summary>
        /// <param name="prefix">3-digit prefix</param>
        /// <param name="existingCodes">List of existing station codes with this prefix</param>
        /// <returns>Next sequence number (starting from 1)</returns>
        private static int FindNextSequence(string prefix, List<string> existingCodes)
        {
            if (!existingCodes.Any())
                return 1;

            // Extract sequence numbers from existing codes
            var sequences = existingCodes
                .Where(code => code.StartsWith(prefix) && code.Length == prefix.Length + 2)
                .Select(code => {
                    string sequencePart = code.Substring(prefix.Length);
                    return int.TryParse(sequencePart, out int seq) ? seq : 0;
                })
                .Where(seq => seq > 0)
                .ToList();

            if (!sequences.Any())
                return 1;

            // Find the highest sequence number and increment
            return sequences.Max() + 1;
        }

        /// <summary>
        /// Generates a fallback station code when postal code extraction fails
        /// </summary>
        /// <param name="opportunityId">Opportunity ID</param>
        /// <returns>Fallback station code</returns>
        private static string GenerateFallbackCode(int opportunityId)
        {
            return $"TMP{opportunityId:D3}01";
        }

        /// <summary>
        /// Validates if a station code follows the expected format
        /// </summary>
        /// <param name="stationCode">Station code to validate</param>
        /// <returns>True if valid format</returns>
        public static bool IsValidStationCode(string stationCode)
        {
            if (string.IsNullOrEmpty(stationCode))
                return false;

            // Check for standard format: 3 digits + 2 digits (e.g., 09801)
            if (stationCode.Length == 5 && stationCode.All(char.IsDigit))
                return true;

            // Check for fallback format: TMP + 3 digits + 2 digits (e.g., TMP00101)
            if (stationCode.Length == 8 && stationCode.StartsWith("TMP") && stationCode.Substring(3).All(char.IsDigit))
                return true;

            return false;
        }

        /// <summary>
        /// Extracts the postal code prefix from a station code
        /// </summary>
        /// <param name="stationCode">Station code</param>
        /// <returns>3-digit postal code prefix or empty string</returns>
        public static string ExtractPostalCodePrefix(string stationCode)
        {
            if (string.IsNullOrEmpty(stationCode) || stationCode.Length < 3)
                return string.Empty;

            if (stationCode.StartsWith("TMP"))
                return string.Empty;

            return stationCode.Substring(0, 3);
        }
    }
}