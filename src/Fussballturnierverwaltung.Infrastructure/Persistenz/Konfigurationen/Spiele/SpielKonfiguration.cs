using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Spiele;

public class SpielKonfiguration : IEntityTypeConfiguration<Spiel>
{
    public void Configure(EntityTypeBuilder<Spiel> builder)
    {
        builder.ToTable("Spiele");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Spielnummer).IsRequired();
        builder.Property(s => s.Status).IsRequired();
        builder.Property(s => s.SimulationsModus).IsRequired();
        builder.Property(s => s.IstRueckspiel).IsRequired();
        builder.Property(s => s.Datum).HasColumnType("date");

        // TurnierInstanzId: NoAction — Spiele werden über TurnierRunde (CASCADE) gelöscht,
        // wenn TurnierInstanz gelöscht wird. Der direkte FK vermeidet Multi-Cascade-Konflikt.
        builder.HasOne(s => s.TurnierInstanz)
            .WithMany(i => i.Spiele)
            .HasForeignKey(s => s.TurnierInstanzId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(s => s.TurnierRunde)
            .WithMany(r => r.Spiele)
            .HasForeignKey(s => s.TurnierRundeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(s => s.HeimPlatzhalterName).HasMaxLength(200);
        builder.Property(s => s.AuswaertsPlatzhalterName).HasMaxLength(200);

        // Zwei FKs auf Mannschaft: beide Restrict, nullable für Platzhalter-Spiele
        builder.HasOne(s => s.HeimMannschaft)
            .WithMany()
            .HasForeignKey(s => s.HeimMannschaftId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.AuswaertsMannschaft)
            .WithMany()
            .HasForeignKey(s => s.AuswaertsMannschaftId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // Selbstreferenz: Rückspiel zeigt auf sein Hinspiel
        builder.HasOne(s => s.Hinspiel)
            .WithMany()
            .HasForeignKey(s => s.HinspielId)
            .OnDelete(DeleteBehavior.NoAction);

        // Selbstreferenzen: welches Vorrundenspiel speist Heim-/Auswärtsplatz (Issue #241).
        // NoAction analog zu HinspielId — vermeidet Multi-Cascade-Konflikt über TurnierRunde.
        builder.HasOne(s => s.HeimQuellSpiel)
            .WithMany()
            .HasForeignKey(s => s.HeimQuellSpielId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(s => s.AuswaertsQuellSpiel)
            .WithMany()
            .HasForeignKey(s => s.AuswaertsQuellSpielId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
