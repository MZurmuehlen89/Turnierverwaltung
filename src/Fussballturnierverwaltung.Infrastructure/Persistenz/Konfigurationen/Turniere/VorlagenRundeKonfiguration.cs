using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Turniere;

public class VorlagenRundeKonfiguration : IEntityTypeConfiguration<VorlagenRunde>
{
    public void Configure(EntityTypeBuilder<VorlagenRunde> builder)
    {
        builder.ToTable("VorlagenRunden");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Reihenfolge).IsRequired();
        builder.Property(r => r.Rundenart).IsRequired();
        builder.Property(r => r.EingangsMannschaftsanzahl).IsRequired();
        builder.Property(r => r.AusgangsMannschaftsanzahl).IsRequired();
        builder.Property(r => r.MitRueckspiel).IsRequired();
        builder.Property(r => r.AuswaertstorRegelAktiv).IsRequired();

        builder.HasOne(r => r.TurnierVorlage)
            .WithMany(v => v.VorlagenRunden)
            .HasForeignKey(r => r.TurnierVorlageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Regelwerk)
            .WithMany()
            .HasForeignKey(r => r.RegelwerkId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(r => new { r.TurnierVorlageId, r.Reihenfolge }).IsUnique();
    }
}
