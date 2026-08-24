using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Turniere;

public class TurnierInstanzKonfiguration : IEntityTypeConfiguration<TurnierInstanz>
{
    public void Configure(EntityTypeBuilder<TurnierInstanz> builder)
    {
        builder.ToTable("TurnierInstanzen");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.SaisonOderJahr).IsRequired().HasMaxLength(20);
        builder.Property(t => t.Status).IsRequired();
        builder.Property(t => t.Startdatum).HasColumnType("date");
        builder.Property(t => t.Enddatum).HasColumnType("date");

        builder.Property(t => t.MannschaftsartFilter).IsRequired();

        builder.HasOne(t => t.TurnierVorlage)
            .WithMany(v => v.TurnierInstanzen)
            .HasForeignKey(t => t.TurnierVorlageId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(t => t.Verband)
            .WithMany()
            .HasForeignKey(t => t.VerbandId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(t => t.Land)
            .WithMany()
            .HasForeignKey(t => t.LandId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(t => t.StandardRegelwerk)
            .WithMany()
            .HasForeignKey(t => t.StandardRegelwerkId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
