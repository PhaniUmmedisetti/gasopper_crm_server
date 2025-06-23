using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using gasopper_crm_server.DTOs;
using gasopper_crm_server.Services;

namespace gasopper_crm_server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OpportunitiesController : ControllerBase
    {
        private readonly IOpportunityService _opportunityService;

        public OpportunitiesController(IOpportunityService opportunityService)
        {
            _opportunityService = opportunityService;
        }

        [HttpGet]
        public async Task<IActionResult> GetOpportunities([FromQuery] bool includeDeleted = false)
        {
            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            var opportunities = await _opportunityService.GetOpportunitiesAsync(currentUserId, currentUserRole, includeDeleted);
            
            return Ok(new { data = opportunities, count = opportunities.Count });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetOpportunity(int id)
        {
            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            var opportunity = await _opportunityService.GetOpportunityByIdAsync(id, currentUserId, currentUserRole);
            
            if (opportunity == null)
                return NotFound(new { message = "Opportunity not found or access denied" });
            
            return Ok(opportunity);
        }

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

        [HttpGet("my-opportunities")]
        public async Task<IActionResult> GetMyOpportunities()
        {
            var (currentUserId, _) = GetCurrentUserInfo();
            var opportunities = await _opportunityService.GetMyOpportunitiesAsync(currentUserId);
            
            return Ok(new { data = opportunities, count = opportunities.Count });
        }

        [HttpGet("team-opportunities")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> GetTeamOpportunities()
        {
            var (currentUserId, _) = GetCurrentUserInfo();
            var opportunities = await _opportunityService.GetTeamOpportunitiesAsync(currentUserId);
            
            return Ok(new { data = opportunities, count = opportunities.Count });
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetOpportunityStats()
        {
            try
            {
                var (currentUserId, currentUserRole) = GetCurrentUserInfo();
                var stats = await _opportunityService.GetOpportunityStatsAsync(currentUserId, currentUserRole);
                
                // Calculate estimated value from stations
                var estimatedValue = stats.TotalStations * 50000; // $50k per station
                
                var response = new
                {
                    active = stats.ActiveOpportunities,
                    closed = stats.CompleteOpportunities,
                    totalValue = estimatedValue
                };
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching opportunity statistics", error = ex.Message });
            }
        }

        [HttpGet("statuses")]
        public async Task<IActionResult> GetOpportunityStatuses()
        {
            var statuses = await _opportunityService.GetOpportunityStatusesAsync();
            return Ok(statuses);
        }

        [HttpPost("{id}/update-status-from-stations")]
        public async Task<IActionResult> UpdateStatusFromStations(int id)
        {
            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            
            var opportunity = await _opportunityService.GetOpportunityByIdAsync(id, currentUserId, currentUserRole);
            if (opportunity == null)
                return NotFound(new { message = "Opportunity not found or access denied" });

            var success = await _opportunityService.UpdateOpportunityStatusBasedOnStationsAsync(id);
            
            if (!success)
                return BadRequest(new { message = "Failed to update opportunity status" });

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