using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Regelwerke;

public class PunkteRegelKonfiguration : IEntityTypeConfiguration<PunkteRegel>
{
    public void Configure(EntityTypeBuilder<PunkteRegel> builder)
    {
        builder.ToTable("PunkteRegeln");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.PunkteFuerSieg).IsRequired();
        builder.Property(p => p.PunkteFuerUnentschieden).IsRequired();
        builder.Property(p => p.PunkteFuerNiederlage).IsRequired();

        builder.HasOne(p => p.Regelwerk)
            .WithMany(r => r.PunkteRegeln)
            .HasForeignKey(p => p.RegelwerkId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
