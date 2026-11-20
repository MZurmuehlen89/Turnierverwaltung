using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>Charakterisierung der Ereigniszeitleiste (#509 Schnitt A, K1): exakte Zeilenlisten, am unverändert
/// verschobenen Code festgehalten. Beim Aufräumen bleiben die Erwartungen stehen, nur <see cref="Bauen"/> ändert sich.</summary>
public class ZeitleisteTests
{
    private const string HEIM = "Heimteam";
    private const string GAST = "Gastteam";

    private static (int Minute, int? Nachspielminute, EreignisArt Art, bool IstHeimSeite, string Beschreibung, bool ImWiederholungsspiel)
        E(int minute, EreignisArt art, bool heim, string beschreibung = "", int? nachspiel = null, bool wiederholung = false)
        => (minute, nachspiel, art, heim, beschreibung, wiederholung);

    private static List<EinzelspielEreignisZeile> Bauen(
        IEnumerable<(int Minute, int? Nachspielminute, EreignisArt Art, bool IstHeimSeite, string Beschreibung, bool ImWiederholungsspiel)> ereignisse,
        int heimRegulaer, int auswaertsRegulaer,
        EntscheidungArt art = EntscheidungArt.RegulaereSpielzeit,
        (int Heim, int Auswaerts)? verlaengerung = null,
        (int Heim, int Auswaerts)? elfmeter = null,
        Wiederholungsergebnis? wiederholung = null,
        byte[]? heimWappen = null, byte[]? auswaertsWappen = null)
    {
        var stand = new SimulationsErgebnis(
            heimRegulaer, auswaertsRegulaer,
            verlaengerung?.Heim, verlaengerung?.Auswaerts,
            elfmeter?.Heim, elfmeter?.Auswaerts,
            art, [], Wiederholung: wiederholung);
        return Zeitleiste.Erstellen(
            stand,
            ereignisse.Select(e => new ZeitleistenEreignis(e.Minute, e.Nachspielminute, e.Art, e.IstHeimSeite, e.Beschreibung, e.ImWiederholungsspiel)),
            new SpielSeite(new Mannschaft { Name = HEIM, Wappen = heimWappen }, HEIM),
            new SpielSeite(new Mannschaft { Name = GAST, Wappen = auswaertsWappen }, GAST)).ToList();
    }

    private static string Text(EinzelspielEreignisZeile z)
        => z.IstTrenner
            ? $"[{z.TrennerArt}] {z.TrennerText} @{z.MinuteWert}"
            : $"{z.Minute} {z.Seite} {z.Art}" + (z.Detail is null ? "" : $" | {z.Detail}");

    private static List<string> Texte(List<EinzelspielEreignisZeile> zeilen)
        => zeilen.Select(Text).ToList();

    // ── Aufbau ───────────────────────────────────────────────────────────

    [Fact]
    public void KeineEreignisse_LeereListe()
    {
        Bauen([], 0, 0).Should().BeEmpty();
    }

    [Fact]
    public void ToreInBeidenHaelften_HalbzeitMitStand_RahmtMitAnUndAbpfiff()
    {
        var zeilen = Bauen(
            [E(20, EreignisArt.Tor, true, "Tor per Kopfball (oben links)"), E(60, EreignisArt.Tor, false, "Tor")],
            1, 1);

        Texte(zeilen).Should().Equal(
            "[Start] ANPFIFF @0",
            "20' Heimteam Tor | per Kopfball (oben links)",
            "[Abschnitt] HALBZEIT · 1:0 @45",
            "60' Gastteam Tor",
            "[Ende] ABPFIFF · 1:1 @0");
    }

    [Fact]
    public void HalbzeitOhneEreignisDanach_WirdNachgetragen()
    {
        var zeilen = Bauen([E(10, EreignisArt.Tor, true, "Tor")], 1, 0);

        Texte(zeilen).Should().Equal(
            "[Start] ANPFIFF @0",
            "10' Heimteam Tor",
            "[Abschnitt] HALBZEIT · 1:0 @45",
            "[Ende] ABPFIFF · 1:0 @0");
    }

    [Fact]
    public void Nachspielzeit_StehtNachDerRegulaerenMinute_HalbzeitErstVorMinute46()
    {
        // Eingabereihenfolge absichtlich durcheinander: 45+3 vor 45+1
        var zeilen = Bauen(
            [E(45, EreignisArt.GelbeKarte, false, "Gelbe Karte Müller", nachspiel: 3),
             E(45, EreignisArt.Tor, true, "Tor", nachspiel: 1),
             E(46, EreignisArt.RoteKarte, true, "Rote Karte")],
            1, 0);

        Texte(zeilen).Should().Equal(
            "[Start] ANPFIFF @0",
            "45+1' Heimteam Tor",
            "45+3' Gastteam Gelbe Karte | Müller",
            "[Abschnitt] HALBZEIT · 1:0 @45",
            "46' Heimteam Rote Karte",
            "[Ende] ABPFIFF · 1:0 @0");
    }

    [Fact]
    public void VerlaengerungUndElfmeterschiessen_Abschnittstrenner_SchuesseNachEndeVerlaengerung()
    {
        var zeilen = Bauen(
            [E(30, EreignisArt.Tor, true, "Tor"),
             E(70, EreignisArt.Tor, false, "Tor"),
             E(100, EreignisArt.Tor, true, "Tor"),
             E(118, EreignisArt.Tor, false, "Tor"),
             E(121, EreignisArt.ElfmeterTor, true, "Elfmeter verwandelt"),
             E(122, EreignisArt.ElfmeterVerschossen, false, "Elfmeter verschossen")],
            1, 1, EntscheidungArt.Elfmeterschiessen, verlaengerung: (1, 1), elfmeter: (4, 3));

        Texte(zeilen).Should().Equal(
            "[Start] ANPFIFF @0",
            "30' Heimteam Tor",
            "[Abschnitt] HALBZEIT · 1:0 @45",
            "70' Gastteam Tor",
            "[Abschnitt] 90 MINUTEN · 1:1 @90",
            "100' Heimteam Tor",
            "[Abschnitt] HALBZEIT VERLÄNGERUNG @105",
            "118' Gastteam Tor",
            "[Abschnitt] ENDE VERLÄNGERUNG · 2:2 @120",
            "121' Heimteam Elfmeter verwandelt",
            "122' Gastteam Elfmeter verschossen",
            "[Elfmeter] ELFMETERSCHIESSEN · 4:3 @0",
            "[Ende] ABPFIFF · 2:2 (4:3 i.E.) @0");
    }

    [Fact]
    public void Wiederholungsspiel_MitEigenenEreignissen_EigenerAbschnittMitNeuerUhr()
    {
        var zeilen = Bauen(
            [E(20, EreignisArt.Tor, true, "Tor"),
             E(80, EreignisArt.Tor, false, "Tor"),
             E(10, EreignisArt.Tor, true, "Tor", wiederholung: true),
             E(50, EreignisArt.Tor, true, "Tor", wiederholung: true),
             E(70, EreignisArt.Tor, false, "Tor", wiederholung: true)],
            1, 1, EntscheidungArt.Wiederholungsspiel, wiederholung: new Wiederholungsergebnis(2, 1, null, null));

        Texte(zeilen).Should().Equal(
            "[Start] ANPFIFF @0",
            "20' Heimteam Tor",
            "[Abschnitt] HALBZEIT · 1:0 @45",
            "80' Gastteam Tor",
            "[Wiederholung] WIEDERHOLUNGSSPIEL @0",
            "10' Heimteam Tor",
            "50' Heimteam Tor",
            "70' Gastteam Tor",
            "[Wiederholung] ENDE WIEDERHOLUNGSSPIEL · 2:1 @0",
            "[Ende] ABPFIFF · 1:1 (Wdh. 2:1) @0");
    }

    [Fact]
    public void Wiederholungsspiel_OhneEigeneEreignisse_AbschnittNurMitEndstand()
    {
        var zeilen = Bauen(
            [E(20, EreignisArt.Tor, true, "Tor"), E(30, EreignisArt.Tor, false, "Tor")],
            1, 1, EntscheidungArt.Wiederholungsspiel, wiederholung: new Wiederholungsergebnis(1, 0, null, null));

        Texte(zeilen).Should().Equal(
            "[Start] ANPFIFF @0",
            "20' Heimteam Tor",
            "30' Gastteam Tor",
            "[Abschnitt] HALBZEIT · 1:1 @45",
            "[Wiederholung] WIEDERHOLUNGSSPIEL @0",
            "[Wiederholung] ENDE WIEDERHOLUNGSSPIEL · 1:0 @0",
            "[Ende] ABPFIFF · 1:1 (Wdh. 1:0) @0");
    }

    [Fact]
    public void Wiederholungsspiel_NurEreignisseOhneErgebnis_KeineEndmarke()
    {
        var zeilen = Bauen(
            [E(20, EreignisArt.Tor, true, "Tor"), E(5, EreignisArt.Tor, false, "Tor", wiederholung: true)],
            1, 0);

        Texte(zeilen).Should().Equal(
            "[Start] ANPFIFF @0",
            "20' Heimteam Tor",
            "[Abschnitt] HALBZEIT · 1:0 @45",
            "[Wiederholung] WIEDERHOLUNGSSPIEL @0",
            "5' Gastteam Tor",
            "[Ende] ABPFIFF · 1:0 @0");
    }

    // ── Zeilen ───────────────────────────────────────────────────────────

    [Fact]
    public void Detailtext_OhnePraefixDerArt_BzwEntfaelltBeiWiederholungOderFehlen()
    {
        var zeilen = Bauen(
            [E(1, EreignisArt.Tor, true, "Tor per Kopfball nach Konter (oben links)"),
             E(2, EreignisArt.GelbeKarte, false, "Gelbe Karte"),
             E(3, EreignisArt.GelbeKarte, true, "Foulspiel von Meier"),
             E(4, EreignisArt.Tor, false, ""),
             E(5, EreignisArt.Tor, true, "tor in der Nachspielzeit"),
             E(6, EreignisArt.Grosschance, false, "Großchance vergeben (Latte)")],
            2, 0);

        Texte(zeilen).Where(t => !t.StartsWith('[')).Should().Equal(
            "1' Heimteam Tor | per Kopfball nach Konter (oben links)",
            "2' Gastteam Gelbe Karte",
            "3' Heimteam Gelbe Karte | Foulspiel von Meier",
            "4' Gastteam Tor",
            "5' Heimteam Tor | in der Nachspielzeit",
            "6' Gastteam Großchance | vergeben (Latte)");
    }

    [Fact]
    public void Zeilen_TragenWappenDerSeite_UndDieArtKennzeichen()
    {
        byte[] heimWappen = [1], auswaertsWappen = [2];
        var zeilen = Bauen(
            [E(1, EreignisArt.Tor, true),
             E(2, EreignisArt.GelbeKarte, false),
             E(3, EreignisArt.RoteKarte, true),
             E(4, EreignisArt.ElfmeterVerschossen, false),
             E(5, EreignisArt.ElfmeterTor, true),
             E(6, EreignisArt.Taktikwechsel, false),
             E(7, EreignisArt.Grosschance, true),
             E(8, EreignisArt.GoldenGoal, false),
             E(9, EreignisArt.SilverGoal, true)],
            3, 2, heimWappen: heimWappen, auswaertsWappen: auswaertsWappen);

        static string Kennzeichen(EinzelspielEreignisZeile z) =>
            (z.IstTor ? "T" : "") + (z.IstGelbeKarte ? "G" : "") + (z.IstRoteKarte ? "R" : "")
            + (z.IstElfmeterVerschossen ? "V" : "") + (z.IstElfmeterschuss ? "S" : "")
            + (z.IstTaktikwechsel ? "K" : "") + (z.IstGrosschance ? "C" : "");

        var ereignisse = zeilen.Where(z => !z.IstTrenner).ToList();

        ereignisse.Select(z => $"{z.MinuteWert} {z.Art} {Kennzeichen(z)} {(z.IstHeimSeite ? "H" : "A")} {z.Wappen![0]}")
            .Should().Equal(
                "1 Tor T H 1",
                "2 Gelbe Karte G A 2",
                "3 Rote Karte R H 1",
                "4 Elfmeter verschossen VS A 2",
                "5 Elfmeter verwandelt TS H 1",
                "6 Taktikwechsel K A 2",
                "7 Großchance C H 1",
                "8 Golden Goal T A 2",
                "9 Silver Goal T H 1");
        zeilen.Where(z => z.IstTrenner).Should().OnlyContain(z => z.Wappen == null);
    }
}
