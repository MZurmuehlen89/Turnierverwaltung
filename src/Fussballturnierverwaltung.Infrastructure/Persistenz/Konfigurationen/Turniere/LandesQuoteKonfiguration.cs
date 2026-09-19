using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Turniere;

public class LandesQuoteKonfiguration : IEntityTypeConfiguration<LandesQuote>
{
    public void Configure(EntityTypeBuilder<LandesQuote> builder)
    {
        builder.ToTable("LandesQuoten");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Anzahl).IsRequired();

        builder.HasOne(q => q.TurnierInstanz)
            .WithMany(t => t.LandesQuoten)
            .HasForeignKey(q => q.TurnierInstanzId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(q => q.Land)
            .WithMany()
            .HasForeignKey(q => q.LandId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
