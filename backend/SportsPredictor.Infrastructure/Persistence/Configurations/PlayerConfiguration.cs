using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPredictor.Domain.Entities;

namespace SportsPredictor.Infrastructure.Persistence.Configurations;

public class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> builder)
    {
        builder.ToTable("Players");

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Position).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(p => p.Team)
            .WithMany()
            .HasForeignKey(p => p.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.ExternalApiFootballId)
            .IsUnique()
            .HasFilter("\"ExternalApiFootballId\" IS NOT NULL");
    }
}
