using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPredictor.Domain.Entities;

namespace SportsPredictor.Infrastructure.Persistence.Configurations;

public class SportConfiguration : IEntityTypeConfiguration<Sport>
{
    /// <summary>Fixed id so every other seed/migration can reference "Football" deterministically.</summary>
    public static readonly Guid FootballId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public void Configure(EntityTypeBuilder<Sport> builder)
    {
        builder.ToTable("Sports");

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(s => s.Name).IsUnique();

        // Anonymous object: HasData maps properties by name via reflection, so this
        // works even though Entity.Id has a protected setter.
        builder.HasData(new { Id = FootballId, Name = "Football" });
    }
}
