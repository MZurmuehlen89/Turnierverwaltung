using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Turniere;

public class TurnierGastgeberKonfiguration : IEntityTypeConfiguration<TurnierGastgeber>
{
    public void Configure(EntityTypeBuilder<TurnierGastgeber> builder)
    {
        builder.ToTable("TurnierGastgeber");
        builder.HasKey(g => g.Id);
        builder.HasIndex(g => new { g.TurnierInstanzId, g.MannschaftId }).IsUnique();

        builder.HasOne(g => g.TurnierInstanz)
            .WithMany(t => t.Gastgeber)
            .HasForeignKey(g => g.TurnierInstanzId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict statt Cascade: Mannschaft → TurnierInstanz (Titelverteidiger, SetNull) → TurnierGastgeber
        // wäre sonst ein zweiter Kaskadenpfad, den SQL Server ablehnt — analog Spiel → Mannschaft.
        builder.HasOne(g => g.Mannschaft)
            .WithMany()
            .HasForeignKey(g => g.MannschaftId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
