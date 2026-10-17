using FluentAssertions;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Seed;
using Fussballturnierverwaltung.Infrastructure.Seed.Daten;

namespace Fussballturnierverwaltung.Tests.Seed;

public class SeedVorlagenAbgleichTests
{
    private const int VORLAGE_ID = 7;

    // ── Struktur ───────────────────────────────────────────────────────────────

    [Fact]
    public void GruppenAnzahlErgaenzen_TraegtLeereGruppenanzahlNach()
    {
        var (bestand, soll) = Em24();
        Gruppenphase(bestand).GruppenAnzahl = null;

        SeedVorlagenAbgleich.GruppenAnzahlErgaenzen(bestand, soll).Should().Be(1);

        Gruppenphase(bestand).GruppenAnzahl.Should().Be(Gruppenphase(soll).GruppenAnzahl).And.NotBeNull();
    }

    [Fact]
    public void GruppenAnzahlErgaenzen_LaesstGesetzteGruppenanzahlStehen()
    {
        var (bestand, soll) = Em24();
        Gruppenphase(bestand).GruppenAnzahl = 4;

        SeedVorlagenAbgleich.GruppenAnzahlErgaenzen(bestand, soll).Should().Be(0);

        Gruppenphase(bestand).GruppenAnzahl.Should().Be(4);
    }

    [Fact]
    public void FehlendeUebergangsregeln_VorlageOhneRegeln_LiefertDieRegelnDesSeedsMitDenRundenDesBestands()
    {
        var (bestand, soll) = Em24();
        var sollRegeln = soll.Runden.SelectMany(r => r.Uebergangsregeln).ToList();

        var regeln = SeedVorlagenAbgleich.FehlendeUebergangsregeln(bestand, soll, []);

        regeln.Should().HaveCount(sollRegeln.Count).And.NotBeEmpty();
        regeln.Should().OnlyContain(u => u.QuellRundeId == Gruppenphase(bestand).Id);
        regeln.Select(u => (u.GruppeIndex, u.PlatzierungVon, u.PlatzierungBis, u.AnzahlBeste)).Should().Equal(
            sollRegeln.Select(u => (u.GruppeIndex, u.PlatzierungVon, u.PlatzierungBis, u.AnzahlBeste)));
    }

    [Fact]
    public void FehlendeUebergangsregeln_VorlageMitEigenerRegel_BleibtLeer()
    {
        var (bestand, soll) = Em24();
        var eigeneRegel = new Uebergangsregel { QuellRundeId = Gruppenphase(bestand).Id, ZielRundeId = 99 };

        SeedVorlagenAbgleich.FehlendeUebergangsregeln(bestand, soll, [eigeneRegel]).Should().BeEmpty();
    }

    [Fact]
    public void FehlendeUebergangsregeln_UmgebauteRunde_BleibtLeer()
    {
        var (bestand, soll) = Em24();
        Gruppenphase(bestand).AusgangsMannschaftsanzahl = 8;

        SeedVorlagenAbgleich.FehlendeUebergangsregeln(bestand, soll, []).Should().BeEmpty();
    }

    [Fact]
    public void EingangUeberUebergangsregelnSetzen_NurBeiRundenMitEingehenderRegel()
    {
        var (bestand, soll) = Em24();
        var zielRunde = SeedVorlagenAbgleich.Gegenstueck(bestand, soll.Runden.First(r => r.EingangUeberUebergangsregeln))!;
        zielRunde.EingangUeberUebergangsregeln = false;
        var eingehendeRegel = new Uebergangsregel { QuellRundeId = Gruppenphase(bestand).Id, ZielRundeId = zielRunde.Id };

        SeedVorlagenAbgleich.EingangUeberUebergangsregelnSetzen(bestand, soll, []).Should().Be(0);
        zielRunde.EingangUeberUebergangsregeln.Should().BeFalse();

        SeedVorlagenAbgleich.EingangUeberUebergangsregelnSetzen(bestand, soll, [eingehendeRegel]).Should().Be(1);
        zielRunde.EingangUeberUebergangsregeln.Should().BeTrue();
    }

    // ── Fester K.-o.-Baum ──────────────────────────────────────────────────────

    [Fact]
    public void FestenKoBaumUebertragen_UnveraenderteVorlage_LiefertVorbelegungenUndMarkiertDieRunden()
    {
        var (bestand, soll) = Wm48OhneFestenBaum();

        var vorbelegungen = SeedVorlagenAbgleich.FestenKoBaumUebertragen(bestand, soll);

        vorbelegungen.Should().HaveCount(soll.Auslosungen.Count).And.NotBeEmpty();
        vorbelegungen.Should().OnlyContain(a => a.TurnierInstanzId == VORLAGE_ID && a.Ergebnisse.Count > 0);
        var betroffeneRunden = bestand.Runden.Where(r => vorbelegungen.Any(a => a.TurnierRundeId == r.Id)).ToList();
        betroffeneRunden.Should().HaveCount(vorbelegungen.Count).And.OnlyContain(r => r.PaarungFestVorgegeben);
        betroffeneRunden.Should().Contain(r => r.KombinationstabelleJson != null);
    }

