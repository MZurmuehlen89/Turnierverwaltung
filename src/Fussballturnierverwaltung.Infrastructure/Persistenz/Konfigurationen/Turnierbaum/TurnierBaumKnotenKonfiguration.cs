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

        // Primäre Elternbeziehung: Knoten gehört zu einem Baum (CASCADE)
        builder.HasOne(k => k.TurnierBaum)
            .WithMany(b => b.Knoten)
            .HasForeignKey(k => k.TurnierBaumId)
            .OnDelete(DeleteBehavior.Cascade);

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

        // Knoten kann optional ein Spiel referenzieren; Spiel-Löschung setzt SpielId auf null
        builder.HasOne(k => k.Spiel)
            .WithMany()
            .HasForeignKey(k => k.SpielId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
