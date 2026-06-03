using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Stammdaten;

public class MannschaftKonfiguration : IEntityTypeConfiguration<Mannschaft>
{
    public void Configure(EntityTypeBuilder<Mannschaft> builder)
    {
        builder.ToTable("Mannschaften");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Name).IsRequired().HasMaxLength(200);
        builder.Property(m => m.Kurzname).IsRequired().HasMaxLength(50);
        builder.Property(m => m.Mannschaftsart).IsRequired();
        builder.Property(m => m.OffensivRating).IsRequired();
        builder.Property(m => m.MittelfeldRating).IsRequired();
        builder.Property(m => m.DefensivRating).IsRequired();
        builder.Property(m => m.Heimstaerke).IsRequired();
        builder.Property(m => m.Auswaertsstaerke).IsRequired();

        // Berechnete Eigenschaft, wird nicht in der Datenbank gespeichert
        builder.Ignore(m => m.GesamtRating);

        builder.HasOne(m => m.Land)
            .WithMany(l => l.Mannschaften)
            .HasForeignKey(m => m.LandId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
