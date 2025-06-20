using System.ComponentModel.DataAnnotations;

namespace gasopper_crm_server.Models
{
    // Base entity for tables with soft delete functionality
    public abstract class SoftDeleteEntity
    {
        [Required]
        public DateTime created_at { get; set; } = DateTime.UtcNow;
        
        [Required]
        public DateTime last_updated { get; set; } = DateTime.UtcNow;
        
        public bool is_deleted { get; set; } = false;
    }
    
    // Base entity for tables without soft delete (like users)
    public abstract class BaseEntity
    {
        [Required]
        public DateTime created_at { get; set; } = DateTime.UtcNow;
        
        [Required]
        public DateTime last_updated { get; set; } = DateTime.UtcNow;
    }
}