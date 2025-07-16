using Microsoft.EntityFrameworkCore;
using gasopper_crm_server.Data;
using gasopper_crm_server.DTOs;
using gasopper_crm_server.Models;
using gasopper_crm_server.Helpers;

namespace gasopper_crm_server.Services
{
    public interface ILeadService
    {
        Task<LeadResponseDto?> CreateLeadAsync(CreateLeadDto createLeadDto, int currentUserId, int currentUserRole);
        Task<LeadResponseDto?> GetLeadByIdAsync(int leadId, int currentUserId, int currentUserRole);
        Task<PaginatedLeadResponseDto> GetLeadsAsync(int currentUserId, int currentUserRole, int page = 1, int pageSize = 20, bool includeDeleted = false);
        Task<LeadResponseDto?> UpdateLeadAsync(int leadId, UpdateLeadDto updateLeadDto, int currentUserId, int currentUserRole);
        Task<bool> DeleteLeadAsync(int leadId, int currentUserId, int currentUserRole);
        Task<LeadResponseDto?> UpdateLeadStatusAsync(int leadId, UpdateLeadStatusDto updateStatusDto, int currentUserId, int currentUserRole);
        Task<LeadResponseDto?> ConvertToOpportunityAsync(int leadId, ConvertLeadToOpportunityDto convertDto, int currentUserId, int currentUserRole);
        Task<List<LeadListResponseDto>> GetMyLeadsAsync(int currentUserId);
        Task<List<LeadListResponseDto>> GetTeamLeadsAsync(int managerId);
        Task<LeadStatsDto> GetLeadStatsAsync(int currentUserId, int currentUserRole);
        Task<List<object>> GetLeadStatusesAsync();
    }

    public class LeadService : ILeadService
    {
        private readonly GasopperDbContext _context;

        public LeadService(GasopperDbContext context)
        {
            _context = context;
        }

