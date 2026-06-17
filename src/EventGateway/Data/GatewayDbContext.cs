using Microsoft.EntityFrameworkCore;

namespace EventGateway.Data;

public sealed class GatewayDbContext(DbContextOptions<GatewayDbContext> options) : DbContext(options)
{
    public DbSet<EventRecord> Events => Set<EventRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<EventRecord>();

        entity.HasKey(e => e.EventId);

        entity.HasIndex(e => new { e.AccountId, e.EventTimestamp, e.EventId });

        entity.Property(e => e.Amount).HasConversion(
            v => v.ToString("F2"),
            v => decimal.Parse(v));

        entity.Property(e => e.EventId).HasMaxLength(128);
        entity.Property(e => e.AccountId).HasMaxLength(64);
        entity.Property(e => e.Currency).HasMaxLength(8);
    }
}
