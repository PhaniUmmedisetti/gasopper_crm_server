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
                var opportunities = await _opportunityService.GetOpportunitiesAsync(currentUserId, currentUserRole, includeDeleted);
                return Ok(new { data = opportunities, count = opportunities.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching opportunities", error = ex.Message });
            }
        }

        [HttpGet("paginated")]
        public async Task<IActionResult> GetOpportunitiesPaginated(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] bool? completionStatus = null,
            [FromQuery] bool showSelfOnly = false,
            [FromQuery] bool includeDeleted = false)
        {
            try
            {
                var (currentUserId, currentUserRole) = GetCurrentUserInfo();
                var result = await _opportunityService.GetOpportunitiesPaginatedAsync(
                    currentUserId, currentUserRole, page, Math.Min(pageSize, 100),
                    completionStatus, showSelfOnly, includeDeleted);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching paginated opportunities", error = ex.Message });
            }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetOpportunity(int id)
        {
            try
            {
                var (currentUserId, currentUserRole) = GetCurrentUserInfo();
                var opportunity = await _opportunityService.GetOpportunityByIdAsync(id, currentUserId, currentUserRole);

                if (opportunity == null)
                    return NotFound(new { message = "Opportunity not found or access denied" });

                return Ok(opportunity);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching opportunity", error = ex.Message });
            }
        }

        [HttpPut("{id:int}")]
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

        [HttpPut("{id:int}/status")]
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

        [HttpPut("{id:int}/assign")]
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
                var opportunities = await _opportunityService.GetMyOpportunitiesAsync(currentUserId);
                return Ok(new { data = opportunities, count = opportunities.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching opportunities", error = ex.Message });
            }
        }

        [HttpGet("my-opportunities/paginated")]
        public async Task<IActionResult> GetMyOpportunitiesPaginated(
           [FromQuery] int page = 1,
           [FromQuery] int pageSize = 20,
           [FromQuery] bool? completionStatus = null)
        {
            try
            {
                var (currentUserId, _) = GetCurrentUserInfo();
                var result = await _opportunityService.GetMyOpportunitiesPaginatedAsync(
                    currentUserId, page, Math.Min(pageSize, 100), completionStatus);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching my paginated opportunities", error = ex.Message });
            }
        }

        [HttpGet("team-opportunities")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> GetTeamOpportunities()
        {
            try
            {
                var (currentUserId, _) = GetCurrentUserInfo();
                var opportunities = await _opportunityService.GetTeamOpportunitiesAsync(currentUserId);
                return Ok(new { data = opportunities, count = opportunities.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching team opportunities", error = ex.Message });
            }
        }

        [HttpGet("team-opportunities/paginated")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> GetTeamOpportunitiesPaginated(
           [FromQuery] int page = 1,
           [FromQuery] int pageSize = 20,
           [FromQuery] bool? completionStatus = null,
           [FromQuery] bool showSelfOnly = false)
        {
            try
            {
                var (currentUserId, _) = GetCurrentUserInfo();
                var result = await _opportunityService.GetTeamOpportunitiesPaginatedAsync(
                    currentUserId, page, Math.Min(pageSize, 100), completionStatus, showSelfOnly);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching team paginated opportunities", error = ex.Message });
            }
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetOpportunityStats()
        {
            try
            {
                var (currentUserId, currentUserRole) = GetCurrentUserInfo();
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
                    total = stats.TotalOpportunities,
                    active = stats.ActiveOpportunities,
                    complete = stats.CompleteOpportunities
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

        private (int userId, int roleId) GetCurrentUserInfo()
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            var userId = int.Parse(identity?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var roleId = int.Parse(identity?.FindFirst("role_id")?.Value ?? "0");

            return (userId, roleId);
        }
    }
}