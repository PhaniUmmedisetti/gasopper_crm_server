using Microsoft.EntityFrameworkCore;
using gasopper_crm_server.Models;

namespace gasopper_crm_server.Data
{
    public class GasopperDbContext : DbContext
    {
        public GasopperDbContext(DbContextOptions<GasopperDbContext> options) : base(options)
        {
        }

        // Core entities
        public DbSet<Role> Roles { get; set; }
        public DbSet<User> Users { get; set; }
        
        // Lead management
        public DbSet<LeadStatus> LeadStatuses { get; set; }
        public DbSet<Lead> Leads { get; set; }
        
        // Opportunity management (UPDATED)
        public DbSet<OpportunityStatus> OpportunityStatuses { get; set; }  // NEW - Simple status system
        public DbSet<Opportunity> Opportunities { get; set; }
        
        // Gas station management
        public DbSet<StationType> StationTypes { get; set; }
        public DbSet<GasStation> GasStations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Role entity
            modelBuilder.Entity<Role>(entity =>
            {
                entity.ToTable("roles");
                entity.HasKey(e => e.role_id);
                entity.Property(e => e.role_name).IsRequired().HasMaxLength(50);
                entity.HasIndex(e => e.role_name).IsUnique();
            });

            // Configure User entity
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(e => e.user_id);
                entity.Property(e => e.employee_id).IsRequired().HasMaxLength(20);
                entity.Property(e => e.email).IsRequired().HasMaxLength(320);
                entity.Property(e => e.phone_number).IsRequired().HasMaxLength(20);
                entity.Property(e => e.address).IsRequired();
                entity.Property(e => e.first_name).IsRequired().HasMaxLength(50);
                entity.Property(e => e.last_name).IsRequired().HasMaxLength(50);
                entity.Property(e => e.password_hash).IsRequired().HasMaxLength(255);
                entity.Property(e => e.jwt_token).HasMaxLength(500);
                entity.Property(e => e.is_active).HasDefaultValue(true);
                entity.Property(e => e.created_at).HasDefaultValueSql("NOW()");
                entity.Property(e => e.last_updated).HasDefaultValueSql("NOW()");

                // Indexes
                entity.HasIndex(e => e.employee_id).IsUnique();
                entity.HasIndex(e => e.email).IsUnique();
                entity.HasIndex(e => e.role_id);
                entity.HasIndex(e => e.manager_id);
                entity.HasIndex(e => e.is_active);

                // Relationships
                entity.HasOne(e => e.Role)
                      .WithMany(r => r.Users)
                      .HasForeignKey(e => e.role_id)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Manager)
                      .WithMany(u => u.DirectReports)
                      .HasForeignKey(e => e.manager_id)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // Configure LeadStatus entity (SIMPLIFIED)
            modelBuilder.Entity<LeadStatus>(entity =>
            {
                entity.ToTable("lead_statuses");
                entity.HasKey(e => e.status_id);
                entity.Property(e => e.status_name).IsRequired().HasMaxLength(50);
                entity.Property(e => e.description);
                entity.HasIndex(e => e.status_name).IsUnique();
            });

            // Configure Lead entity
            modelBuilder.Entity<Lead>(entity =>
            {
                entity.ToTable("leads");
                entity.HasKey(e => e.lead_id);
                entity.Property(e => e.name).IsRequired().HasMaxLength(150);
                entity.Property(e => e.phone_number).IsRequired().HasMaxLength(20);
                entity.Property(e => e.email).IsRequired().HasMaxLength(320);
                entity.Property(e => e.address).IsRequired();
                entity.Property(e => e.expected_stations).IsRequired();
                entity.Property(e => e.referral_name).HasMaxLength(100);
                entity.Property(e => e.referral_email).HasMaxLength(320);
                entity.Property(e => e.referral_phone).HasMaxLength(20);
                entity.Property(e => e.referral_address);
                entity.Property(e => e.is_deleted).HasDefaultValue(false);
                entity.Property(e => e.created_at).HasDefaultValueSql("NOW()");
                entity.Property(e => e.last_updated).HasDefaultValueSql("NOW()");

                // Indexes
                entity.HasIndex(e => e.assigned_to);
                entity.HasIndex(e => e.created_by);
                entity.HasIndex(e => e.status_id);
                entity.HasIndex(e => e.is_deleted);

                // Relationships - EXPLICITLY configure to avoid shadow properties
                entity.HasOne(e => e.Status)
                      .WithMany(s => s.Leads)
                      .HasForeignKey(e => e.status_id);

                entity.HasOne(e => e.AssignedToUser)
                      .WithMany(u => u.AssignedLeads)
                      .HasForeignKey(e => e.assigned_to)
                      .OnDelete(DeleteBehavior.Restrict)
                      .HasConstraintName("FK_leads_assigned_to_users");

                entity.HasOne(e => e.CreatedByUser)
                      .WithMany(u => u.CreatedLeads)
                      .HasForeignKey(e => e.created_by)
                      .OnDelete(DeleteBehavior.Restrict)
                      .HasConstraintName("FK_leads_created_by_users");
            });

