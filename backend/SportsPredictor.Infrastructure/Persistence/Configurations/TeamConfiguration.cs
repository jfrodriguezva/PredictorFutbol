using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPredictor.Domain.Entities;

namespace SportsPredictor.Infrastructure.Persistence.Configurations;

public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("Teams");

        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Country).HasMaxLength(100);

        builder.HasOne(t => t.Sport)
            .WithMany()
            .HasForeignKey(t => t.SportId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.ExternalApiFootballId)
            .IsUnique()
            .HasFilter("\"ExternalApiFootballId\" IS NOT NULL");
    }
}
