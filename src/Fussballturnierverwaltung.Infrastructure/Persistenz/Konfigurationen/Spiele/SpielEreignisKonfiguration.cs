using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Spiele;

public class SpielEreignisKonfiguration : IEntityTypeConfiguration<SpielEreignis>
{
    public void Configure(EntityTypeBuilder<SpielEreignis> builder)
    {
        builder.ToTable("SpielEreignisse");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Minute).IsRequired();
        builder.Property(e => e.EreignisArt).IsRequired();
        builder.Property(e => e.Beschreibung).HasMaxLength(500);

        builder.HasOne(e => e.Spiel)
            .WithMany(s => s.Ereignisse)
            .HasForeignKey(e => e.SpielId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Mannschaft)
            .WithMany()
            .HasForeignKey(e => e.MannschaftId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
