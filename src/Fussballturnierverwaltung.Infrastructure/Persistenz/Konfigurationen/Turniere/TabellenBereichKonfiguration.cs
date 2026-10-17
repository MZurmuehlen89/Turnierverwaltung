using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Konfigurationen.Turniere;

public class TabellenBereichKonfiguration : IEntityTypeConfiguration<TabellenBereich>
{
    public void Configure(EntityTypeBuilder<TabellenBereich> builder)
    {
        builder.ToTable("TabellenBereiche");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.PlatzVon).IsRequired();
        builder.Property(b => b.PlatzBis).IsRequired();
        builder.Property(b => b.Art).IsRequired();
        builder.Property(b => b.Bezeichnung).IsRequired().HasMaxLength(100);

        builder.HasOne(b => b.TurnierRunde)
            .WithMany(r => r.TabellenBereiche)
            .HasForeignKey(b => b.TurnierRundeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
