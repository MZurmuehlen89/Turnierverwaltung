using Fussballturnierverwaltung.Domain.Entitaeten.Turnierbaum;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Turnierbaum;

public class TurnierBaumKonfiguration : IEntityTypeConfiguration<TurnierBaum>
{
    public void Configure(EntityTypeBuilder<TurnierBaum> builder)
    {
        builder.ToTable("TurnierBaeume");
        builder.HasKey(b => b.Id);

        builder.HasOne(b => b.TurnierInstanz)
            .WithMany()
            .HasForeignKey(b => b.TurnierInstanzId)
            .OnDelete(DeleteBehavior.Cascade);

        // WurzelKnotenId: nullable — wird gesetzt nachdem der Knoten existiert.
        // NoAction: Knoten-Löschung setzt WurzelKnotenId nicht automatisch (App-Logik zuständig).
        builder.HasOne(b => b.WurzelKnoten)
            .WithMany()
            .HasForeignKey(b => b.WurzelKnotenId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
