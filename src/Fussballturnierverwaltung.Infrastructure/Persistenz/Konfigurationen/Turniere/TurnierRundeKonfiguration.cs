using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
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
        builder.Property(r => r.NeutralerSpielort).IsRequired().HasDefaultValue(false);
        builder.Property(r => r.Ueberraschungsfaktor).IsRequired().HasDefaultValue(0);
        builder.Property(r => r.KonfigurationJson).HasColumnType("nvarchar(max)");
        builder.Property(r => r.KombinationstabelleJson).HasColumnType("nvarchar(max)");
        builder.Property(r => r.EingangUeberUebergangsregeln).IsRequired().HasDefaultValue(false);
        builder.Property(r => r.VorgegebeneAuslosungsart).IsRequired(false);
        builder.Property(r => r.VorgegebeneGruppenposition).IsRequired().HasDefaultValue(Gruppenposition.NachTopf);
        // Gruppennummern als JSON-Array, z. B. [1,2,4] für A, B, D (#487).
        builder.PrimitiveCollection(r => r.GastgeberGruppen).IsRequired();
        builder.Property(r => r.TitelverteidigerGruppe).IsRequired(false);
        // Liste von Enum-Werten als JSON-Array in einer Spalte, z. B. [0,8] (#356).
        builder.PrimitiveCollection(r => r.VorgegebeneAuslosungsregeln).IsRequired();

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
        builder.Property(r => r.GegnerJeTopf).IsRequired().HasDefaultValue(2);

        builder.HasOne(r => r.StandardRangliste)
            .WithMany()
            .HasForeignKey(r => r.StandardRanglisteId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasIndex(r => new { r.TurnierInstanzId, r.Reihenfolge }).IsUnique();
    }
}
