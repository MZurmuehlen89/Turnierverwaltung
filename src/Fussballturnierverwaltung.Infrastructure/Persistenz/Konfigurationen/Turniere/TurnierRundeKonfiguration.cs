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
        builder.Property(r => r.GruppenAnzahl);
        builder.Property(r => r.MitRueckspiel).IsRequired();
        builder.Property(r => r.AuswaertstorRegelAktiv).IsRequired();
        builder.Property(r => r.KonfigurationJson).HasColumnType("nvarchar(max)");
        builder.Property(r => r.KombinationstabelleJson).HasColumnType("nvarchar(max)");
        builder.Property(r => r.EingangUeberUebergangsregeln).IsRequired().HasDefaultValue(false);
        builder.Property(r => r.VorgegebeneAuslosungsart).IsRequired(false);

        builder.HasOne(r => r.TurnierInstanz)
            .WithMany(i => i.Runden)
            .HasForeignKey(r => r.TurnierInstanzId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Regelwerk)
            .WithMany()
            .HasForeignKey(r => r.RegelwerkId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.Property(r => r.TitelverteidigerInTopf1).IsRequired().HasDefaultValue(false);

        builder.HasOne(r => r.StandardRanglistenQuelle)
            .WithMany()
            .HasForeignKey(r => r.StandardRanglistenQuelleId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasIndex(r => new { r.TurnierInstanzId, r.Reihenfolge }).IsUnique();
    }
}
