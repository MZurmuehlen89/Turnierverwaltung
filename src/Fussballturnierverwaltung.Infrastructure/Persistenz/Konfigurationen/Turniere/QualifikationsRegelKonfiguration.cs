using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Turniere;

public class QualifikationsRegelKonfiguration : IEntityTypeConfiguration<QualifikationsRegel>
{
    public void Configure(EntityTypeBuilder<QualifikationsRegel> builder)
    {
        builder.ToTable("QualifikationsRegeln");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.GruppeIndex);
        builder.Property(r => r.PlatzierungVon).IsRequired();
        builder.Property(r => r.PlatzierungBis).IsRequired();

        builder.HasOne(r => r.QuellRunde)
            .WithMany(t => t.AufstiegsRegeln)
            .HasForeignKey(r => r.QuellRundeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.ZielRunde)
            .WithMany()
            .HasForeignKey(r => r.ZielRundeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
