using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPredictor.Domain.Entities;

namespace SportsPredictor.Infrastructure.Persistence.Configurations;

public class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        builder.ToTable("Matches", t => t.HasCheckConstraint(
            "CK_Matches_HomeAwayDifferent",
            "\"HomeTeamId\" <> \"AwayTeamId\""));

        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(m => m.Competition)
            .WithMany()
            .HasForeignKey(m => m.CompetitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Season)
            .WithMany()
            .HasForeignKey(m => m.SeasonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.HomeTeam)
            .WithMany()
            .HasForeignKey(m => m.HomeTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.AwayTeam)
            .WithMany()
            .HasForeignKey(m => m.AwayTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Venue)
            .WithMany()
            .HasForeignKey(m => m.VenueId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => m.ExternalApiFootballId)
            .IsUnique()
            .HasFilter("\"ExternalApiFootballId\" IS NOT NULL");

        builder.HasIndex(m => m.MatchDate);
        builder.HasIndex(m => new { m.SeasonId, m.CompetitionId });
    }
}
