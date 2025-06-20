using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace gasopper_crm_server.Models
{
    [Table("roles")]
    public class Role
    {
        [Key]
        [Column("role_id")]
        public int role_id { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("role_name")]
        public string role_name { get; set; } = string.Empty;

        // Navigation property
        public virtual ICollection<User> Users { get; set; } = new List<User>();
    }
}