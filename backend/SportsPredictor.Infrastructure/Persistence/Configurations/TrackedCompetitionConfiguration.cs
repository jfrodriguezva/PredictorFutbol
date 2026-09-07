using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPredictor.Domain.Entities;

namespace SportsPredictor.Infrastructure.Persistence.Configurations;

public class TrackedCompetitionConfiguration : IEntityTypeConfiguration<TrackedCompetition>
{
    public void Configure(EntityTypeBuilder<TrackedCompetition> builder)
    {
        builder.ToTable("TrackedCompetitions");

        builder.HasOne(t => t.Competition)
            .WithMany()
            .HasForeignKey(t => t.CompetitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => new { t.CompetitionId, t.Season }).IsUnique();
        builder.HasIndex(t => t.ExternalLeagueId);
    }
}