    [Fact]
    public void FestenKoBaumUebertragen_EineBetroffeneRundeUmgebaut_AendertNichts()
    {
        var (bestand, soll) = Wm48OhneFestenBaum();
        bestand.Runden.Single(r => r.Name == "Achtelfinale").EingangsMannschaftsanzahl = 8;

        SeedVorlagenAbgleich.FestenKoBaumUebertragen(bestand, soll).Should().BeEmpty();

        bestand.Runden.Should().OnlyContain(r => !r.PaarungFestVorgegeben && r.KombinationstabelleJson == null);
    }

    [Fact]
    public void FestenKoBaumUebertragen_BetroffeneRundeHatSchonEineAuslosung_AendertNichts()
    {
        var (bestand, soll) = Wm48OhneFestenBaum();
        bestand.Auslosungen.Add(new Auslosung { TurnierRundeId = bestand.Runden.Single(r => r.Name == "Achtelfinale").Id });

        SeedVorlagenAbgleich.FestenKoBaumUebertragen(bestand, soll).Should().BeEmpty();

        bestand.Runden.Should().OnlyContain(r => !r.PaarungFestVorgegeben);
    }

    // ── Tabellenbereiche (#420) ───────────────────────────────────────────────

    [Fact]
    public void FehlendeTabellenBereiche_VorlageOhneBereiche_LiefertDieBereicheDesSeedsFuerDieRundeDesBestands()
    {
        var (bestand, soll) = BestandUndSoll("Bundesliga", "18 Teams");
        bestand.Runden.Single().TabellenBereiche.Clear();

        var bereiche = SeedVorlagenAbgleich.FehlendeTabellenBereiche(bestand, soll);

        bereiche.Should().OnlyContain(b => b.TurnierRundeId == bestand.Runden.Single().Id);
        bereiche.Select(b => (b.PlatzVon, b.PlatzBis, b.Art, b.Bezeichnung)).Should().Equal(
            soll.Runden.Single().TabellenBereiche.Select(b => (b.PlatzVon, b.PlatzBis, b.Art, b.Bezeichnung)));
    }

    [Fact]
    public void FehlendeTabellenBereiche_RundeMitEigenenBereichen_BleibtLeer()
    {
        var (bestand, soll) = BestandUndSoll("Bundesliga", "18 Teams");
        var saison = bestand.Runden.Single();
        saison.TabellenBereiche.Clear();
        saison.TabellenBereiche.Add(new TabellenBereich { PlatzVon = 1, PlatzBis = 1, Art = TabellenBereichArt.Weiterkommen, Bezeichnung = "Meister" });

        SeedVorlagenAbgleich.FehlendeTabellenBereiche(bestand, soll).Should().BeEmpty();
    }

    [Fact]
    public void FehlendeTabellenBereiche_UmgebauteRunde_BleibtLeer()
    {
        var (bestand, soll) = BestandUndSoll("Bundesliga", "18 Teams");
        var saison = bestand.Runden.Single();
        saison.TabellenBereiche.Clear();
        saison.EingangsMannschaftsanzahl = 20;

        SeedVorlagenAbgleich.FehlendeTabellenBereiche(bestand, soll).Should().BeEmpty();
    }

    // ── Aufbau ─────────────────────────────────────────────────────────────────

    private static (TurnierInstanz Bestand, TurnierInstanz Soll) Em24() => BestandUndSoll("UEFA EM", "24 Teams · 2016+");

    private static (TurnierInstanz Bestand, TurnierInstanz Soll) Wm48OhneFestenBaum()
    {
        var (bestand, soll) = BestandUndSoll("FIFA WM", "48 Teams · ab 2026");
        bestand.Auslosungen.Clear();
        foreach (var runde in bestand.Runden)
        {
            runde.PaarungFestVorgegeben = false;
            runde.KombinationstabelleJson = null;
        }
        return (bestand, soll);
    }

    // Der Bestand ist der Seed-Stand mit vergebenen Schlüsseln, wie ihn die Datenbank zurückgibt.
    private static (TurnierInstanz Bestand, TurnierInstanz Soll) BestandUndSoll(string name, string variante)
    {
        TurnierInstanz AusSeed() => TurnierVorlagenSeedDaten.Erstellen().Single(v => v.Name == name && v.Variante == variante);

        var bestand = AusSeed();
        bestand.Id = VORLAGE_ID;
        int rundenId = 100;
        foreach (var runde in bestand.Runden)
        {
            runde.Id = rundenId++;
            runde.TurnierInstanzId = VORLAGE_ID;
        }
        return (bestand, AusSeed());
    }

    private static TurnierRunde Gruppenphase(TurnierInstanz vorlage) =>
        vorlage.Runden.Single(r => r.Rundenart == Rundenart.Gruppenphase);
}
