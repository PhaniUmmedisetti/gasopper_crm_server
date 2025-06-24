// REPLACE your entire OpportunitiesController.cs with this corrected version

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
                return NotFound(new { message = "Opportunity not found, access denied, or invalid status" });

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

            return Ok(new
            {
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

        // FIXED: Opportunity Stats - Ensuring Business Logic Consistency
        [HttpGet("stats")]
        public async Task<IActionResult> GetOpportunityStats()
        {
            try
            {
                var (currentUserId, currentUserRole) = GetCurrentUserInfo();
                var stats = await _opportunityService.GetOpportunityStatsAsync(currentUserId, currentUserRole);

                // CRITICAL: Return stats based on GAS STATION COMPLETION, not database status
                var response = new
                {
                    // MAIN FIELDS (based on gas station completion logic)
                    totalOpportunities = stats.TotalOpportunities,
                    activeOpportunities = stats.ActiveOpportunities,      // Has incomplete gas stations
                    completeOpportunities = stats.CompleteOpportunities,  // All gas stations complete
                    completionRate = stats.CompletionRate,
                    
                    // GAS STATION METRICS (actual counts from database)
                    totalStations = stats.TotalStations,
                    completeStations = stats.CompleteStations,
                    stationCompletionRate = stats.StationCompletionRate,
                    averageStationsPerOpportunity = stats.AverageStationsPerOpportunity,
                    averageDaysToComplete = stats.AverageDaysToComplete,
                    statusBreakdown = stats.StatusBreakdown,

                    // LEGACY FIELDS (for backward compatibility)
                    active = stats.ActiveOpportunities,
                    closed = stats.CompleteOpportunities,
                    totalValue = stats.TotalStations * 50000  // $50k per station
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
            return Ok(new
            {
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