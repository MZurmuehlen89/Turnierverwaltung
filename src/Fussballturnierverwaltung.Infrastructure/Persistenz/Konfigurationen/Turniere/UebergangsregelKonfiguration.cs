using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Turniere;

public class UebergangsregelKonfiguration : IEntityTypeConfiguration<Uebergangsregel>
{
    public void Configure(EntityTypeBuilder<Uebergangsregel> builder)
    {
        builder.ToTable("Uebergangsregeln");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.GruppeIndex);
        builder.Property(r => r.PlatzierungVon).IsRequired();
        builder.Property(r => r.PlatzierungBis).IsRequired();
        builder.Property(r => r.AnzahlBeste);

        builder.HasOne(r => r.QuellRunde)
            .WithMany(t => t.Uebergangsregeln)
            .HasForeignKey(r => r.QuellRundeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.ZielRunde)
            .WithMany()
            .HasForeignKey(r => r.ZielRundeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
