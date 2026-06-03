using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Regelwerke;

public class EntscheidungsVerfahrenKonfiguration : IEntityTypeConfiguration<EntscheidungsVerfahren>
{
    public void Configure(EntityTypeBuilder<EntscheidungsVerfahren> builder)
    {
        builder.ToTable("Entscheidungsverfahren");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Reihenfolge).IsRequired();
        builder.Property(e => e.VerfahrenArt).IsRequired();

        builder.HasOne(e => e.Regelwerk)
            .WithMany(r => r.Entscheidungsverfahren)
            .HasForeignKey(e => e.RegelwerkId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
