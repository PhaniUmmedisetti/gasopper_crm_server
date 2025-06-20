using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace gasopper_crm_server.Models
{
    [Table("users")]
    public class User
    {
        [Key]
        [Column("user_id")]
        public int user_id { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("employee_id")]
        public string employee_id { get; set; } = string.Empty;

        [Required]
        [MaxLength(320)]
        [Column("email")]
        public string email { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        [Column("phone_number")]
        public string phone_number { get; set; } = string.Empty;

        [Required]
        [Column("address")]
        public string address { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        [Column("first_name")]
        public string first_name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        [Column("last_name")]
        public string last_name { get; set; } = string.Empty;

        [Required]
        [Column("role_id")]
        public int role_id { get; set; }

        [Column("manager_id")]
        public int? manager_id { get; set; }

        [Required]
        [MaxLength(255)]
        [Column("password_hash")]
        public string password_hash { get; set; } = string.Empty;

        [MaxLength(500)]
        [Column("jwt_token")]
        public string? jwt_token { get; set; }

        [Required]
        [Column("is_active")]
        public bool is_active { get; set; } = true;

        [Required]
        [Column("iat")]
        public long iat { get; set; }

        [Required]
        [Column("exp")]
        public long exp { get; set; }

        [Column("created_at")]
        public DateTime created_at { get; set; } = DateTime.UtcNow;

        [Column("last_login")]
        public DateTime? last_login { get; set; }

        [Column("last_updated")]
        public DateTime last_updated { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("role_id")]
        public virtual Role? Role { get; set; }

        [ForeignKey("manager_id")]
        public virtual User? Manager { get; set; }

        // Collection navigation properties
        public virtual ICollection<User> DirectReports { get; set; } = new List<User>();
        public virtual ICollection<Lead> CreatedLeads { get; set; } = new List<Lead>();
        public virtual ICollection<Lead> AssignedLeads { get; set; } = new List<Lead>();
        public virtual ICollection<Opportunity> CreatedOpportunities { get; set; } = new List<Opportunity>();
        public virtual ICollection<Opportunity> AssignedOpportunities { get; set; } = new List<Opportunity>();
        public virtual ICollection<GasStation> CreatedGasStations { get; set; } = new List<GasStation>();
    }
}