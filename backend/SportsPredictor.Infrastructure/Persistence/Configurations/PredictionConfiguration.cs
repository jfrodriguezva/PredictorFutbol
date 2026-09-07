using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPredictor.Domain.Entities;

namespace SportsPredictor.Infrastructure.Persistence.Configurations;

public class PredictionConfiguration : IEntityTypeConfiguration<Prediction>
{
    public void Configure(EntityTypeBuilder<Prediction> builder)
    {
        builder.ToTable("Predictions");

        builder.Property(p => p.Market).IsRequired().HasMaxLength(50);
        builder.Property(p => p.Selection).IsRequired().HasMaxLength(50);
        builder.Property(p => p.ActualOutcome).HasMaxLength(50);

        builder.HasOne(p => p.Match)
            .WithMany()
            .HasForeignKey(p => p.MatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.ModelVersion)
            .WithMany()
            .HasForeignKey(p => p.ModelVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.MatchId, p.ModelVersionId, p.Market, p.PredictionDate });
    }
}
