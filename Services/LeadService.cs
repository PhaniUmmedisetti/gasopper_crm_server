using Microsoft.EntityFrameworkCore;
using gasopper_crm_server.Data;
using gasopper_crm_server.DTOs;
using gasopper_crm_server.Models;

namespace gasopper_crm_server.Services
{
    public interface ILeadService
    {
        Task<LeadResponseDto?> CreateLeadAsync(CreateLeadDto createLeadDto, int currentUserId, int currentUserRole);
        Task<LeadResponseDto?> GetLeadByIdAsync(int leadId, int currentUserId, int currentUserRole);
        Task<List<LeadListResponseDto>> GetLeadsAsync(int currentUserId, int currentUserRole, bool includeDeleted = false);
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
                // Salespeople can create leads assigned to themselves
                var assignedTo = createLeadDto.AssignedTo ?? currentUserId;

                // Only Admin/Manager can assign to others
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
                    status_id = 1 // New
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

                // Apply role-based filtering
                if (currentUserRole == 3) // Salesperson - own only
                {
                    query = query.Where(l => l.assigned_to == currentUserId);
                }
                else if (currentUserRole == 2) // Manager - own + team
                {
                    var teamIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId && u.is_active)
                        .Select(u => u.user_id)
                        .ToListAsync();
                    teamIds.Add(currentUserId);

                    query = query.Where(l => teamIds.Contains(l.assigned_to));
                }
                // Admin sees all

                var lead = await query.FirstOrDefaultAsync();
                if (lead == null) return null;

                // FIXED: Check for opportunity dynamically with role-based filtering
                var hasOpportunity = await _context.Opportunities
                    .Where(o => o.lead_id == leadId && !o.is_deleted)
                    .Where(o => currentUserRole == 1 || // Admin sees all
                               (currentUserRole == 3 && o.assigned_to == currentUserId) || // Salesperson - own only
                               (currentUserRole == 2 && ( // Manager - own + team
                                   o.assigned_to == currentUserId ||
                                   _context.Users.Any(u => u.user_id == o.assigned_to && u.manager_id == currentUserId && u.is_active)
                               )))
                    .AnyAsync();

