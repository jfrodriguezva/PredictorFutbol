using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPredictor.Domain.Entities;

namespace SportsPredictor.Infrastructure.Persistence.Configurations;

public class DataAvailabilityConfiguration : IEntityTypeConfiguration<DataAvailability>
{
    public void Configure(EntityTypeBuilder<DataAvailability> builder)
    {
        builder.ToTable("DataAvailabilities");

        builder.HasOne(d => d.Match)
            .WithMany()
            .HasForeignKey(d => d.MatchId)
            .OnDelete(DeleteBehavior.Restrict);

        // One coverage record per match; re-checking coverage updates this row's CapturedAt.
        builder.HasIndex(d => d.MatchId).IsUnique();
    }
}
