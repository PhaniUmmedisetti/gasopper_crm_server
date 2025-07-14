// FINAL FIXED GasStationService.cs - Corrected role-based permissions + completion logic
// FIXES: Sign-off permissions now work correctly for all roles and completion states

using Microsoft.EntityFrameworkCore;
using gasopper_crm_server.Data;
using gasopper_crm_server.DTOs;
using gasopper_crm_server.Models;
using gasopper_crm_server.Helpers;

namespace gasopper_crm_server.Services
{
    public interface IGasStationService
    {
        Task<GasStationResponseDto?> CreateGasStationAsync(int opportunityId, CreateGasStationDto dto, int currentUserId, int currentUserRole);
        Task<GasStationResponseDto?> GetGasStationByIdAsync(int id, int currentUserId, int currentUserRole);
        Task<List<GasStationListResponseDto>> GetGasStationsByOpportunityAsync(int opportunityId, int currentUserId, int currentUserRole);
        Task<List<GasStationListResponseDto>> GetGasStationsAsync(int currentUserId, int currentUserRole);
        Task<GasStationResponseDto?> UpdateGasStationAsync(int id, UpdateGasStationDto dto, int currentUserId, int currentUserRole);
        Task<bool> DeleteGasStationAsync(int id, int currentUserId, int currentUserRole);
        Task<List<GasStationListResponseDto>> GetMyGasStationsAsync(int currentUserId);
        Task<List<GasStationListResponseDto>> GetTeamGasStationsAsync(int currentUserId);
        Task<GasStationStatsDto> GetGasStationStatsAsync(int currentUserId, int currentUserRole);
        Task<List<StationTypeDto>> GetStationTypesAsync();
        Task<bool> UpdateOpportunityStatusFromStationsAsync(int opportunityId, int currentUserId, int currentUserRole);

        // FIXED: Sign-off functionality with role-based permissions
        Task<bool> SignOffStationAsync(int stationId, int currentUserId, int currentUserRole);
        Task<bool> CanUserSignOffStationAsync(int stationId, int currentUserId, int currentUserRole);
    }

    public class GasStationService : IGasStationService
    {
        private readonly GasopperDbContext _context;

        public GasStationService(GasopperDbContext context)
        {
            _context = context;
        }

        public async Task<GasStationResponseDto?> CreateGasStationAsync(int opportunityId, CreateGasStationDto dto, int currentUserId, int currentUserRole)
        {
            try
            {
                if (!await CanUserAccessOpportunityAsync(opportunityId, currentUserId, currentUserRole))
                {
                    Console.WriteLine($"[ERROR] User {currentUserId} cannot access opportunity {opportunityId}");
                    return null;
                }

                var stationCode = await StationCodeGenerator.GenerateUniqueStationCodeAsync(_context, dto.PostalCode);
                if (string.IsNullOrEmpty(stationCode))
                {
                    Console.WriteLine($"[ERROR] Failed to generate station code for postal code {dto.PostalCode}");
                    return null;
                }

                Console.WriteLine($"[DEBUG] Generated station code: {stationCode} for postal code {dto.PostalCode}");

                var gasStation = new GasStation
                {
                    opportunity_id = opportunityId,
                    station_name = dto.StationName,
                    address_line_1 = dto.AddressLine1,
                    address_line_2 = dto.AddressLine2,
                    city = dto.City,
                    state = dto.State,
                    postal_code = dto.PostalCode,
                    country = dto.Country,
                    station_code = stationCode,
                    poc_name = dto.PocName,
                    poc_phone = dto.PocPhone,
                    poc_email = dto.PocEmail,
                    number_of_pumps = dto.NumberOfPumps,
                    number_of_employees = dto.NumberOfEmployees,
                    station_type_id = dto.StationTypeId,
                    notes = dto.Notes,
                    created_by = currentUserId,
                    is_deleted = false,
                    is_signed_off = false,
                    signed_off_at = null
                };

                _context.GasStations.Add(gasStation);
                await _context.SaveChangesAsync();

                Console.WriteLine($"[DEBUG] Created station {gasStation.station_id} with code {stationCode}");

                await UpdateOpportunityStatusFromStationsAsync(opportunityId, currentUserId, currentUserRole);

                return await GetGasStationByIdAsync(gasStation.station_id, currentUserId, currentUserRole);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] CreateGasStationAsync failed: {ex.Message}");
                return null;
            }
        }

