using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Turniere;

public class VerbandsQuoteKonfiguration : IEntityTypeConfiguration<VerbandsQuote>
{
    public void Configure(EntityTypeBuilder<VerbandsQuote> builder)
    {
        builder.ToTable("VerbandsQuoten");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Anzahl).IsRequired();

        builder.HasOne(q => q.TurnierInstanz)
            .WithMany(t => t.VerbandsQuoten)
            .HasForeignKey(q => q.TurnierInstanzId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(q => q.Verband)
            .WithMany()
            .HasForeignKey(q => q.VerbandId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
