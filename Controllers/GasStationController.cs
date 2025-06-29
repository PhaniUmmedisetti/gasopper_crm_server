// REPLACE your entire GasStationsController.cs with this complete production-ready version
// All endpoints with proper error handling, logging, and role-based access

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

        /// <summary>
        /// Get gas station statistics based on user role
        /// </summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetGasStationStats()
        {
            try
            {
                var (currentUserId, currentUserRole) = GetCurrentUserInfo();

                Console.WriteLine($"[DEBUG] GetGasStationStats called - UserId: {currentUserId}, Role: {currentUserRole}");

                var stats = await _gasStationService.GetGasStationStatsAsync(currentUserId, currentUserRole);

                var response = new
                {
                    total = stats.TotalStations,
                    completed = stats.CompleteStations,
                    active = stats.TotalStations - stats.CompleteStations,
                    completionRate = stats.CompletionRate,

                    // Additional useful metrics
                    byOpportunity = stats.AverageStationsPerOpportunity,
                    stationTypeBreakdown = stats.StationTypeBreakdown,
                    completionBreakdown = stats.CompletionBreakdown
                };

                Console.WriteLine($"[DEBUG] Stats: Total={stats.TotalStations}, Complete={stats.CompleteStations}, Rate={stats.CompletionRate}%");

                return Ok(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetGasStationStats failed: {ex.Message}");
                return StatusCode(500, new { message = "Error fetching gas station statistics", error = ex.Message });
            }
        }

        /// <summary>
        /// Get all gas stations with role-based filtering
        /// NEW ENDPOINT for frontend gas stations list page
        /// </summary>
        [HttpGet("get-stations")]
        public async Task<IActionResult> GetGasStations()
        {
            try
            {
                var (currentUserId, currentUserRole) = GetCurrentUserInfo();

                Console.WriteLine($"[DEBUG] GetGasStations called - UserId: {currentUserId}, Role: {currentUserRole}");

                var gasStations = await _gasStationService.GetGasStationsAsync(currentUserId, currentUserRole);

                Console.WriteLine($"[DEBUG] Gas stations returned: {gasStations?.Count ?? 0}");

                return Ok(new { data = gasStations, count = gasStations.Count });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetGasStations failed: {ex.Message}");
                return StatusCode(500, new { message = "Error fetching gas stations", error = ex.Message });
            }
        }

        /// <summary>
        /// Get available station types for dropdowns
        /// </summary>
        [HttpGet("types")]
        public async Task<IActionResult> GetStationTypes()
        {
            try
            {
                Console.WriteLine($"[DEBUG] GetStationTypes called");

                var stationTypes = await _gasStationService.GetStationTypesAsync();

                Console.WriteLine($"[DEBUG] Station types returned: {stationTypes?.Count ?? 0}");

                return Ok(stationTypes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetStationTypes failed: {ex.Message}");
                return StatusCode(500, new { message = "Error fetching station types", error = ex.Message });
            }
        }

        /// <summary>
        /// Get all gas stations for a specific opportunity
        /// </summary>
        [HttpGet("opportunities/{opportunityId}/stations")]
        public async Task<IActionResult> GetStationsByOpportunity(int opportunityId)
        {
            try
            {
                var (currentUserId, currentUserRole) = GetCurrentUserInfo();

                Console.WriteLine($"[DEBUG] GetStationsByOpportunity called - OpportunityId: {opportunityId}, UserId: {currentUserId}");

                var gasStations = await _gasStationService.GetGasStationsByOpportunityAsync(opportunityId, currentUserId, currentUserRole);

                Console.WriteLine($"[DEBUG] Stations for opportunity {opportunityId}: {gasStations?.Count ?? 0}");

                return Ok(new
                {
                    data = gasStations,
                    count = gasStations.Count,
                    opportunityId = opportunityId
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetStationsByOpportunity failed: {ex.Message}");
                return StatusCode(500, new { message = "Error fetching stations for opportunity", error = ex.Message });
            }
        }

        /// <summary>
        /// Create a new gas station for an opportunity
        /// Station code will be auto-generated
        /// </summary>
        [HttpPost("opportunities/{opportunityId}/stations")]
        public async Task<IActionResult> CreateStationForOpportunity(int opportunityId, [FromBody] CreateGasStationDto createDto)
        {
            if (!ModelState.IsValid)
            {
                Console.WriteLine($"[ERROR] CreateStationForOpportunity - Invalid model state");
                return BadRequest(ModelState);
            }

            try
            {
                var (currentUserId, currentUserRole) = GetCurrentUserInfo();

                Console.WriteLine($"[DEBUG] CreateStationForOpportunity called - OpportunityId: {opportunityId}, StationName: {createDto.StationName}, UserId: {currentUserId}");

                var gasStation = await _gasStationService.CreateGasStationAsync(opportunityId, createDto, currentUserId, currentUserRole);

                if (gasStation == null)
                {
                    Console.WriteLine($"[ERROR] Failed to create station for opportunity {opportunityId}");
                    return BadRequest(new { message = "Unable to create gas station. Opportunity not found or access denied." });
                }

                Console.WriteLine($"[DEBUG] Station created successfully: {gasStation.StationId} with code {gasStation.StationCode}");

                return CreatedAtAction(
                    nameof(GetStationById),
                    new { id = gasStation.StationId },
                    gasStation);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] CreateStationForOpportunity failed: {ex.Message}");
                return StatusCode(500, new { message = "Error creating gas station", error = ex.Message });
            }
        }

        /// <summary>
        /// Update an existing gas station
        /// Station code cannot be modified
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateStation(int id, [FromBody] UpdateGasStationDto updateDto)
        {
            if (!ModelState.IsValid)
            {
                Console.WriteLine($"[ERROR] UpdateStation - Invalid model state for station {id}");
                return BadRequest(ModelState);
            }

            try
            {
                var (currentUserId, currentUserRole) = GetCurrentUserInfo();

                Console.WriteLine($"[DEBUG] UpdateStation called - StationId: {id}, UserId: {currentUserId}");

                var gasStation = await _gasStationService.UpdateGasStationAsync(id, updateDto, currentUserId, currentUserRole);

                if (gasStation == null)
                {
                    Console.WriteLine($"[ERROR] Station {id} not found or access denied for update");
                    return NotFound(new { message = "Gas station not found or access denied" });
                }

                Console.WriteLine($"[DEBUG] Station updated successfully: {gasStation.StationId}");
                return Ok(gasStation);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] UpdateStation failed: {ex.Message}");
                return StatusCode(500, new { message = "Error updating gas station", error = ex.Message });
            }
        }

        /// <summary>
        /// Soft delete a gas station
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteStation(int id)
        {
            try
            {
                var (currentUserId, currentUserRole) = GetCurrentUserInfo();

                Console.WriteLine($"[DEBUG] DeleteStation called - StationId: {id}, UserId: {currentUserId}");

                var success = await _gasStationService.DeleteGasStationAsync(id, currentUserId, currentUserRole);

                if (!success)
                {
                    Console.WriteLine($"[ERROR] Station {id} not found or access denied for deletion");
                    return NotFound(new { message = "Gas station not found or access denied" });
                }

                Console.WriteLine($"[DEBUG] Station deleted successfully: {id}");
                return Ok(new { message = "Gas station deleted successfully" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] DeleteStation failed: {ex.Message}");
                return StatusCode(500, new { message = "Error deleting gas station", error = ex.Message });
            }
        }

        /// <summary>
        /// Get a specific gas station by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetStationById(int id)
        {
            try
            {
                var (currentUserId, currentUserRole) = GetCurrentUserInfo();

                Console.WriteLine($"[DEBUG] GetStationById called - StationId: {id}, UserId: {currentUserId}");

                var gasStation = await _gasStationService.GetGasStationByIdAsync(id, currentUserId, currentUserRole);

                if (gasStation == null)
                {
                    Console.WriteLine($"[ERROR] Station {id} not found or access denied");
                    return NotFound(new { message = "Gas station not found or access denied" });
                }

                Console.WriteLine($"[DEBUG] Station found: {gasStation.StationId} - {gasStation.StationName} (Code: {gasStation.StationCode})");
                return Ok(gasStation);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetStationById failed: {ex.Message}");
                return StatusCode(500, new { message = "Error fetching gas station", error = ex.Message });
            }
        }

        /// <summary>
        /// Get team gas stations (Manager/Admin only)
        /// </summary>
        [HttpGet("team-stations")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> GetTeamStations()
        {
            try
            {
                var (currentUserId, _) = GetCurrentUserInfo();

                Console.WriteLine($"[DEBUG] GetTeamStations called - UserId: {currentUserId}");

                var gasStations = await _gasStationService.GetTeamGasStationsAsync(currentUserId);

                Console.WriteLine($"[DEBUG] Team stations returned: {gasStations?.Count ?? 0}");

                return Ok(new { data = gasStations, count = gasStations.Count });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetTeamStations failed: {ex.Message}");
                return StatusCode(500, new { message = "Error fetching team gas stations", error = ex.Message });
            }
        }

        /// <summary>
        /// Get my personal gas stations
        /// </summary>
        [HttpGet("my-stations")]
        public async Task<IActionResult> GetMyStations()
        {
            try
            {
                var (currentUserId, _) = GetCurrentUserInfo();

                Console.WriteLine($"[DEBUG] GetMyStations called - UserId: {currentUserId}");

                var gasStations = await _gasStationService.GetMyGasStationsAsync(currentUserId);

                Console.WriteLine($"[DEBUG] My stations returned: {gasStations?.Count ?? 0}");

                return Ok(new { data = gasStations, count = gasStations.Count });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetMyStations failed: {ex.Message}");
                return StatusCode(500, new { message = "Error fetching personal gas stations", error = ex.Message });
            }
        }

        /// <summary>
        /// Manually update opportunity status based on station completion
        /// </summary>
        [HttpPost("opportunities/{opportunityId}/update-status")]
        public async Task<IActionResult> UpdateOpportunityStatusFromStations(int opportunityId)
        {
            try
            {
                var (currentUserId, currentUserRole) = GetCurrentUserInfo();

                Console.WriteLine($"[DEBUG] UpdateOpportunityStatusFromStations called - OpportunityId: {opportunityId}, UserId: {currentUserId}");

                var success = await _gasStationService.UpdateOpportunityStatusFromStationsAsync(opportunityId, currentUserId, currentUserRole);

                if (!success)
                {
                    Console.WriteLine($"[ERROR] Failed to update opportunity {opportunityId} status");
                    return BadRequest(new { message = "Unable to update opportunity status. Opportunity not found or access denied." });
                }

                Console.WriteLine($"[DEBUG] Opportunity {opportunityId} status updated successfully");
                return Ok(new { message = "Opportunity status updated successfully" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] UpdateOpportunityStatusFromStations failed: {ex.Message}");
                return StatusCode(500, new { message = "Error updating opportunity status", error = ex.Message });
            }
        }

        /// <summary>
        /// Extract current user ID and role from JWT token
        /// </summary>
        private (int userId, int roleId) GetCurrentUserInfo()
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            var userId = int.Parse(identity?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var roleId = int.Parse(identity?.FindFirst("role_id")?.Value ?? "0");

            return (userId, roleId);
        }
    }
}