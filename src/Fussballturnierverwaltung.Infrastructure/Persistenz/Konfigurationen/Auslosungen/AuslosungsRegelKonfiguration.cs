using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Auslosungen;

public class AuslosungsRegelKonfiguration : IEntityTypeConfiguration<AuslosungsRegel>
{
    public void Configure(EntityTypeBuilder<AuslosungsRegel> builder)
    {
        builder.ToTable("AuslosungsRegeln");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Regelart).IsRequired();
        builder.Property(r => r.Beschreibung).HasMaxLength(500);
        builder.Property(r => r.IstAktiv).IsRequired();

        builder.HasOne(r => r.Auslosung)
            .WithMany(a => a.Regeln)
            .HasForeignKey(r => r.AuslosungId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
