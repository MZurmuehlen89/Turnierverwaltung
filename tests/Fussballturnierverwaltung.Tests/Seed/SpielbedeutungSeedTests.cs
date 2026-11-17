using FluentAssertions;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Seed.Daten;

namespace Fussballturnierverwaltung.Tests.Seed;

/// <summary>#668 (K6): Spielbedeutung der Seed-Vorlagen – Finale hoch, Spiel um Platz 3 gering, sonst normal.</summary>
public class SpielbedeutungSeedTests
{
    [Fact]
    public void Vorlagen_SpielbedeutungNachRundenart()
    {
        var runden = TurnierVorlagenSeedDaten.Erstellen().SelectMany(v => v.Runden).ToList();

        runden.Where(r => r.Rundenart == Rundenart.Finale).Should().NotBeEmpty()
            .And.OnlyContain(r => r.Spielbedeutung == Spielbedeutung.Hoch);
        runden.Where(r => r.Rundenart == Rundenart.SpielUmPlatzDrei).Should().NotBeEmpty()
            .And.OnlyContain(r => r.Spielbedeutung == Spielbedeutung.Gering);
        runden.Where(r => r.Rundenart is not (Rundenart.Finale or Rundenart.SpielUmPlatzDrei))
            .Should().OnlyContain(r => r.Spielbedeutung == Spielbedeutung.Normal);
    }
}
