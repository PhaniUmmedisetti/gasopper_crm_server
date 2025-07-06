using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using gasopper_crm_server.DTOs;
using gasopper_crm_server.Services;

namespace gasopper_crm_server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IOtpService _otpService;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IAuthService authService,
            IOtpService otpService,
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<AuthController> logger)
        {
            _authService = authService;
            _otpService = otpService;
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.LoginAsync(loginDto);

            if (result == null)
                return Unauthorized(new { success = false, message = "Invalid email or password" });

            return Ok(result);
        }

        [HttpPost("send-otp")]
        public async Task<IActionResult> SendOtp([FromBody] SendOtpDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var success = await _otpService.GenerateAndSendOtpAsync(dto.Email);

                // Always return success to not reveal if email exists (security practice)
                return Ok(new
                {
                    success = true,
                    message = "If the email exists in our system, an OTP has been sent.",
                    expiryMinutes = 5
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendOtp endpoint");
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while processing your request."
                });
            }
        }

        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var isValidOtp = await _otpService.ValidateOtpAsync(dto.Email, dto.OtpCode);

                if (!isValidOtp)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid or expired OTP code. Please try again or request a new code."
                    });
                }

                // Get user and generate JWT token
                var loginResponse = await _authService.AuthenticateWithEmailAsync(dto.Email);

                if (loginResponse == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Authentication failed. Please try again."
                    });
                }

                _logger.LogInformation($"Successful OTP login for email: {dto.Email}");
                return Ok(loginResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in VerifyOtp endpoint");
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while processing your request."
                });
            }
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            if (userId == 0)
                return BadRequest(new { success = false, message = "Invalid user" });

            var success = await _authService.LogoutAsync(userId);

            if (!success)
                return BadRequest(new { success = false, message = "Logout failed" });

            return Ok(new { success = true, message = "Logout successful" });
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            Console.WriteLine("🔍 /me endpoint called");

            try
            {
                var identity = HttpContext.User.Identity as ClaimsIdentity;

                Console.WriteLine($"🔍 Identity authenticated: {identity?.IsAuthenticated}");
                Console.WriteLine($"🔍 Claims count: {identity?.Claims?.Count() ?? 0}");

                if (identity?.Claims != null)
                {
                    foreach (var claim in identity.Claims)
                    {
                        Console.WriteLine($"🔍 Claim: {claim.Type} = {claim.Value}");
                    }
                }

                if (identity == null || !identity.IsAuthenticated)
                {
                    Console.WriteLine("❌ Not authenticated");
                    return Unauthorized(new { message = "Not authenticated" });
                }

                var userIdClaim = identity.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                Console.WriteLine($"🔍 User ID claim: {userIdClaim}");

                var userId = int.Parse(userIdClaim ?? "0");

                if (userId == 0)
                {
                    Console.WriteLine("❌ Invalid user ID");
                    return Unauthorized(new { message = "Invalid user ID in token" });
                }

                var userInfo = await _authService.GetUserInfoAsync(userId);

                if (userInfo == null)
                {
                    Console.WriteLine("❌ User info not found");
                    return Unauthorized(new { message = "User not found or inactive" });
                }

                Console.WriteLine($"✅ Returning user info for: {userInfo.FirstName} {userInfo.LastName}");
                return Ok(userInfo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Exception in /me: {ex.Message}");
                return Unauthorized(new { message = "Authentication failed", error = ex.Message });
            }
        }

        [HttpPost("cleanup-expired-otps")]
        [Authorize]
        public async Task<IActionResult> CleanupExpiredOtps()
        {
            try
            {
                var success = await _otpService.CleanupExpiredOtpsAsync();
                return Ok(new { success = success, message = "Cleanup completed" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in cleanup expired OTPs");
                return StatusCode(500, new { success = false, message = "Cleanup failed" });
            }
        }

        [HttpPost("debug-email")]
        public async Task<IActionResult> DebugEmail([FromBody] string testEmail)
        {
            try
            {
                _logger.LogInformation($"🔍 Starting email debug for: {testEmail}");

                // Check if email service is registered
                if (_emailService == null)
                {
                    _logger.LogError("❌ EmailService is null - not registered in DI");
                    return BadRequest(new
                    {
                        success = false,
                        error = "EmailService not registered",
                        step = "Service Registration"
                    });
                }

                // Check configuration
                var smtpHost = _configuration["EmailSettings:SmtpHost"];
                var smtpPort = _configuration["EmailSettings:SmtpPort"];
                var smtpUsername = _configuration["EmailSettings:SmtpUsername"];
                var smtpPassword = _configuration["EmailSettings:SmtpPassword"];
                var fromEmail = _configuration["EmailSettings:FromEmail"];

                _logger.LogInformation($"🔧 Email Configuration:");
                _logger.LogInformation($"   SMTP Host: {smtpHost}");
                _logger.LogInformation($"   SMTP Port: {smtpPort}");
                _logger.LogInformation($"   SMTP Username: {smtpUsername}");
                _logger.LogInformation($"   From Email: {fromEmail}");
                _logger.LogInformation($"   Password Length: {smtpPassword?.Length ?? 0}");

                if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(smtpUsername) || string.IsNullOrEmpty(smtpPassword))
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = "Missing email configuration",
                        step = "Configuration Check",
                        config = new
                        {
                            hasHost = !string.IsNullOrEmpty(smtpHost),
                            hasUsername = !string.IsNullOrEmpty(smtpUsername),
                            hasPassword = !string.IsNullOrEmpty(smtpPassword),
                            hasFromEmail = !string.IsNullOrEmpty(fromEmail)
                        }
                    });
                }

                // Test email send
                var success = await _emailService.SendOtpEmailAsync(testEmail, "123456", "Debug Test");

                return Ok(new
                {
                    success = success,
                    message = success ? "Debug email sent successfully!" : "Email sending failed - check logs",
                    step = "Email Send Test",
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Email debug failed");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    step = "Exception Caught",
                    stackTrace = ex.StackTrace
                });
            }
        }

        [HttpGet("debug-config")]
        public IActionResult DebugConfig()
        {
            try
            {
                var emailConfig = new
                {
                    SmtpHost = _configuration["EmailSettings:SmtpHost"],
                    SmtpPort = _configuration["EmailSettings:SmtpPort"],
                    SmtpUsername = _configuration["EmailSettings:SmtpUsername"],
                    FromEmail = _configuration["EmailSettings:FromEmail"],
                    FromName = _configuration["EmailSettings:FromName"],
                    HasPassword = !string.IsNullOrEmpty(_configuration["EmailSettings:SmtpPassword"]),
                    PasswordLength = _configuration["EmailSettings:SmtpPassword"]?.Length ?? 0
                };

                var otpConfig = new
                {
                    ExpiryMinutes = _configuration["OtpSettings:ExpiryMinutes"],
                    MaxAttempts = _configuration["OtpSettings:MaxAttempts"],
                    RateLimitMinutes = _configuration["OtpSettings:RateLimitMinutes"],
                    MaxOtpsPerWindow = _configuration["OtpSettings:MaxOtpsPerWindow"]
                };

                return Ok(new
                {
                    emailConfig = emailConfig,
                    otpConfig = otpConfig,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("test")]
        public IActionResult Test()
        {
            return Ok(new
            {
                message = "Auth controller is working!",
                timestamp = DateTime.UtcNow,
                otpEnabled = true
            });
        }

        [HttpGet("debug-claims")]
        [Authorize]
        public IActionResult DebugClaims()
        {
            Console.WriteLine("🔍 Debug claims endpoint called");

            var identity = HttpContext.User.Identity as ClaimsIdentity;
            if (identity == null)
            {
                Console.WriteLine("❌ No identity");
                return Unauthorized(new { message = "No identity" });
            }

            var claims = identity.Claims.Select(c => new
            {
                Type = c.Type,
                Value = c.Value
            }).ToList();

            Console.WriteLine($"🔍 Total claims: {claims.Count}");

            return Ok(new
            {
                isAuthenticated = identity.IsAuthenticated,
                authenticationType = identity.AuthenticationType,
                name = identity.Name,
                claims = claims
            });
        }
    }
}