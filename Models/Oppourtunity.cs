using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace gasopper_crm_server.Models
{
    [Table("opportunities")]
    public class Opportunity : SoftDeleteEntity
    {
        [Key]
        public int opportunity_id { get; set; }
        
        // Foreign key to leads (1:1 relationship)
        [Required]
        public int lead_id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string owner_name { get; set; } = string.Empty;
        
        [Required]
        public string owner_address { get; set; } = string.Empty;
        
        // Status tracking (replaces old stage_id)
        [Required]
        public int status_id { get; set; }
        
        // Assignment tracking
        [Required]
        public int assigned_to { get; set; }
        
        [Required]
        public int created_by { get; set; }
        
        // Navigation properties
        [ForeignKey("lead_id")]
        public virtual Lead Lead { get; set; } = null!;
        
        [ForeignKey("status_id")]
        public virtual OpportunityStatus OpportunityStatus { get; set; } = null!;
        
        [ForeignKey("assigned_to")]
        public virtual User AssignedToUser { get; set; } = null!;
        
        [ForeignKey("created_by")]
        public virtual User CreatedByUser { get; set; } = null!;
        
        // One opportunity can have many gas stations
        public virtual ICollection<GasStation> GasStations { get; set; } = new List<GasStation>();
    }
}