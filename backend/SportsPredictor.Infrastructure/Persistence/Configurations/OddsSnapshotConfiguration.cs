using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPredictor.Domain.Entities;

namespace SportsPredictor.Infrastructure.Persistence.Configurations;

public class OddsSnapshotConfiguration : IEntityTypeConfiguration<OddsSnapshot>
{
    public void Configure(EntityTypeBuilder<OddsSnapshot> builder)
    {
        builder.ToTable("OddsSnapshots");

        builder.Property(o => o.Sportsbook).IsRequired().HasMaxLength(100);
        builder.Property(o => o.Market).IsRequired().HasMaxLength(50);
        builder.Property(o => o.Selection).IsRequired().HasMaxLength(50);

        // No explicit HasColumnType: EF Core's Sqlite provider maps decimal to a TEXT
        // column by default, which avoids SQLite's automatic NUMERIC-affinity coercion
        // to REAL/INTEGER that a "decimal(10,4)"-style declaration would trigger.

        builder.HasOne(o => o.Match)
            .WithMany()
            .HasForeignKey(o => o.MatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(o => new { o.MatchId, o.Sportsbook, o.Market, o.CapturedAt });
    }
}
