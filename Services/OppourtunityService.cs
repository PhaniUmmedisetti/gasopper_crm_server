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

        // NEW: Simple pagination methods
        Task<PaginatedOpportunitiesResponseDto> GetOpportunitiesPaginatedAsync(int currentUserId, int currentUserRole, int page, int pageSize, bool includeDeleted = false);
        Task<PaginatedOpportunitiesResponseDto> GetMyOpportunitiesPaginatedAsync(int currentUserId, int page, int pageSize);
        Task<PaginatedOpportunitiesResponseDto> GetTeamOpportunitiesPaginatedAsync(int managerId, int page, int pageSize);
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
                    .Where(o => o.Lead != null && !o.Lead.is_deleted);

                if (currentUserRole == 3)
                {
                    query = query.Where(o => o.assigned_to == currentUserId);
                }
                else if (currentUserRole == 2)
                {
                    var teamMemberIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId && u.is_active)
                        .Select(u => u.user_id)
                        .ToListAsync();

                    teamMemberIds.Add(currentUserId);
                    query = query.Where(o => teamMemberIds.Contains(o.assigned_to));
                }

                var opportunity = await query.FirstOrDefaultAsync();
                if (opportunity == null) return null;

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
                    .Where(o => o.Lead != null && !o.Lead.is_deleted)
                    .AsQueryable();

                if (!includeDeleted)
                {
                    query = query.Where(o => !o.is_deleted);
                }

                if (currentUserRole == 3)
                {
                    query = query.Where(o => o.assigned_to == currentUserId);
                }
                else if (currentUserRole == 2)
                {
                    var teamMemberIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId && u.is_active)
                        .Select(u => u.user_id)
                        .ToListAsync();

                    teamMemberIds.Add(currentUserId);
                    query = query.Where(o => teamMemberIds.Contains(o.assigned_to));
                }

                var opportunities = await query
                    .OrderByDescending(o => o.last_updated)
                    .ToListAsync();

                return opportunities.Select(MapToOpportunityListDto).ToList();
            }
            catch (Exception)
            {
                return new List<OpportunityListDto>();
            }
        }

        // NEW: Paginated methods
        public async Task<PaginatedOpportunitiesResponseDto> GetOpportunitiesPaginatedAsync(int currentUserId, int currentUserRole, int page, int pageSize, bool includeDeleted = false)
        {
            try
            {
                var query = _context.Opportunities
                    .Include(o => o.Lead)
                    .Include(o => o.OpportunityStatus)
                    .Include(o => o.AssignedToUser)
                    .Include(o => o.GasStations.Where(gs => !gs.is_deleted))
                    .Where(o => o.Lead != null && !o.Lead.is_deleted)
                    .AsQueryable();

                if (!includeDeleted)
                {
                    query = query.Where(o => !o.is_deleted);
                }

                if (currentUserRole == 3)
                {
                    query = query.Where(o => o.assigned_to == currentUserId);
                }
                else if (currentUserRole == 2)
                {
                    var teamMemberIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId && u.is_active)
                        .Select(u => u.user_id)
                        .ToListAsync();
                    teamMemberIds.Add(currentUserId);
                    query = query.Where(o => teamMemberIds.Contains(o.assigned_to));
                }

                var totalItems = await query.CountAsync();
                var opportunities = await query
                    .OrderByDescending(o => o.last_updated)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var result = opportunities.Select(MapToOpportunityListDto).ToList();
                var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

                return new PaginatedOpportunitiesResponseDto
                {
                    Data = result,
                    Pagination = new PaginationDto
                    {
                        CurrentPage = page,
                        TotalPages = totalPages,
                        TotalItems = totalItems,
                        PageSize = pageSize
                    }
                };
            }
            catch (Exception)
            {
                return new PaginatedOpportunitiesResponseDto
                {
                    Data = new List<OpportunityListDto>(),
                    Pagination = new PaginationDto
                    {
                        CurrentPage = 1,
                        TotalPages = 0,
                        TotalItems = 0,
                        PageSize = pageSize
                    }
                };
            }
        }

        public async Task<PaginatedOpportunitiesResponseDto> GetMyOpportunitiesPaginatedAsync(int currentUserId, int page, int pageSize)
        {
            try
            {
                var query = _context.Opportunities
                    .Include(o => o.Lead)
                    .Include(o => o.OpportunityStatus)
                    .Include(o => o.AssignedToUser)
                    .Include(o => o.GasStations.Where(gs => !gs.is_deleted))
                    .Where(o => o.assigned_to == currentUserId && !o.is_deleted)
                    .Where(o => o.Lead != null && !o.Lead.is_deleted);

                var totalItems = await query.CountAsync();
                var opportunities = await query
                    .OrderByDescending(o => o.last_updated)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var result = opportunities.Select(MapToOpportunityListDto).ToList();
                var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

                return new PaginatedOpportunitiesResponseDto
                {
                    Data = result,
                    Pagination = new PaginationDto
                    {
                        CurrentPage = page,
                        TotalPages = totalPages,
                        TotalItems = totalItems,
                        PageSize = pageSize
                    }
                };
            }
            catch (Exception)
            {
                return new PaginatedOpportunitiesResponseDto
                {
                    Data = new List<OpportunityListDto>(),
                    Pagination = new PaginationDto
                    {
                        CurrentPage = 1,
                        TotalPages = 0,
                        TotalItems = 0,
                        PageSize = pageSize
                    }
                };
            }
        }

        public async Task<PaginatedOpportunitiesResponseDto> GetTeamOpportunitiesPaginatedAsync(int managerId, int page, int pageSize)
        {
            try
            {
                var teamMemberIds = await _context.Users
                    .Where(u => u.manager_id == managerId && u.is_active)
                    .Select(u => u.user_id)
                    .ToListAsync();
                teamMemberIds.Add(managerId);

                var query = _context.Opportunities
                    .Include(o => o.Lead)
                    .Include(o => o.OpportunityStatus)
                    .Include(o => o.AssignedToUser)
                    .Include(o => o.GasStations.Where(gs => !gs.is_deleted))
                    .Where(o => teamMemberIds.Contains(o.assigned_to) && !o.is_deleted)
                    .Where(o => o.Lead != null && !o.Lead.is_deleted);

                var totalItems = await query.CountAsync();
                var opportunities = await query
                    .OrderByDescending(o => o.last_updated)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var result = opportunities.Select(MapToOpportunityListDto).ToList();
                var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

                return new PaginatedOpportunitiesResponseDto
                {
                    Data = result,
                    Pagination = new PaginationDto
                    {
                        CurrentPage = page,
                        TotalPages = totalPages,
                        TotalItems = totalItems,
                        PageSize = pageSize
                    }
                };
            }
            catch (Exception)
            {
                return new PaginatedOpportunitiesResponseDto
                {
                    Data = new List<OpportunityListDto>(),
                    Pagination = new PaginationDto
                    {
                        CurrentPage = 1,
                        TotalPages = 0,
                        TotalItems = 0,
                        PageSize = pageSize
                    }
                };
            }
        }

        public async Task<OpportunityResponseDto?> UpdateOpportunityAsync(int opportunityId, UpdateOpportunityDto updateDto, int currentUserId, int currentUserRole)
        {
            try
            {
                var opportunity = await _context.Opportunities.FindAsync(opportunityId);
                if (opportunity == null || !await CanAccessOpportunityAsync(opportunity, currentUserId, currentUserRole))
                    return null;

                if (!string.IsNullOrEmpty(updateDto.OwnerName))
                    opportunity.owner_name = updateDto.OwnerName;

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

                if (updateDto.ActualStations.HasValue)
                    opportunity.actual_stations = updateDto.ActualStations.Value;

                if (updateDto.AssignedTo.HasValue && currentUserRole <= 2)
                {
                    if (currentUserRole == 2)
                    {
                        var isTeamMember = await _context.Users
                            .AnyAsync(u => u.user_id == updateDto.AssignedTo.Value &&
                                          (u.manager_id == currentUserId || u.user_id == currentUserId));

                        if (isTeamMember)
                            opportunity.assigned_to = updateDto.AssignedTo.Value;
                    }
                    else if (currentUserRole == 1)
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
                updateDto.ActualStations = existing.actual_stations;

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
                    .Where(o => o.Lead != null && !o.Lead.is_deleted)
                    .OrderByDescending(o => o.last_updated)
                    .ToListAsync();

                return opportunities.Select(MapToOpportunityListDto).ToList();
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
                    .Where(o => o.Lead != null && !o.Lead.is_deleted)
                    .OrderByDescending(o => o.last_updated)
                    .ToListAsync();

                return opportunities.Select(MapToOpportunityListDto).ToList();
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
                    .Where(o => o.Lead != null && !o.Lead.is_deleted);

                if (currentUserRole == 3)
                {
                    query = query.Where(o => o.assigned_to == currentUserId);
                }
                else if (currentUserRole == 2)
                {
                    var teamMemberIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId && u.is_active)
                        .Select(u => u.user_id)
                        .ToListAsync();

                    teamMemberIds.Add(currentUserId);
                    query = query.Where(o => teamMemberIds.Contains(o.assigned_to));
                }

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
                return new OpportunityStatsDto();
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
                    opportunity.status_id = 1;
                }
                else if (stations.All(IsStationComplete))
                {
                    opportunity.status_id = 2;
                }
                else
                {
                    opportunity.status_id = 1;
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
                if (currentUserRole == 1)
                    return true;

                if (currentUserRole == 3)
                    return opportunity.assigned_to == currentUserId;

                if (currentUserRole == 2)
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
            return station.is_signed_off;
        }

        private OpportunityResponseDto MapToOpportunityResponseDto(Opportunity opportunity)
        {
            var stations = opportunity.GasStations.ToList();
            var completeStations = stations.Count(IsStationComplete);
            var incompleteStations = stations.Count - completeStations;
            var completionPercentage = stations.Any() ? Math.Round((double)completeStations / stations.Count * 100, 1) : 0.0;

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
                Stations = new List<OpportunityStationDto>(),
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