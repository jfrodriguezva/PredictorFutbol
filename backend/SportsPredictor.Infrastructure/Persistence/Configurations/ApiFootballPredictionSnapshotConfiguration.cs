using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPredictor.Domain.Entities;

namespace SportsPredictor.Infrastructure.Persistence.Configurations;

public class ApiFootballPredictionSnapshotConfiguration : IEntityTypeConfiguration<ApiFootballPredictionSnapshot>
{
    public void Configure(EntityTypeBuilder<ApiFootballPredictionSnapshot> builder)
    {
        builder.ToTable("ApiFootballPredictionSnapshots");

        builder.Property(p => p.PredictedWinner).HasMaxLength(200);
        builder.Property(p => p.PredictedScore).HasMaxLength(50);
        builder.Property(p => p.RawJson).IsRequired();

        builder.HasOne(p => p.Match)
            .WithMany()
            .HasForeignKey(p => p.MatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.MatchId, p.CapturedAt });
    }
}
