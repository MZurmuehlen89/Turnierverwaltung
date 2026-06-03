using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Spiele;

public class SpielErgebnisKonfiguration : IEntityTypeConfiguration<SpielErgebnis>
{
    public void Configure(EntityTypeBuilder<SpielErgebnis> builder)
    {
        builder.ToTable("SpielErgebnisse");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.HeimToreRegulaer).IsRequired();
        builder.Property(e => e.AuswaertsToreRegulaer).IsRequired();
        builder.Property(e => e.EntscheidungArt).IsRequired();

        // Berechnete Eigenschaften nicht in der DB speichern
        builder.Ignore(e => e.HeimToreGesamt);
        builder.Ignore(e => e.AuswaertsToreGesamt);

        // 1:1 mit Spiel — SpielErgebnis besitzt den FK
        builder.HasOne(e => e.Spiel)
            .WithOne(s => s.Ergebnis)
            .HasForeignKey<SpielErgebnis>(e => e.SpielId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.SiegerMannschaft)
            .WithMany()
            .HasForeignKey(e => e.SiegerMannschaftId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