        public async Task<LeadResponseDto?> CreateLeadAsync(CreateLeadDto createLeadDto, int currentUserId, int currentUserRole)
        {
            try
            {
                var assignedTo = createLeadDto.AssignedTo ?? currentUserId;

                if (createLeadDto.AssignedTo.HasValue && createLeadDto.AssignedTo != currentUserId && currentUserRole == 3)
                    return null;

                var lead = new Lead
                {
                    name = createLeadDto.Name,
                    phone_number = createLeadDto.PhoneNumber,
                    email = createLeadDto.Email,
                    address = createLeadDto.Address,
                    expected_stations = createLeadDto.ExpectedStations,
                    referral_name = createLeadDto.ReferralName,
                    referral_email = createLeadDto.ReferralEmail,
                    referral_phone = createLeadDto.ReferralPhone,
                    referral_address = createLeadDto.ReferralAddress,
                    assigned_to = assignedTo,
                    created_by = currentUserId,
                    status_id = 1
                };

                _context.Leads.Add(lead);
                await _context.SaveChangesAsync();

                return await GetLeadByIdAsync(lead.lead_id, currentUserId, currentUserRole);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<LeadResponseDto?> GetLeadByIdAsync(int leadId, int currentUserId, int currentUserRole)
        {
            try
            {
                var query = _context.Leads
                    .Include(l => l.AssignedToUser)
                    .Include(l => l.CreatedByUser)
                    .Include(l => l.Status)
                    .Where(l => l.lead_id == leadId && !l.is_deleted);

                if (currentUserRole == 3)
                {
                    query = query.Where(l => l.assigned_to == currentUserId);
                }
                else if (currentUserRole == 2)
                {
                    var teamIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId && u.is_active)
                        .Select(u => u.user_id)
                        .ToListAsync();
                    teamIds.Add(currentUserId);

                    query = query.Where(l => teamIds.Contains(l.assigned_to));
                }

                var lead = await query.FirstOrDefaultAsync();
                if (lead == null) return null;

                var hasOpportunity = await _context.Opportunities
                    .Where(o => o.lead_id == leadId && !o.is_deleted)
                    .Where(o => currentUserRole == 1 ||
                               (currentUserRole == 3 && o.assigned_to == currentUserId) ||
                               (currentUserRole == 2 && (
                                   o.assigned_to == currentUserId ||
                                   _context.Users.Any(u => u.user_id == o.assigned_to && u.manager_id == currentUserId && u.is_active)
                               )))
                    .AnyAsync();

                var opportunity = hasOpportunity ? await _context.Opportunities
                    .Include(o => o.OpportunityStatus)
                    .Where(o => o.lead_id == leadId && !o.is_deleted)
                    .Where(o => currentUserRole == 1 ||
                               (currentUserRole == 3 && o.assigned_to == currentUserId) ||
                               (currentUserRole == 2 && (
                                   o.assigned_to == currentUserId ||
                                   _context.Users.Any(u => u.user_id == o.assigned_to && u.manager_id == currentUserId && u.is_active)
                               )))
                    .FirstOrDefaultAsync() : null;

                return new LeadResponseDto
                {
                    LeadId = lead.lead_id,
                    Name = lead.name,
                    PhoneNumber = lead.phone_number,
                    Email = lead.email,
                    Address = lead.address,
                    ExpectedStations = lead.expected_stations,
                    ReferralName = lead.referral_name,
                    ReferralEmail = lead.referral_email,
                    ReferralPhone = lead.referral_phone,
                    ReferralAddress = lead.referral_address,
                    StatusId = lead.status_id,
                    StatusName = lead.Status?.status_name ?? "",
                    AssignedTo = lead.assigned_to,
                    AssignedToName = UserNameHelper.FormatAssignedUserName(lead.AssignedToUser),
                    CreatedBy = lead.created_by,
                    CreatedByName = UserNameHelper.FormatCreatedByUserName(lead.CreatedByUser),
                    CreatedAt = lead.created_at,
                    LastUpdated = lead.last_updated,
                    OpportunityId = opportunity?.opportunity_id,
                    HasOpportunity = hasOpportunity,
                    OpportunityStatus = opportunity?.OpportunityStatus?.status_name ?? "",
                    IsDeleted = lead.is_deleted
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<PaginatedLeadResponseDto> GetLeadsAsync(int currentUserId, int currentUserRole, int page = 1, int pageSize = 20, bool includeDeleted = false)
        {
            try
            {
                var query = _context.Leads
                    .Include(l => l.AssignedToUser)
                    .Include(l => l.CreatedByUser)
                    .Include(l => l.Status)
                    .AsQueryable();

                if (!includeDeleted)
                    query = query.Where(l => !l.is_deleted);

                if (currentUserRole == 3)
                {
                    query = query.Where(l => l.assigned_to == currentUserId);
                }
                else if (currentUserRole == 2)
                {
                    var teamIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId && u.is_active)
                        .Select(u => u.user_id)
                        .ToListAsync();
                    teamIds.Add(currentUserId);

                    query = query.Where(l => teamIds.Contains(l.assigned_to));
                }

                var totalItems = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
                page = Math.Max(1, Math.Min(page, totalPages));

                var leads = await query
                    .OrderByDescending(l => l.last_updated)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var allOpportunities = await _context.Opportunities
                    .Where(o => !o.is_deleted)
                    .Select(o => new { o.lead_id, o.assigned_to, o.opportunity_id })
                    .ToListAsync();

                var accessibleOpportunityLeadIds = new List<int>();

                if (currentUserRole == 1)
                {
                    accessibleOpportunityLeadIds = allOpportunities.Select(o => o.lead_id).ToList();
                }
                else if (currentUserRole == 3)
                {
                    accessibleOpportunityLeadIds = allOpportunities
                        .Where(o => o.assigned_to == currentUserId)
                        .Select(o => o.lead_id)
                        .ToList();
                }
                else if (currentUserRole == 2)
                {
                    var teamIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId && u.is_active)
                        .Select(u => u.user_id)
                        .ToListAsync();
                    teamIds.Add(currentUserId);

                    accessibleOpportunityLeadIds = allOpportunities
                        .Where(o => teamIds.Contains(o.assigned_to))
                        .Select(o => o.lead_id)
                        .ToList();
                }

                var result = leads.Select(l => new LeadListResponseDto
                {
                    LeadId = l.lead_id,
                    Name = l.name,
                    Email = l.email,
                    PhoneNumber = l.phone_number,
                    Address = l.address,
                    ExpectedStations = l.expected_stations,
                    StatusName = l.Status?.status_name ?? "",
                    AssignedToName = UserNameHelper.FormatAssignedUserName(l.AssignedToUser),
                    CreatedByName = UserNameHelper.FormatCreatedByUserName(l.CreatedByUser),
                    CreatedAt = l.created_at,
                    LastUpdated = l.last_updated,
                    HasOpportunity = accessibleOpportunityLeadIds.Contains(l.lead_id),
                    IsDeleted = l.is_deleted
                }).ToList();

                return new PaginatedLeadResponseDto
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
                return new PaginatedLeadResponseDto
                {
                    Data = new List<LeadListResponseDto>(),
                    Pagination = new PaginationDto { CurrentPage = 1, TotalPages = 0, TotalItems = 0, PageSize = pageSize }
                };
            }
        }

        public async Task<LeadResponseDto?> UpdateLeadAsync(int leadId, UpdateLeadDto updateLeadDto, int currentUserId, int currentUserRole)
        {
            try
            {
                var lead = await _context.Leads.FindAsync(leadId);
                if (lead == null || !await CanAccessLeadAsync(lead, currentUserId, currentUserRole))
                    return null;

                if (!string.IsNullOrEmpty(updateLeadDto.Name))
                    lead.name = updateLeadDto.Name;
                if (!string.IsNullOrEmpty(updateLeadDto.PhoneNumber))
                    lead.phone_number = updateLeadDto.PhoneNumber;
                if (!string.IsNullOrEmpty(updateLeadDto.Email))
                    lead.email = updateLeadDto.Email;
                if (!string.IsNullOrEmpty(updateLeadDto.Address))
                    lead.address = updateLeadDto.Address;
                if (updateLeadDto.ExpectedStations.HasValue)
                    lead.expected_stations = updateLeadDto.ExpectedStations.Value;

                if (updateLeadDto.ReferralName != null)
                    lead.referral_name = updateLeadDto.ReferralName;
                if (updateLeadDto.ReferralEmail != null)
                    lead.referral_email = updateLeadDto.ReferralEmail;
                if (updateLeadDto.ReferralPhone != null)
                    lead.referral_phone = updateLeadDto.ReferralPhone;
                if (updateLeadDto.ReferralAddress != null)
                    lead.referral_address = updateLeadDto.ReferralAddress;

                if (updateLeadDto.AssignedTo.HasValue && (currentUserRole == 1 || currentUserRole == 2))
                    lead.assigned_to = updateLeadDto.AssignedTo.Value;

                await _context.SaveChangesAsync();
                return await GetLeadByIdAsync(leadId, currentUserId, currentUserRole);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<bool> DeleteLeadAsync(int leadId, int currentUserId, int currentUserRole)
        {
            try
            {
                var lead = await _context.Leads.FindAsync(leadId);
                if (lead == null || !await CanAccessLeadAsync(lead, currentUserId, currentUserRole))
                    return false;

                lead.is_deleted = true;
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<LeadResponseDto?> UpdateLeadStatusAsync(int leadId, UpdateLeadStatusDto updateStatusDto, int currentUserId, int currentUserRole)
        {
            try
            {
                var lead = await _context.Leads.FindAsync(leadId);
                if (lead == null || !await CanAccessLeadAsync(lead, currentUserId, currentUserRole))
                    return null;

                if (updateStatusDto.StatusId < 1 || updateStatusDto.StatusId > 7)
                    return null;

                var statusExists = await _context.LeadStatuses
                    .AnyAsync(s => s.status_id == updateStatusDto.StatusId);

                if (!statusExists)
                    return null;

                lead.status_id = updateStatusDto.StatusId;
                await _context.SaveChangesAsync();
                return await GetLeadByIdAsync(leadId, currentUserId, currentUserRole);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<LeadResponseDto?> ConvertToOpportunityAsync(int leadId, ConvertLeadToOpportunityDto convertDto, int currentUserId, int currentUserRole)
        {
            try
            {
                var lead = await _context.Leads
                    .Include(l => l.Opportunity)
                    .FirstOrDefaultAsync(l => l.lead_id == leadId && !l.is_deleted);

                if (lead == null || !await CanAccessLeadAsync(lead, currentUserId, currentUserRole))
                    return null;

                if (lead.Opportunity != null && !lead.Opportunity.is_deleted)
                    return null;

                var opportunity = new Opportunity
                {
                    lead_id = leadId,
                    owner_name = convertDto.OwnerName,
                    address_line_1 = convertDto.AddressLine1,
                    address_line_2 = convertDto.AddressLine2,
                    city = convertDto.City,
                    state = convertDto.State,
                    postal_code = convertDto.PostalCode,
                    country = convertDto.Country ?? "United States",
                    actual_stations = convertDto.ActualStations,
                    assigned_to = convertDto.AssignedTo ?? lead.assigned_to,
                    created_by = currentUserId,
                    status_id = 1
                };

                _context.Opportunities.Add(opportunity);

                var convertedStatus = await _context.LeadStatuses
                    .Where(s => s.status_name.ToLower().Contains("converted"))
                    .Select(s => s.status_id)
                    .FirstOrDefaultAsync();

                lead.status_id = convertedStatus > 0 ? convertedStatus : 2;

                await _context.SaveChangesAsync();
                return await GetLeadByIdAsync(leadId, currentUserId, currentUserRole);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<List<LeadListResponseDto>> GetMyLeadsAsync(int currentUserId)
        {
            var result = await GetLeadsAsync(currentUserId, 3, 1, 1000, false);
            return result.Data;
        }

        public async Task<List<LeadListResponseDto>> GetTeamLeadsAsync(int managerId)
        {
            var result = await GetLeadsAsync(managerId, 2, 1, 1000, false);
            return result.Data;
        }

        public async Task<LeadStatsDto> GetLeadStatsAsync(int currentUserId, int currentUserRole)
        {
            try
            {
                var leadsQuery = _context.Leads
                    .Include(l => l.Status)
                    .Where(l => !l.is_deleted);

                if (currentUserRole == 3)
                {
                    leadsQuery = leadsQuery.Where(l => l.assigned_to == currentUserId);
                }
                else if (currentUserRole == 2)
                {
                    var teamMemberIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId && u.is_active)
                        .Select(u => u.user_id)
                        .ToListAsync();

                    teamMemberIds.Add(currentUserId);
                    leadsQuery = leadsQuery.Where(l => teamMemberIds.Contains(l.assigned_to));
                }

                var totalLeads = await leadsQuery.CountAsync();

                var opportunitiesQuery = _context.Opportunities
                    .Where(o => !o.is_deleted)
                    .Where(o => o.lead_id != null)
                    .Join(_context.Leads.Where(l => !l.is_deleted),
                          o => o.lead_id,
                          l => l.lead_id,
                          (o, l) => o);

                if (currentUserRole == 3)
                {
                    opportunitiesQuery = opportunitiesQuery.Where(o => o.assigned_to == currentUserId);
                }
                else if (currentUserRole == 2)
                {
                    var teamMemberIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId && u.is_active)
                        .Select(u => u.user_id)
                        .ToListAsync();

                    teamMemberIds.Add(currentUserId);
                    opportunitiesQuery = opportunitiesQuery.Where(o => teamMemberIds.Contains(o.assigned_to));
                }

                var convertedOpportunities = await opportunitiesQuery.CountAsync();

                var conversionRate = totalLeads > 0 ? Math.Round((double)convertedOpportunities / totalLeads * 100, 1) : 0.0;

                var conversions = await (from l in leadsQuery
                                         join o in opportunitiesQuery on l.lead_id equals o.lead_id
                                         select new
                                         {
                                             LeadCreated = l.created_at,
                                             OpportunityCreated = o.created_at
                                         }).ToListAsync();

                var averageDaysToConvert = conversions.Any()
                    ? Math.Round(conversions.Average(c => (c.OpportunityCreated - c.LeadCreated).TotalDays), 1)
                    : 0.0;

                var statusBreakdown = await leadsQuery
                    .GroupBy(l => l.Status.status_name)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Status ?? "", x => x.Count);

                return new LeadStatsDto
                {
                    TotalLeads = totalLeads,
                    ConvertedLeads = convertedOpportunities,
                    ConversionRate = conversionRate,
                    AverageDaysToConvert = (int)Math.Round(averageDaysToConvert),
                    StatusBreakdown = statusBreakdown
                };
            }
            catch (Exception)
            {
                return new LeadStatsDto
                {
                    TotalLeads = 0,
                    ConvertedLeads = 0,
                    ConversionRate = 0.0,
                    AverageDaysToConvert = 0,
                    StatusBreakdown = new Dictionary<string, int>()
                };
            }
        }

        public async Task<List<object>> GetLeadStatusesAsync()
        {
            try
            {
                return await _context.LeadStatuses
                    .OrderBy(s => s.status_id)
                    .Select(s => new
                    {
                        id = s.status_id,
                        name = s.status_name,
                        description = s.description ?? ""
                    })
                    .ToListAsync<object>();
            }
            catch (Exception)
            {
                return new List<object>();
            }
        }

        private async Task<bool> CanAccessLeadAsync(Lead lead, int currentUserId, int currentUserRole)
        {
            try
            {
                if (currentUserRole == 1) return true;
                if (currentUserRole == 3) return lead.assigned_to == currentUserId;

                if (currentUserRole == 2)
                {
                    if (lead.assigned_to == currentUserId) return true;

                    var assignedUser = await _context.Users.FindAsync(lead.assigned_to);
                    return assignedUser?.manager_id == currentUserId;
                }

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}