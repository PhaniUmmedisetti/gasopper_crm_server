using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace gasopper_crm_server.Models
{
    [Table("lead_statuses")]
    public class LeadStatus
    {
        [Key]
        public int status_id { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string status_name { get; set; } = string.Empty;
        
        public string? description { get; set; }
        
        // Navigation property
        public virtual ICollection<Lead> Leads { get; set; } = new List<Lead>();
    }
}