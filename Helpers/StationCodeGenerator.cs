// Helpers/StationCodeGenerator.cs
// UPDATED: Helper utility for generating unique station codes based on STATION postal codes with GLOBAL sequence numbering

using Microsoft.EntityFrameworkCore;
using gasopper_crm_server.Data;
using System.Text.RegularExpressions;

namespace gasopper_crm_server.Helpers
{
    public static class StationCodeGenerator
    {
        /// <summary>
        /// Generates a unique station code based on the STATION's postal code with GLOBAL sequence numbering.
        /// Format: {First3DigitsOfPostalCode}{GlobalSequentialNumber:D2}
        /// Example: 
        /// - Station 1 with postal 09823 → 09801
        /// - Station 2 with postal 09823 → 09802  
        /// - Station 3 with postal 09823 → 09803
        /// - Station 4 with postal 09845 → 09804 (same first 3 digits, continues global sequence)
        /// </summary>
        /// <param name="context">Database context</param>
        /// <param name="stationPostalCode">Station's postal code (from station creation form)</param>
        /// <returns>Unique station code or empty string if failed</returns>
        public static async Task<string> GenerateUniqueStationCodeAsync(GasopperDbContext context, string stationPostalCode)
        {
            try
            {
                // Validate and clean postal code
                string cleanedPostalCode = CleanPostalCode(stationPostalCode);
                if (!IsValidPostalCode(cleanedPostalCode))
                {
                    Console.WriteLine($"[ERROR] Invalid postal code: {stationPostalCode}");
                    return GenerateFallbackCode();
                }

                // Extract first 3 digits as prefix
                string prefix = cleanedPostalCode.Substring(0, 3);
                Console.WriteLine($"[DEBUG] Using postal code prefix: {prefix} from postal code: {cleanedPostalCode}");

                // Find ALL existing station codes with the same first 3 digits (GLOBAL search across all opportunities)
                var existingCodes = await context.GasStations
                    .Where(gs => !gs.is_deleted && gs.station_code.StartsWith(prefix))
                    .Select(gs => gs.station_code)
                    .ToListAsync();

                Console.WriteLine($"[DEBUG] Found {existingCodes.Count} existing stations with prefix {prefix}");

                // Find the next available GLOBAL sequence number
                int nextSequence = FindNextGlobalSequence(prefix, existingCodes);

                // Generate the new station code
                string stationCode = $"{prefix}{nextSequence:D2}";
                
                Console.WriteLine($"[DEBUG] Generated station code: {stationCode} (prefix: {prefix}, sequence: {nextSequence})");
                return stationCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to generate station code for postal {stationPostalCode}: {ex.Message}");
                return GenerateFallbackCode();
            }
        }

        /// <summary>
        /// BACKWARD COMPATIBILITY: Old method that used opportunity postal code
        /// Now redirects to use station postal code
        /// </summary>
        /// <param name="context">Database context</param>
        /// <param name="opportunityId">Opportunity ID (used for fallback only)</param>
        /// <returns>Fallback station code</returns>
        [Obsolete("Use GenerateUniqueStationCodeAsync(context, stationPostalCode) instead")]
        public static Task<string> GenerateUniqueStationCodeAsync(GasopperDbContext context, int opportunityId)
        {
            Console.WriteLine($"[WARNING] Using deprecated method. Opportunity {opportunityId} - generating fallback code");
            return Task.FromResult(GenerateFallbackCode(opportunityId));
        }

        /// <summary>
        /// Cleans postal code by removing non-digits and extracting first 5 digits
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
            return digitsOnly.Length >= 5 ? digitsOnly.Substring(0, 5) : digitsOnly.PadLeft(5, '0');
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
        /// Finds the next available GLOBAL sequence number for a given 3-digit prefix
        /// This searches across ALL opportunities and stations with the same prefix
        /// </summary>
        /// <param name="prefix">3-digit postal code prefix (e.g., "098")</param>
        /// <param name="existingCodes">List of existing station codes with this prefix</param>
        /// <returns>Next GLOBAL sequence number (starting from 01)</returns>
        private static int FindNextGlobalSequence(string prefix, List<string> existingCodes)
        {
            if (!existingCodes.Any())
            {
                Console.WriteLine($"[DEBUG] No existing codes for prefix {prefix}, starting with 01");
                return 1;
            }

            // Extract sequence numbers from existing codes
            // Expected format: {3-digit-prefix}{2-digit-sequence} (e.g., 09801, 09802)
            var sequences = existingCodes
                .Where(code => code.StartsWith(prefix) && code.Length == 5) // 3 digits prefix + 2 digits sequence
                .Select(code => {
                    string sequencePart = code.Substring(3); // Get last 2 digits
                    bool parsed = int.TryParse(sequencePart, out int seq);
                    Console.WriteLine($"[DEBUG] Code: {code}, Sequence part: {sequencePart}, Parsed: {parsed}, Value: {seq}");
                    return parsed ? seq : 0;
                })
                .Where(seq => seq > 0)
                .OrderBy(seq => seq)
                .ToList();

            if (!sequences.Any())
            {
                Console.WriteLine($"[DEBUG] No valid sequences found for prefix {prefix}, starting with 01");
                return 1;
            }

            // Find the highest sequence number and increment
            int maxSequence = sequences.Max();
            int nextSequence = maxSequence + 1;
            
            Console.WriteLine($"[DEBUG] Prefix {prefix}: Found {sequences.Count} sequences, max: {maxSequence}, next: {nextSequence}");
            return nextSequence;
        }

        /// <summary>
        /// Generates a fallback station code when postal code processing fails
        /// </summary>
        /// <param name="opportunityId">Optional opportunity ID for fallback</param>
        /// <returns>Fallback station code</returns>
        private static string GenerateFallbackCode(int? opportunityId = null)
        {
            var timestamp = DateTime.UtcNow.ToString("MMddHHmm");
            if (opportunityId.HasValue)
            {
                return $"TMP{opportunityId:D3}{timestamp.Substring(0, 2)}";
            }
            return $"TMP{timestamp}";
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

            // Check for fallback format: TMP + digits (e.g., TMP00112)
            if (stationCode.StartsWith("TMP") && stationCode.Length > 3)
                return true;

            return false;
        }

        /// <summary>
        /// Extracts the 3-digit prefix from a station code
        /// </summary>
        /// <param name="stationCode">Station code</param>
        /// <returns>3-digit prefix or empty string</returns>
        public static string ExtractPrefixFromStationCode(string stationCode)
        {
            if (string.IsNullOrEmpty(stationCode) || stationCode.Length < 3)
                return string.Empty;

            if (stationCode.StartsWith("TMP"))
                return string.Empty;

            // Extract first 3 digits as prefix
            return stationCode.Substring(0, 3);
        }

        /// <summary>
        /// Extracts the sequence number from a station code
        /// </summary>
        /// <param name="stationCode">Station code</param>
        /// <returns>Sequence number or 0 if invalid</returns>
        public static int ExtractSequenceFromStationCode(string stationCode)
        {
            if (string.IsNullOrEmpty(stationCode) || stationCode.Length != 5 || stationCode.StartsWith("TMP"))
                return 0;

            string sequencePart = stationCode.Substring(3);
            return int.TryParse(sequencePart, out int sequence) ? sequence : 0;
        }
    }
}