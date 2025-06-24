// REPLACE your entire GasStationsController.cs with this corrected version

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
    public class GasStationsController : ControllerBase
    {
        private readonly IGasStationService _gasStationService;

        public GasStationsController(IGasStationService gasStationService)
        {
            _gasStationService = gasStationService;
        }

        // FIXED: Real gas station stats from database instead of fake hardcoded values
        [HttpGet("stats")]
        public async Task<IActionResult> GetGasStationStats()
        {
            try
            {
                var (currentUserId, currentUserRole) = GetCurrentUserInfo();
                
                // CRITICAL FIX: Get REAL stats from database, not fake hardcoded values
                var stats = await _gasStationService.GetGasStationStatsAsync(currentUserId, currentUserRole);
                
                var response = new
                {
                    total = stats.TotalStations,
                    completed = stats.CompleteStations,
                    active = stats.TotalStations - stats.CompleteStations,
                    completionRate = stats.CompletionRate,
                    
                    // Additional useful metrics
                    byOpportunity = stats.AverageStationsPerOpportunity,
                    stationTypeBreakdown = stats.StationTypeBreakdown
                };
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching gas station statistics", error = ex.Message });
            }
        }

        [HttpGet("types")]
        public async Task<IActionResult> GetStationTypes()
        {
            var stationTypes = await _gasStationService.GetStationTypesAsync();
            return Ok(stationTypes);
        }

        [HttpGet("opportunities/{opportunityId}/stations")]
        public async Task<IActionResult> GetStationsByOpportunity(int opportunityId)
        {
            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            var gasStations = await _gasStationService.GetGasStationsByOpportunityAsync(opportunityId, currentUserId, currentUserRole);

            return Ok(new
            {
                data = gasStations,
                count = gasStations.Count,
                opportunityId = opportunityId
            });
        }

        [HttpPost("opportunities/{opportunityId}/stations")]
        public async Task<IActionResult> CreateStationForOpportunity(int opportunityId, [FromBody] CreateGasStationDto createDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            var gasStation = await _gasStationService.CreateGasStationAsync(opportunityId, createDto, currentUserId, currentUserRole);

            if (gasStation == null)
                return BadRequest(new { message = "Unable to create gas station. Opportunity not found or access denied." });

            return CreatedAtAction(
                nameof(GetStationById),
                new { id = gasStation.StationId },
                gasStation);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateStation(int id, [FromBody] UpdateGasStationDto updateDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            var gasStation = await _gasStationService.UpdateGasStationAsync(id, updateDto, currentUserId, currentUserRole);

            if (gasStation == null)
                return NotFound(new { message = "Gas station not found or access denied" });

            return Ok(gasStation);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteStation(int id)
        {
            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            var success = await _gasStationService.DeleteGasStationAsync(id, currentUserId, currentUserRole);

            if (!success)
                return NotFound(new { message = "Gas station not found or access denied" });

            return Ok(new { message = "Gas station deleted successfully" });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetStationById(int id)
        {
            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            var gasStation = await _gasStationService.GetGasStationByIdAsync(id, currentUserId, currentUserRole);

            if (gasStation == null)
                return NotFound(new { message = "Gas station not found or access denied" });

            return Ok(gasStation);
        }

        // FIXED: Use GetTeamGasStationsAsync instead of GetAllGasStationsAsync (Manager/Admin only)
        [HttpGet("team-stations")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> GetTeamStations()
        {
            var (currentUserId, _) = GetCurrentUserInfo();
            var gasStations = await _gasStationService.GetTeamGasStationsAsync(currentUserId);

            return Ok(new { data = gasStations, count = gasStations.Count });
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