        public async Task<GasStationResponseDto?> GetGasStationByIdAsync(int id, int currentUserId, int currentUserRole)
        {
            try
            {
                var gasStation = await _context.GasStations
                    .Include(gs => gs.StationType)
                    .Include(gs => gs.CreatedByUser)
                    .Include(gs => gs.Opportunity)
                        .ThenInclude(o => o.Lead)
                    .FirstOrDefaultAsync(gs => gs.station_id == id && !gs.is_deleted);

                if (gasStation == null)
                {
                    Console.WriteLine($"[DEBUG] Station {id} not found");
                    return null;
                }

                if (!await CanUserAccessOpportunityAsync(gasStation.opportunity_id, currentUserId, currentUserRole))
                {
                    Console.WriteLine($"[ERROR] User {currentUserId} cannot access station {id}");
                    return null;
                }

                return await MapToGasStationResponseDtoAsync(gasStation, currentUserId, currentUserRole);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetGasStationByIdAsync failed: {ex.Message}");
                return null;
            }
        }

        public async Task<List<GasStationListResponseDto>> GetGasStationsByOpportunityAsync(int opportunityId, int currentUserId, int currentUserRole)
        {
            try
            {
                if (!await CanUserAccessOpportunityAsync(opportunityId, currentUserId, currentUserRole))
                {
                    Console.WriteLine($"[ERROR] User {currentUserId} cannot access opportunity {opportunityId}");
                    return new List<GasStationListResponseDto>();
                }

                var gasStations = await _context.GasStations
                    .Include(gs => gs.StationType)
                    .Include(gs => gs.CreatedByUser)
                    .Include(gs => gs.Opportunity)
                        .ThenInclude(o => o.Lead)
                    .Where(gs => gs.opportunity_id == opportunityId && !gs.is_deleted)
                    .OrderBy(gs => gs.station_code)
                    .ToListAsync();

                var results = new List<GasStationListResponseDto>();
                foreach (var gs in gasStations)
                {
                    results.Add(await MapToGasStationListResponseDtoAsync(gs, currentUserId, currentUserRole));
                }
                return results;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetGasStationsByOpportunityAsync failed: {ex.Message}");
                return new List<GasStationListResponseDto>();
            }
        }

        public async Task<List<GasStationListResponseDto>> GetGasStationsAsync(int currentUserId, int currentUserRole)
        {
            try
            {
                var query = _context.GasStations
                    .Include(gs => gs.StationType)
                    .Include(gs => gs.CreatedByUser)
                    .Include(gs => gs.Opportunity)
                        .ThenInclude(o => o.Lead)
                    .Where(gs => !gs.is_deleted);

                query = await ApplyRoleBasedFilteringAsync(query, currentUserId, currentUserRole);

                var gasStations = await query
                    .OrderBy(gs => gs.station_code)
                    .ToListAsync();

                var results = new List<GasStationListResponseDto>();
                foreach (var gs in gasStations)
                {
                    results.Add(await MapToGasStationListResponseDtoAsync(gs, currentUserId, currentUserRole));
                }
                return results;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetGasStationsAsync failed: {ex.Message}");
                return new List<GasStationListResponseDto>();
            }
        }

