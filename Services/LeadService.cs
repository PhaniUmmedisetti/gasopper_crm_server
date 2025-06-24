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
                    .Include(l => l.Opportunity)
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
                    OpportunityId = lead.Opportunity?.opportunity_id,
                    HasOpportunity = lead.Opportunity != null,
                    OpportunityStatus = lead.Opportunity?.OpportunityStatus?.status_name ?? "",
                    IsDeleted = lead.is_deleted
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<List<LeadListResponseDto>> GetLeadsAsync(int currentUserId, int currentUserRole, bool includeDeleted = false)
        {
            try
            {
                var query = _context.Leads
                    .Include(l => l.AssignedToUser)
                    .Include(l => l.Status)
                    .Include(l => l.Opportunity)
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

                return leads.Select(l => new LeadListResponseDto
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
                    HasOpportunity = l.Opportunity != null,
                    IsDeleted = l.is_deleted
                }).ToList();
            }
            catch (Exception)
            {
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

        public async Task<LeadResponseDto?> ConvertToOpportunityAsync(int leadId, ConvertLeadToOpportunityDto convertDto, int currentUserId, int currentUserRole)
        {
            try
            {
                var lead = await _context.Leads
                    .Include(l => l.Opportunity)
                    .FirstOrDefaultAsync(l => l.lead_id == leadId);

                if (lead == null || !await CanAccessLeadAsync(lead, currentUserId, currentUserRole) || lead.Opportunity != null)
                    return null;

                var opportunity = new Opportunity
                {
                    lead_id = leadId,
                    owner_name = convertDto.OwnerName,
                    owner_address = convertDto.OwnerAddress,
                    assigned_to = convertDto.AssignedTo ?? lead.assigned_to,
                    created_by = currentUserId,
                    status_id = 1 // Active
                };

                _context.Opportunities.Add(opportunity);

                // Set lead to converted status - works with both schemas
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

        // Replace ONLY the GetLeadStatsAsync method in your LeadService.cs

        public async Task<LeadStatsDto> GetLeadStatsAsync(int currentUserId, int currentUserRole)
        {
            try
            {
                // Build base query with role-based filtering
                var query = _context.Leads.Where(l => !l.is_deleted);

                // Apply role-based filtering
                if (currentUserRole == 3) // Salesperson
                {
                    query = query.Where(l => l.assigned_to == currentUserId);
                }
                else if (currentUserRole == 2) // Manager
                {
                    var teamMemberIds = await _context.Users
                        .Where(u => u.manager_id == currentUserId && u.is_active)
                        .Select(u => u.user_id)
                        .ToListAsync();

                    teamMemberIds.Add(currentUserId);
                    query = query.Where(l => teamMemberIds.Contains(l.assigned_to));
                }
                // Admin sees all (no additional filtering)

                // Get all leads data we need
                var leadsWithOpportunities = await query
                    .Include(l => l.Opportunity)
                    .ToListAsync();

                var totalLeads = leadsWithOpportunities.Count;

                // CRITICAL FIX: Count converted leads by checking if they have opportunities
                // This matches exactly what your frontend does: hasOpportunity: true
                var convertedLeads = leadsWithOpportunities
                    .Count(l => l.Opportunity != null && !l.Opportunity.is_deleted);

                var newLeads = totalLeads - convertedLeads; // Simple calculation

                var conversionRate = totalLeads > 0 ? Math.Round((double)convertedLeads / totalLeads * 100, 1) : 0.0;

                // Calculate average days to convert
                var avgDaysToConvert = 0;
                try
                {
                    var convertedLeadsWithDates = leadsWithOpportunities
                        .Where(l => l.Opportunity != null && !l.Opportunity.is_deleted)
                        .ToList();

                    if (convertedLeadsWithDates.Any())
                    {
                        var totalDays = convertedLeadsWithDates
                            .Sum(l => (l.Opportunity.created_at - l.created_at).Days);
                        avgDaysToConvert = totalDays / convertedLeadsWithDates.Count;
                    }
                }
                catch
                {
                    avgDaysToConvert = 0;
                }

                // Status breakdown - matches frontend expectations
                var statusBreakdown = new Dictionary<string, int>
        {
            { "New", newLeads },
            { "Converted", convertedLeads }
        };

                return new LeadStatsDto
                {
                    TotalLeads = totalLeads,
                    NewLeads = newLeads,
                    ConvertedLeads = convertedLeads, // This now matches exactly what frontend counts
                    ConversionRate = conversionRate,
                    AverageDaysToConvert = avgDaysToConvert,
                    StatusBreakdown = statusBreakdown
                };
            }
            catch (Exception ex)
            {
                // Log the exception for debugging
                Console.WriteLine($"Error in GetLeadStatsAsync: {ex.Message}");

                // Return empty stats if error occurs
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
                    .OrderBy(s => s.status_id) // Use status_id instead of status_order
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