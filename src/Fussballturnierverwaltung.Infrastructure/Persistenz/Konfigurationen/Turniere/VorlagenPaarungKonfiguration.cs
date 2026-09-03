using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Turniere;

public class VorlagenPaarungKonfiguration : IEntityTypeConfiguration<VorlagenPaarung>
{
    public void Configure(EntityTypeBuilder<VorlagenPaarung> builder)
    {
        builder.ToTable("VorlagenPaarungen");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.PaarungsNummer).IsRequired();
        builder.Property(p => p.SlotNr).IsRequired();
        builder.Property(p => p.PlatzhalterBezeichnung).IsRequired().HasMaxLength(200);

        builder.HasOne(p => p.VorlagenRunde)
            .WithMany(r => r.Paarungen)
            .HasForeignKey(p => p.VorlagenRundeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.VorlagenRundeId, p.PaarungsNummer, p.SlotNr }).IsUnique();
    }
}
