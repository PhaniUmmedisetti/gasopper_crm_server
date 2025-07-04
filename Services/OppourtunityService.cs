using Microsoft.EntityFrameworkCore;
using gasopper_crm_server.Data;
using gasopper_crm_server.DTOs;
using gasopper_crm_server.Models;

namespace gasopper_crm_server.Services
{
    public interface IOpportunityService
    {
        Task<OpportunityResponseDto?> GetOpportunityByIdAsync(int opportunityId, int currentUserId, int currentUserRole);
        Task<List<OpportunityListDto>> GetOpportunitiesAsync(int currentUserId, int currentUserRole, bool includeDeleted = false);
        Task<OpportunityResponseDto?> UpdateOpportunityAsync(int opportunityId, UpdateOpportunityDto updateDto, int currentUserId, int currentUserRole);
        Task<OpportunityResponseDto?> UpdateOpportunityStatusAsync(int opportunityId, UpdateOpportunityStatusDto statusDto, int currentUserId, int currentUserRole);
        Task<OpportunityResponseDto?> AssignOpportunityAsync(int opportunityId, AssignOpportunityDto assignDto, int currentUserId, int currentUserRole);
        Task<List<OpportunityListDto>> GetMyOpportunitiesAsync(int currentUserId);
        Task<List<OpportunityListDto>> GetTeamOpportunitiesAsync(int managerId);
        Task<OpportunityStatsDto> GetOpportunityStatsAsync(int currentUserId, int currentUserRole);
        Task<List<OpportunityStatusDto>> GetOpportunityStatusesAsync();
        Task<bool> UpdateOpportunityStatusBasedOnStationsAsync(int opportunityId);
    }

    public class OpportunityService : IOpportunityService
    {
        private readonly GasopperDbContext _context;

        public OpportunityService(GasopperDbContext context)
        {
            _context = context;
        }

        public async Task<OpportunityResponseDto?> GetOpportunityByIdAsync(int opportunityId, int currentUserId, int currentUserRole)
        {
            try
            {
                var query = _context.Opportunities
                    .Include(o => o.Lead)
                    .Include(o => o.OpportunityStatus)
                    .Include(o => o.AssignedToUser)
                    .Include(o => o.CreatedByUser)
                    .Include(o => o.GasStations.Where(gs => !gs.is_deleted))
                        .ThenInclude(gs => gs.StationType)
                    .Include(o => o.GasStations.Where(gs => !gs.is_deleted))
                        .ThenInclude(gs => gs.CreatedByUser)
                    .Where(o => o.opportunity_id == opportunityId && !o.is_deleted)
                    .Where(o => o.Lead != null && !o.Lead.is_deleted); // Exclude opportunities with deleted leads

                // Apply role-based filtering
                if (currentUserRole == 3) // Salesperson - own only
                {
                    query = query.Where(o => o.assigned_to == currentUserId);
                }
                else if (currentUserRole == 2) // Manager - own + team
                {
                    var teamMemberIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId && u.is_active)
                        .Select(u => u.user_id)
                        .ToListAsync();

                    teamMemberIds.Add(currentUserId);
                    query = query.Where(o => teamMemberIds.Contains(o.assigned_to));
                }
                // Admin sees all

                var opportunity = await query.FirstOrDefaultAsync();

                if (opportunity == null)
                    return null;

                return MapToOpportunityResponseDto(opportunity);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<List<OpportunityListDto>> GetOpportunitiesAsync(int currentUserId, int currentUserRole, bool includeDeleted = false)
        {
            try
            {
                var query = _context.Opportunities
                    .Include(o => o.Lead)
                    .Include(o => o.OpportunityStatus)
                    .Include(o => o.AssignedToUser)
                    .Include(o => o.GasStations.Where(gs => !gs.is_deleted))
                    .Where(o => o.Lead != null && !o.Lead.is_deleted) // FIXED: Exclude opportunities with deleted leads
                    .AsQueryable();

                if (!includeDeleted)
                {
                    query = query.Where(o => !o.is_deleted);
                }

                // Apply role-based filtering
                if (currentUserRole == 3) // Salesperson - own only
                {
                    query = query.Where(o => o.assigned_to == currentUserId);
                }
                else if (currentUserRole == 2) // Manager - own + team
                {
                    var teamMemberIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId && u.is_active)
                        .Select(u => u.user_id)
                        .ToListAsync();

                    teamMemberIds.Add(currentUserId);
                    query = query.Where(o => teamMemberIds.Contains(o.assigned_to));
                }
                // Admin sees all

                var opportunities = await query
                    .OrderByDescending(o => o.last_updated)
                    .ToListAsync();

                var result = new List<OpportunityListDto>();
                foreach (var opportunity in opportunities)
                {
                    var dto = MapToOpportunityListDto(opportunity);
                    result.Add(dto);
                }

                return result;
            }
            catch (Exception)
            {
                return new List<OpportunityListDto>();
            }
        }

