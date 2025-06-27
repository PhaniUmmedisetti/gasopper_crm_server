using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace gasopper_crm_server.Models
{
    [Table("opportunities")]
    public class Opportunity : SoftDeleteEntity
    {
        [Key]
        public int opportunity_id { get; set; }
        
        [Required]
        public int lead_id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string owner_name { get; set; } = string.Empty;
        
        // KEEP OLD FIELD for backward compatibility (if you want)
        public string? owner_address { get; set; }
        
        // ADD NEW FIELDS for split address
        [MaxLength(200)]
        public string? address_line_1 { get; set; }
        
        [MaxLength(200)]
        public string? address_line_2 { get; set; }
        
        [MaxLength(100)]
        public string? city { get; set; }
        
        [MaxLength(100)]
        public string? state { get; set; }
        
        [MaxLength(20)]
        public string? postal_code { get; set; }
        
        [MaxLength(100)]
        public string? country { get; set; } = "United States";
        
        [Required]
        public int status_id { get; set; }
        
        [Required]
        public int assigned_to { get; set; }
        
        [Required]
        public int created_by { get; set; }
        
        // Navigation properties (unchanged)
        [ForeignKey("lead_id")]
        public virtual Lead Lead { get; set; } = null!;
        
        [ForeignKey("status_id")]
        public virtual OpportunityStatus OpportunityStatus { get; set; } = null!;
        
        [ForeignKey("assigned_to")]
        public virtual User AssignedToUser { get; set; } = null!;
        
        [ForeignKey("created_by")]
        public virtual User CreatedByUser { get; set; } = null!;
        
        public virtual ICollection<GasStation> GasStations { get; set; } = new List<GasStation>();
    }
}