                var opportunity = hasOpportunity ? await _context.Opportunities
                    .Include(o => o.OpportunityStatus)
                    .Where(o => o.lead_id == leadId && !o.is_deleted)
                    .Where(o => currentUserRole == 1 || // Admin sees all
                               (currentUserRole == 3 && o.assigned_to == currentUserId) || // Salesperson - own only
                               (currentUserRole == 2 && ( // Manager - own + team
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
                    AssignedToName = $"{lead.AssignedToUser?.first_name ?? ""} {lead.AssignedToUser?.last_name ?? ""}".Trim(),
                    CreatedBy = lead.created_by,
                    CreatedByName = $"{lead.CreatedByUser?.first_name ?? ""} {lead.CreatedByUser?.last_name ?? ""}".Trim(),
                    CreatedAt = lead.created_at,
                    LastUpdated = lead.last_updated,
                    OpportunityId = opportunity?.opportunity_id,
                    HasOpportunity = hasOpportunity, // FIXED: Dynamic calculation
                    OpportunityStatus = opportunity?.OpportunityStatus?.status_name ?? "",
                    IsDeleted = lead.is_deleted
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        // TEMPORARY DEBUG VERSION - Replace GetLeadsAsync method with this:

        public async Task<List<LeadListResponseDto>> GetLeadsAsync(int currentUserId, int currentUserRole, bool includeDeleted = false)
        {
            try
            {
                Console.WriteLine($"[DEBUG] GetLeadsAsync - UserId: {currentUserId}, Role: {currentUserRole}");

                var query = _context.Leads
                    .Include(l => l.AssignedToUser)
                    .Include(l => l.Status)
                    .AsQueryable();

                if (!includeDeleted)
                    query = query.Where(l => !l.is_deleted);

                // Apply role-based filtering
                if (currentUserRole == 3) // Salesperson - own only
                {
                    query = query.Where(l => l.assigned_to == currentUserId);
                }
                else if (currentUserRole == 2) // Manager - own + team
                {
                    var teamIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId && u.is_active)
                        .Select(u => u.user_id)
                        .ToListAsync();
                    teamIds.Add(currentUserId);

                    query = query.Where(l => teamIds.Contains(l.assigned_to));
                }
                // Admin sees all

                var leads = await query
                    .OrderByDescending(l => l.last_updated)
                    .ToListAsync();

                Console.WriteLine($"[DEBUG] Found {leads.Count} leads for user");

                // SIMPLIFIED: Get ALL opportunities without role filtering first to debug
                var allOpportunities = await _context.Opportunities
                    .Where(o => !o.is_deleted) // FIXED: Removed lead_id null check since it's not nullable
                    .Select(o => new { o.lead_id, o.assigned_to, o.opportunity_id })
                    .ToListAsync();

                Console.WriteLine($"[DEBUG] Found {allOpportunities.Count} total opportunities");
                Console.WriteLine($"[DEBUG] Opportunity lead_ids: [{string.Join(", ", allOpportunities.Select(o => o.lead_id))}]");

                // Get opportunities that match current user's access
                var accessibleOpportunityLeadIds = new List<int>();

                if (currentUserRole == 1) // Admin - all opportunities
                {
                    accessibleOpportunityLeadIds = allOpportunities.Select(o => o.lead_id).ToList(); // FIXED: Removed .Value
                }
                else if (currentUserRole == 3) // Salesperson - own only
                {
                    accessibleOpportunityLeadIds = allOpportunities
                        .Where(o => o.assigned_to == currentUserId)
                        .Select(o => o.lead_id) // FIXED: Removed .Value
                        .ToList();
                }
                else if (currentUserRole == 2) // Manager - own + team
                {
                    var teamIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId && u.is_active)
                        .Select(u => u.user_id)
                        .ToListAsync();
                    teamIds.Add(currentUserId);

                    accessibleOpportunityLeadIds = allOpportunities
                        .Where(o => teamIds.Contains(o.assigned_to))
                        .Select(o => o.lead_id) // FIXED: Removed .Value
                        .ToList();
                }

                Console.WriteLine($"[DEBUG] Accessible opportunity lead_ids for role {currentUserRole}: [{string.Join(", ", accessibleOpportunityLeadIds)}]");
                Console.WriteLine($"[DEBUG] Lead IDs: [{string.Join(", ", leads.Select(l => l.lead_id))}]");

                var result = leads.Select(l =>
                {
                    var hasOpportunity = accessibleOpportunityLeadIds.Contains(l.lead_id);
                    Console.WriteLine($"[DEBUG] Lead {l.lead_id} ({l.name}) - hasOpportunity: {hasOpportunity}");

                    return new LeadListResponseDto
                    {
                        LeadId = l.lead_id,
                        Name = l.name,
                        Email = l.email,
                        PhoneNumber = l.phone_number,
                        ExpectedStations = l.expected_stations,
                        StatusName = l.Status?.status_name ?? "",
                        AssignedToName = $"{l.AssignedToUser?.first_name ?? ""} {l.AssignedToUser?.last_name ?? ""}".Trim(),
                        CreatedAt = l.created_at,
                        LastUpdated = l.last_updated,
                        HasOpportunity = hasOpportunity,
                        IsDeleted = l.is_deleted
                    };
                }).ToList();

                var convertedCount = result.Count(r => r.HasOpportunity);
                Console.WriteLine($"[DEBUG] Final result: {result.Count} leads, {convertedCount} converted");

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetLeadsAsync failed: {ex.Message}");
                return new List<LeadListResponseDto>();
            }
        }

        public async Task<LeadResponseDto?> UpdateLeadAsync(int leadId, UpdateLeadDto updateLeadDto, int currentUserId, int currentUserRole)
        {
            try
            {
                var lead = await _context.Leads.FindAsync(leadId);
                if (lead == null || !await CanAccessLeadAsync(lead, currentUserId, currentUserRole))
                    return null;

                // Update fields
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

                // Handle referral information updates
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

                // Flexible status validation - works with both schemas
                if (updateStatusDto.StatusId < 1 || updateStatusDto.StatusId > 7)
                    return null;

                // Verify the status exists in the database
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

        // REPLACE the ConvertToOpportunityAsync method in your existing LeadService.cs

        public async Task<LeadResponseDto?> ConvertToOpportunityAsync(int leadId, ConvertLeadToOpportunityDto convertDto, int currentUserId, int currentUserRole)
        {
            try
            {
                var lead = await _context.Leads
                    .Include(l => l.Opportunity)
                    .FirstOrDefaultAsync(l => l.lead_id == leadId && !l.is_deleted);

                if (lead == null || !await CanAccessLeadAsync(lead, currentUserId, currentUserRole))
                    return null;

                // Check if already converted
                if (lead.Opportunity != null && !lead.Opportunity.is_deleted)
                    return null;

                // UPDATED: Create opportunity with split address fields AND actual_stations
                var opportunity = new Opportunity
                {
                    lead_id = leadId,
                    owner_name = convertDto.OwnerName,
                    address_line_1 = convertDto.AddressLine1, // UPDATED
                    address_line_2 = convertDto.AddressLine2, // UPDATED
                    city = convertDto.City, // UPDATED
                    state = convertDto.State, // UPDATED
                    postal_code = convertDto.PostalCode, // UPDATED
                    country = convertDto.Country ?? "United States", // UPDATED
                    actual_stations = convertDto.ActualStations, // ADDED: Save actual stations count
                    assigned_to = convertDto.AssignedTo ?? lead.assigned_to,
                    created_by = currentUserId,
                    status_id = 1 // Active
                };

                _context.Opportunities.Add(opportunity);

                // Set lead to converted status
                var convertedStatus = await _context.LeadStatuses
                    .Where(s => s.status_name.ToLower().Contains("converted"))
                    .Select(s => s.status_id)
                    .FirstOrDefaultAsync();

                lead.status_id = convertedStatus > 0 ? convertedStatus : 2; // Default to 2 if not found

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
            return await GetLeadsAsync(currentUserId, 3, false); // Force salesperson view
        }

        public async Task<List<LeadListResponseDto>> GetTeamLeadsAsync(int managerId)
        {
            return await GetLeadsAsync(managerId, 2, false); // Force manager view
        }


        // REPLACE ONLY the GetLeadStatsAsync method in your LeadService.cs

        public async Task<LeadStatsDto> GetLeadStatsAsync(int currentUserId, int currentUserRole)
        {
            try
            {
                // Get accessible leads with SAME role-based filtering as your existing GetLeadsAsync method
                var leadsQuery = _context.Leads
                    .Include(l => l.Status)
                    .Where(l => !l.is_deleted);

                // Apply IDENTICAL role-based filtering as your existing GetLeadsAsync method  
                if (currentUserRole == 3) // Salesperson can only see their own leads
                {
                    leadsQuery = leadsQuery.Where(l => l.assigned_to == currentUserId);
                }
                else if (currentUserRole == 2) // Manager can see own + team leads
                {
                    var teamMemberIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId && u.is_active)
                        .Select(u => u.user_id)
                        .ToListAsync();

                    teamMemberIds.Add(currentUserId);
                    leadsQuery = leadsQuery.Where(l => teamMemberIds.Contains(l.assigned_to));
                }
                // Admin can see all leads (no additional filtering)

                // Calculate lead statistics from accessible leads
                var totalLeads = await leadsQuery.CountAsync();

                // FIXED: Count VALID converted opportunities from opportunities table with SAME role-based filtering
                var opportunitiesQuery = _context.Opportunities
                    .Where(o => !o.is_deleted)
                    .Where(o => o.lead_id != null) // CRITICAL: Only count opportunities with valid lead_id
                    .Join(_context.Leads.Where(l => !l.is_deleted), // CRITICAL: Join with non-deleted leads
                          o => o.lead_id,
                          l => l.lead_id,
                          (o, l) => o); // This ensures we only count opportunities with existing, non-deleted leads

                // Apply SAME role-based filtering to opportunities
                if (currentUserRole == 3) // Salesperson can only see their own opportunities
                {
                    opportunitiesQuery = opportunitiesQuery.Where(o => o.assigned_to == currentUserId);
                }
                else if (currentUserRole == 2) // Manager can see own + team opportunities
                {
                    var teamMemberIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId && u.is_active)
                        .Select(u => u.user_id)
                        .ToListAsync();

                    teamMemberIds.Add(currentUserId);
                    opportunitiesQuery = opportunitiesQuery.Where(o => teamMemberIds.Contains(o.assigned_to));
                }
                // Admin can see all opportunities (no additional filtering)

                var convertedOpportunities = await opportunitiesQuery.CountAsync();

                // Calculate conversion rate based on actual VALID opportunity count
                var conversionRate = totalLeads > 0 ? Math.Round((double)convertedOpportunities / totalLeads * 100, 1) : 0.0;

                // Calculate average days to convert - only for VALID conversions
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

                // Status breakdown from accessible leads
                var statusBreakdown = await leadsQuery
                    .GroupBy(l => l.Status.status_name)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Status ?? "", x => x.Count);

                return new LeadStatsDto
                {
                    TotalLeads = totalLeads,
                    ConvertedLeads = convertedOpportunities,  // FIXED: Use VALID opportunity count from opportunities table
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
                if (currentUserRole == 1) return true; // Admin
                if (currentUserRole == 3) return lead.assigned_to == currentUserId; // Salesperson own only

                // Manager - check if it's their lead or team member's lead
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