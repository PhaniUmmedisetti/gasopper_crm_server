using Microsoft.EntityFrameworkCore;
using gasopper_crm_server.Data;
using gasopper_crm_server.DTOs;
using gasopper_crm_server.Models;

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
                // Check if user can access the opportunity
                if (!await CanUserAccessOpportunityAsync(opportunityId, currentUserId, currentUserRole))
                    return null;

                var gasStation = new GasStation
                {
                    opportunity_id = opportunityId,
                    station_name = dto.StationName,
                    address = dto.Address,
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

                // Auto-update opportunity status based on station completion
                await UpdateOpportunityStatusFromStationsAsync(opportunityId, currentUserId, currentUserRole);

                return await GetGasStationByIdAsync(gasStation.station_id, currentUserId, currentUserRole);
            }
            catch (Exception)
            {
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
                    return null;

                // Check if user can access this station's opportunity
                if (!await CanUserAccessOpportunityAsync(gasStation.opportunity_id, currentUserId, currentUserRole))
                    return null;

                return MapToGasStationResponseDto(gasStation);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<List<GasStationListResponseDto>> GetGasStationsByOpportunityAsync(int opportunityId, int currentUserId, int currentUserRole)
        {
            try
            {
                // Check if user can access the opportunity
                if (!await CanUserAccessOpportunityAsync(opportunityId, currentUserId, currentUserRole))
                    return new List<GasStationListResponseDto>();

                var gasStations = await _context.GasStations
                    .Include(gs => gs.StationType)
                    .Include(gs => gs.CreatedByUser)
                    .Include(gs => gs.Opportunity)
                        .ThenInclude(o => o.Lead)
                    .Where(gs => gs.opportunity_id == opportunityId && !gs.is_deleted)
                    .OrderBy(gs => gs.station_name)
                    .ToListAsync();

                return gasStations.Select(MapToGasStationListResponseDto).ToList();
            }
            catch (Exception)
            {
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

                // Apply role-based filtering through opportunity access
                query = await ApplyRoleBasedFilteringAsync(query, currentUserId, currentUserRole);

                var gasStations = await query
                    .OrderBy(gs => gs.station_name)
                    .ToListAsync();

                return gasStations.Select(MapToGasStationListResponseDto).ToList();
            }
            catch (Exception)
            {
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
                    return null;

                // Check if user can access this station's opportunity
                if (!await CanUserAccessOpportunityAsync(gasStation.opportunity_id, currentUserId, currentUserRole))
                    return null;

                // Update only provided fields
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

                // Auto-update opportunity status based on station completion
                await UpdateOpportunityStatusFromStationsAsync(gasStation.opportunity_id, currentUserId, currentUserRole);

                return await GetGasStationByIdAsync(id, currentUserId, currentUserRole);
            }
            catch (Exception)
            {
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
                    return false;

                // Check if user can access this station's opportunity
                if (!await CanUserAccessOpportunityAsync(gasStation.opportunity_id, currentUserId, currentUserRole))
                    return false;

                gasStation.is_deleted = true;
                await _context.SaveChangesAsync();

                // Auto-update opportunity status based on remaining stations
                await UpdateOpportunityStatusFromStationsAsync(gasStation.opportunity_id, currentUserId, currentUserRole);

                return true;
            }
            catch (Exception)
            {
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
                    .OrderBy(gs => gs.station_name)
                    .ToListAsync();

                return gasStations.Select(MapToGasStationListResponseDto).ToList();
            }
            catch (Exception)
            {
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
                    .OrderBy(gs => gs.station_name)
                    .ToListAsync();

                return gasStations.Select(MapToGasStationListResponseDto).ToList();
            }
            catch (Exception)
            {
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

                // Apply role-based filtering
                query = await ApplyRoleBasedFilteringAsync(query, currentUserId, currentUserRole);

                var gasStations = await query.ToListAsync();

                var totalStations = gasStations.Count;
                var completeStations = gasStations.Count(gs => IsStationComplete(gs));
                var incompleteStations = totalStations - completeStations;
                var completionRate = totalStations > 0 ? (double)completeStations / totalStations * 100 : 0;

                // Calculate average stations per opportunity
                var opportunityIds = gasStations.Select(gs => gs.opportunity_id).Distinct().ToList();
                var averageStationsPerOpportunity = opportunityIds.Count > 0 ? (double)totalStations / opportunityIds.Count : 0;

                // Station type breakdown
                var stationTypeBreakdown = gasStations
                    .Where(gs => gs.StationType != null)
                    .GroupBy(gs => gs.StationType!.type_name)
                    .ToDictionary(g => g.Key, g => g.Count());

                // Completion breakdown
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
            catch (Exception)
            {
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
            catch (Exception)
            {
                return new List<StationTypeDto>();
            }
        }

        public async Task<bool> UpdateOpportunityStatusFromStationsAsync(int opportunityId, int currentUserId, int currentUserRole)
        {
            try
            {
                // Check if user can access the opportunity
                if (!await CanUserAccessOpportunityAsync(opportunityId, currentUserId, currentUserRole))
                    return false;

                var opportunity = await _context.Opportunities
                    .Include(o => o.GasStations.Where(gs => !gs.is_deleted))
                    .FirstOrDefaultAsync(o => o.opportunity_id == opportunityId && !o.is_deleted);

                if (opportunity == null)
                    return false;

                var totalStations = opportunity.GasStations.Count;
                var completeStations = opportunity.GasStations.Count(gs => IsStationComplete(gs));

                // Update opportunity status based on station completion
                // Status 1 = Active, Status 2 = Complete
                int newStatusId = (totalStations > 0 && completeStations == totalStations) ? 2 : 1;

                if (opportunity.status_id != newStatusId)
                {
                    opportunity.status_id = newStatusId;
                    await _context.SaveChangesAsync();
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // PRIVATE HELPER METHODS

        private async Task<bool> CanUserAccessOpportunityAsync(int opportunityId, int userId, int userRole)
        {
            try
            {
                var opportunity = await _context.Opportunities
                    .FirstOrDefaultAsync(o => o.opportunity_id == opportunityId && !o.is_deleted);

                if (opportunity == null)
                    return false;

                // Apply same role-based logic as OpportunityService
                if (userRole == 3) // Salesperson - own only
                    return opportunity.assigned_to == userId;

                if (userRole == 2) // Manager - own + team
                {
                    var teamMemberIds = await _context.Users
                        .Where(u => u.manager_id == userId && u.is_active)
                        .Select(u => u.user_id)
                        .ToListAsync();

                    teamMemberIds.Add(userId);
                    return teamMemberIds.Contains(opportunity.assigned_to);
                }

                return true; // Admin sees all
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task<IQueryable<GasStation>> ApplyRoleBasedFilteringAsync(IQueryable<GasStation> query, int currentUserId, int currentUserRole)
        {
            if (currentUserRole == 3) // Salesperson - own opportunities only
            {
                return query.Where(gs => gs.Opportunity.assigned_to == currentUserId);
            }
            else if (currentUserRole == 2) // Manager - own + team opportunities
            {
                var teamMemberIds = await _context.Users
                    .Where(u => u.manager_id == currentUserId && u.is_active)
                    .Select(u => u.user_id)
                    .ToListAsync();

                teamMemberIds.Add(currentUserId);
                return query.Where(gs => teamMemberIds.Contains(gs.Opportunity.assigned_to));
            }

            return query; // Admin sees all
        }

        private static bool IsStationComplete(GasStation station)
        {
            // Required fields: station_name, address (always required)
            var hasRequiredFields = !string.IsNullOrWhiteSpace(station.station_name)
                                   && !string.IsNullOrWhiteSpace(station.address);

            // Optional fields for completion: poc_name, poc_phone, poc_email, number_of_pumps, number_of_employees, station_type_id
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
            var totalFields = 8; // station_name, address, poc_name, poc_phone, poc_email, number_of_pumps, number_of_employees, station_type_id
            var filledFields = 0;

            if (!string.IsNullOrWhiteSpace(station.station_name)) filledFields++;
            if (!string.IsNullOrWhiteSpace(station.address)) filledFields++;
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
                PocName = gasStation.poc_name,
                PocPhone = gasStation.poc_phone,
                PocEmail = gasStation.poc_email,
                NumberOfPumps = gasStation.number_of_pumps,
                NumberOfEmployees = gasStation.number_of_employees,
                StationTypeId = gasStation.station_type_id,
                StationTypeName = gasStation.StationType?.type_name,
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
                PocName = gasStation.poc_name,
                StationTypeName = gasStation.StationType?.type_name,
                IsComplete = IsStationComplete(gasStation),
                CompletionPercentage = CalculateStationCompletionPercentage(gasStation),
                CreatedByName = $"{gasStation.CreatedByUser?.first_name ?? ""} {gasStation.CreatedByUser?.last_name ?? ""}".Trim(),
                CreatedAt = gasStation.created_at,
                OpportunityLeadName = gasStation.Opportunity?.Lead?.name ?? ""
            };
        }
    }
}