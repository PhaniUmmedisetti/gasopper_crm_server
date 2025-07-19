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

                // Salesperson cannot assign to others
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
                    status_id = 1, // Default to "New" status
                    created_at = DateTime.UtcNow,
                    last_updated = DateTime.UtcNow,
                    is_deleted = false
                };

                _context.Leads.Add(lead);
                await _context.SaveChangesAsync();

                return await GetLeadByIdAsync(lead.lead_id, currentUserId, currentUserRole);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] CreateLeadAsync failed: {ex.Message}");
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

                // Apply role-based filtering
                if (currentUserRole == 3) // Salesperson
                {
                    query = query.Where(l => l.assigned_to == currentUserId);
                }
                else if (currentUserRole == 2) // Manager
                {
                    var teamIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId && u.is_active)
                        .Select(u => u.user_id)
                        .ToListAsync();
                    teamIds.Add(currentUserId);

                    query = query.Where(l => teamIds.Contains(l.assigned_to));
                }
                // Admin sees all (no additional filtering)

                var lead = await query.FirstOrDefaultAsync();
                if (lead == null) return null;

                // FIXED: Check for opportunity existence without role-based access filtering
                var opportunity = await _context.Opportunities
                    .Include(o => o.OpportunityStatus)
                    .Where(o => o.lead_id == leadId && !o.is_deleted)
                    .FirstOrDefaultAsync();

                var hasOpportunity = opportunity != null;

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
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetLeadByIdAsync failed: {ex.Message}");
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

                // Apply role-based filtering
                if (currentUserRole == 3) // Salesperson
                {
                    query = query.Where(l => l.assigned_to == currentUserId);
                }
                else if (currentUserRole == 2) // Manager
                {
                    var teamIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId && u.is_active)
                        .Select(u => u.user_id)
                        .ToListAsync();
                    teamIds.Add(currentUserId);

                    query = query.Where(l => teamIds.Contains(l.assigned_to));
                }
                // Admin sees all (no additional filtering)

                var totalItems = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
                page = Math.Max(1, Math.Min(page, totalPages));

                var leads = await query
                    .OrderByDescending(l => l.last_updated)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // FIXED: Get all opportunities and create a simple lookup (no role filtering)
                var allOpportunities = await _context.Opportunities
                    .Where(o => !o.is_deleted)
                    .Select(o => o.lead_id)
                    .ToListAsync();

                var opportunityLeadIds = new HashSet<int>(allOpportunities);

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
                    HasOpportunity = opportunityLeadIds.Contains(l.lead_id),
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
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetLeadsAsync failed: {ex.Message}");
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

                // Update basic fields
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

                // Update referral fields (nullable)
                if (updateLeadDto.ReferralName != null)
                    lead.referral_name = updateLeadDto.ReferralName;
                if (updateLeadDto.ReferralEmail != null)
                    lead.referral_email = updateLeadDto.ReferralEmail;
                if (updateLeadDto.ReferralPhone != null)
                    lead.referral_phone = updateLeadDto.ReferralPhone;
                if (updateLeadDto.ReferralAddress != null)
                    lead.referral_address = updateLeadDto.ReferralAddress;

                // Only Admin/Manager can reassign leads
                if (updateLeadDto.AssignedTo.HasValue && (currentUserRole == 1 || currentUserRole == 2))
                    lead.assigned_to = updateLeadDto.AssignedTo.Value;

                lead.last_updated = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return await GetLeadByIdAsync(leadId, currentUserId, currentUserRole);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] UpdateLeadAsync failed: {ex.Message}");
                return null;
            }
        }

        // REPLACE this method in services/LeadService.cs starting at line 286

        public async Task<bool> DeleteLeadAsync(int leadId, int currentUserId, int currentUserRole)
        {
            try
            {
                var lead = await _context.Leads.FindAsync(leadId);
                if (lead == null || !await CanAccessLeadAsync(lead, currentUserId, currentUserRole))
                    return false;

                // ✅ MANUAL CASCADE SOFT DELETE - Start transaction
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Step 1: Find the related opportunity (if any)
                    var opportunity = await _context.Opportunities
                        .FirstOrDefaultAsync(o => o.lead_id == leadId && !o.is_deleted);

                    if (opportunity != null)
                    {
                        // Step 2: Soft delete all gas stations for this opportunity
                        var gasStations = await _context.GasStations
                            .Where(gs => gs.opportunity_id == opportunity.opportunity_id && !gs.is_deleted)
                            .ToListAsync();

                        foreach (var station in gasStations)
                        {
                            station.is_deleted = true;
                            station.last_updated = DateTime.UtcNow;
                            Console.WriteLine($"[DEBUG] Soft deleted gas station {station.station_id} (cascade from lead {leadId})");
                        }

                        // Step 3: Soft delete the opportunity
                        opportunity.is_deleted = true;
                        opportunity.last_updated = DateTime.UtcNow;
                        Console.WriteLine($"[DEBUG] Soft deleted opportunity {opportunity.opportunity_id} (cascade from lead {leadId})");
                    }

                    // Step 4: Finally, soft delete the lead
                    lead.is_deleted = true;
                    lead.last_updated = DateTime.UtcNow;
                    Console.WriteLine($"[DEBUG] Soft deleted lead {leadId}");

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    Console.WriteLine($"[SUCCESS] Lead {leadId} and all related records soft deleted successfully");
                    return true;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    Console.WriteLine($"[ERROR] Transaction failed, rolling back: {ex.Message}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] DeleteLeadAsync failed: {ex.Message}");
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

                // Validate status range (1-7 for lead statuses)
                if (updateStatusDto.StatusId < 1 || updateStatusDto.StatusId > 7)
                    return null;

                var statusExists = await _context.LeadStatuses
                    .AnyAsync(s => s.status_id == updateStatusDto.StatusId);

                if (!statusExists)
                    return null;

                lead.status_id = updateStatusDto.StatusId;
                lead.last_updated = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return await GetLeadByIdAsync(leadId, currentUserId, currentUserRole);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] UpdateLeadStatusAsync failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Converts a lead to an opportunity with updated assignment logic.
        /// Both created_by and assigned_to default to the converting user.
        /// </summary>
        public async Task<LeadResponseDto?> ConvertToOpportunityAsync(int leadId, ConvertLeadToOpportunityDto convertDto, int currentUserId, int currentUserRole)
        {
            try
            {
                // Get the lead first
                var lead = await _context.Leads
                    .Include(l => l.Status)
                    .Include(l => l.AssignedToUser)
                    .Include(l => l.CreatedByUser)
                    .FirstOrDefaultAsync(l => l.lead_id == leadId && !l.is_deleted);

                if (lead == null || !await CanAccessLeadAsync(lead, currentUserId, currentUserRole))
                    return null;

                // FIXED: Check if lead already has an opportunity (no role filtering)
                var existingOpportunity = await _context.Opportunities
                    .FirstOrDefaultAsync(o => o.lead_id == leadId && !o.is_deleted);

                if (existingOpportunity != null)
                    return null; // Lead already converted

                // UPDATED LOGIC: Both created_by and assigned_to set to converting user
                // Unless specifically overridden by AssignedTo in the DTO
                var assignedToUser = convertDto.AssignedTo ?? currentUserId; // Default to current user

                // Validate assignment permissions for Admin/Manager roles
                if (convertDto.AssignedTo.HasValue && convertDto.AssignedTo.Value != currentUserId)
                {
                    // Only Admin/Manager can assign to others
                    if (currentUserRole > 2) // Salesperson cannot assign to others
                    {
                        assignedToUser = currentUserId; // Force assignment to self for salespeople
                    }
                    else if (currentUserRole == 2) // Manager can only assign to team members
                    {
                        var isTeamMember = await _context.Users
                            .AnyAsync(u => u.user_id == convertDto.AssignedTo.Value &&
                                          (u.manager_id == currentUserId || u.user_id == currentUserId));

                        if (!isTeamMember)
                        {
                            assignedToUser = currentUserId; // Default to self if not team member
                        }
                    }
                    // Admin (role 1) can assign to anyone, so use the provided value
                }

                // Create opportunity with converting user as both creator and assignee (by default)
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

                    // KEY CHANGE: Both fields set to converting user (unless overridden by valid assignment)
                    assigned_to = assignedToUser,     // Converting user or valid assignee
                    created_by = currentUserId,       // Always the converting user

                    status_id = 1, // Active status
                    created_at = DateTime.UtcNow,
                    last_updated = DateTime.UtcNow,
                    is_deleted = false
                };

                _context.Opportunities.Add(opportunity);

                // Update lead status to converted
                var convertedStatusId = await GetConvertedStatusId();
                lead.status_id = convertedStatusId;
                lead.last_updated = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Return updated lead with opportunity information
                return await GetLeadByIdAsync(leadId, currentUserId, currentUserRole);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] ConvertToOpportunityAsync failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Helper method to get the "Converted" status ID
        /// </summary>
        private async Task<int> GetConvertedStatusId()
        {
            var convertedStatus = await _context.LeadStatuses
                .FirstOrDefaultAsync(s => s.status_name.ToLower() == "converted");

            return convertedStatus?.status_id ?? 5; // Default to 5 if not found
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

                // Apply role-based filtering to leads
                if (currentUserRole == 3) // Salesperson
                {
                    leadsQuery = leadsQuery.Where(l => l.assigned_to == currentUserId);
                }
                else if (currentUserRole == 2) // Manager
                {
                    var teamMemberIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId && u.is_active)
                        .Select(u => u.user_id)
                        .ToListAsync();

                    teamMemberIds.Add(currentUserId);
                    leadsQuery = leadsQuery.Where(l => teamMemberIds.Contains(l.assigned_to));
                }
                // Admin sees all (no additional filtering)

                var totalLeads = await leadsQuery.CountAsync();

                // FIXED: Count opportunities with IDENTICAL role-based filtering as OpportunityService
                var opportunitiesQuery = _context.Opportunities
                    .Include(o => o.Lead)
                    .Where(o => !o.is_deleted)
                    .Where(o => o.Lead != null && !o.Lead.is_deleted);

                // Apply IDENTICAL role-based filtering as OpportunityService.GetOpportunitiesAsync()
                if (currentUserRole == 3) // Salesperson
                {
                    opportunitiesQuery = opportunitiesQuery.Where(o => o.assigned_to == currentUserId);
                }
                else if (currentUserRole == 2) // Manager
                {
                    var teamMemberIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId && u.is_active)
                        .Select(u => u.user_id)
                        .ToListAsync();

                    teamMemberIds.Add(currentUserId);
                    opportunitiesQuery = opportunitiesQuery.Where(o => teamMemberIds.Contains(o.assigned_to));
                }
                // Admin sees all (no additional filtering)

                var convertedOpportunities = await opportunitiesQuery.CountAsync();

                var conversionRate = totalLeads > 0 ? Math.Round((double)convertedOpportunities / totalLeads * 100, 1) : 0.0;

                // Calculate average days to convert
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

                // Get status breakdown
                var statusBreakdown = await leadsQuery
                    .GroupBy(l => l.Status.status_name)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Status ?? "", x => x.Count);

                // Get new leads count (status_id = 1)
                var newLeads = await leadsQuery
                    .Where(l => l.status_id == 1)
                    .CountAsync();

                return new LeadStatsDto
                {
                    TotalLeads = totalLeads,
                    NewLeads = newLeads,
                    ConvertedLeads = convertedOpportunities,
                    ConversionRate = conversionRate,
                    AverageDaysToConvert = (int)Math.Round(averageDaysToConvert),
                    StatusBreakdown = statusBreakdown
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetLeadStatsAsync failed: {ex.Message}");
                return new LeadStatsDto
                {
                    TotalLeads = 0,
                    NewLeads = 0,
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
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetLeadStatusesAsync failed: {ex.Message}");
                return new List<object>();
            }
        }

        /// <summary>
        /// Checks if the current user can access the specified lead based on role permissions
        /// </summary>
        private async Task<bool> CanAccessLeadAsync(Lead lead, int currentUserId, int currentUserRole)
        {
            try
            {
                if (currentUserRole == 1) return true; // Admin can access all

                if (currentUserRole == 3) return lead.assigned_to == currentUserId; // Salesperson can access own only

                if (currentUserRole == 2) // Manager can access own + team
                {
                    if (lead.assigned_to == currentUserId) return true;

                    var assignedUser = await _context.Users.FindAsync(lead.assigned_to);
                    return assignedUser?.manager_id == currentUserId;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] CanAccessLeadAsync failed: {ex.Message}");
                return false;
            }
        }
    }
}