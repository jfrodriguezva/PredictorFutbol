using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPredictor.Domain.Entities;

namespace SportsPredictor.Infrastructure.Persistence.Configurations;

public class StandingSnapshotConfiguration : IEntityTypeConfiguration<StandingSnapshot>
{
    public void Configure(EntityTypeBuilder<StandingSnapshot> builder)
    {
        builder.ToTable("StandingSnapshots");

        builder.Property(s => s.Form).HasMaxLength(20);

        builder.HasOne(s => s.Competition)
            .WithMany()
            .HasForeignKey(s => s.CompetitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Season)
            .WithMany()
            .HasForeignKey(s => s.SeasonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Team)
            .WithMany()
            .HasForeignKey(s => s.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        // Snapshots are append-only; this index is what lets us reconstruct
        // "the table as of just before match X" efficiently.
        builder.HasIndex(s => new { s.SeasonId, s.TeamId, s.CapturedAt });
    }
}
