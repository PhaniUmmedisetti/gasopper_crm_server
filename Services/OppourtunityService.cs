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
                // FIXED: Build base query first
                var query = _context.Opportunities
                    .Include(o => o.Lead)
                    .Include(o => o.OpportunityStatus)
                    .Include(o => o.AssignedToUser)
                    .Include(o => o.CreatedByUser)
                    .Include(o => o.GasStations.Where(gs => !gs.is_deleted))
                        .ThenInclude(gs => gs.StationType)
                    .Where(o => o.opportunity_id == opportunityId && !o.is_deleted);

                // FIXED: Apply role-based filtering with MATERIALIZED team member IDs
                if (currentUserRole == 3) // Salesperson can only see their own opportunities
                {
                    query = query.Where(o => o.assigned_to == currentUserId);
                }
                else if (currentUserRole == 2) // Manager can see own + team opportunities
                {
                    // FIXED: Materialize team member IDs first
                    var teamMemberIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId && u.is_active)
                        .Select(u => u.user_id)
                        .ToListAsync();

                    teamMemberIds.Add(currentUserId); // Add manager's own ID

                    query = query.Where(o => teamMemberIds.Contains(o.assigned_to));
                }
                // Admin can see all opportunities (no additional filtering)

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

        // FIXED: This MUST return empty list for salespeople with no opportunities, NOT throw 401
        public async Task<List<OpportunityListDto>> GetOpportunitiesAsync(int currentUserId, int currentUserRole, bool includeDeleted = false)
        {
            try
            {
                // FIXED: Build base query
                var query = _context.Opportunities
                    .Include(o => o.Lead)
                    .Include(o => o.OpportunityStatus)
                    .Include(o => o.AssignedToUser)
                    .Include(o => o.GasStations.Where(gs => !gs.is_deleted))
                    .AsQueryable();

                if (!includeDeleted)
                {
                    query = query.Where(o => !o.is_deleted);
                }

                // FIXED: Apply role-based filtering with MATERIALIZED team member IDs
                if (currentUserRole == 3) // Salesperson can only see their own opportunities
                {
                    query = query.Where(o => o.assigned_to == currentUserId);
                }
                else if (currentUserRole == 2) // Manager can see own + team opportunities
                {
                    // FIXED: Materialize team member IDs first
                    var teamMemberIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId && u.is_active)
                        .Select(u => u.user_id)
                        .ToListAsync();

                    teamMemberIds.Add(currentUserId); // Add manager's own ID

                    query = query.Where(o => teamMemberIds.Contains(o.assigned_to));
                }
                // Admin can see all opportunities (no additional filtering)

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
                // FIXED: Return empty list on error, not null
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
                opportunity.owner_name = updateDto.OwnerName;
                opportunity.owner_address = updateDto.OwnerAddress;

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
                    OwnerName = "", // Will be filled from existing data
                    OwnerAddress = "", // Will be filled from existing data
                    AssignedTo = assignDto.AssignedTo
                };

                // Get existing opportunity to preserve current data
                var existing = await _context.Opportunities.FindAsync(opportunityId);
                if (existing == null)
                    return null;

                updateDto.OwnerName = existing.owner_name;
                updateDto.OwnerAddress = existing.owner_address;

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
                // FIXED: Materialize team member IDs first
                var teamMemberIds = await _context.Users
                    .Where(u => u.manager_id == managerId && u.is_active)
                    .Select(u => u.user_id)
                    .ToListAsync();

                teamMemberIds.Add(managerId); // Include manager's own opportunities

                var opportunities = await _context.Opportunities
                    .Include(o => o.Lead)
                    .Include(o => o.OpportunityStatus)
                    .Include(o => o.AssignedToUser)
                    .Include(o => o.GasStations.Where(gs => !gs.is_deleted))
                    .Where(o => teamMemberIds.Contains(o.assigned_to) && !o.is_deleted)
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

        // FIXED: Opportunity stats that work for all roles
        public async Task<OpportunityStatsDto> GetOpportunityStatsAsync(int currentUserId, int currentUserRole)
        {
            try
            {
                // FIXED: Build base query with materialized team member IDs
                var query = _context.Opportunities
                    .Include(o => o.GasStations.Where(gs => !gs.is_deleted))
                    .Where(o => !o.is_deleted);

                // Apply role-based filtering
                if (currentUserRole == 3) // Salesperson can only see their own opportunities
                {
                    query = query.Where(o => o.assigned_to == currentUserId);
                }
                else if (currentUserRole == 2) // Manager can see own + team opportunities
                {
                    // FIXED: Materialize team member IDs first
                    var teamMemberIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId && u.is_active)
                        .Select(u => u.user_id)
                        .ToListAsync();

                    teamMemberIds.Add(currentUserId); // Add manager's own ID

                    query = query.Where(o => teamMemberIds.Contains(o.assigned_to));
                }
                // Admin can see all opportunities (no additional filtering)

                var totalOpportunities = await query.CountAsync();
                var activeOpportunities = await query.CountAsync(o => o.status_id == 1); // Active
                var completeOpportunities = await query.CountAsync(o => o.status_id == 2); // Complete

                // Completion rate
                var completionRate = totalOpportunities > 0 ? Math.Round((double)completeOpportunities / totalOpportunities * 100, 1) : 0.0;

                // Station statistics
                var allStations = await query
                    .SelectMany(o => o.GasStations.Where(gs => !gs.is_deleted))
                    .ToListAsync();

                var totalStations = allStations.Count;
                var completeStations = allStations.Count(s => IsStationComplete(s));
                var stationCompletionRate = totalStations > 0 ? Math.Round((double)completeStations / totalStations * 100, 1) : 0.0;

                // Average stations per opportunity
                var avgStationsPerOpp = totalOpportunities > 0 ? Math.Round((double)totalStations / totalOpportunities, 1) : 0.0;

                // Average days to complete
                var completeOppWithDates = await query
                    .Where(o => o.status_id == 2)
                    .Select(o => new { o.created_at, o.last_updated })
                    .ToListAsync();

                var avgDaysToComplete = 0;
                if (completeOppWithDates.Any())
                {
                    var totalDays = completeOppWithDates
                        .Sum(x => (x.last_updated - x.created_at).Days);
                    avgDaysToComplete = totalDays / completeOppWithDates.Count;
                }

                // Status breakdown
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
                // FIXED: Return empty stats on error
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
                    StatusBreakdown = new Dictionary<string, int> { { "Active", 0 }, { "Complete", 0 } }
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

        // Auto-update opportunity status based on gas station completion
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
                
                // If no stations, keep as Active
                if (!stations.Any())
                {
                    opportunity.status_id = 1; // Active
                }
                // If all stations are complete, mark as Complete
                else if (stations.All(IsStationComplete))
                {
                    opportunity.status_id = 2; // Complete
                }
                // If any station is incomplete, mark as Active
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

        // FIXED: Async method for access checking
        private async Task<bool> CanAccessOpportunityAsync(Opportunity opportunity, int currentUserId, int currentUserRole)
        {
            try
            {
                if (currentUserRole == 1) // Admin can access all
                    return true;

                if (currentUserRole == 3) // Salesperson can only access their own
                    return opportunity.assigned_to == currentUserId;

                if (currentUserRole == 2) // Manager can access own + team
                {
                    if (opportunity.assigned_to == currentUserId)
                        return true;

                    // Check if assigned user is in manager's team
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

        // Check if a gas station is complete (all required fields filled)
        private static bool IsStationComplete(GasStation station)
        {
            return !string.IsNullOrEmpty(station.poc_name) &&
                   !string.IsNullOrEmpty(station.poc_phone) &&
                   station.number_of_pumps.HasValue &&
                   station.number_of_employees.HasValue &&
                   station.station_type_id.HasValue;
        }

        // Get missing fields for a gas station
        private static List<string> GetMissingFields(GasStation station)
        {
            var missing = new List<string>();

            if (string.IsNullOrEmpty(station.poc_name))
                missing.Add("POC Name");
            if (string.IsNullOrEmpty(station.poc_phone))
                missing.Add("POC Phone");
            if (!station.number_of_pumps.HasValue)
                missing.Add("Number of Pumps");
            if (!station.number_of_employees.HasValue)
                missing.Add("Number of Employees");
            if (!station.station_type_id.HasValue)
                missing.Add("Station Type");

            return missing;
        }

        private OpportunityResponseDto MapToOpportunityResponseDto(Opportunity opportunity)
        {
            var stations = opportunity.GasStations.ToList();
            var completeStations = stations.Count(IsStationComplete);
            var incompleteStations = stations.Count - completeStations;
            var completionPercentage = stations.Any() ? Math.Round((double)completeStations / stations.Count * 100, 1) : 0.0;

            var stationDtos = stations.Select(s => new OpportunityStationDto
            {
                StationId = s.station_id,
                StationName = s.station_name,
                Address = s.address,
                PocName = s.poc_name,
                PocPhone = s.poc_phone,
                PocEmail = s.poc_email,
                NumberOfPumps = s.number_of_pumps,
                NumberOfEmployees = s.number_of_employees,
                StationTypeName = s.StationType?.type_name,
                IsComplete = IsStationComplete(s),
                MissingFields = GetMissingFields(s),
                CreatedAt = s.created_at
            }).ToList();

            return new OpportunityResponseDto
            {
                OpportunityId = opportunity.opportunity_id,
                LeadId = opportunity.lead_id,
                LeadName = opportunity.Lead?.name ?? "",
                LeadEmail = opportunity.Lead?.email ?? "",
                LeadPhone = opportunity.Lead?.phone_number ?? "",
                OwnerName = opportunity.owner_name,
                OwnerAddress = opportunity.owner_address,
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
                LastUpdated = opportunity.last_updated
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
                StatusName = opportunity.OpportunityStatus?.status_name ?? "",
                AssignedToName = $"{opportunity.AssignedToUser?.first_name ?? ""} {opportunity.AssignedToUser?.last_name ?? ""}".Trim(),
                TotalStations = stations.Count,
                CompleteStations = completeStations,
                CompletionPercentage = completionPercentage,
                CreatedAt = opportunity.created_at,
                LastUpdated = opportunity.last_updated
            };
        }
    }
}