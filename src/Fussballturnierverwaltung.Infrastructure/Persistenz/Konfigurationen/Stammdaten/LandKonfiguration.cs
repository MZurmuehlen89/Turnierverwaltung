using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Stammdaten;

public class LandKonfiguration : IEntityTypeConfiguration<Land>
{
    public void Configure(EntityTypeBuilder<Land> builder)
    {
        builder.ToTable("Laender");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Name).IsRequired().HasMaxLength(200);
        builder.Property(l => l.Kurzname).IsRequired().HasMaxLength(50);
        builder.Property(l => l.FifaCode).IsRequired().HasMaxLength(3);
        builder.Property(l => l.Kontinent).IsRequired();
        builder.HasIndex(l => l.FifaCode).IsUnique();

        builder.HasOne(l => l.Verband)
            .WithMany(v => v.Laender)
            .HasForeignKey(l => l.VerbandId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
