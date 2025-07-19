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

        Task<bool> UpdateOpportunityLastUpdatedAsync(int opportunityId);



        // UPDATED: Enhanced pagination methods with completion status filtering
        Task<PaginatedOpportunitiesResponseDto> GetOpportunitiesPaginatedAsync(int currentUserId, int currentUserRole, int page, int pageSize, bool? completionStatus = null, bool showSelfOnly = false, bool includeDeleted = false);
        Task<PaginatedOpportunitiesResponseDto> GetMyOpportunitiesPaginatedAsync(int currentUserId, int page, int pageSize, bool? completionStatus = null);
        Task<PaginatedOpportunitiesResponseDto> GetTeamOpportunitiesPaginatedAsync(int managerId, int page, int pageSize, bool? completionStatus = null, bool showSelfOnly = false);
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
                Console.WriteLine($"[OpportunityService] GetOpportunityByIdAsync called for ID: {opportunityId}");

                // FIXED: Build query with ALL necessary includes for gas stations
                var query = _context.Opportunities
                    .Include(o => o.Lead)
                    .Include(o => o.OpportunityStatus)
                    .Include(o => o.AssignedToUser)
                    .Include(o => o.CreatedByUser)
                    // CRITICAL FIX: Include gas stations with ALL navigation properties
                    .Include(o => o.GasStations.Where(gs => !gs.is_deleted))
                        .ThenInclude(gs => gs.StationType)  // ADDED: Include station type
                    .Include(o => o.GasStations.Where(gs => !gs.is_deleted))
                        .ThenInclude(gs => gs.CreatedByUser)  // ADDED: Include created by user
                    .Where(o => o.opportunity_id == opportunityId && !o.is_deleted);

                Console.WriteLine($"[OpportunityService] Query built for opportunity {opportunityId}");

                // Apply role-based filtering
                if (currentUserRole == 3) // Salesperson can only see their own opportunities
                {
                    query = query.Where(o => o.assigned_to == currentUserId);
                }
                else if (currentUserRole == 2) // Manager can see own + team opportunities
                {
                    // Get team member IDs first (materialized)
                    var teamMemberIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId || u.user_id == currentUserId)
                        .Select(u => u.user_id)
                        .ToListAsync();

                    query = query.Where(o => teamMemberIds.Contains(o.assigned_to));
                }
                // Admin can see all opportunities (no additional filtering)

                var opportunity = await query.FirstOrDefaultAsync();

                Console.WriteLine($"[OpportunityService] Query executed for opportunity {opportunityId}");

                if (opportunity == null)
                {
                    Console.WriteLine($"[OpportunityService] Opportunity {opportunityId} not found or no access");
                    return null;
                }

                Console.WriteLine($"[OpportunityService] Found opportunity {opportunityId}, gas stations count: {opportunity.GasStations?.Count ?? 0}");

                // ENHANCED DEBUG: Log each gas station
                if (opportunity.GasStations != null)
                {
                    foreach (var station in opportunity.GasStations)
                    {
                        Console.WriteLine($"[OpportunityService] Station found: ID={station.station_id}, Name={station.station_name}, IsDeleted={station.is_deleted}");
                    }
                }

                var result = MapToOpportunityResponseDto(opportunity);

                Console.WriteLine($"[OpportunityService] Mapped result for opportunity {opportunityId}: TotalStations={result.TotalStations}, StationsCount={result.Stations?.Count ?? 0}");

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OpportunityService] Error in GetOpportunityByIdAsync for ID {opportunityId}: {ex.Message}");
                Console.WriteLine($"[OpportunityService] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        public async Task<bool> UpdateOpportunityLastUpdatedAsync(int opportunityId)
        {
            try
            {
                var opportunity = await _context.Opportunities
                    .FirstOrDefaultAsync(o => o.opportunity_id == opportunityId && !o.is_deleted);

                if (opportunity == null)
                    return false;

                // Update the last_updated timestamp to current time
                opportunity.last_updated = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                Console.WriteLine($"[DEBUG] Opportunity {opportunityId} last_updated timestamp updated to {opportunity.last_updated}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] UpdateOpportunityLastUpdatedAsync failed: {ex.Message}");
                return false;
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

                // FIXED: Apply IDENTICAL role-based filtering as LeadService.GetLeadStatsAsync()
                if (currentUserRole == 3) // Salesperson
                {
                    query = query.Where(o => o.assigned_to == currentUserId);
                }
                else if (currentUserRole == 2) // Manager
                {
                    var teamMemberIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId && u.is_active)
                        .Select(u => u.user_id)
                        .ToListAsync();

                    teamMemberIds.Add(currentUserId);
                    query = query.Where(o => teamMemberIds.Contains(o.assigned_to));
                }
                // Admin sees all (no additional filtering)

                var opportunities = await query
                    .OrderByDescending(o => o.last_updated)
                    .ToListAsync();

                return opportunities.Select(MapToOpportunityListDto).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetOpportunitiesAsync failed: {ex.Message}");
                return new List<OpportunityListDto>();
            }
        }

        // NEW: Paginated methods
        // UPDATED: Enhanced pagination methods with completion status filtering// FIXED: Enhanced pagination methods with correct completion status filtering
        // FIXED: Enhanced pagination methods with correct completion status filtering
        public async Task<PaginatedOpportunitiesResponseDto> GetOpportunitiesPaginatedAsync(int currentUserId, int currentUserRole, int page, int pageSize, bool? completionStatus = null, bool showSelfOnly = false, bool includeDeleted = false)
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

                // FIXED: Apply role-based filtering with proper showSelfOnly logic
                if (currentUserRole == 3) // Salesperson - always self only
                {
                    query = query.Where(o => o.assigned_to == currentUserId);
                }
                else if (currentUserRole == 2) // Manager
                {
                    if (showSelfOnly)
                    {
                        // Manager wants only their own opportunities
                        query = query.Where(o => o.assigned_to == currentUserId);
                    }
                    else
                    {
                        // Manager wants team opportunities (including their own)
                        var teamMemberIds = await _context.Users
                            .Where(u => u.manager_id == currentUserId && u.is_active)
                            .Select(u => u.user_id)
                            .ToListAsync();
                        teamMemberIds.Add(currentUserId);
                        query = query.Where(o => teamMemberIds.Contains(o.assigned_to));
                    }
                }
                else if (currentUserRole == 1) // Admin
                {
                    if (showSelfOnly)
                    {
                        // Admin wants only opportunities assigned to them specifically
                        query = query.Where(o => o.assigned_to == currentUserId);
                    }
                    // If showSelfOnly is false, Admin sees ALL opportunities (no additional filtering)
                }

                // Get opportunities after role filtering
                var opportunities = await query.ToListAsync();

                // FIXED: Apply completion status filtering in memory
                if (completionStatus.HasValue)
                {
                    opportunities = opportunities.Where(opp =>
                    {
                        var stations = opp.GasStations.Where(gs => !gs.is_deleted).ToList();

                        if (completionStatus.Value) // Complete: has stations AND all are signed off
                        {
                            return stations.Any() && stations.All(gs => gs.is_signed_off);
                        }
                        else // Incomplete: no stations OR at least one not signed off
                        {
                            return !stations.Any() || stations.Any(gs => !gs.is_signed_off);
                        }
                    }).ToList();
                }

                var totalItems = opportunities.Count;
                var paginatedOpportunities = opportunities
                    .OrderByDescending(o => o.last_updated)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var result = paginatedOpportunities.Select(MapToOpportunityListDto).ToList();
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
            catch (Exception ex)
            {
                // Log the exception for debugging
                Console.WriteLine($"Error in GetOpportunitiesPaginatedAsync: {ex.Message}");
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

        public async Task<PaginatedOpportunitiesResponseDto> GetMyOpportunitiesPaginatedAsync(int currentUserId, int page, int pageSize, bool? completionStatus = null)
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

                var opportunities = await query.ToListAsync();

                // FIXED: Apply completion status filtering in memory
                if (completionStatus.HasValue)
                {
                    opportunities = opportunities.Where(opp =>
                    {
                        var stations = opp.GasStations.Where(gs => !gs.is_deleted).ToList();

                        if (completionStatus.Value) // Complete
                        {
                            return stations.Any() && stations.All(gs => gs.is_signed_off);
                        }
                        else // Incomplete
                        {
                            return !stations.Any() || stations.Any(gs => !gs.is_signed_off);
                        }
                    }).ToList();
                }

                var totalItems = opportunities.Count;
                var paginatedOpportunities = opportunities
                    .OrderByDescending(o => o.last_updated)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var result = paginatedOpportunities.Select(MapToOpportunityListDto).ToList();
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

        public async Task<PaginatedOpportunitiesResponseDto> GetTeamOpportunitiesPaginatedAsync(int managerId, int page, int pageSize, bool? completionStatus = null, bool showSelfOnly = false)
        {
            try
            {
                var query = _context.Opportunities
                    .Include(o => o.Lead)
                    .Include(o => o.OpportunityStatus)
                    .Include(o => o.AssignedToUser)
                    .Include(o => o.GasStations.Where(gs => !gs.is_deleted))
                    .Where(o => !o.is_deleted)
                    .Where(o => o.Lead != null && !o.Lead.is_deleted);

                // Apply self-only or team filtering
                if (showSelfOnly)
                {
                    query = query.Where(o => o.assigned_to == managerId);
                }
                else
                {
                    var teamMemberIds = await _context.Users
                        .Where(u => u.manager_id == managerId && u.is_active)
                        .Select(u => u.user_id)
                        .ToListAsync();
                    teamMemberIds.Add(managerId);
                    query = query.Where(o => teamMemberIds.Contains(o.assigned_to));
                }

                var opportunities = await query.ToListAsync();

                // FIXED: Apply completion status filtering in memory
                if (completionStatus.HasValue)
                {
                    opportunities = opportunities.Where(opp =>
                    {
                        var stations = opp.GasStations.Where(gs => !gs.is_deleted).ToList();

                        if (completionStatus.Value) // Complete
                        {
                            return stations.Any() && stations.All(gs => gs.is_signed_off);
                        }
                        else // Incomplete
                        {
                            return !stations.Any() || stations.Any(gs => !gs.is_signed_off);
                        }
                    }).ToList();
                }

                var totalItems = opportunities.Count;
                var paginatedOpportunities = opportunities
                    .OrderByDescending(o => o.last_updated)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var result = paginatedOpportunities.Select(MapToOpportunityListDto).ToList();
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

                // ✅ FIXED LOGIC: Complete only when ALL stations are SIGNED OFF
                if (!stations.Any())
                {
                    // No stations = Active (status_id = 1)
                    opportunity.status_id = 1;
                }
                else if (stations.All(gs => gs.is_signed_off))
                {
                    // ALL stations signed off = Complete (status_id = 2)
                    opportunity.status_id = 2;
                }
                else
                {
                    // Some stations not signed off = Active (status_id = 1)
                    opportunity.status_id = 1;
                }

                await _context.SaveChangesAsync();

                Console.WriteLine($"[DEBUG] Opportunity {opportunityId} status updated to {opportunity.status_id} (signed off: {stations.Count(gs => gs.is_signed_off)}/{stations.Count})");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] UpdateOpportunityStatusBasedOnStationsAsync failed: {ex.Message}");
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

        private static double CalculateStationCompletionPercentage(GasStation station)
        {
            var totalFields = 8; // Total required fields
            var completedFields = 0;

            if (!string.IsNullOrWhiteSpace(station.station_name)) completedFields++;
            if (!string.IsNullOrWhiteSpace(station.address_line_1)) completedFields++;
            if (!string.IsNullOrWhiteSpace(station.poc_name)) completedFields++;
            if (!string.IsNullOrWhiteSpace(station.poc_phone)) completedFields++;
            if (!string.IsNullOrWhiteSpace(station.poc_email)) completedFields++;
            if (station.number_of_pumps.HasValue) completedFields++;
            if (station.number_of_employees.HasValue) completedFields++;
            if (station.station_type_id.HasValue) completedFields++;

            return Math.Round((double)completedFields / totalFields * 100, 1);
        }
        private static List<string> GetMissingFields(GasStation station)
        {
            var missing = new List<string>();

            if (string.IsNullOrWhiteSpace(station.station_name))
                missing.Add("Station Name");
            if (string.IsNullOrWhiteSpace(station.address_line_1))
                missing.Add("Address");
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

            // CRITICAL FIX: Map the actual stations instead of returning empty list
            var stationDtos = stations.Select(s => new OpportunityStationDto
            {
                StationId = s.station_id,
                StationName = s.station_name ?? "",
                Address = s.Address, // Uses computed property from model
                StationCode = s.station_code ?? "",
                PocName = s.poc_name,
                PocPhone = s.poc_phone,
                PocEmail = s.poc_email,
                NumberOfPumps = s.number_of_pumps,
                NumberOfEmployees = s.number_of_employees,
                StationTypeName = s.StationType?.type_name ?? "",
                IsComplete = IsStationComplete(s),
                CompletionPercentage = CalculateStationCompletionPercentage(s),
                MissingFields = GetMissingFields(s),
                CreatedAt = s.created_at,
                StatusId = s.station_type_id ?? 0,
                StatusName = s.StationType?.type_name ?? "",
                Description = s.notes ?? ""
            }).OrderBy(s => s.StationCode).ToList();

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
                Stations = stationDtos, // ✅ FIXED: Return actual mapped stations
                CreatedAt = opportunity.created_at,
                LastUpdated = opportunity.last_updated,
                IsDeleted = opportunity.is_deleted
            };
        }

        private OpportunityListDto MapToOpportunityListDto(Opportunity opportunity)
        {
            var stations = opportunity.GasStations.ToList();

            // ✅ FIXED: Complete stations = signed off stations (not just completion percentage)
            var completeStations = stations.Count(gs => gs.is_signed_off);
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
                CompleteStations = completeStations, // ✅ FIXED: Use signed off count
                CompletionPercentage = completionPercentage,
                CreatedAt = opportunity.created_at,
                LastUpdated = opportunity.last_updated
            };
        }
    }
}