using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPredictor.Domain.Entities;

namespace SportsPredictor.Infrastructure.Persistence.Configurations;

public class VenueConfiguration : IEntityTypeConfiguration<Venue>
{
    public void Configure(EntityTypeBuilder<Venue> builder)
    {
        builder.ToTable("Venues");

        builder.Property(v => v.Name).IsRequired().HasMaxLength(200);
        builder.Property(v => v.City).HasMaxLength(100);
        builder.Property(v => v.Country).HasMaxLength(100);

        builder.HasIndex(v => v.ExternalApiFootballId)
            .IsUnique()
            .HasFilter("\"ExternalApiFootballId\" IS NOT NULL");
    }
}
