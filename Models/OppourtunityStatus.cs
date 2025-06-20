using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace gasopper_crm_server.Models
{
    [Table("opportunity_statuses")]
    public class OpportunityStatus
    {
        [Key]
        public int status_id { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string status_name { get; set; } = string.Empty;
        
        public string? description { get; set; }
        
        // Navigation property - one status can have many opportunities
        public virtual ICollection<Opportunity> Opportunities { get; set; } = new List<Opportunity>();
    }
}