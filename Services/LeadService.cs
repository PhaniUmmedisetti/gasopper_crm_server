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

        public async Task<LeadResponseDto?> GetLeadByIdAsync(int leadId, int currentUserId, int currentUserRole)
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
                AssignedToName = $"{lead.AssignedToUser?.first_name} {lead.AssignedToUser?.last_name}".Trim(),
                CreatedBy = lead.created_by,
                CreatedByName = $"{lead.CreatedByUser?.first_name} {lead.CreatedByUser?.last_name}".Trim(),
                CreatedAt = lead.created_at,
                LastUpdated = lead.last_updated,
                OpportunityId = lead.Opportunity?.opportunity_id,
                OpportunityStatus = lead.Opportunity?.OpportunityStatus?.status_name,
                IsDeleted = lead.is_deleted
            };
        }

        public async Task<List<LeadListResponseDto>> GetLeadsAsync(int currentUserId, int currentUserRole, bool includeDeleted = false)
        {
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

            return leads.Select(l => new LeadListResponseDto
            {
                LeadId = l.lead_id,
                Name = l.name,
                Email = l.email,
                PhoneNumber = l.phone_number,
                ExpectedStations = l.expected_stations,
                StatusName = l.Status?.status_name ?? "",
                AssignedToName = $"{l.AssignedToUser?.first_name} {l.AssignedToUser?.last_name}".Trim(),
                CreatedAt = l.created_at,
                LastUpdated = l.last_updated,
                HasOpportunity = l.Opportunity != null,
                IsDeleted = l.is_deleted
            }).ToList();
        }

        public async Task<LeadResponseDto?> UpdateLeadAsync(int leadId, UpdateLeadDto updateLeadDto, int currentUserId, int currentUserRole)
        {
            var lead = await _context.Leads.FindAsync(leadId);
            if (lead == null || !CanAccessLead(lead, currentUserId, currentUserRole))
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

            await _context.SaveChangesAsync();
            return await GetLeadByIdAsync(leadId, currentUserId, currentUserRole);
        }

        public async Task<bool> DeleteLeadAsync(int leadId, int currentUserId, int currentUserRole)
        {
            var lead = await _context.Leads.FindAsync(leadId);
            if (lead == null || !CanAccessLead(lead, currentUserId, currentUserRole))
                return false;

            lead.is_deleted = true;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<LeadResponseDto?> UpdateLeadStatusAsync(int leadId, UpdateLeadStatusDto updateStatusDto, int currentUserId, int currentUserRole)
        {
            var lead = await _context.Leads.FindAsync(leadId);
            if (lead == null || !CanAccessLead(lead, currentUserId, currentUserRole))
                return null;

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

            if (lead == null || !CanAccessLead(lead, currentUserId, currentUserRole) || lead.Opportunity != null)
                return null;

            var opportunity = new Opportunity
            {
                lead_id = leadId,
                owner_name = convertDto.OwnerName,
                owner_address = convertDto.OwnerAddress,
                assigned_to = convertDto.AssignedTo ?? currentUserId,
                created_by = currentUserId,
                status_id = 1 // Active
            };

            _context.Opportunities.Add(opportunity);
            lead.status_id = 2; // Converted

            await _context.SaveChangesAsync();
            return await GetLeadByIdAsync(leadId, currentUserId, currentUserRole);
        }

        public async Task<List<LeadListResponseDto>> GetMyLeadsAsync(int currentUserId)
        {
            return await GetLeadsAsync(currentUserId, 3, false); // Force salesperson view
        }

        public async Task<List<LeadListResponseDto>> GetTeamLeadsAsync(int managerId)
        {
            return await GetLeadsAsync(managerId, 2, false); // Force manager view
        }

        public async Task<LeadStatsDto> GetLeadStatsAsync(int currentUserId, int currentUserRole)
        {
            var leads = await GetLeadsAsync(currentUserId, currentUserRole, false);
            
            var total = leads.Count;
            var newLeads = leads.Count(l => l.StatusName == "New");
            var converted = leads.Count(l => l.StatusName == "Converted");
            var conversionRate = total > 0 ? Math.Round((double)converted / total * 100, 1) : 0.0;

            return new LeadStatsDto
            {
                TotalLeads = total,
                NewLeads = newLeads,
                ConvertedLeads = converted,
                ConversionRate = conversionRate,
                AverageDaysToConvert = 0,
                StatusBreakdown = new Dictionary<string, int>
                {
                    { "New", newLeads },
                    { "Converted", converted }
                }
            };
        }

        public async Task<List<object>> GetLeadStatusesAsync()
        {
            return await _context.LeadStatuses
                .Select(s => new
                {
                    id = s.status_id,
                    name = s.status_name,
                    description = s.description ?? ""
                })
                .ToListAsync<object>();
        }

        private bool CanAccessLead(Lead lead, int currentUserId, int currentUserRole)
        {
            if (currentUserRole == 1) return true; // Admin
            if (currentUserRole == 3) return lead.assigned_to == currentUserId; // Salesperson own only
            
            // Manager - check if it's their lead or team member's lead
            if (currentUserRole == 2)
            {
                if (lead.assigned_to == currentUserId) return true;
                
                var assignedUser = _context.Users.Find(lead.assigned_to);
                return assignedUser?.manager_id == currentUserId;
            }
            
            return false;
        }
    }
}