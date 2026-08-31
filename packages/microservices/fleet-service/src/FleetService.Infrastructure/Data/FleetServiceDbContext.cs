using Microsoft.EntityFrameworkCore;
using FleetService.Core.Entities;
using System.Linq;

namespace FleetService.Infrastructure.Data;

public class FleetServiceDbContext : DbContext
{
    public FleetServiceDbContext(DbContextOptions<FleetServiceDbContext> options) : base(options) { }

    /// <summary>Constructor for derived contexts (e.g. the schema-per-tenant variant).</summary>
    protected FleetServiceDbContext(DbContextOptions options) : base(options) { }

    public DbSet<DriverProfile> DriverProfiles { get; set; }
    public DbSet<LicenseClass> LicenseClasses { get; set; }
    public DbSet<DriverProfileChange> DriverProfileChanges { get; set; }
    public DbSet<Truck> Trucks { get; set; }
    public DbSet<VehicleClass> VehicleClasses { get; set; }
    public DbSet<Material> Materials { get; set; }
    public DbSet<MaterialVariant> MaterialVariants { get; set; }
    public DbSet<MaterialPhoto> MaterialPhotos { get; set; }
    public DbSet<MaterialVariantPhoto> MaterialVariantPhotos { get; set; }
    public DbSet<MaterialCost> MaterialCosts { get; set; }
    public DbSet<TripType> TripTypes { get; set; }
    public DbSet<Trip> Trips { get; set; }
    public DbSet<Expense> Expenses { get; set; }
    public DbSet<TripDeposit> TripDeposits { get; set; }
    public DbSet<Feedback> Feedbacks { get; set; }
    public DbSet<FieldVehicle> FieldVehicles { get; set; }
    public DbSet<VehicleDispatch> VehicleDispatches { get; set; }
    public DbSet<DispatchFuelLog> DispatchFuelLogs { get; set; }
    public DbSet<FieldVehiclePhoto> FieldVehiclePhotos { get; set; }
    public DbSet<FleetAuditLog> FleetAuditLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Money-column precision backstop (#383): every decimal defaults to numeric(18,2)
        // unless a naming pattern below says otherwise, or an explicit .HasColumnType(...)
        // further down in this method overrides it for that specific property.
        foreach (var prop in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            var name = prop.Name;
            prop.SetColumnType(
                name.Contains("Latitude") || name.Contains("Longitude") ? "numeric(9,6)"
                : name.EndsWith("Pct") || name.EndsWith("Percent") || name.Contains("Probability") ? "numeric(9,4)"
                : name.Contains("ExchangeRate") ? "numeric(18,6)"
                : "numeric(18,2)");
        }

        // Global soft-delete filters
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            if (typeof(BaseEntity).IsAssignableFrom(clrType))
            {
                var parameter = System.Linq.Expressions.Expression.Parameter(clrType, "e");
                var property = System.Linq.Expressions.Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
                var notDeleted = System.Linq.Expressions.Expression.Not(property);
                var lambda = System.Linq.Expressions.Expression.Lambda(notDeleted, parameter);
                modelBuilder.Entity(clrType).HasQueryFilter(lambda);
            }
        }

        // DriverProfile ↔ LicenseClass many-to-many
        modelBuilder.Entity<DriverProfile>()
            .HasMany(d => d.LicenseClasses)
            .WithMany(l => l.DriverProfiles)
            .UsingEntity(j => j.ToTable("DriverProfileLicenseClasses"));

        // DriverProfileChange foreign keys (no cascade on both sides)
        modelBuilder.Entity<DriverProfileChange>(entity =>
        {
            entity.HasOne(e => e.OldProfile)
                .WithMany()
                .HasForeignKey(e => e.OldProfileId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.NewProfile)
                .WithMany(p => p.Changes)
                .HasForeignKey(e => e.NewProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Truck — mapped onto the live "Vehicles" table (see Truck.cs for why)
        modelBuilder.Entity<Truck>(entity =>
        {
            entity.ToTable("Vehicles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LicensePlate).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(e => e.LicensePlate).IsUnique();
            entity.HasOne(e => e.VehicleClass).WithMany().HasForeignKey(e => e.VehicleClassId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        // Trip — TruckId/TruckLocation* columns were renamed to VehicleId/VehicleLocation* by
        // the same already-applied migration (see Truck.cs); Purpose is a genuinely new column.
        modelBuilder.Entity<Trip>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TruckId).HasColumnName("VehicleId");
            entity.Property(e => e.TruckLocationLatitude).HasColumnName("VehicleLocationLatitude");
            entity.Property(e => e.TruckLocationLongitude).HasColumnName("VehicleLocationLongitude");
            entity.HasOne(e => e.Truck).WithMany(t => t.Trips).HasForeignKey(e => e.TruckId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.TripType).WithMany(tt => tt.Trips).HasForeignKey(e => e.TripTypeId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Material).WithMany().HasForeignKey(e => e.MaterialId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.MaterialVariant).WithMany().HasForeignKey(e => e.MaterialVariantId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => e.DriverId);
            entity.HasIndex(e => e.Status);
        });

        // Expense / Receipt
        modelBuilder.Entity<Expense>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Trip).WithMany(t => t.Expenses).HasForeignKey(e => e.TripId).OnDelete(DeleteBehavior.Cascade);
        });
        // TripDeposit
        modelBuilder.Entity<TripDeposit>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            entity.HasOne(e => e.Trip).WithMany(t => t.Deposits).HasForeignKey(e => e.TripId).OnDelete(DeleteBehavior.Cascade);
        });
        // MaterialVariant
        modelBuilder.Entity<MaterialVariant>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Material).WithMany(m => m.Variants).HasForeignKey(e => e.MaterialId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
