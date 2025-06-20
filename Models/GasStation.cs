using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace gasopper_crm_server.Models
{
    [Table("gas_stations")]
    public class GasStation : SoftDeleteEntity
    {
        [Key]
        public int station_id { get; set; }
        
        [Required]
        public int opportunity_id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string station_name { get; set; } = string.Empty;
        
        [Required]
        public string address { get; set; } = string.Empty;
        
        // Point of Contact (all nullable)
        [MaxLength(100)]
        public string? poc_name { get; set; }
        
        [MaxLength(20)]
        public string? poc_phone { get; set; }
        
        [MaxLength(320)]
        [EmailAddress]
        public string? poc_email { get; set; }
        
        // Station details (nullable - frontend validation)
        [Range(1, int.MaxValue, ErrorMessage = "Number of pumps must be greater than 0")]
        public int? number_of_pumps { get; set; }
        
        [Range(1, int.MaxValue, ErrorMessage = "Number of employees must be greater than 0")]
        public int? number_of_employees { get; set; }
        
        public int? station_type_id { get; set; }
        
        public string? notes { get; set; }
        
        // Foreign keys
        [Required]
        public int created_by { get; set; }
        
        // Navigation properties
        [ForeignKey("opportunity_id")]
        public virtual Opportunity Opportunity { get; set; } = null!;
        
        [ForeignKey("created_by")]
        public virtual User CreatedByUser { get; set; } = null!;
        
        [ForeignKey("station_type_id")]
        public virtual StationType? StationType { get; set; }
    }
}