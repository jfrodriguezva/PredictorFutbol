using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPredictor.Domain.Entities;

namespace SportsPredictor.Infrastructure.Persistence.Configurations;

public class InjurySnapshotConfiguration : IEntityTypeConfiguration<InjurySnapshot>
{
    public void Configure(EntityTypeBuilder<InjurySnapshot> builder)
    {
        builder.ToTable("InjurySnapshots");

        builder.Property(i => i.Type).IsRequired().HasMaxLength(50);
        builder.Property(i => i.Reason).HasMaxLength(200);
        builder.Property(i => i.Source).IsRequired().HasMaxLength(50);

        builder.HasOne(i => i.Player)
            .WithMany()
            .HasForeignKey(i => i.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Team)
            .WithMany()
            .HasForeignKey(i => i.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Match)
            .WithMany()
            .HasForeignKey(i => i.MatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.PlayerId, i.CapturedAt });
    }
}
