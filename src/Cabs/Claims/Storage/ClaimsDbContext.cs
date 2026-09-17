using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;

namespace LegacyFighter.Cabs.Claims.Storage;

public class ClaimsDbContext : DbContext
{
  private readonly ClaimsDatabase _database;

  public ClaimsDbContext(ClaimsDatabase database)
  {
    _database = database;
  }

  public DbSet<ClaimRecord> Claims { get; set; } = default!;
  public DbSet<ClaimantRecord> Claimants { get; set; } = default!;
  public DbSet<ClaimedTransitRecord> ClaimedTransits { get; set; } = default!;

  protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
  {
    optionsBuilder.UseSqlite(_database.Connection);
    base.OnConfiguring(optionsBuilder);
  }

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    var instantConverter = new ValueConverter<Instant, long>(
      instant => instant.ToUnixTimeTicks(),
      ticks => Instant.FromUnixTimeTicks(ticks));

    modelBuilder.Entity<ClaimRecord>(builder =>
    {
      builder.ToTable("Claims");
      builder.HasKey(c => c.ClaimId);
      builder.Property(c => c.ClaimId).ValueGeneratedNever();
      builder.Property(c => c.Status).HasConversion<string>().IsRequired();
      builder.Property(c => c.CompletionMode).HasConversion<string>();
      builder.Property(c => c.CreatedAt).HasConversion(instantConverter).IsRequired();
    });
    modelBuilder.Entity<ClaimantRecord>(builder =>
    {
      builder.ToTable("Claimants");
      builder.HasKey(c => c.ClientId);
      builder.Property(c => c.ClientId).ValueGeneratedNever();
    });
    modelBuilder.Entity<ClaimedTransitRecord>(builder =>
    {
      builder.ToTable("ClaimedTransits");
      builder.HasKey(t => t.TransitId);
      builder.Property(t => t.TransitId).ValueGeneratedNever();
    });
  }
}
