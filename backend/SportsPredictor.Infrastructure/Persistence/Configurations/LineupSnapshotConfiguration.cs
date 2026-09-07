using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPredictor.Domain.Entities;

namespace SportsPredictor.Infrastructure.Persistence.Configurations;

public class LineupSnapshotConfiguration : IEntityTypeConfiguration<LineupSnapshot>
{
    public void Configure(EntityTypeBuilder<LineupSnapshot> builder)
    {
        builder.ToTable("LineupSnapshots");

        builder.Property(l => l.Formation).IsRequired().HasMaxLength(20);
        builder.Property(l => l.CoachName).HasMaxLength(200);

        builder.HasOne(l => l.Match)
            .WithMany()
            .HasForeignKey(l => l.MatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Team)
            .WithMany()
            .HasForeignKey(l => l.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.MatchId, l.TeamId, l.CapturedAt });
    }
}