            // Configure OpportunityStatus entity (NEW - Simple system)
            modelBuilder.Entity<OpportunityStatus>(entity =>
            {
                entity.ToTable("opportunity_statuses");
                entity.HasKey(e => e.status_id);
                entity.Property(e => e.status_name).IsRequired().HasMaxLength(50);
                entity.Property(e => e.description);
                entity.HasIndex(e => e.status_name).IsUnique();
            });

            // Configure Opportunity entity (UPDATED)
            modelBuilder.Entity<Opportunity>(entity =>
            {
                entity.ToTable("opportunities");
                entity.HasKey(e => e.opportunity_id);
                entity.Property(e => e.owner_name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.owner_address).IsRequired();
                entity.Property(e => e.is_deleted).HasDefaultValue(false);
                entity.Property(e => e.created_at).HasDefaultValueSql("NOW()");
                entity.Property(e => e.last_updated).HasDefaultValueSql("NOW()");

                // Indexes
                entity.HasIndex(e => e.lead_id).IsUnique(); // 1:1 relationship with leads
                entity.HasIndex(e => e.assigned_to);
                entity.HasIndex(e => e.created_by);
                entity.HasIndex(e => e.status_id);
                entity.HasIndex(e => e.is_deleted);

                // Relationships - EXPLICITLY configure to avoid shadow properties
                entity.HasOne(e => e.Lead)
                      .WithOne(l => l.Opportunity)
                      .HasForeignKey<Opportunity>(e => e.lead_id)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.OpportunityStatus)
                      .WithMany(s => s.Opportunities)
                      .HasForeignKey(e => e.status_id);

                entity.HasOne(e => e.AssignedToUser)
                      .WithMany(u => u.AssignedOpportunities)
                      .HasForeignKey(e => e.assigned_to)
                      .OnDelete(DeleteBehavior.Restrict)
                      .HasConstraintName("FK_opportunities_assigned_to_users");

                entity.HasOne(e => e.CreatedByUser)
                      .WithMany(u => u.CreatedOpportunities)
                      .HasForeignKey(e => e.created_by)
                      .OnDelete(DeleteBehavior.Restrict)
                      .HasConstraintName("FK_opportunities_created_by_users");
            });

            // Configure StationType entity
            modelBuilder.Entity<StationType>(entity =>
            {
                entity.ToTable("station_types");
                entity.HasKey(e => e.station_type_id);
                entity.Property(e => e.type_name).IsRequired().HasMaxLength(50);
                entity.HasIndex(e => e.type_name).IsUnique();
            });

            // Configure GasStation entity
            modelBuilder.Entity<GasStation>(entity =>
            {
                entity.ToTable("gas_stations");
                entity.HasKey(e => e.station_id);
                entity.Property(e => e.station_name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.address).IsRequired();
                entity.Property(e => e.poc_name).HasMaxLength(100);
                entity.Property(e => e.poc_phone).HasMaxLength(20);
                entity.Property(e => e.poc_email).HasMaxLength(320);
                entity.Property(e => e.notes);
                entity.Property(e => e.is_deleted).HasDefaultValue(false);
                entity.Property(e => e.created_at).HasDefaultValueSql("NOW()");
                entity.Property(e => e.last_updated).HasDefaultValueSql("NOW()");

                // Indexes
                entity.HasIndex(e => e.opportunity_id);
                entity.HasIndex(e => e.created_by);
                entity.HasIndex(e => e.station_type_id);
                entity.HasIndex(e => e.is_deleted);

                // Relationships - EXPLICITLY configure to avoid shadow properties
                entity.HasOne(e => e.Opportunity)
                      .WithMany(o => o.GasStations)
                      .HasForeignKey(e => e.opportunity_id)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.StationType)
                      .WithMany(st => st.GasStations)
                      .HasForeignKey(e => e.station_type_id)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.CreatedByUser)
                      .WithMany(u => u.CreatedGasStations)
                      .HasForeignKey(e => e.created_by)
                      .OnDelete(DeleteBehavior.Restrict)
                      .HasConstraintName("FK_gas_stations_created_by_users");
            });

            // Configure check constraints
            modelBuilder.Entity<Lead>()
                .ToTable(t => t.HasCheckConstraint("CK_leads_expected_stations", "expected_stations > 0"));

            modelBuilder.Entity<GasStation>()
                .ToTable(t => t.HasCheckConstraint("CK_gas_stations_number_of_pumps", "number_of_pumps > 0"))
                .ToTable(t => t.HasCheckConstraint("CK_gas_stations_number_of_employees", "number_of_employees > 0"));
        }

        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateTimestamps()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity is SoftDeleteEntity && e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                ((SoftDeleteEntity)entry.Entity).last_updated = DateTime.UtcNow;
            }
        }
    }
}