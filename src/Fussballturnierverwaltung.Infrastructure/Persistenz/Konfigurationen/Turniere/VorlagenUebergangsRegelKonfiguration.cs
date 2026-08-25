using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Turniere;

public class VorlagenUebergangsRegelKonfiguration : IEntityTypeConfiguration<VorlagenUebergangsRegel>
{
    public void Configure(EntityTypeBuilder<VorlagenUebergangsRegel> builder)
    {
        builder.ToTable("VorlagenUebergangsregeln");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.GruppeIndex);
        builder.Property(r => r.PlatzierungVon).IsRequired();
        builder.Property(r => r.PlatzierungBis).IsRequired();
        builder.Property(r => r.AnzahlBeste);

        builder.HasOne(r => r.QuellVorlagenRunde)
            .WithMany(v => v.AusgehendeUebergangsregeln)
            .HasForeignKey(r => r.QuellVorlagenRundeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.ZielVorlagenRunde)
            .WithMany()
            .HasForeignKey(r => r.ZielVorlagenRundeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
