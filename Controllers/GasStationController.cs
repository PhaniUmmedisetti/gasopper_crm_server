// Controllers/GasStationsController.cs
// COMPLETE: Added pagination endpoints with filter parameters while maintaining ALL existing functionality

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

        // UPDATED: Get paginated gas stations with role-based filtering AND status/search filters
        /// <summary>
        /// Get paginated gas stations based on user role with optional filtering
        /// Returns: { data: [...], pagination: { currentPage, totalPages, totalItems, pageSize } }
        /// </summary>
        [HttpGet("paginated")]
        public async Task<IActionResult> GetGasStationsPaginated(
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 20,
            [FromQuery] bool? isSignedOff = null,  // null = all, true = complete, false = incomplete
            [FromQuery] string? search = null)     // search in station name or code
        {
            try
            {
                // Validate pagination parameters
                if (page < 1) page = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 20; // Limit max page size

                var (currentUserId, currentUserRole) = GetCurrentUserInfo();

                Console.WriteLine($"[DEBUG] GetGasStationsPaginated called - UserId: {currentUserId}, Role: {currentUserRole}, Page: {page}, PageSize: {pageSize}, IsSignedOff: {isSignedOff}, Search: '{search}'");

                var result = await _gasStationService.GetGasStationsPaginatedAsync(currentUserId, currentUserRole, page, pageSize, isSignedOff, search);

                Console.WriteLine($"[DEBUG] Paginated gas stations returned - Total: {result.Pagination.TotalItems}, Page: {result.Pagination.CurrentPage}/{result.Pagination.TotalPages}, Count: {result.Data.Count}");

                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetGasStationsPaginated failed: {ex.Message}");
                return StatusCode(500, new { message = "Error fetching paginated gas stations", error = ex.Message });
            }
        }

        // UPDATED: Get paginated user's own gas stations with filters
        /// <summary>
        /// Get paginated gas stations assigned to current user with optional filtering
        /// Returns: { data: [...], pagination: { currentPage, totalPages, totalItems, pageSize } }
        /// </summary>
        [HttpGet("my-stations/paginated")]
        public async Task<IActionResult> GetMyGasStationsPaginated(
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 20,
            [FromQuery] bool? isSignedOff = null,
            [FromQuery] string? search = null)
        {
            try
            {
                // Validate pagination parameters
                if (page < 1) page = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 20;

                var (currentUserId, _) = GetCurrentUserInfo();

                Console.WriteLine($"[DEBUG] GetMyGasStationsPaginated called - UserId: {currentUserId}, Page: {page}, PageSize: {pageSize}, IsSignedOff: {isSignedOff}, Search: '{search}'");

                var result = await _gasStationService.GetMyGasStationsPaginatedAsync(currentUserId, page, pageSize, isSignedOff, search);

                Console.WriteLine($"[DEBUG] Paginated my gas stations returned - Total: {result.Pagination.TotalItems}, Page: {result.Pagination.CurrentPage}/{result.Pagination.TotalPages}, Count: {result.Data.Count}");

                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetMyGasStationsPaginated failed: {ex.Message}");
                return StatusCode(500, new { message = "Error fetching paginated personal gas stations", error = ex.Message });
            }
        }

        // UPDATED: Get paginated team gas stations with filters (Manager/Admin only)
        /// <summary>
        /// Get paginated gas stations for team members with optional filtering (Manager/Admin only)
        /// Returns: { data: [...], pagination: { currentPage, totalPages, totalItems, pageSize } }
        /// </summary>
        [HttpGet("team-stations/paginated")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> GetTeamGasStationsPaginated(
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 20,
            [FromQuery] bool? isSignedOff = null,
            [FromQuery] string? search = null)
        {
            try
            {
                // Validate pagination parameters
                if (page < 1) page = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 20;

                var (currentUserId, _) = GetCurrentUserInfo();

                Console.WriteLine($"[DEBUG] GetTeamGasStationsPaginated called - UserId: {currentUserId}, Page: {page}, PageSize: {pageSize}, IsSignedOff: {isSignedOff}, Search: '{search}'");

                var result = await _gasStationService.GetTeamGasStationsPaginatedAsync(currentUserId, page, pageSize, isSignedOff, search);

                Console.WriteLine($"[DEBUG] Paginated team gas stations returned - Total: {result.Pagination.TotalItems}, Page: {result.Pagination.CurrentPage}/{result.Pagination.TotalPages}, Count: {result.Data.Count}");

                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetTeamGasStationsPaginated failed: {ex.Message}");
                return StatusCode(500, new { message = "Error fetching paginated team gas stations", error = ex.Message });
            }
        }

        // EXISTING: Get gas station statistics based on user role (unchanged)
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

                    // Sign-off statistics
                    signedOff = stats.SignedOffStations,
                    pendingSignOff = stats.PendingSignOffStations,
                    signOffRate = stats.SignOffRate,

                    // Additional useful metrics
                    byOpportunity = stats.AverageStationsPerOpportunity,
                    stationTypeBreakdown = stats.StationTypeBreakdown,
                    completionBreakdown = stats.CompletionBreakdown,
                    signOffBreakdown = stats.SignOffBreakdown
                };

                Console.WriteLine($"[DEBUG] Stats: Total={stats.TotalStations}, Complete={stats.CompleteStations}, SignedOff={stats.SignedOffStations}, Rate={stats.CompletionRate}%");

                return Ok(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetGasStationStats failed: {ex.Message}");
                return StatusCode(500, new { message = "Error fetching gas station statistics", error = ex.Message });
            }
        }

        // EXISTING: Get all gas stations with role-based filtering (unchanged)
        /// <summary>
        /// Get all gas stations with role-based filtering (backward compatibility)
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

                return Ok(new { data = gasStations, count = gasStations?.Count });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetGasStations failed: {ex.Message}");
                return StatusCode(500, new { message = "Error fetching gas stations", error = ex.Message });
            }
        }

        // EXISTING: Get available station types for dropdowns (unchanged)
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

        // EXISTING: Get all gas stations for a specific opportunity (unchanged)
        /// <summary>
        /// Get all gas stations for a specific opportunity
        /// </summary>
        [HttpGet("opportunities/{opportunityId:int}/stations")]
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
                    count = gasStations?.Count,
                    opportunityId = opportunityId
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetStationsByOpportunity failed: {ex.Message}");
                return StatusCode(500, new { message = "Error fetching stations for opportunity", error = ex.Message });
            }
        }

        // EXISTING: Create a new gas station for an opportunity (unchanged)
        /// <summary>
        /// Create a new gas station for an opportunity
        /// Station code will be auto-generated
        /// </summary>
        [HttpPost("opportunities/{opportunityId:int}/stations")]
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

        // EXISTING: Update an existing gas station (unchanged)
        /// <summary>
        /// Update an existing gas station
        /// Station code cannot be modified
        /// Signed-off stations cannot be updated
        /// </summary>
        [HttpPut("{id:int}")]
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
                    return NotFound(new { message = "Gas station not found, access denied, or station is signed off" });
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

        // EXISTING: Soft delete a gas station (unchanged)
        /// <summary>
        /// Soft delete a gas station
        /// Signed-off stations cannot be deleted
        /// </summary>
        [HttpDelete("{id:int}")]
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
                    return NotFound(new { message = "Gas station not found, access denied, or station is signed off" });
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

        // EXISTING: Get a specific gas station by ID (unchanged)
        /// <summary>
        /// Get a specific gas station by ID
        /// </summary>
        [HttpGet("{id:int}")]
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

                Console.WriteLine($"[DEBUG] Station found: {gasStation.StationId} - {gasStation.StationName} (Code: {gasStation.StationCode}, SignedOff: {gasStation.IsSignedOff})");
                return Ok(gasStation);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetStationById failed: {ex.Message}");
                return StatusCode(500, new { message = "Error fetching gas station", error = ex.Message });
            }
        }

        // EXISTING: Sign off a gas station (unchanged)
        /// <summary>
        /// Sign off a gas station (permanently freeze it)
        /// Uses role-based permissions - Admin/Manager/Salesperson can sign off based on access rights
        /// Station must have all fields filled before sign-off
        /// </summary>
        [HttpPost("{id:int}/sign-off")]
        public async Task<IActionResult> SignOffStation(int id, [FromBody] SignOffStationDto signOffDto)
        {
            if (!ModelState.IsValid)
            {
                Console.WriteLine($"[ERROR] SignOffStation - Invalid model state for station {id}");
                return BadRequest(ModelState);
            }

            if (!signOffDto.ConfirmSignOff)
            {
                Console.WriteLine($"[ERROR] SignOffStation - Sign-off not confirmed for station {id}");
                return BadRequest(new { message = "Sign-off confirmation is required" });
            }

            try
            {
                var (currentUserId, currentUserRole) = GetCurrentUserInfo();

                Console.WriteLine($"[DEBUG] SignOffStation called - StationId: {id}, UserId: {currentUserId}, Role: {currentUserRole}");

                var success = await _gasStationService.SignOffStationAsync(id, currentUserId, currentUserRole);

                if (!success)
                {
                    Console.WriteLine($"[ERROR] Failed to sign off station {id}");
                    return BadRequest(new { message = "Unable to sign off station. Station not found, access denied, already signed off, or not ready for sign-off." });
                }

                Console.WriteLine($"[DEBUG] Station {id} signed off successfully by user {currentUserId} (role: {currentUserRole})");

                // Return updated station data
                var updatedStation = await _gasStationService.GetGasStationByIdAsync(id, currentUserId, currentUserRole);
                return Ok(new 
                { 
                    message = "Gas station signed off successfully",
                    station = updatedStation
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] SignOffStation failed: {ex.Message}");
                return StatusCode(500, new { message = "Error signing off gas station", error = ex.Message });
            }
        }

        // EXISTING: Check if current user can sign off a specific station (unchanged)
        /// <summary>
        /// Check if current user can sign off a specific station
        /// Uses role-based permissions for consistency
        /// </summary>
        [HttpGet("{id:int}/can-sign-off")]
        public async Task<IActionResult> CanSignOffStation(int id)
        {
            try
            {
                var (currentUserId, currentUserRole) = GetCurrentUserInfo();

                Console.WriteLine($"[DEBUG] CanSignOffStation called - StationId: {id}, UserId: {currentUserId}, Role: {currentUserRole}");

                var canSignOff = await _gasStationService.CanUserSignOffStationAsync(id, currentUserId, currentUserRole);

                Console.WriteLine($"[DEBUG] User {currentUserId} (role: {currentUserRole}) can sign off station {id}: {canSignOff}");

                return Ok(new { canSignOff = canSignOff });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] CanSignOffStation failed: {ex.Message}");
                return StatusCode(500, new { message = "Error checking sign-off permissions", error = ex.Message });
            }
        }

        // EXISTING: Get team gas stations (Manager/Admin only) - NON-PAGINATED (unchanged)
        /// <summary>
        /// Get team gas stations (Manager/Admin only) - Non-paginated version for backward compatibility
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

                return Ok(new { data = gasStations, count = gasStations?.Count });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetTeamStations failed: {ex.Message}");
                return StatusCode(500, new { message = "Error fetching team gas stations", error = ex.Message });
            }
        }

        // EXISTING: Get my personal gas stations - NON-PAGINATED (unchanged)
        /// <summary>
        /// Get my personal gas stations - Non-paginated version for backward compatibility
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

                return Ok(new { data = gasStations, count = gasStations?.Count });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetMyStations failed: {ex.Message}");
                return StatusCode(500, new { message = "Error fetching personal gas stations", error = ex.Message });
            }
        }

        // EXISTING: Manually update opportunity status based on station completion (unchanged)
        /// <summary>
        /// Manually update opportunity status based on station completion
        /// </summary>
        [HttpPost("opportunities/{opportunityId:int}/update-status")]
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