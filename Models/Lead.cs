// Your Lead model should look like this - please check if it matches:

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace gasopper_crm_server.Models
{
    [Table("leads")]
    public class Lead : SoftDeleteEntity
    {
        [Key]
        public int lead_id { get; set; }
        
        [Required]
        [MaxLength(150)]
        public string name { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(20)]
        public string phone_number { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(320)]
        public string email { get; set; } = string.Empty;
        
        [Required]
        public string address { get; set; } = string.Empty;
        
        [Required]
        public int expected_stations { get; set; }
        
        // Referral information (optional)
        [MaxLength(100)]
        public string? referral_name { get; set; }
        
        [MaxLength(320)]
        public string? referral_email { get; set; }
        
        [MaxLength(20)]
        public string? referral_phone { get; set; }
        
        public string? referral_address { get; set; }
        
        // Foreign keys
        public int? status_id { get; set; }
        public int assigned_to { get; set; }
        public int created_by { get; set; }
        
        // Navigation properties - THESE MUST MATCH DbContext
        [ForeignKey("status_id")]
        public virtual LeadStatus? Status { get; set; }
        
        [ForeignKey("assigned_to")]
        public virtual User AssignedToUser { get; set; } = null!;
        
        [ForeignKey("created_by")]
        public virtual User CreatedByUser { get; set; } = null!;
        
        // 1:1 relationship with opportunity
        public virtual Opportunity? Opportunity { get; set; }
    }
}