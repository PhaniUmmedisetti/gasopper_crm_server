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

        // FIXED: Make owner_address nullable since new conversions use split fields
        // public string? owner_address { get; set; }

        // NEW: Split address fields
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

        // ADDED: Actual number of stations (user input during conversion)
        [Required]
        public int actual_stations { get; set; } = 0;

        [Required]
        public int status_id { get; set; }

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

        public virtual ICollection<GasStation> GasStations { get; set; } = new List<GasStation>();
    }
}