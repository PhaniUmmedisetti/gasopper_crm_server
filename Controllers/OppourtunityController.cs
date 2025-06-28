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
            try
            {
                var (currentUserId, currentUserRole) = GetCurrentUserInfo();
                
                // ADDED: Debug logging
                Console.WriteLine($"[DEBUG] GetOpportunities called - UserId: {currentUserId}, Role: {currentUserRole}, IncludeDeleted: {includeDeleted}");
                
                var opportunities = await _opportunityService.GetOpportunitiesAsync(currentUserId, currentUserRole, includeDeleted);
                
                // ADDED: Debug logging
                Console.WriteLine($"[DEBUG] Opportunities returned: {opportunities?.Count ?? 0}");
                if (opportunities?.Any() == true)
                {
                    Console.WriteLine($"[DEBUG] First opportunity: OpportunityId={opportunities.First().OpportunityId}, LeadName={opportunities.First().LeadName}");
                }

                return Ok(new { data = opportunities, count = opportunities.Count });
            }
            catch (Exception ex)
            {
                // ADDED: Debug logging
                Console.WriteLine($"[ERROR] GetOpportunities failed: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                
                return StatusCode(500, new { message = "Error fetching opportunities", error = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetOpportunity(int id)
        {
            try
            {
                var (currentUserId, currentUserRole) = GetCurrentUserInfo();
                
                // ADDED: Debug logging
                Console.WriteLine($"[DEBUG] GetOpportunity called - OpportunityId: {id}, UserId: {currentUserId}, Role: {currentUserRole}");
                
                var opportunity = await _opportunityService.GetOpportunityByIdAsync(id, currentUserId, currentUserRole);

                if (opportunity == null)
                {
                    Console.WriteLine($"[DEBUG] Opportunity {id} not found or access denied");
                    return NotFound(new { message = "Opportunity not found or access denied" });
                }

                Console.WriteLine($"[DEBUG] Opportunity found: {opportunity.OpportunityId} - {opportunity.LeadName}");
                return Ok(opportunity);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetOpportunity failed: {ex.Message}");
                return StatusCode(500, new { message = "Error fetching opportunity", error = ex.Message });
            }
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
            try
            {
                var (currentUserId, _) = GetCurrentUserInfo();
                
                Console.WriteLine($"[DEBUG] GetMyOpportunities called - UserId: {currentUserId}");
                
                var opportunities = await _opportunityService.GetMyOpportunitiesAsync(currentUserId);
                
                Console.WriteLine($"[DEBUG] My opportunities returned: {opportunities?.Count ?? 0}");
                
                return Ok(new { data = opportunities, count = opportunities.Count });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetMyOpportunities failed: {ex.Message}");
                return StatusCode(500, new { message = "Error fetching opportunities", error = ex.Message });
            }
        }

        [HttpGet("team-opportunities")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> GetTeamOpportunities()
        {
            try
            {
                var (currentUserId, _) = GetCurrentUserInfo();
                
                Console.WriteLine($"[DEBUG] GetTeamOpportunities called - ManagerId: {currentUserId}");
                
                var opportunities = await _opportunityService.GetTeamOpportunitiesAsync(currentUserId);
                
                Console.WriteLine($"[DEBUG] Team opportunities returned: {opportunities?.Count ?? 0}");
                
                return Ok(new { data = opportunities, count = opportunities.Count });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetTeamOpportunities failed: {ex.Message}");
                return StatusCode(500, new { message = "Error fetching team opportunities", error = ex.Message });
            }
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetOpportunityStats()
        {
            try
            {
                var (currentUserId, currentUserRole) = GetCurrentUserInfo();
                
                Console.WriteLine($"[DEBUG] GetOpportunityStats called - UserId: {currentUserId}, Role: {currentUserRole}");
                
                var stats = await _opportunityService.GetOpportunityStatsAsync(currentUserId, currentUserRole);

                var response = new
                {
                    totalOpportunities = stats.TotalOpportunities,
                    activeOpportunities = stats.ActiveOpportunities,
                    completeOpportunities = stats.CompleteOpportunities,
                    completionRate = stats.CompletionRate,
                    totalStations = stats.TotalStations,
                    completeStations = stats.CompleteStations,
                    stationCompletionRate = stats.StationCompletionRate,
                    averageStationsPerOpportunity = stats.AverageStationsPerOpportunity,
                    averageDaysToComplete = stats.AverageDaysToComplete,
                    statusBreakdown = stats.StatusBreakdown,

                    // Legacy support
                    total = stats.TotalOpportunities,
                    active = stats.ActiveOpportunities,
                    complete = stats.CompleteOpportunities
                };

                Console.WriteLine($"[DEBUG] Stats: Total={stats.TotalOpportunities}, Active={stats.ActiveOpportunities}, Complete={stats.CompleteOpportunities}");
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetOpportunityStats failed: {ex.Message}");
                return StatusCode(500, new { message = "Error fetching opportunity statistics", error = ex.Message });
            }
        }

        [HttpGet("statuses")]
        public async Task<IActionResult> GetOpportunityStatuses()
        {
            var statuses = await _opportunityService.GetOpportunityStatusesAsync();
            return Ok(statuses);
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