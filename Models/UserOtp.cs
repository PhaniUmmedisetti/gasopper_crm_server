using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace gasopper_crm_server.Models
{
    [Table("user_otps")]
    public class UserOtp
    {
        [Key]
        [Column("otp_id")]
        public int OtpId { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Required]
        [MaxLength(6)]
        [Column("otp_code")]
        public string OtpCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(320)]
        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Column("expires_at")]
        public DateTime ExpiresAt { get; set; }

        [Column("is_used")]
        public bool IsUsed { get; set; } = false;

        [Column("attempts")]
        public int Attempts { get; set; } = 0;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // NO NAVIGATION PROPERTIES - this eliminates the user_id1 column issue
    }
}