        public async Task<OpportunityResponseDto?> UpdateOpportunityAsync(int opportunityId, UpdateOpportunityDto updateDto, int currentUserId, int currentUserRole)
        {
            try
            {
                var opportunity = await _context.Opportunities.FindAsync(opportunityId);
                if (opportunity == null || !await CanAccessOpportunityAsync(opportunity, currentUserId, currentUserRole))
                    return null;

                // Update basic fields
                if (!string.IsNullOrEmpty(updateDto.OwnerName))
                    opportunity.owner_name = updateDto.OwnerName;

                // REMOVED: owner_address handling - no longer exists in model

                // Update split address fields
                if (!string.IsNullOrEmpty(updateDto.AddressLine1))
                    opportunity.address_line_1 = updateDto.AddressLine1;
                if (updateDto.AddressLine2 != null)
                    opportunity.address_line_2 = updateDto.AddressLine2;
                if (!string.IsNullOrEmpty(updateDto.City))
                    opportunity.city = updateDto.City;
                if (!string.IsNullOrEmpty(updateDto.State))
                    opportunity.state = updateDto.State;
                if (!string.IsNullOrEmpty(updateDto.PostalCode))
                    opportunity.postal_code = updateDto.PostalCode;
                if (!string.IsNullOrEmpty(updateDto.Country))
                    opportunity.country = updateDto.Country;

                // ADDED: Update actual stations count
                if (updateDto.ActualStations.HasValue)
                    opportunity.actual_stations = updateDto.ActualStations.Value;

                // Handle assignment changes (Admin/Manager only)
                if (updateDto.AssignedTo.HasValue && currentUserRole <= 2)
                {
                    if (currentUserRole == 2) // Manager can only assign to team members
                    {
                        var isTeamMember = await _context.Users
                            .AnyAsync(u => u.user_id == updateDto.AssignedTo.Value &&
                                          (u.manager_id == currentUserId || u.user_id == currentUserId));

                        if (isTeamMember)
                            opportunity.assigned_to = updateDto.AssignedTo.Value;
                    }
                    else if (currentUserRole == 1) // Admin can assign to anyone
                    {
                        opportunity.assigned_to = updateDto.AssignedTo.Value;
                    }
                }

                await _context.SaveChangesAsync();
                return await GetOpportunityByIdAsync(opportunityId, currentUserId, currentUserRole);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<OpportunityResponseDto?> UpdateOpportunityStatusAsync(int opportunityId, UpdateOpportunityStatusDto statusDto, int currentUserId, int currentUserRole)
        {
            try
            {
                var opportunity = await _context.Opportunities.FindAsync(opportunityId);
                if (opportunity == null || !await CanAccessOpportunityAsync(opportunity, currentUserId, currentUserRole))
                    return null;

                // Validate status (1=Active, 2=Complete)
                if (statusDto.StatusId < 1 || statusDto.StatusId > 2)
                    return null;

                opportunity.status_id = statusDto.StatusId;
                await _context.SaveChangesAsync();

                return await GetOpportunityByIdAsync(opportunityId, currentUserId, currentUserRole);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<OpportunityResponseDto?> AssignOpportunityAsync(int opportunityId, AssignOpportunityDto assignDto, int currentUserId, int currentUserRole)
        {
            try
            {
                var updateDto = new UpdateOpportunityDto
                {
                    AssignedTo = assignDto.AssignedTo
                };

                // Get existing opportunity to preserve current data
                var existing = await _context.Opportunities.FindAsync(opportunityId);
                if (existing == null)
                    return null;

                updateDto.OwnerName = existing.owner_name;
                updateDto.AddressLine1 = existing.address_line_1;
                updateDto.AddressLine2 = existing.address_line_2;
                updateDto.City = existing.city;
                updateDto.State = existing.state;
                updateDto.PostalCode = existing.postal_code;
                updateDto.Country = existing.country;
                updateDto.ActualStations = existing.actual_stations; // ADDED: Preserve actual stations

                return await UpdateOpportunityAsync(opportunityId, updateDto, currentUserId, currentUserRole);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<List<OpportunityListDto>> GetMyOpportunitiesAsync(int currentUserId)
        {
            try
            {
                var opportunities = await _context.Opportunities
                    .Include(o => o.Lead)
                    .Include(o => o.OpportunityStatus)
                    .Include(o => o.AssignedToUser)
                    .Include(o => o.GasStations.Where(gs => !gs.is_deleted))
                    .Where(o => o.assigned_to == currentUserId && !o.is_deleted)
                    .Where(o => o.Lead != null && !o.Lead.is_deleted) // Exclude opportunities with deleted leads
                    .OrderByDescending(o => o.last_updated)
                    .ToListAsync();

                var result = new List<OpportunityListDto>();
                foreach (var opportunity in opportunities)
                {
                    var dto = MapToOpportunityListDto(opportunity);
                    result.Add(dto);
                }

                return result;
            }
            catch (Exception)
            {
                return new List<OpportunityListDto>();
            }
        }

        public async Task<List<OpportunityListDto>> GetTeamOpportunitiesAsync(int managerId)
        {
            try
            {
                var teamMemberIds = await _context.Users
                    .Where(u => u.manager_id == managerId && u.is_active)
                    .Select(u => u.user_id)
                    .ToListAsync();

                teamMemberIds.Add(managerId);

                var opportunities = await _context.Opportunities
                    .Include(o => o.Lead)
                    .Include(o => o.OpportunityStatus)
                    .Include(o => o.AssignedToUser)
                    .Include(o => o.GasStations.Where(gs => !gs.is_deleted))
                    .Where(o => teamMemberIds.Contains(o.assigned_to) && !o.is_deleted)
                    .Where(o => o.Lead != null && !o.Lead.is_deleted) // Exclude opportunities with deleted leads
                    .OrderByDescending(o => o.last_updated)
                    .ToListAsync();

                var result = new List<OpportunityListDto>();
                foreach (var opportunity in opportunities)
                {
                    var dto = MapToOpportunityListDto(opportunity);
                    result.Add(dto);
                }

                return result;
            }
            catch (Exception)
            {
                return new List<OpportunityListDto>();
            }
        }

        public async Task<OpportunityStatsDto> GetOpportunityStatsAsync(int currentUserId, int currentUserRole)
        {
            try
            {
                var query = _context.Opportunities
                    .Include(o => o.Lead)
                    .Include(o => o.GasStations.Where(gs => !gs.is_deleted))
                    .Where(o => !o.is_deleted)
                    .Where(o => o.Lead != null && !o.Lead.is_deleted); // FIXED: Exclude opportunities with deleted leads

                // Apply role-based filtering
                if (currentUserRole == 3) // Salesperson - own only
                {
                    query = query.Where(o => o.assigned_to == currentUserId);
                }
                else if (currentUserRole == 2) // Manager - own + team
                {
                    var teamMemberIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId && u.is_active)
                        .Select(u => u.user_id)
                        .ToListAsync();

                    teamMemberIds.Add(currentUserId);
                    query = query.Where(o => teamMemberIds.Contains(o.assigned_to));
                }
                // Admin sees all

                var opportunities = await query.ToListAsync();
                var totalOpportunities = opportunities.Count;

                var activeOpportunities = 0;
                var completeOpportunities = 0;

                foreach (var opportunity in opportunities)
                {
                    var stations = opportunity.GasStations.ToList();

                    if (!stations.Any())
                    {
                        activeOpportunities++;
                    }
                    else if (stations.All(IsStationComplete))
                    {
                        completeOpportunities++;
                    }
                    else
                    {
                        activeOpportunities++;
                    }
                }

                var completionRate = totalOpportunities > 0 ? Math.Round((double)completeOpportunities / totalOpportunities * 100, 1) : 0.0;

                var allStations = opportunities.SelectMany(o => o.GasStations).ToList();
                var totalStations = allStations.Count;
                var completeStations = allStations.Count(IsStationComplete);
                var stationCompletionRate = totalStations > 0 ? Math.Round((double)completeStations / totalStations * 100, 1) : 0.0;

                var avgStationsPerOpp = totalOpportunities > 0 ? Math.Round((double)totalStations / totalOpportunities, 1) : 0.0;

                var completedOpps = opportunities.Where(o => o.GasStations.Any() && o.GasStations.All(IsStationComplete)).ToList();
                var avgDaysToComplete = 0;
                if (completedOpps.Any())
                {
                    var totalDays = completedOpps.Sum(o => (DateTime.UtcNow - o.created_at).Days);
                    avgDaysToComplete = totalDays / completedOpps.Count;
                }

                var statusBreakdown = new Dictionary<string, int>
                {
                    { "Active", activeOpportunities },
                    { "Complete", completeOpportunities }
                };

                return new OpportunityStatsDto
                {
                    TotalOpportunities = totalOpportunities,
                    ActiveOpportunities = activeOpportunities,
                    CompleteOpportunities = completeOpportunities,
                    CompletionRate = completionRate,
                    TotalStations = totalStations,
                    CompleteStations = completeStations,
                    StationCompletionRate = stationCompletionRate,
                    AverageStationsPerOpportunity = avgStationsPerOpp,
                    AverageDaysToComplete = avgDaysToComplete,
                    StatusBreakdown = statusBreakdown
                };
            }
            catch (Exception)
            {
                return new OpportunityStatsDto
                {
                    TotalOpportunities = 0,
                    ActiveOpportunities = 0,
                    CompleteOpportunities = 0,
                    CompletionRate = 0.0,
                    TotalStations = 0,
                    CompleteStations = 0,
                    StationCompletionRate = 0.0,
                    AverageStationsPerOpportunity = 0.0,
                    AverageDaysToComplete = 0,
                    StatusBreakdown = new Dictionary<string, int>()
                };
            }
        }

        public async Task<List<OpportunityStatusDto>> GetOpportunityStatusesAsync()
        {
            try
            {
                return await _context.OpportunityStatuses
                    .Select(s => new OpportunityStatusDto
                    {
                        StatusId = s.status_id,
                        StatusName = s.status_name,
                        Description = s.description ?? ""
                    })
                    .ToListAsync();
            }
            catch (Exception)
            {
                return new List<OpportunityStatusDto>();
            }
        }

        public async Task<bool> UpdateOpportunityStatusBasedOnStationsAsync(int opportunityId)
        {
            try
            {
                var opportunity = await _context.Opportunities
                    .Include(o => o.GasStations.Where(gs => !gs.is_deleted))
                    .FirstOrDefaultAsync(o => o.opportunity_id == opportunityId);

                if (opportunity == null)
                    return false;

                var stations = opportunity.GasStations.ToList();

                if (!stations.Any())
                {
                    opportunity.status_id = 1; // Active
                }
                else if (stations.All(IsStationComplete))
                {
                    opportunity.status_id = 2; // Complete
                }
                else
                {
                    opportunity.status_id = 1; // Active
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task<bool> CanAccessOpportunityAsync(Opportunity opportunity, int currentUserId, int currentUserRole)
        {
            try
            {
                if (currentUserRole == 1) // Admin
                    return true;

                if (currentUserRole == 3) // Salesperson
                    return opportunity.assigned_to == currentUserId;

                if (currentUserRole == 2) // Manager
                {
                    if (opportunity.assigned_to == currentUserId)
                        return true;

                    var assignedUser = await _context.Users.FindAsync(opportunity.assigned_to);
                    return assignedUser?.manager_id == currentUserId;
                }

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsStationComplete(GasStation station)
        {
            // FIXED: Updated to use new address fields
            var hasRequiredFields = !string.IsNullOrWhiteSpace(station.station_name)
                                   && !string.IsNullOrWhiteSpace(station.address_line_1)
                                   && !string.IsNullOrWhiteSpace(station.city)
                                   && !string.IsNullOrWhiteSpace(station.state)
                                   && !string.IsNullOrWhiteSpace(station.postal_code);

            var hasOptionalFields = !string.IsNullOrWhiteSpace(station.poc_name)
                                   && !string.IsNullOrWhiteSpace(station.poc_phone)
                                   && !string.IsNullOrWhiteSpace(station.poc_email)
                                   && station.number_of_pumps.HasValue
                                   && station.number_of_employees.HasValue
                                   && station.station_type_id.HasValue;

            return hasRequiredFields && hasOptionalFields;
        }

        private static List<string> GetMissingFields(GasStation station)
        {
            var missing = new List<string>();

            if (string.IsNullOrWhiteSpace(station.station_name))
                missing.Add("Station Name");

            // FIXED: Updated to check new address fields
            if (string.IsNullOrWhiteSpace(station.address_line_1))
                missing.Add("Address Line 1");
            if (string.IsNullOrWhiteSpace(station.city))
                missing.Add("City");
            if (string.IsNullOrWhiteSpace(station.state))
                missing.Add("State");
            if (string.IsNullOrWhiteSpace(station.postal_code))
                missing.Add("Postal Code");

            if (string.IsNullOrWhiteSpace(station.poc_name))
                missing.Add("POC Name");
            if (string.IsNullOrWhiteSpace(station.poc_phone))
                missing.Add("POC Phone");
            if (string.IsNullOrWhiteSpace(station.poc_email))
                missing.Add("POC Email");
            if (!station.number_of_pumps.HasValue)
                missing.Add("Number of Pumps");
            if (!station.number_of_employees.HasValue)
                missing.Add("Number of Employees");
            if (!station.station_type_id.HasValue)
                missing.Add("Station Type");

            return missing;
        }

        private static double CalculateStationCompletionPercentage(GasStation station)
        {
            var totalFields = 10; // Total number of fields we check
            var completedFields = 0;

            // Required fields
            if (!string.IsNullOrWhiteSpace(station.station_name)) completedFields++;
            if (!string.IsNullOrWhiteSpace(station.address_line_1)) completedFields++;
            if (!string.IsNullOrWhiteSpace(station.city)) completedFields++;
            if (!string.IsNullOrWhiteSpace(station.state)) completedFields++;
            if (!string.IsNullOrWhiteSpace(station.postal_code)) completedFields++;

            // Optional fields
            if (!string.IsNullOrWhiteSpace(station.poc_name)) completedFields++;
            if (!string.IsNullOrWhiteSpace(station.poc_phone)) completedFields++;
            if (!string.IsNullOrWhiteSpace(station.poc_email)) completedFields++;
            if (station.number_of_pumps.HasValue) completedFields++;
            if (station.number_of_employees.HasValue) completedFields++;

            return Math.Round((double)completedFields / totalFields * 100, 1);
        }

        private OpportunityResponseDto MapToOpportunityResponseDto(Opportunity opportunity)
        {
            var stations = opportunity.GasStations.ToList();
            var completeStations = stations.Count(IsStationComplete);
            var incompleteStations = stations.Count - completeStations;
            var completionPercentage = stations.Any() ? Math.Round((double)completeStations / stations.Count * 100, 1) : 0.0;

            var stationDtos = stations.Select(s =>
            {
                var missingFields = GetMissingFields(s);
                var isComplete = IsStationComplete(s);
                var stationCompletionPercentage = CalculateStationCompletionPercentage(s);

                return new OpportunityStationDto
                {
                    StationId = s.station_id,
                    StationName = s.station_name,
                    Address = s.Address,
                    StationCode = s.station_code,
                    PocName = s.poc_name,
                    PocPhone = s.poc_phone,
                    PocEmail = s.poc_email,
                    NumberOfPumps = s.number_of_pumps,
                    NumberOfEmployees = s.number_of_employees,
                    StationTypeName = s.StationType?.type_name,
                    IsComplete = isComplete,
                    CompletionPercentage = stationCompletionPercentage,
                    MissingFields = missingFields,
                    CreatedAt = s.created_at,
                    StatusId = s.station_type_id ?? 0,
                    StatusName = s.StationType?.type_name ?? "",
                    Description = s.notes ?? ""
                };
            }).OrderBy(s => s.StationName).ToList();

            // Create combined address for legacy support
            var combinedAddress = "";
            if (!string.IsNullOrEmpty(opportunity.address_line_1))
            {
                combinedAddress = opportunity.address_line_1;
                if (!string.IsNullOrEmpty(opportunity.address_line_2))
                    combinedAddress += ", " + opportunity.address_line_2;
                if (!string.IsNullOrEmpty(opportunity.city))
                    combinedAddress += ", " + opportunity.city;
                if (!string.IsNullOrEmpty(opportunity.state))
                    combinedAddress += ", " + opportunity.state;
                if (!string.IsNullOrEmpty(opportunity.postal_code))
                    combinedAddress += " " + opportunity.postal_code;
                if (!string.IsNullOrEmpty(opportunity.country) && opportunity.country != "United States")
                    combinedAddress += ", " + opportunity.country;
            }

            return new OpportunityResponseDto
            {
                OpportunityId = opportunity.opportunity_id,
                LeadId = opportunity.lead_id,
                LeadName = opportunity.Lead?.name ?? "",
                LeadEmail = opportunity.Lead?.email ?? "",
                LeadPhone = opportunity.Lead?.phone_number ?? "",
                OwnerName = opportunity.owner_name,
                AddressLine1 = opportunity.address_line_1 ?? "",
                AddressLine2 = opportunity.address_line_2,
                City = opportunity.city ?? "",
                State = opportunity.state ?? "",
                PostalCode = opportunity.postal_code ?? "",
                Country = opportunity.country ?? "United States",
                OwnerAddress = combinedAddress,
                ActualStations = opportunity.actual_stations,
                StatusId = opportunity.status_id,
                StatusName = opportunity.OpportunityStatus?.status_name ?? "",
                StatusDescription = opportunity.OpportunityStatus?.description ?? "",
                AssignedTo = opportunity.assigned_to,
                AssignedToName = $"{opportunity.AssignedToUser?.first_name ?? ""} {opportunity.AssignedToUser?.last_name ?? ""}".Trim(),
                CreatedBy = opportunity.created_by,
                CreatedByName = $"{opportunity.CreatedByUser?.first_name ?? ""} {opportunity.CreatedByUser?.last_name ?? ""}".Trim(),
                TotalStations = stations.Count,
                CompleteStations = completeStations,
                IncompleteStations = incompleteStations,
                CompletionPercentage = completionPercentage,
                Stations = stationDtos,
                CreatedAt = opportunity.created_at,
                LastUpdated = opportunity.last_updated,
                IsDeleted = opportunity.is_deleted
            };
        }

        private OpportunityListDto MapToOpportunityListDto(Opportunity opportunity)
        {
            var stations = opportunity.GasStations.ToList();
            var completeStations = stations.Count(IsStationComplete);
            var completionPercentage = stations.Any() ? Math.Round((double)completeStations / stations.Count * 100, 1) : 0.0;

            return new OpportunityListDto
            {
                OpportunityId = opportunity.opportunity_id,
                LeadName = opportunity.Lead?.name ?? "",
                OwnerName = opportunity.owner_name,
                StatusId = opportunity.status_id,
                StatusName = opportunity.OpportunityStatus?.status_name ?? "",
                AssignedToName = $"{opportunity.AssignedToUser?.first_name ?? ""} {opportunity.AssignedToUser?.last_name ?? ""}".Trim(),
                ActualStations = opportunity.actual_stations,
                TotalStations = stations.Count,
                CompleteStations = completeStations,
                CompletionPercentage = completionPercentage,
                CreatedAt = opportunity.created_at,
                LastUpdated = opportunity.last_updated
            };
        }
    }
}