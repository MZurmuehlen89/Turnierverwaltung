using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Turniere;

public class TurnierRundeKonfiguration : IEntityTypeConfiguration<TurnierRunde>
{
    public void Configure(EntityTypeBuilder<TurnierRunde> builder)
    {
        builder.ToTable("TurnierRunden");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Reihenfolge).IsRequired();
        builder.Property(r => r.Rundenart).IsRequired();
        builder.Property(r => r.EingangsMannschaftsanzahl).IsRequired();
        builder.Property(r => r.AusgangsMannschaftsanzahl).IsRequired();
        builder.Property(r => r.AuswaertstorRegelAktiv).IsRequired();
        builder.Property(r => r.KonfigurationJson).HasColumnType("nvarchar(max)");

        builder.HasOne(r => r.TurnierInstanz)
            .WithMany(i => i.Runden)
            .HasForeignKey(r => r.TurnierInstanzId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Regelwerk)
            .WithMany()
            .HasForeignKey(r => r.RegelwerkId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.TurnierInstanzId, r.Reihenfolge }).IsUnique();
    }
}
