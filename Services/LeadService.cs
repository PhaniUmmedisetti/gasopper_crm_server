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
            // Determine assignment - default to current user if not specified
            var assignedTo = createLeadDto.AssignedTo ?? currentUserId;

            // Validation: Only Admin/Manager can assign to others
            if (createLeadDto.AssignedTo.HasValue && createLeadDto.AssignedTo != currentUserId)
            {
                if (currentUserRole == 3) // Salesperson cannot assign to others
                    return null;

                if (currentUserRole == 2) // Manager can only assign to team members
                {
                    var isTeamMember = await _context.Users
                        .AnyAsync(u => u.user_id == assignedTo && u.manager_id == currentUserId);

                    if (!isTeamMember && assignedTo != currentUserId)
                        return null;
                }
            }

            // Create new lead
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
                status_id = 1 // Default to "New" status
            };

            _context.Leads.Add(lead);
            await _context.SaveChangesAsync();

            return await GetLeadByIdAsync(lead.lead_id, currentUserId, currentUserRole);
        }

        public async Task<LeadResponseDto?> GetLeadByIdAsync(int leadId, int currentUserId, int currentUserRole)
        {
            var query = BuildLeadQuery(currentUserId, currentUserRole);

            var lead = await query
                .FirstOrDefaultAsync(l => l.lead_id == leadId);

            if (lead == null)
                return null;

            return MapToLeadResponseDto(lead);
        }

        public async Task<List<LeadListResponseDto>> GetLeadsAsync(int currentUserId, int currentUserRole, bool includeDeleted = false)
        {
            var query = BuildLeadQuery(currentUserId, currentUserRole);

            if (!includeDeleted)
            {
                query = query.Where(l => !l.is_deleted);
            }

            var leads = await query
                .OrderByDescending(l => l.last_updated)
                .ToListAsync();

            return leads.Select(MapToLeadListResponseDto).ToList();
        }

        public async Task<LeadResponseDto?> UpdateLeadAsync(int leadId, UpdateLeadDto updateLeadDto, int currentUserId, int currentUserRole)
        {
            var lead = await _context.Leads.FindAsync(leadId);
            if (lead == null || !CanAccessLead(lead, currentUserId, currentUserRole))
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

            // Update referral information
            if (updateLeadDto.ReferralName != null)
                lead.referral_name = updateLeadDto.ReferralName;

            if (updateLeadDto.ReferralEmail != null)
                lead.referral_email = updateLeadDto.ReferralEmail;

            if (updateLeadDto.ReferralPhone != null)
                lead.referral_phone = updateLeadDto.ReferralPhone;

            if (updateLeadDto.ReferralAddress != null)
                lead.referral_address = updateLeadDto.ReferralAddress;

            // Handle assignment changes (Admin/Manager only)
            if (updateLeadDto.AssignedTo.HasValue && currentUserRole <= 2)
            {
                if (currentUserRole == 2) // Manager can only assign to team members
                {
                    var isTeamMember = await _context.Users
                        .AnyAsync(u => u.user_id == updateLeadDto.AssignedTo.Value &&
                                      (u.manager_id == currentUserId || u.user_id == currentUserId));

                    if (isTeamMember)
                        lead.assigned_to = updateLeadDto.AssignedTo.Value;
                }
                else if (currentUserRole == 1) // Admin can assign to anyone
                {
                    lead.assigned_to = updateLeadDto.AssignedTo.Value;
                }
            }

            await _context.SaveChangesAsync();
            return await GetLeadByIdAsync(leadId, currentUserId, currentUserRole);
        }

        public async Task<bool> DeleteLeadAsync(int leadId, int currentUserId, int currentUserRole)
        {
            var lead = await _context.Leads.FindAsync(leadId);
            if (lead == null || !CanAccessLead(lead, currentUserId, currentUserRole))
                return false;

            // Soft delete
            lead.is_deleted = true;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<LeadResponseDto?> UpdateLeadStatusAsync(int leadId, UpdateLeadStatusDto updateStatusDto, int currentUserId, int currentUserRole)
        {
            var lead = await _context.Leads.FindAsync(leadId);
            if (lead == null || !CanAccessLead(lead, currentUserId, currentUserRole))
                return null;

            // SIMPLIFIED: Only allow status 1 (New) or 2 (Converted)
            if (updateStatusDto.StatusId < 1 || updateStatusDto.StatusId > 2)
                return null;

            lead.status_id = updateStatusDto.StatusId;
            await _context.SaveChangesAsync();

            return await GetLeadByIdAsync(leadId, currentUserId, currentUserRole);
        }

        public async Task<LeadResponseDto?> ConvertToOpportunityAsync(int leadId, ConvertLeadToOpportunityDto convertDto, int currentUserId, int currentUserRole)
        {
            var lead = await _context.Leads
                .Include(l => l.Opportunity)
                .FirstOrDefaultAsync(l => l.lead_id == leadId);

            if (lead == null || !CanAccessLead(lead, currentUserId, currentUserRole))
                return null;

            // Check if already converted
            if (lead.Opportunity != null)
                return null;

            // Determine assignment
            var assignedTo = convertDto.AssignedTo ?? currentUserId;

            // Validate assignment permissions (same logic as lead creation)
            if (convertDto.AssignedTo.HasValue && convertDto.AssignedTo != currentUserId)
            {
                if (currentUserRole == 3) // Salesperson cannot assign to others
                    return null;

                if (currentUserRole == 2) // Manager can only assign to team members
                {
                    var isTeamMember = await _context.Users
                        .AnyAsync(u => u.user_id == assignedTo && u.manager_id == currentUserId);

                    if (!isTeamMember && assignedTo != currentUserId)
                        return null;
                }
            }

            // Create opportunity
            var opportunity = new Opportunity
            {
                lead_id = leadId,
                owner_name = convertDto.OwnerName,
                owner_address = convertDto.OwnerAddress,
                assigned_to = assignedTo,
                created_by = currentUserId,
                status_id = 1 // Default to "Active" status (using new opportunity_statuses table)
            };

            _context.Opportunities.Add(opportunity);

            // SIMPLIFIED: Update lead status to "Converted" (status_id = 2)
            lead.status_id = 2;

            await _context.SaveChangesAsync();
            return await GetLeadByIdAsync(leadId, currentUserId, currentUserRole);
        }

        public async Task<List<LeadListResponseDto>> GetMyLeadsAsync(int currentUserId)
        {
            var leads = await _context.Leads
                .Include(l => l.AssignedToUser)
                .Include(l => l.Status)
                .Include(l => l.Opportunity)
                .Where(l => l.assigned_to == currentUserId && !l.is_deleted)
                .OrderByDescending(l => l.last_updated)
                .ToListAsync();

            return leads.Select(MapToLeadListResponseDto).ToList();
        }

        public async Task<List<LeadListResponseDto>> GetTeamLeadsAsync(int managerId)
        {
            var teamMemberIds = await _context.Users
                .Where(u => u.manager_id == managerId && u.is_active)
                .Select(u => u.user_id)
                .ToListAsync();

            teamMemberIds.Add(managerId); // Include manager's own leads

            var leads = await _context.Leads
                .Include(l => l.AssignedToUser)
                .Include(l => l.Status)
                .Include(l => l.Opportunity)
                .Where(l => teamMemberIds.Contains(l.assigned_to) && !l.is_deleted)
                .OrderByDescending(l => l.last_updated)
                .ToListAsync();

            return leads.Select(MapToLeadListResponseDto).ToList();
        }

        // SIMPLIFIED: Only New vs Converted stats
        public async Task<LeadStatsDto> GetLeadStatsAsync(int currentUserId, int currentUserRole)
        {
            var query = BuildLeadQuery(currentUserId, currentUserRole)
                .Where(l => !l.is_deleted);

            var totalLeads = await query.CountAsync();

            // SIMPLIFIED: New leads (status_id = 1)
            var newLeads = await query.CountAsync(l => l.status_id == 1);

            // SIMPLIFIED: Converted leads (status_id = 2)
            var convertedLeads = await query.CountAsync(l => l.status_id == 2);

            // Conversion rate
            var conversionRate = totalLeads > 0 ? Math.Round((double)convertedLeads / totalLeads * 100, 1) : 0.0;

            // Average days to convert (only for converted leads)
            var convertedLeadsWithDates = await query
                .Where(l => l.status_id == 2 && l.Opportunity != null)
                .Select(l => new { l.created_at, OpportunityCreated = l.Opportunity!.created_at })
                .ToListAsync();

            var averageDaysToConvert = 0;
            if (convertedLeadsWithDates.Any())
            {
                var totalDays = convertedLeadsWithDates
                    .Sum(x => (x.OpportunityCreated - x.created_at).Days);
                averageDaysToConvert = totalDays / convertedLeadsWithDates.Count;
            }

            // SIMPLIFIED: Status breakdown (only "New" and "Converted")
            var statusBreakdown = new Dictionary<string, int>
            {
                { "New", newLeads },
                { "Converted", convertedLeads }
            };

            return new LeadStatsDto
            {
                TotalLeads = totalLeads,
                NewLeads = newLeads,
                ConvertedLeads = convertedLeads,
                ConversionRate = conversionRate,
                AverageDaysToConvert = averageDaysToConvert,
                StatusBreakdown = statusBreakdown
            };
        }

        // SIMPLIFIED: Only return essential status info
        public async Task<List<object>> GetLeadStatusesAsync()
        {
            return await _context.LeadStatuses
                .Select(s => new
                {
                    id = s.status_id,
                    name = s.status_name,
                    description = s.description
                })
                .ToListAsync<object>();
        }

        private IQueryable<Lead> BuildLeadQuery(int currentUserId, int currentUserRole)
        {
            var query = _context.Leads
                .Include(l => l.AssignedToUser)
                .Include(l => l.CreatedByUser)
                .Include(l => l.Status)
                .Include(l => l.Opportunity)
                    .ThenInclude(o => o!.OpportunityStatus) // Updated from Stage to Status
                .AsQueryable();

            // Apply role-based filtering
            if (currentUserRole == 3) // Salesperson can only see their own leads
            {
                query = query.Where(l => l.assigned_to == currentUserId);
            }
            else if (currentUserRole == 2) // Manager can see own + team leads
            {
                var teamMemberIds = _context.Users
                    .Where(u => u.manager_id == currentUserId && u.is_active)
                    .Select(u => u.user_id);

                query = query.Where(l => l.assigned_to == currentUserId || teamMemberIds.Contains(l.assigned_to));
            }
            // Admin can see all leads (no additional filtering)

            return query;
        }

        private bool CanAccessLead(Lead lead, int currentUserId, int currentUserRole)
        {
            if (currentUserRole == 1) // Admin can access all
                return true;

            if (currentUserRole == 3) // Salesperson can only access their own
                return lead.assigned_to == currentUserId;

            if (currentUserRole == 2) // Manager can access own + team
            {
                if (lead.assigned_to == currentUserId)
                    return true;

                // Check if assigned user is in manager's team
                var assignedUser = _context.Users.Find(lead.assigned_to);
                return assignedUser?.manager_id == currentUserId;
            }

            return false;
        }

        // CLEANED: Removed color_code, is_final, stage references
        private static LeadResponseDto MapToLeadResponseDto(Lead lead)
        {
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
                StatusName = lead.Status?.status_name,
                AssignedTo = lead.assigned_to,
                AssignedToName = $"{lead.AssignedToUser.first_name} {lead.AssignedToUser.last_name}",
                CreatedBy = lead.created_by,
                CreatedByName = $"{lead.CreatedByUser.first_name} {lead.CreatedByUser.last_name}",
                CreatedAt = lead.created_at,
                LastUpdated = lead.last_updated,
                OpportunityId = lead.Opportunity?.opportunity_id,
                OpportunityStatus = lead.Opportunity?.OpportunityStatus?.status_name, // Updated from Stage
                IsDeleted = lead.is_deleted
            };
        }

        // CLEANED: Removed color_code references
        private static LeadListResponseDto MapToLeadListResponseDto(Lead lead)
        {
            return new LeadListResponseDto
            {
                LeadId = lead.lead_id,
                Name = lead.name,
                Email = lead.email,
                PhoneNumber = lead.phone_number,
                ExpectedStations = lead.expected_stations,
                StatusName = lead.Status?.status_name,
                AssignedToName = $"{lead.AssignedToUser.first_name} {lead.AssignedToUser.last_name}",
                CreatedAt = lead.created_at,
                LastUpdated = lead.last_updated,
                HasOpportunity = lead.Opportunity != null,
                IsDeleted = lead.is_deleted
            };
        }
    }
}