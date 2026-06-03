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

        // Zwei FKs auf Mannschaft: beide Restrict, da SQL Server nur einen CASCADE-Pfad pro Tabelle erlaubt
        builder.HasOne(s => s.HeimMannschaft)
            .WithMany()
            .HasForeignKey(s => s.HeimMannschaftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.AuswaertsMannschaft)
            .WithMany()
            .HasForeignKey(s => s.AuswaertsMannschaftId)
            .OnDelete(DeleteBehavior.Restrict);

        // Selbstreferenz: Rückspiel zeigt auf sein Hinspiel
        builder.HasOne(s => s.Hinspiel)
            .WithMany()
            .HasForeignKey(s => s.HinspielId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
