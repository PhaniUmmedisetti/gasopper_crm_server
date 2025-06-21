using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using gasopper_crm_server.DTOs;
using gasopper_crm_server.Services;

namespace gasopper_crm_server.Controllers
{
    [ApiController]
    [Route("api/GasStation")]
    [Authorize] // ALL endpoints require authentication
    public class GasStationsController : ControllerBase
    {
        private readonly IGasStationService _gasStationService;

        public GasStationsController(IGasStationService gasStationService)
        {
            _gasStationService = gasStationService;
        }

        // ✅ 1. GET STATION TYPES (Reference Data)
        // GET /api/GasStations/types
        [HttpGet("types")]
        public async Task<IActionResult> GetStationTypes()
        {
            var stationTypes = await _gasStationService.GetStationTypesAsync();
            return Ok(stationTypes);
        }

        // ✅ 2. VIEW STATIONS UNDER OPPORTUNITY (Core Business Flow)
        // GET /api/GasStations/opportunities/{opportunityId}/stations
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

        // ✅ 3. CREATE STATION UNDER OPPORTUNITY (Core Business Flow)
        // POST /api/GasStations/opportunities/{opportunityId}/stations
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

        // ✅ 4. EDIT STATION DETAILS (Role-based access)
        // PUT /api/GasStations/{id}
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

        // ✅ 5. DELETE STATION (Role-based: Sales=own, Manager=team, Admin=all)
        // DELETE /api/GasStations/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteStation(int id)
        {
            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            var success = await _gasStationService.DeleteGasStationAsync(id, currentUserId, currentUserRole);

            if (!success)
                return NotFound(new { message = "Gas station not found or access denied" });

            return Ok(new { message = "Gas station deleted successfully" });
        }

        // ✅ BONUS: Get single station details (for UI forms)
        // GET /api/GasStations/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetStationById(int id)
        {
            var (currentUserId, currentUserRole) = GetCurrentUserInfo();
            var gasStation = await _gasStationService.GetGasStationByIdAsync(id, currentUserId, currentUserRole);

            if (gasStation == null)
                return NotFound(new { message = "Gas station not found or access denied" });

            return Ok(gasStation);
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