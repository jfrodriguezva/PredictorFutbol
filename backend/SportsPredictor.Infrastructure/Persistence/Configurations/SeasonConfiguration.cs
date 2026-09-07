using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPredictor.Domain.Entities;

namespace SportsPredictor.Infrastructure.Persistence.Configurations;

public class SeasonConfiguration : IEntityTypeConfiguration<Season>
{
    public void Configure(EntityTypeBuilder<Season> builder)
    {
        builder.ToTable("Seasons");

        builder.Property(s => s.Name).IsRequired().HasMaxLength(50);

        builder.HasOne(s => s.Competition)
            .WithMany()
            .HasForeignKey(s => s.CompetitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.CompetitionId, s.Name }).IsUnique();
    }
}
