using FluentAssertions;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Infrastructure.Seed;
using Fussballturnierverwaltung.Infrastructure.Seed.Daten;

namespace Fussballturnierverwaltung.Tests.Seed;

/// <summary>Quoten und Kontingente der Turniervorlagen (#574): Seed-Daten passen zu den Vorlagen, die Nachrüstung
/// ergänzt je Kontingent.</summary>
public class QuotenSeedTests
{
    private static readonly IReadOnlyDictionary<string, int> _verbandIds =
        VerbaendeSeedDaten.Erstellen().Select((v, i) => (v.Kurzname, Id: i + 1)).ToDictionary(v => v.Kurzname, v => v.Id);

    private static readonly IReadOnlyDictionary<string, int> _landIds =
        LaenderSeedDaten.Erstellen(_verbandIds).Select((l, i) => (l.FifaCode, Id: i + 100)).ToDictionary(l => l.FifaCode, l => l.Id);

    private static List<TurnierInstanz> Vorlagen() => TurnierVorlagenSeedDaten.Erstellen(verbandLookup: _verbandIds, landLookup: _landIds);

    [Fact]
    public void JedeQuotenVorlageGibtEsImSeed()
    {
        var schluessel = Vorlagen().Select(v => (v.Name, v.Variante)).ToHashSet();

        QuotenSeedDaten.JeVorlage.Keys.Should().OnlyContain(k => schluessel.Contains(k));
    }

    [Fact]
    public void JedesKuerzelGibtEsImSeed()
    {
        var quoten = QuotenSeedDaten.JeVorlage.Values.ToList();

        quoten.SelectMany(q => q.Verbaende).Select(v => v.Verband).Should().OnlyContain(k => _verbandIds.ContainsKey(k));
        quoten.SelectMany(q => q.Laender).Select(l => l.FifaCode).Should().OnlyContain(k => _landIds.ContainsKey(k));
    }

    [Fact]
    public void QuotenPassenInDieErsteRunde()
    {
        foreach (var vorlage in Vorlagen().Where(v => v.VerbandsQuoten.Count > 0 || v.LandesQuoten.Count > 0))
        {
            int eingang = vorlage.Runden.MinBy(r => r.Reihenfolge)!.EingangsMannschaftsanzahl;
            vorlage.VerbandsQuoten.Sum(q => q.Anzahl).Should().BeLessThanOrEqualTo(eingang, $"{vorlage.Name} {vorlage.Variante}");
            vorlage.LandesQuoten.Sum(q => q.Anzahl).Should().BeLessThanOrEqualTo(eingang, $"{vorlage.Name} {vorlage.Variante}");
        }
    }

    [Fact]
    public void Erstellen_HaengtQuotenAnDieVorlage()
    {
        var vorlagen = Vorlagen();

        var wm = vorlagen.Single(v => v.Name == "FIFA WM" && v.Variante == "48 Teams · ab 2026");
        wm.VerbandsQuoten.Sum(q => q.Anzahl).Should().Be(46);
        wm.VerbandsQuoten.Single(q => q.VerbandId == _verbandIds["UEFA"]).Anzahl.Should().Be(16);

        var pokal = vorlagen.Single(v => v.Name == "DFB-Pokal");
        pokal.LandesQuoten.Select(q => (q.LandId, q.Anzahl, q.Spielklasse, q.AbPlatz)).Should().BeEquivalentTo(
            [(_landIds["GER"], 18, (int?)1, (int?)1), (_landIds["GER"], 18, 2, 1), (_landIds["GER"], 4, 3, 1)]);
    }

    [Fact]
    public void Erstellen_OhneLookups_OhneQuoten() =>
        TurnierVorlagenSeedDaten.Erstellen().Should().OnlyContain(v => v.VerbandsQuoten.Count == 0 && v.LandesQuoten.Count == 0);

    // ── Nachrüstung ────────────────────────────────────────────────────────────

    private static (TurnierInstanz Bestand, TurnierInstanz Soll) Pokal()
    {
        var soll = Vorlagen().Single(v => v.Name == "DFB-Pokal");
        var bestand = new TurnierInstanz { Id = 7, Name = soll.Name, Variante = soll.Variante };
        return (bestand, soll);
    }

    [Fact]
    public void FehlendeQuoten_VorlageOhneQuoten_LiefertAlleMitIdDerVorlage()
    {
        var (bestand, soll) = Pokal();

        var (verbaende, laender) = SeedVorlagenAbgleich.FehlendeQuoten(bestand, soll);

        verbaende.Should().BeEmpty();
        laender.Should().HaveCount(3).And.OnlyContain(q => q.TurnierInstanzId == 7);
    }

    [Fact]
    public void FehlendeQuoten_VorhandenesKontingentMitEigenerAnzahl_BleibtUndFehlendeKommen()
    {
        var (bestand, soll) = Pokal();
        bestand.LandesQuoten.Add(new LandesQuote { LandId = _landIds["GER"], Anzahl = 10, Spielklasse = 1, AbPlatz = 1 });

        var (_, laender) = SeedVorlagenAbgleich.FehlendeQuoten(bestand, soll);

        laender.Select(q => q.Spielklasse).Should().BeEquivalentTo([2, 3]);
    }

    [Fact]
    public void FehlendeQuoten_VerbandMitEigenerAnzahl_Bleibt()
    {
        var soll = Vorlagen().Single(v => v.Name == "FIFA Klub-WM");
        var bestand = new TurnierInstanz { Id = 7, Name = soll.Name, Variante = soll.Variante };
        bestand.VerbandsQuoten.Add(new VerbandsQuote { VerbandId = _verbandIds["UEFA"], Anzahl = 3 });

        var (verbaende, _) = SeedVorlagenAbgleich.FehlendeQuoten(bestand, soll);

        verbaende.Select(q => q.VerbandId).Should().NotContain(_verbandIds["UEFA"]).And.HaveCount(5);
    }
}
