using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using gasopper_crm_server.Data;
using gasopper_crm_server.Models;

namespace gasopper_crm_server.Services
{
    public interface IOtpService
    {
        Task<bool> GenerateAndSendOtpAsync(string email);
        Task<bool> ValidateOtpAsync(string email, string otpCode);
        Task<bool> CleanupExpiredOtpsAsync();
    }

    public class OtpService : IOtpService
    {
        private readonly GasopperDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<OtpService> _logger;
        private readonly IConfiguration _configuration;

        public OtpService(
            GasopperDbContext context, 
            IEmailService emailService, 
            ILogger<OtpService> logger,
            IConfiguration configuration)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<bool> GenerateAndSendOtpAsync(string email)
        {
            try
            {
                _logger.LogInformation($"🔍 Starting OTP generation for email: {email}");

                // Find user by email
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.email.ToLower() == email.ToLower() && u.is_active);

                if (user == null)
                {
                    _logger.LogWarning($"❌ User not found for email: {email}");
                    return true; // Return true for security (don't reveal if email exists)
                }

                _logger.LogInformation($"✅ User found: {user.first_name} {user.last_name} (ID: {user.user_id})");

                // Clean up expired OTPs for this user (simple cleanup)
                var expiredOtps = await _context.UserOtps
                    .Where(o => o.UserId == user.user_id && (o.ExpiresAt < DateTime.UtcNow || o.IsUsed))
                    .ToListAsync();

                if (expiredOtps.Any())
                {
                    _context.UserOtps.RemoveRange(expiredOtps);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"🧹 Cleaned up {expiredOtps.Count} expired OTPs for user {user.user_id}");
                }

                // Check rate limiting (simple check)
                var cutoffTime = DateTime.UtcNow.AddMinutes(-15); // 15 minutes
                var recentOtpCount = await _context.UserOtps
                    .Where(o => o.Email.ToLower() == email.ToLower() && o.CreatedAt >= cutoffTime)
                    .CountAsync();

                if (recentOtpCount >= 3) // Max 3 OTPs per 15 minutes
                {
                    _logger.LogWarning($"❌ Rate limit exceeded for email: {email} ({recentOtpCount} requests)");
                    return true; // Return true for security
                }

                // Generate secure 6-digit OTP
                var otpCode = GenerateSecureOtp();
                _logger.LogInformation($"🔑 Generated OTP: {otpCode} for user {user.user_id}");

                // Create OTP record with simple approach
                var userOtp = new UserOtp
                {
                    UserId = user.user_id,
                    OtpCode = otpCode,
                    Email = email.ToLower(),
                    ExpiresAt = DateTime.UtcNow.AddMinutes(5), // 5 minutes expiry
                    IsUsed = false,
                    Attempts = 0,
                    CreatedAt = DateTime.UtcNow
                };

                _context.UserOtps.Add(userOtp);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"💾 OTP saved to database with ID: {userOtp.OtpId}");

                // Send OTP email
                var emailSent = await _emailService.SendOtpEmailAsync(
                    email, 
                    otpCode, 
                    $"{user.first_name} {user.last_name}"
                );

                if (!emailSent)
                {
                    _logger.LogError($"❌ Failed to send email to {email}, removing OTP from database");
                    _context.UserOtps.Remove(userOtp);
                    await _context.SaveChangesAsync();
                    return false;
                }

                _logger.LogInformation($"✅ OTP email sent successfully to {email}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error generating OTP for email: {email}");
                return false;
            }
        }

        public async Task<bool> ValidateOtpAsync(string email, string otpCode)
        {
            try
            {
                _logger.LogInformation($"🔍 Validating OTP for email: {email}");

                // Find the most recent valid OTP for this email
                var userOtp = await _context.UserOtps
                    .Where(otp => otp.Email.ToLower() == email.ToLower() &&
                                  otp.OtpCode == otpCode &&
                                  !otp.IsUsed &&
                                  otp.ExpiresAt > DateTime.UtcNow &&
                                  otp.Attempts < 3)
                    .OrderByDescending(otp => otp.CreatedAt)
                    .FirstOrDefaultAsync();

                if (userOtp == null)
                {
                    _logger.LogWarning($"❌ Invalid OTP attempt for email: {email}");
                    
                    // Increment attempts for existing OTP if found
                    var existingOtp = await _context.UserOtps
                        .Where(otp => otp.Email.ToLower() == email.ToLower() &&
                                      !otp.IsUsed &&
                                      otp.ExpiresAt > DateTime.UtcNow)
                        .OrderByDescending(otp => otp.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (existingOtp != null && existingOtp.Attempts < 3)
                    {
                        existingOtp.Attempts++;
                        await _context.SaveChangesAsync();
                        _logger.LogInformation($"⚠️ Incremented attempts to {existingOtp.Attempts} for email: {email}");
                    }

                    return false;
                }

                // Mark OTP as used
                userOtp.IsUsed = true;
                userOtp.Attempts++;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"✅ OTP validated successfully for email: {email}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error validating OTP for email: {email}");
                return false;
            }
        }

        public async Task<bool> CleanupExpiredOtpsAsync()
        {
            try
            {
                var expiredOtps = await _context.UserOtps
                    .Where(otp => otp.ExpiresAt <= DateTime.UtcNow || otp.IsUsed)
                    .ToListAsync();

                if (expiredOtps.Any())
                {
                    _context.UserOtps.RemoveRange(expiredOtps);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"🧹 Cleaned up {expiredOtps.Count} expired OTPs");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error cleaning up expired OTPs");
                return false;
            }
        }

        private static string GenerateSecureOtp()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            
            // Convert to positive integer and get 6 digits
            var number = Math.Abs(BitConverter.ToInt32(bytes, 0));
            return (number % 1000000).ToString("D6");
        }
    }
}