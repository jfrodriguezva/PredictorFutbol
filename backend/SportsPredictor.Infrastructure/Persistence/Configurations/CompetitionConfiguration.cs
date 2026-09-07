using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPredictor.Domain.Entities;

namespace SportsPredictor.Infrastructure.Persistence.Configurations;

public class CompetitionConfiguration : IEntityTypeConfiguration<Competition>
{
    public void Configure(EntityTypeBuilder<Competition> builder)
    {
        builder.ToTable("Competitions");

        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Country).HasMaxLength(100);
        builder.Property(c => c.CompetitionType).HasConversion<string>().HasMaxLength(30);

        builder.HasOne(c => c.Sport)
            .WithMany()
            .HasForeignKey(c => c.SportId)
            .OnDelete(DeleteBehavior.Restrict);

        // Filtered unique index: many rows can have a null ExternalApiFootballId
        // (not yet ingested from the provider), but a non-null value must be unique.
        builder.HasIndex(c => c.ExternalApiFootballId)
            .IsUnique()
            .HasFilter("\"ExternalApiFootballId\" IS NOT NULL");
    }
}
