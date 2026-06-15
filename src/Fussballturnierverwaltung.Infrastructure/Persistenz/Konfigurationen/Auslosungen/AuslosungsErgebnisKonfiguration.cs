using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Auslosungen;

public class AuslosungsErgebnisKonfiguration : IEntityTypeConfiguration<AuslosungsErgebnis>
{
    public void Configure(EntityTypeBuilder<AuslosungsErgebnis> builder)
    {
        builder.ToTable("AuslosungsErgebnisse");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PaarungsNummer).IsRequired();
        builder.Property(e => e.IstFreilos).IsRequired();
        builder.Property(e => e.HeimPlatzhalterName).HasMaxLength(200);
        builder.Property(e => e.AuswaertsPlatzhalterName).HasMaxLength(200);

        builder.HasOne(e => e.Auslosung)
            .WithMany(a => a.Ergebnisse)
            .HasForeignKey(e => e.AuslosungId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.HeimMannschaft)
            .WithMany()
            .HasForeignKey(e => e.HeimMannschaftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.AuswaertsMannschaft)
            .WithMany()
            .HasForeignKey(e => e.AuswaertsMannschaftId)
            .OnDelete(DeleteBehavior.Restrict);

        // NoAction: verhindert Cascade-Pfad TurnierInstanzen → Auslosungen → AuslosungsErgebnisse → TurnierBaumKnoten
        builder.HasOne(e => e.ZugeordneterTurnierBaumKnoten)
            .WithMany()
            .HasForeignKey(e => e.ZugeordneterTurnierBaumKnotenId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