        public async Task<GasStationResponseDto?> UpdateGasStationAsync(int id, UpdateGasStationDto dto, int currentUserId, int currentUserRole)
        {
            try
            {
                var gasStation = await _context.GasStations
                    .Include(gs => gs.Opportunity)
                    .FirstOrDefaultAsync(gs => gs.station_id == id && !gs.is_deleted);

                if (gasStation == null)
                {
                    Console.WriteLine($"[DEBUG] Station {id} not found for update");
                    return null;
                }

                if (!await CanUserEditStationAsync(gasStation, currentUserId, currentUserRole))
                {
                    Console.WriteLine($"[ERROR] User {currentUserId} cannot update station {id}");
                    return null;
                }

                if (gasStation.is_signed_off)
                {
                    Console.WriteLine($"[ERROR] Station {id} is signed off and cannot be updated");
                    return null;
                }

                if (dto.StationName != null)
                    gasStation.station_name = dto.StationName;
                if (dto.PocName != null)
                    gasStation.poc_name = dto.PocName;
                if (dto.PocPhone != null)
                    gasStation.poc_phone = dto.PocPhone;
                if (dto.PocEmail != null)
                    gasStation.poc_email = dto.PocEmail;
                if (dto.NumberOfPumps.HasValue)
                    gasStation.number_of_pumps = dto.NumberOfPumps;
                if (dto.NumberOfEmployees.HasValue)
                    gasStation.number_of_employees = dto.NumberOfEmployees;
                if (dto.StationTypeId.HasValue)
                    gasStation.station_type_id = dto.StationTypeId;
                if (dto.Notes != null)
                    gasStation.notes = dto.Notes;

                await _context.SaveChangesAsync();

                Console.WriteLine($"[DEBUG] Station {id} updated successfully");

                await UpdateOpportunityStatusFromStationsAsync(gasStation.opportunity_id, currentUserId, currentUserRole);

                return await GetGasStationByIdAsync(id, currentUserId, currentUserRole);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] UpdateGasStationAsync failed: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> DeleteGasStationAsync(int id, int currentUserId, int currentUserRole)
        {
            try
            {
                var gasStation = await _context.GasStations
                    .Include(gs => gs.Opportunity)
                    .FirstOrDefaultAsync(gs => gs.station_id == id && !gs.is_deleted);

                if (gasStation == null)
                {
                    Console.WriteLine($"[DEBUG] Station {id} not found for deletion");
                    return false;
                }

                if (!await CanUserEditStationAsync(gasStation, currentUserId, currentUserRole))
                {
                    Console.WriteLine($"[ERROR] User {currentUserId} cannot delete station {id}");
                    return false;
                }

                if (gasStation.is_signed_off)
                {
                    Console.WriteLine($"[ERROR] Station {id} is signed off and cannot be deleted");
                    return false;
                }

                gasStation.is_deleted = true;
                await _context.SaveChangesAsync();

                Console.WriteLine($"[DEBUG] Station {id} soft deleted successfully");

                await UpdateOpportunityStatusFromStationsAsync(gasStation.opportunity_id, currentUserId, currentUserRole);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] DeleteGasStationAsync failed: {ex.Message}");
                return false;
            }
        }

