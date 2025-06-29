// REPLACE your entire GasStationService.cs with this complete version
using Microsoft.EntityFrameworkCore;
using gasopper_crm_server.Data;
using gasopper_crm_server.DTOs;
using gasopper_crm_server.Models;
using gasopper_crm_server.Helpers; // FIXED: Import from Helpers folder

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

                var stationCode = await StationCodeGenerator.GenerateUniqueStationCodeAsync(_context, opportunityId);
                if (string.IsNullOrEmpty(stationCode))
                {
                    Console.WriteLine($"[ERROR] Failed to generate station code for opportunity {opportunityId}");
                    return null;
                }

                Console.WriteLine($"[DEBUG] Generated station code: {stationCode} for opportunity {opportunityId}");

                var gasStation = new GasStation
                {
                    opportunity_id = opportunityId,
                    station_name = dto.StationName,
                    address = dto.Address,
                    station_code = stationCode,
                    poc_name = dto.PocName,
                    poc_phone = dto.PocPhone,
                    poc_email = dto.PocEmail,
                    number_of_pumps = dto.NumberOfPumps,
                    number_of_employees = dto.NumberOfEmployees,
                    station_type_id = dto.StationTypeId,
                    notes = dto.Notes,
                    created_by = currentUserId,
                    is_deleted = false
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

                return MapToGasStationResponseDto(gasStation);
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

                return gasStations.Select(MapToGasStationListResponseDto).ToList();
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

                return gasStations.Select(MapToGasStationListResponseDto).ToList();
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
                    .FirstOrDefaultAsync(gs => gs.station_id == id && !gs.is_deleted);

                if (gasStation == null)
                {
                    Console.WriteLine($"[DEBUG] Station {id} not found for update");
                    return null;
                }

                if (!await CanUserAccessOpportunityAsync(gasStation.opportunity_id, currentUserId, currentUserRole))
                {
                    Console.WriteLine($"[ERROR] User {currentUserId} cannot update station {id}");
                    return null;
                }

                if (dto.StationName != null)
                    gasStation.station_name = dto.StationName;
                if (dto.Address != null)
                    gasStation.address = dto.Address;
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
                    .FirstOrDefaultAsync(gs => gs.station_id == id && !gs.is_deleted);

                if (gasStation == null)
                {
                    Console.WriteLine($"[DEBUG] Station {id} not found for deletion");
                    return false;
                }

                if (!await CanUserAccessOpportunityAsync(gasStation.opportunity_id, currentUserId, currentUserRole))
                {
                    Console.WriteLine($"[ERROR] User {currentUserId} cannot delete station {id}");
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

                return gasStations.Select(MapToGasStationListResponseDto).ToList();
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

                return gasStations.Select(MapToGasStationListResponseDto).ToList();
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
                var completeStations = gasStations.Count(gs => IsStationComplete(gs));
                var incompleteStations = totalStations - completeStations;
                var completionRate = totalStations > 0 ? (double)completeStations / totalStations * 100 : 0;

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

                return new GasStationStatsDto
                {
                    TotalStations = totalStations,
                    CompleteStations = completeStations,
                    IncompleteStations = incompleteStations,
                    CompletionRate = Math.Round(completionRate, 1),
                    AverageStationsPerOpportunity = (int)Math.Round(averageStationsPerOpportunity),
                    StationTypeBreakdown = stationTypeBreakdown,
                    CompletionBreakdown = completionBreakdown
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
                var completeStations = opportunity.GasStations.Count(gs => IsStationComplete(gs));

                int newStatusId = (totalStations > 0 && completeStations == totalStations) ? 2 : 1;

                if (opportunity.status_id != newStatusId)
                {
                    opportunity.status_id = newStatusId;
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"[DEBUG] Opportunity {opportunityId} status updated to {newStatusId}");
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] UpdateOpportunityStatusFromStationsAsync failed: {ex.Message}");
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

        private static bool IsStationComplete(GasStation station)
        {
            var hasRequiredFields = !string.IsNullOrWhiteSpace(station.station_name)
                                   && !string.IsNullOrWhiteSpace(station.address)
                                   && !string.IsNullOrWhiteSpace(station.station_code);

            var hasOptionalFields = !string.IsNullOrWhiteSpace(station.poc_name)
                                   && !string.IsNullOrWhiteSpace(station.poc_phone)
                                   && !string.IsNullOrWhiteSpace(station.poc_email)
                                   && station.number_of_pumps.HasValue
                                   && station.number_of_employees.HasValue
                                   && station.station_type_id.HasValue;

            return hasRequiredFields && hasOptionalFields;
        }

        private static double CalculateStationCompletionPercentage(GasStation station)
        {
            var totalFields = 9;
            var filledFields = 0;

            if (!string.IsNullOrWhiteSpace(station.station_name)) filledFields++;
            if (!string.IsNullOrWhiteSpace(station.address)) filledFields++;
            if (!string.IsNullOrWhiteSpace(station.station_code)) filledFields++;
            if (!string.IsNullOrWhiteSpace(station.poc_name)) filledFields++;
            if (!string.IsNullOrWhiteSpace(station.poc_phone)) filledFields++;
            if (!string.IsNullOrWhiteSpace(station.poc_email)) filledFields++;
            if (station.number_of_pumps.HasValue) filledFields++;
            if (station.number_of_employees.HasValue) filledFields++;
            if (station.station_type_id.HasValue) filledFields++;

            return Math.Round((double)filledFields / totalFields * 100, 1);
        }

        private static GasStationResponseDto MapToGasStationResponseDto(GasStation gasStation)
        {
            return new GasStationResponseDto
            {
                StationId = gasStation.station_id,
                OpportunityId = gasStation.opportunity_id,
                StationName = gasStation.station_name,
                Address = gasStation.address,
                StationCode = gasStation.station_code,
                PocName = gasStation.poc_name,
                PocPhone = gasStation.poc_phone,
                PocEmail = gasStation.poc_email,
                NumberOfPumps = gasStation.number_of_pumps,
                NumberOfEmployees = gasStation.number_of_employees,
                StationTypeId = gasStation.station_type_id,
                Notes = gasStation.notes,
                IsComplete = IsStationComplete(gasStation),
                CompletionPercentage = CalculateStationCompletionPercentage(gasStation),
                CreatedBy = gasStation.created_by,
                CreatedByName = $"{gasStation.CreatedByUser?.first_name ?? ""} {gasStation.CreatedByUser?.last_name ?? ""}".Trim(),
                CreatedAt = gasStation.created_at,
                LastUpdated = gasStation.last_updated,
                OpportunityLeadName = gasStation.Opportunity?.Lead?.name ?? "",
                OpportunityOwnerName = gasStation.Opportunity?.owner_name ?? ""
            };
        }

        private static GasStationListResponseDto MapToGasStationListResponseDto(GasStation gasStation)
        {
            return new GasStationListResponseDto
            {
                StationId = gasStation.station_id,
                OpportunityId = gasStation.opportunity_id,
                StationName = gasStation.station_name,
                Address = gasStation.address,
                StationCode = gasStation.station_code,
                PocName = gasStation.poc_name,
                PocPhone = gasStation.poc_phone,
                PocEmail = gasStation.poc_email,
                NumberOfPumps = gasStation.number_of_pumps,
                NumberOfEmployees = gasStation.number_of_employees,
                StationTypeId = gasStation.station_type_id,
                IsComplete = IsStationComplete(gasStation),
                CompletionPercentage = CalculateStationCompletionPercentage(gasStation),
                CreatedByName = $"{gasStation.CreatedByUser?.first_name ?? ""} {gasStation.CreatedByUser?.last_name ?? ""}".Trim(),
                CreatedAt = gasStation.created_at,
                OpportunityLeadName = gasStation.Opportunity?.Lead?.name ?? ""
            };
        }
    }
}