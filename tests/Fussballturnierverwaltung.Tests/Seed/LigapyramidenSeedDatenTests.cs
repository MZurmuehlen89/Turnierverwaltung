using FluentAssertions;
using Fussballturnierverwaltung.Infrastructure.Seed.Daten;

namespace Fussballturnierverwaltung.Tests.Seed;

/// <summary>Seed-Daten der Ligapyramiden (#393, #575, #580, #582): passen zu den Vereinen und tragen die Landes-Kontingente.</summary>
public class LigapyramidenSeedDatenTests
{
    private static readonly IReadOnlyDictionary<string, int> _landIds =
        LaenderSeedDaten.Erstellen(VerbaendeSeedDaten.Erstellen().Select((v, i) => (v.Kurzname, Id: i + 1))
                .ToDictionary(v => v.Kurzname, v => v.Id))
            .Select((l, i) => (l.FifaCode, Id: i + 100))
            .ToDictionary(l => l.FifaCode, l => l.Id);

    [Fact]
    public void JedesLandHatHoechstensEinePyramide() =>
        LigapyramidenSeedDaten.Alle.Select(p => p.Land).Should().OnlyHaveUniqueItems();

    // Ein Name, den der Seed nicht kennt, würde still zur Lücke — Tippfehler fallen nur hier auf.
    [Fact]
    public void JederVereinDerPyramidenStehtImSeedSeinesLandesOderEinesGastlands()
    {
        var landJeId = _landIds.ToDictionary(l => l.Value, l => l.Key);
        var vereine = VereinsmannschaftenSeedDaten.Erstellen(_landIds)
            .Select(m => (Land: landJeId[m.LandId], m.Name))
            .ToHashSet();

        var unbekannt = LigapyramidenSeedDaten.Alle
            .SelectMany(p => p.Abschnitte.SelectMany(a => a.Vereine).OfType<string>().Select(name => (Pyramide: p, Name: name)))
            .Where(v => !v.Pyramide.VereinsLaender.Any(land => vereine.Contains((land, v.Name))))
            .Select(v => (v.Pyramide.Land, v.Name))
            .ToList();

        unbekannt.Should().BeEmpty();
    }

    // „Platz 1 = Meister“ (#574): jedes Kontingent braucht in seiner Spielklasse einen Verein auf jedem seiner Plätze.
    [Fact]
    public void JedesLandesKontingent_FindetAufJedemSeinerPlaetzeEinenVerein()
    {
        var pyramideJeLand = LigapyramidenSeedDaten.Alle.ToDictionary(p => p.Land);

        var unerfuellt = QuotenSeedDaten.JeVorlage
            .SelectMany(v => v.Value.Laender.Select(k => (Vorlage: $"{v.Key.Name} {v.Key.Variante}", Kontingent: k)))
            .Where(x => !PlaetzeBesetzt(pyramideJeLand.GetValueOrDefault(x.Kontingent.FifaCode), x.Kontingent))
            .Select(x => $"{x.Vorlage}: {x.Kontingent}")
            .ToList();

        unerfuellt.Should().BeEmpty();
    }

    // Parallele Staffeln einer Klasse zählen ihre Plätze durch, wie in Kontingente.Spielklassen.
    private static bool PlaetzeBesetzt(LigapyramidenSeedDaten.Pyramide? pyramide, QuotenSeedDaten.LandesKontingent kontingent)
    {
        if (pyramide is null) return false;
        var klasse = pyramide.Abschnitte.Where(a => a.Spielklasse == kontingent.Spielklasse).SelectMany(a => a.Vereine).ToList();
        int bis = kontingent.AbPlatz + kontingent.Anzahl - 1;
        return bis <= klasse.Count && klasse.Skip(kontingent.AbPlatz - 1).Take(kontingent.Anzahl).All(v => v is not null);
    }
}
