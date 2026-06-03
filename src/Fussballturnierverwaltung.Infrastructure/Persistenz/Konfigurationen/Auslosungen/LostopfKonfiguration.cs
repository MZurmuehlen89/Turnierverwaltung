using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Auslosungen;

public class LostopfKonfiguration : IEntityTypeConfiguration<Lostopf>
{
    public void Configure(EntityTypeBuilder<Lostopf> builder)
    {
        builder.ToTable("Lostoepfe");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Name).IsRequired().HasMaxLength(100);
        builder.Property(l => l.Reihenfolge).IsRequired();

        builder.HasOne(l => l.Auslosung)
            .WithMany(a => a.Lostoepfe)
            .HasForeignKey(l => l.AuslosungId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => new { l.AuslosungId, l.Reihenfolge }).IsUnique();
    }
}
