using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using gasopper_crm_server.DTOs;
using gasopper_crm_server.Services;

namespace gasopper_crm_server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // ALL endpoints require authentication, but no specific role restrictions
    public class OpportunitiesController : ControllerBase
    {
        private readonly IOpportunityService _opportunityService;

        public OpportunitiesController(IOpportunityService opportunityService)
        {
            _opportunityService = opportunityService;
        }

        // ✅ ALL AUTHENTICATED USERS can view opportunities (role-based filtering in service)
        [HttpGet]
        public async Task<IActionResult> GetOpportunities([FromQuery] bool includeDeleted = false)
        {
            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            var opportunities = await _opportunityService.GetOpportunitiesAsync(currentUserId, currentUserRole, includeDeleted);
            
            return Ok(new { data = opportunities, count = opportunities.Count });
        }

        // ✅ ALL AUTHENTICATED USERS can view single opportunity (role-based access check in service)
        [HttpGet("{id}")]
        public async Task<IActionResult> GetOpportunity(int id)
        {
            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            var opportunity = await _opportunityService.GetOpportunityByIdAsync(id, currentUserId, currentUserRole);
            
            if (opportunity == null)
                return NotFound(new { message = "Opportunity not found or access denied" });
            
            return Ok(opportunity);
        }

        // 🚫 NO POST endpoint - opportunities are created via lead conversion only

        // ✅ ALL AUTHENTICATED USERS can update their accessible opportunities
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateOpportunity(int id, [FromBody] UpdateOpportunityDto updateDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            var opportunity = await _opportunityService.UpdateOpportunityAsync(id, updateDto, currentUserId, currentUserRole);
            
            if (opportunity == null)
                return NotFound(new { message = "Opportunity not found or access denied" });
            
            return Ok(opportunity);
        }

        // ✅ ALL AUTHENTICATED USERS can update status of their accessible opportunities
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateOpportunityStatus(int id, [FromBody] UpdateOpportunityStatusDto statusDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            var opportunity = await _opportunityService.UpdateOpportunityStatusAsync(id, statusDto, currentUserId, currentUserRole);
            
            if (opportunity == null)
                return NotFound(new { message = "Opportunity not found, access denied, or invalid status (must be 1-Active or 2-Complete)" });
            
            return Ok(opportunity);
        }

        // 🔒 ONLY Manager/Admin can reassign opportunities
        [HttpPut("{id}/assign")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> AssignOpportunity(int id, [FromBody] AssignOpportunityDto assignDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            var opportunity = await _opportunityService.AssignOpportunityAsync(id, assignDto, currentUserId, currentUserRole);
            
            if (opportunity == null)
                return NotFound(new { message = "Opportunity not found or assignment not allowed" });
            
            return Ok(new { 
                message = "Opportunity assigned successfully",
                opportunity = opportunity
            });
        }

        // ✅ ALL AUTHENTICATED USERS can view their own opportunities
        [HttpGet("my-opportunities")]
        public async Task<IActionResult> GetMyOpportunities()
        {
            var (currentUserId, _) = GetCurrentUserInfo();
            var opportunities = await _opportunityService.GetMyOpportunitiesAsync(currentUserId);
            
            return Ok(new { data = opportunities, count = opportunities.Count });
        }

        // 🔒 ONLY Manager/Admin can view team opportunities
        [HttpGet("team-opportunities")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> GetTeamOpportunities()
        {
            var (currentUserId, _) = GetCurrentUserInfo();
            var opportunities = await _opportunityService.GetTeamOpportunitiesAsync(currentUserId);
            
            return Ok(new { data = opportunities, count = opportunities.Count });
        }

        // ✅ ALL AUTHENTICATED USERS can view their stats (role-based filtering in service)
        [HttpGet("stats")]
        public async Task<IActionResult> GetOpportunityStats()
        {
            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            var stats = await _opportunityService.GetOpportunityStatsAsync(currentUserId, currentUserRole);
            
            return Ok(stats);
        }

        // ✅ ALL AUTHENTICATED USERS can view available statuses
        [HttpGet("statuses")]
        public async Task<IActionResult> GetOpportunityStatuses()
        {
            var statuses = await _opportunityService.GetOpportunityStatusesAsync();
            return Ok(statuses);
        }

        // ✅ ALL AUTHENTICATED USERS can trigger auto-status updates for their opportunities
        [HttpPost("{id}/update-status-from-stations")]
        public async Task<IActionResult> UpdateStatusFromStations(int id)
        {
            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            
            // First check if user can access this opportunity
            var opportunity = await _opportunityService.GetOpportunityByIdAsync(id, currentUserId, currentUserRole);
            if (opportunity == null)
                return NotFound(new { message = "Opportunity not found or access denied" });

            var success = await _opportunityService.UpdateOpportunityStatusBasedOnStationsAsync(id);
            
            if (!success)
                return BadRequest(new { message = "Failed to update opportunity status" });

            // Return updated opportunity
            var updatedOpportunity = await _opportunityService.GetOpportunityByIdAsync(id, currentUserId, currentUserRole);
            return Ok(new { 
                message = "Opportunity status updated based on station completion",
                opportunity = updatedOpportunity
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