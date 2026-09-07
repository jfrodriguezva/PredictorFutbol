using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPredictor.Domain.Entities;

namespace SportsPredictor.Infrastructure.Persistence.Configurations;

public class FeatureValueConfiguration : IEntityTypeConfiguration<FeatureValue>
{
    public void Configure(EntityTypeBuilder<FeatureValue> builder)
    {
        builder.ToTable("FeatureValues");

        builder.Property(f => f.FeatureName).IsRequired().HasMaxLength(100);

        builder.HasOne(f => f.Match)
            .WithMany()
            .HasForeignKey(f => f.MatchId)
            .OnDelete(DeleteBehavior.Restrict);

        // One value per (match, feature) at a time; a recomputation creates a new row.
        builder.HasIndex(f => new { f.MatchId, f.FeatureName, f.CapturedAt });
        builder.HasIndex(f => f.AvailableAt);
    }
}
