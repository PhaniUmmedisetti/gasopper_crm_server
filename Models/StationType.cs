using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace gasopper_crm_server.Models
{
    [Table("station_types")]
    public class StationType
    {
        [Key]
        public int station_type_id { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string type_name { get; set; } = string.Empty;
        
        // Navigation property
        public virtual ICollection<GasStation> GasStations { get; set; } = new List<GasStation>();
    }
}