        // FIXED: Perfect sign-off implementation with role-based permissions
        public async Task<bool> SignOffStationAsync(int stationId, int currentUserId, int currentUserRole)
        {
            try
            {
                var gasStation = await _context.GasStations
                    .Include(gs => gs.Opportunity)
                    .FirstOrDefaultAsync(gs => gs.station_id == stationId && !gs.is_deleted);

                if (gasStation == null)
                {
                    Console.WriteLine($"[DEBUG] Station {stationId} not found for sign-off");
                    return false;
                }

                // FIXED: Use role-based permission logic instead of creator-only check
                if (!await CanUserEditStationAsync(gasStation, currentUserId, currentUserRole))
                {
                    Console.WriteLine($"[ERROR] User {currentUserId} does not have permission to sign off station {stationId}");
                    return false;
                }

                if (gasStation.is_signed_off)
                {
                    Console.WriteLine($"[ERROR] Station {stationId} is already signed off");
                    return false;
                }

                // FIXED: Use completion percentage instead of strict validation
                var completionPercentage = CalculateStationCompletionPercentage(gasStation);
                if (completionPercentage < 100)
                {
                    Console.WriteLine($"[ERROR] Station {stationId} is not ready for sign-off - completion: {completionPercentage}%");
                    return false;
                }

                gasStation.is_signed_off = true;
                gasStation.signed_off_at = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                Console.WriteLine($"[DEBUG] Station {stationId} signed off successfully by user {currentUserId} (role: {currentUserRole})");

                await UpdateOpportunityStatusFromStationsAsync(gasStation.opportunity_id, currentUserId, currentUserRole);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] SignOffStationAsync failed: {ex.Message}");
                return false;
            }
        }

        // FIXED: Perfect permission checking with role-based logic
        public async Task<bool> CanUserSignOffStationAsync(int stationId, int currentUserId, int currentUserRole)
        {
            try
            {
                var gasStation = await _context.GasStations
                    .Include(gs => gs.Opportunity)
                    .FirstOrDefaultAsync(gs => gs.station_id == stationId && !gs.is_deleted);

                if (gasStation == null)
                    return false;

                if (gasStation.is_signed_off)
                    return false;

                // FIXED: Use role-based permission logic for consistency
                if (!await CanUserEditStationAsync(gasStation, currentUserId, currentUserRole))
                    return false;

                // FIXED: Use completion percentage instead of strict validation
                var completionPercentage = CalculateStationCompletionPercentage(gasStation);
                return completionPercentage >= 100;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] CanUserSignOffStationAsync failed: {ex.Message}");
                return false;
            }
        }

        public async Task<List<GasStationListResponseDto>> GetMyGasStationsAsync(int currentUserId)
        {
            try
            {
                var gasStations = await _context.GasStations
                    .Include(gs => gs.StationType)
                    .Include(gs => gs.CreatedByUser)
                    .Include(gs => gs.Opportunity)
                        .ThenInclude(o => o.Lead)
                    .Where(gs => !gs.is_deleted && gs.Opportunity.assigned_to == currentUserId)
                    .OrderBy(gs => gs.station_code)
                    .ToListAsync();

                var results = new List<GasStationListResponseDto>();
                foreach (var gs in gasStations)
                {
                    results.Add(await MapToGasStationListResponseDtoAsync(gs, currentUserId, 3)); // Assume salesperson for "my stations"
                }
                return results;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetMyGasStationsAsync failed: {ex.Message}");
                return new List<GasStationListResponseDto>();
            }
        }

        public async Task<List<GasStationListResponseDto>> GetTeamGasStationsAsync(int currentUserId)
        {
            try
            {
                var teamMemberIds = await _context.Users
                    .Where(u => u.manager_id == currentUserId && u.is_active)
                    .Select(u => u.user_id)
                    .ToListAsync();

                teamMemberIds.Add(currentUserId);

                var gasStations = await _context.GasStations
                    .Include(gs => gs.StationType)
                    .Include(gs => gs.CreatedByUser)
                    .Include(gs => gs.Opportunity)
                        .ThenInclude(o => o.Lead)
                    .Where(gs => !gs.is_deleted && teamMemberIds.Contains(gs.Opportunity.assigned_to))
                    .OrderBy(gs => gs.station_code)
                    .ToListAsync();

                var results = new List<GasStationListResponseDto>();
                foreach (var gs in gasStations)
                {
                    results.Add(await MapToGasStationListResponseDtoAsync(gs, currentUserId, 2)); // Assume manager for team stations
                }
                return results;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetTeamGasStationsAsync failed: {ex.Message}");
                return new List<GasStationListResponseDto>();
            }
        }

        public async Task<GasStationStatsDto> GetGasStationStatsAsync(int currentUserId, int currentUserRole)
        {
            try
            {
                var query = _context.GasStations
                    .Include(gs => gs.StationType)
                    .Include(gs => gs.Opportunity)
                    .Where(gs => !gs.is_deleted);

                query = await ApplyRoleBasedFilteringAsync(query, currentUserId, currentUserRole);

                var gasStations = await query.ToListAsync();

                var totalStations = gasStations.Count;
                
                // FIXED: Use completion percentage for consistency
                var completeStations = gasStations.Count(gs => CalculateStationCompletionPercentage(gs) >= 100);
                var incompleteStations = totalStations - completeStations;
                var completionRate = totalStations > 0 ? (double)completeStations / totalStations * 100 : 0;

                var signedOffStations = gasStations.Count(gs => gs.is_signed_off);
                var pendingSignOffStations = gasStations.Count(gs => CalculateStationCompletionPercentage(gs) >= 100 && !gs.is_signed_off);
                var signOffRate = totalStations > 0 ? (double)signedOffStations / totalStations * 100 : 0;

                var opportunityIds = gasStations.Select(gs => gs.opportunity_id).Distinct().ToList();
                var averageStationsPerOpportunity = opportunityIds.Count > 0 ? (double)totalStations / opportunityIds.Count : 0;

                var stationTypeBreakdown = gasStations
                    .Where(gs => gs.StationType != null)
                    .GroupBy(gs => gs.StationType!.type_name)
                    .ToDictionary(g => g.Key, g => g.Count());

                var completionBreakdown = new Dictionary<string, int>
                {
                    ["Complete"] = completeStations,
                    ["Incomplete"] = incompleteStations
                };

                var signOffBreakdown = new Dictionary<string, int>
                {
                    ["Signed Off"] = signedOffStations,
                    ["Ready for Sign-off"] = pendingSignOffStations,
                    ["Not Ready"] = totalStations - signedOffStations - pendingSignOffStations
                };

                return new GasStationStatsDto
                {
                    TotalStations = totalStations,
                    CompleteStations = completeStations,
                    IncompleteStations = incompleteStations,
                    CompletionRate = Math.Round(completionRate, 1),
                    AverageStationsPerOpportunity = (int)Math.Round(averageStationsPerOpportunity),
                    SignedOffStations = signedOffStations,
                    PendingSignOffStations = pendingSignOffStations,
                    SignOffRate = Math.Round(signOffRate, 1),
                    StationTypeBreakdown = stationTypeBreakdown,
                    CompletionBreakdown = completionBreakdown,
                    SignOffBreakdown = signOffBreakdown
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetGasStationStatsAsync failed: {ex.Message}");
                return new GasStationStatsDto();
            }
        }

        public async Task<List<StationTypeDto>> GetStationTypesAsync()
        {
            try
            {
                var stationTypes = await _context.StationTypes
                    .OrderBy(st => st.station_type_id)
                    .ToListAsync();

                return stationTypes.Select(st => new StationTypeDto
                {
                    StationTypeId = st.station_type_id,
                    TypeName = st.type_name
                }).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetStationTypesAsync failed: {ex.Message}");
                return new List<StationTypeDto>();
            }
        }

        public async Task<bool> UpdateOpportunityStatusFromStationsAsync(int opportunityId, int currentUserId, int currentUserRole)
        {
            try
            {
                if (!await CanUserAccessOpportunityAsync(opportunityId, currentUserId, currentUserRole))
                {
                    Console.WriteLine($"[ERROR] User {currentUserId} cannot update opportunity {opportunityId} status");
                    return false;
                }

                var opportunity = await _context.Opportunities
                    .Include(o => o.GasStations.Where(gs => !gs.is_deleted))
                    .FirstOrDefaultAsync(o => o.opportunity_id == opportunityId && !o.is_deleted);

                if (opportunity == null)
                {
                    Console.WriteLine($"[DEBUG] Opportunity {opportunityId} not found");
                    return false;
                }

                var totalStations = opportunity.GasStations.Count;
                var signedOffStations = opportunity.GasStations.Count(gs => gs.is_signed_off);

                int newStatusId = (totalStations > 0 && signedOffStations == totalStations) ? 3 : 2; // 3 = Complete, 2 = Active
                if (opportunity.status_id != newStatusId)
                {
                    opportunity.status_id = newStatusId;
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"[DEBUG] Opportunity {opportunityId} status updated to {newStatusId} (signed off: {signedOffStations}/{totalStations})");
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] UpdateOpportunityStatusFromStationsAsync failed: {ex.Message}");
                return false;
            }
        }

        // Perfect role-based permission logic
        private async Task<bool> CanUserEditStationAsync(GasStation gasStation, int currentUserId, int currentUserRole)
        {
            try
            {
                // Load the Opportunity relationship if not already loaded
                if (gasStation.Opportunity == null)
                {
                    await _context.Entry(gasStation)
                        .Reference(gs => gs.Opportunity)
                        .LoadAsync();
                }

                if (gasStation.Opportunity == null)
                {
                    Console.WriteLine($"[ERROR] Could not load Opportunity for station {gasStation.station_id}");
                    return false;
                }

                // Admin can edit any station
                if (currentUserRole == 1)
                {
                    return true;
                }

                // Manager can edit stations from opportunities assigned to them or their team
                if (currentUserRole == 2)
                {
                    // Can edit if opportunity is assigned to manager
                    if (gasStation.Opportunity.assigned_to == currentUserId)
                    {
                        return true;
                    }

                    // Can edit if opportunity is assigned to a team member
                    var teamMemberIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId && u.is_active)
                        .Select(u => u.user_id)
                        .ToListAsync();

                    return teamMemberIds.Contains(gasStation.Opportunity.assigned_to);
                }

                // Salesperson can edit stations from opportunities assigned to them
                if (currentUserRole == 3)
                {
                    return gasStation.Opportunity.assigned_to == currentUserId;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] CanUserEditStationAsync failed: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> CanUserAccessOpportunityAsync(int opportunityId, int userId, int userRole)
        {
            try
            {
                var opportunity = await _context.Opportunities
                    .FirstOrDefaultAsync(o => o.opportunity_id == opportunityId && !o.is_deleted);

                if (opportunity == null)
                    return false;

                if (userRole == 3) // Salesperson
                    return opportunity.assigned_to == userId;

                if (userRole == 2) // Manager
                {
                    var teamMemberIds = await _context.Users
                        .Where(u => u.manager_id == userId && u.is_active)
                        .Select(u => u.user_id)
                        .ToListAsync();

                    teamMemberIds.Add(userId);
                    return teamMemberIds.Contains(opportunity.assigned_to);
                }

                return true; // Admin
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] CanUserAccessOpportunityAsync failed: {ex.Message}");
                return false;
            }
        }

        private async Task<IQueryable<GasStation>> ApplyRoleBasedFilteringAsync(IQueryable<GasStation> query, int currentUserId, int currentUserRole)
        {
            if (currentUserRole == 3) // Salesperson
            {
                return query.Where(gs => gs.Opportunity.assigned_to == currentUserId);
            }
            else if (currentUserRole == 2) // Manager
            {
                var teamMemberIds = await _context.Users
                    .Where(u => u.manager_id == currentUserId && u.is_active)
                    .Select(u => u.user_id)
                    .ToListAsync();

                teamMemberIds.Add(currentUserId);
                return query.Where(gs => teamMemberIds.Contains(gs.Opportunity.assigned_to));
            }

            return query; // Admin
        }

        // FIXED: Use completion percentage for isComplete calculation
        private static bool IsStationComplete(GasStation station)
        {
            return CalculateStationCompletionPercentage(station) >= 100;
        }

        private static double CalculateStationCompletionPercentage(GasStation station)
        {
            var totalFields = 10; // Match frontend calculation
            var filledFields = 0;

            // Same 10 fields as frontend
            if (!string.IsNullOrWhiteSpace(station.station_name)) filledFields++;
            if (!string.IsNullOrWhiteSpace(station.address_line_1)) filledFields++;
            if (!string.IsNullOrWhiteSpace(station.city)) filledFields++;
            if (!string.IsNullOrWhiteSpace(station.state)) filledFields++;
            if (!string.IsNullOrWhiteSpace(station.postal_code)) filledFields++;
            if (!string.IsNullOrWhiteSpace(station.poc_name)) filledFields++;
            if (!string.IsNullOrWhiteSpace(station.poc_phone)) filledFields++;
            if (!string.IsNullOrWhiteSpace(station.poc_email)) filledFields++;
            if (station.number_of_pumps.HasValue) filledFields++;
            if (station.number_of_employees.HasValue) filledFields++;

            return Math.Round((double)filledFields / totalFields * 100, 1);
        }

        // FIXED: Perfect DTO mapping with role-based permissions and completion percentage
        private async Task<GasStationResponseDto> MapToGasStationResponseDtoAsync(GasStation gasStation, int currentUserId, int currentUserRole)
        {
            var canEdit = await CanUserEditStationAsync(gasStation, currentUserId, currentUserRole);
            var completionPercentage = CalculateStationCompletionPercentage(gasStation);
            
            // FIXED: Use completion percentage instead of strict validation
            var canSignOff = canEdit && !gasStation.is_signed_off && completionPercentage >= 100;
            
            return new GasStationResponseDto
            {
                StationId = gasStation.station_id,
                OpportunityId = gasStation.opportunity_id,
                StationName = gasStation.station_name,
                AddressLine1 = gasStation.address_line_1,
                AddressLine2 = gasStation.address_line_2,
                City = gasStation.city,
                State = gasStation.state,
                PostalCode = gasStation.postal_code,
                Country = gasStation.country,
                Address = gasStation.Address,
                StationCode = gasStation.station_code,
                PocName = gasStation.poc_name,
                PocPhone = gasStation.poc_phone,
                PocEmail = gasStation.poc_email,
                NumberOfPumps = gasStation.number_of_pumps,
                NumberOfEmployees = gasStation.number_of_employees,
                StationTypeId = gasStation.station_type_id,
                Notes = gasStation.notes,
                IsComplete = completionPercentage >= 100, // FIXED: Use completion percentage
                CompletionPercentage = completionPercentage,
                IsSignedOff = gasStation.is_signed_off,
                SignedOffAt = gasStation.signed_off_at,
                CanSignOff = canSignOff, // FIXED: Role-based + completion percentage
                CanEdit = canEdit && !gasStation.is_signed_off, // FIXED: Role-based permission calculation
                CreatedBy = gasStation.created_by,
                CreatedByName = $"{gasStation.CreatedByUser?.first_name ?? ""} {gasStation.CreatedByUser?.last_name ?? ""}".Trim(),
                CreatedAt = gasStation.created_at,
                LastUpdated = gasStation.last_updated,
                OpportunityLeadName = gasStation.Opportunity?.Lead?.name ?? "",
                OpportunityOwnerName = gasStation.Opportunity?.owner_name ?? ""
            };
        }

        private async Task<GasStationListResponseDto> MapToGasStationListResponseDtoAsync(GasStation gasStation, int currentUserId, int currentUserRole)
        {
            var canEdit = await CanUserEditStationAsync(gasStation, currentUserId, currentUserRole);
            var completionPercentage = CalculateStationCompletionPercentage(gasStation);
            
            // FIXED: Use completion percentage instead of strict validation
            var canSignOff = canEdit && !gasStation.is_signed_off && completionPercentage >= 100;
            
            return new GasStationListResponseDto
            {
                StationId = gasStation.station_id,
                OpportunityId = gasStation.opportunity_id,
                StationName = gasStation.station_name,
                AddressLine1 = gasStation.address_line_1,
                AddressLine2 = gasStation.address_line_2,
                City = gasStation.city,
                State = gasStation.state,
                PostalCode = gasStation.postal_code,
                Country = gasStation.country,
                Address = gasStation.Address,
                StationCode = gasStation.station_code,
                PocName = gasStation.poc_name,
                PocPhone = gasStation.poc_phone,
                PocEmail = gasStation.poc_email,
                NumberOfPumps = gasStation.number_of_pumps,
                NumberOfEmployees = gasStation.number_of_employees,
                StationTypeId = gasStation.station_type_id,
                IsComplete = completionPercentage >= 100, // FIXED: Use completion percentage
                CompletionPercentage = completionPercentage,
                IsSignedOff = gasStation.is_signed_off,
                SignedOffAt = gasStation.signed_off_at,
                CanSignOff = canSignOff, // FIXED: Role-based + completion percentage
                CanEdit = canEdit && !gasStation.is_signed_off, // FIXED: Role-based permission calculation
                CreatedByName = $"{gasStation.CreatedByUser?.first_name ?? ""} {gasStation.CreatedByUser?.last_name ?? ""}".Trim(),
                CreatedAt = gasStation.created_at,
                OpportunityLeadName = gasStation.Opportunity?.Lead?.name ?? ""
            };
        }
    }
}