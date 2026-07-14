using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Auslosungen;

public class LostopfEintragKonfiguration : IEntityTypeConfiguration<LostopfEintrag>
{
    public void Configure(EntityTypeBuilder<LostopfEintrag> builder)
    {
        builder.ToTable("LostopfEintraege");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PlatzhalterName).HasMaxLength(200);

        builder.HasOne(e => e.Lostopf)
            .WithMany(l => l.Eintraege)
            .HasForeignKey(e => e.LostopfId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Mannschaft)
            .WithMany()
            .HasForeignKey(e => e.MannschaftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.QualifikationsRegel)
            .WithMany()
            .HasForeignKey(e => e.QualifikationsRegelId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
