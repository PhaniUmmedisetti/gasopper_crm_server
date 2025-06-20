using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using gasopper_crm_server.DTOs;
using gasopper_crm_server.Services;

namespace gasopper_crm_server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // ALL endpoints require authentication
    public class LeadsController : ControllerBase
    {
        private readonly ILeadService _leadService;

        public LeadsController(ILeadService leadService)
        {
            _leadService = leadService;
        }

        // ✅ ALL AUTHENTICATED USERS can view leads (including Salespeople)
        // Role-based filtering applied in service layer
        [HttpGet]
        public async Task<IActionResult> GetLeads([FromQuery] bool includeDeleted = false)
        {
            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            var leads = await _leadService.GetLeadsAsync(currentUserId, currentUserRole, includeDeleted);
            
            return Ok(new { data = leads, count = leads.Count });
        }

        // ✅ ALL AUTHENTICATED USERS can view single lead (including Salespeople)
        [HttpGet("{id}")]
        public async Task<IActionResult> GetLead(int id)
        {
            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            var lead = await _leadService.GetLeadByIdAsync(id, currentUserId, currentUserRole);
            
            if (lead == null)
                return NotFound(new { message = "Lead not found or access denied" });
            
            return Ok(lead);
        }

        // ✅ ALL AUTHENTICATED USERS can create leads (INCLUDING SALESPEOPLE)
        // This is the main job of salespeople - they MUST be able to create leads
        [HttpPost]
        public async Task<IActionResult> CreateLead([FromBody] CreateLeadDto createLeadDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            var lead = await _leadService.CreateLeadAsync(createLeadDto, currentUserId, currentUserRole);
            
            if (lead == null)
                return BadRequest(new { message = "Unable to create lead. Check assignment permissions." });
            
            return CreatedAtAction(nameof(GetLead), new { id = lead.LeadId }, lead);
        }

        // ✅ ALL AUTHENTICATED USERS can update their accessible leads (including Salespeople)
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateLead(int id, [FromBody] UpdateLeadDto updateLeadDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            var lead = await _leadService.UpdateLeadAsync(id, updateLeadDto, currentUserId, currentUserRole);
            
            if (lead == null)
                return NotFound(new { message = "Lead not found or access denied" });
            
            return Ok(lead);
        }

        // ✅ ALL AUTHENTICATED USERS can delete their accessible leads (including Salespeople)
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteLead(int id)
        {
            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            var success = await _leadService.DeleteLeadAsync(id, currentUserId, currentUserRole);
            
            if (!success)
                return NotFound(new { message = "Lead not found or access denied" });
            
            return Ok(new { message = "Lead deleted successfully" });
        }

        // ✅ ALL AUTHENTICATED USERS can update status of their accessible leads (including Salespeople)
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateLeadStatus(int id, [FromBody] UpdateLeadStatusDto updateStatusDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            var lead = await _leadService.UpdateLeadStatusAsync(id, updateStatusDto, currentUserId, currentUserRole);
            
            if (lead == null)
                return NotFound(new { message = "Lead not found, access denied, or invalid status" });
            
            return Ok(lead);
        }

        // ✅ ALL AUTHENTICATED USERS can convert their accessible leads (including Salespeople)
        [HttpPost("{id}/convert-to-opportunity")]
        public async Task<IActionResult> ConvertToOpportunity(int id, [FromBody] ConvertLeadToOpportunityDto convertDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            var lead = await _leadService.ConvertToOpportunityAsync(id, convertDto, currentUserId, currentUserRole);
            
            if (lead == null)
                return BadRequest(new { message = "Unable to convert lead. Lead may not exist, already be converted, or access denied." });
            
            return Ok(new { 
                message = "Lead converted to opportunity successfully",
                lead = lead,
                opportunityId = lead.OpportunityId
            });
        }

        // ✅ ALL AUTHENTICATED USERS can view their own leads (including Salespeople)
        [HttpGet("my-leads")]
        public async Task<IActionResult> GetMyLeads()
        {
            var (currentUserId, _) = GetCurrentUserInfo();
            var leads = await _leadService.GetMyLeadsAsync(currentUserId);
            
            return Ok(new { data = leads, count = leads.Count });
        }

        // 🔒 ONLY Manager/Admin can view team leads (Salespeople cannot see team data)
        [HttpGet("team-leads")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> GetTeamLeads()
        {
            var (currentUserId, _) = GetCurrentUserInfo();
            var leads = await _leadService.GetTeamLeadsAsync(currentUserId);
            
            return Ok(new { data = leads, count = leads.Count });
        }

        // ✅ ALL AUTHENTICATED USERS can view their stats (including Salespeople)
        [HttpGet("stats")]
        public async Task<IActionResult> GetLeadStats()
        {
            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            var stats = await _leadService.GetLeadStatsAsync(currentUserId, currentUserRole);
            
            return Ok(stats);
        }

        // ✅ ALL AUTHENTICATED USERS can view available statuses (including Salespeople)
        [HttpGet("statuses")]
        public async Task<IActionResult> GetLeadStatuses()
        {
            var statuses = await _leadService.GetLeadStatusesAsync();
            return Ok(statuses);
        }

        // 🔒 ONLY Manager/Admin can reassign leads (Salespeople cannot reassign)
        [HttpPut("{id}/assign")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> AssignLead(int id, [FromBody] AssignLeadDto assignDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var updateDto = new UpdateLeadDto { AssignedTo = assignDto.AssignedTo };
            
            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            var lead = await _leadService.UpdateLeadAsync(id, updateDto, currentUserId, currentUserRole);
            
            if (lead == null)
                return NotFound(new { message = "Lead not found or assignment not allowed" });
            
            return Ok(new { 
                message = "Lead assigned successfully",
                lead = lead
            });
        }

        private (int userId, int roleId) GetCurrentUserInfo()
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            var userId = int.Parse(identity?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var roleId = int.Parse(identity?.FindFirst("role_id")?.Value ?? "0");
            
            return (userId, roleId);
        }
    }
}