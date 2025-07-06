using System.ComponentModel.DataAnnotations;

namespace gasopper_crm_server.DTOs
{
    public class SendOtpDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public class VerifyOtpDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(6, MinimumLength = 6)]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP must be exactly 6 digits")]
        public string OtpCode { get; set; } = string.Empty;
    }

    public class OtpResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public UserInfoDto? User { get; set; }
        public string? Token { get; set; }
    }
}