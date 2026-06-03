using Fussballturnierverwaltung.Domain.Entitaeten.Turnierbaum;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Turnierbaum;

public class TurnierBaumKnotenKonfiguration : IEntityTypeConfiguration<TurnierBaumKnoten>
{
    public void Configure(EntityTypeBuilder<TurnierBaumKnoten> builder)
    {
        builder.ToTable("TurnierBaumKnoten");
        builder.HasKey(k => k.Id);
        builder.Property(k => k.Runde).IsRequired();

        // NoAction statt Cascade: TurnierBaum ↔ TurnierBaumKnoten sind bidirektional verknüpft
        // (WurzelKnotenId in TurnierBaum). SQL Server lehnt CASCADE bei Zyklen ab.
        // Die Anwendungsschicht ist für die korrekte Löschreihenfolge verantwortlich.
        builder.HasOne(k => k.TurnierBaum)
            .WithMany(b => b.Knoten)
            .HasForeignKey(k => k.TurnierBaumId)
            .OnDelete(DeleteBehavior.NoAction);

        // Selbstreferenzen: NoAction — Löschreihenfolge liegt bei der App-Logik
        builder.HasOne(k => k.ElternKnoten)
            .WithMany()
            .HasForeignKey(k => k.ElternKnotenId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(k => k.LinkerKindKnoten)
            .WithMany()
            .HasForeignKey(k => k.LinkerKindKnotenId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(k => k.RechterKindKnoten)
            .WithMany()
            .HasForeignKey(k => k.RechterKindKnotenId)
            .OnDelete(DeleteBehavior.NoAction);

        // NoAction statt SetNull: verhindert zusätzlichen Cascade-Pfad über Spiele
        builder.HasOne(k => k.Spiel)
            .WithMany()
            .HasForeignKey(k => k.SpielId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
