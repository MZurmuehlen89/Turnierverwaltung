using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>#176 K1–K4 und K6: der Bedarf eines Teams aus Stand, offenen Spielen und Zonen. Zehn Teams, Zonen wie in einer
/// kleinen Liga: Rang 1–2 „Aufstieg“, Rang 9–10 „Abstieg“, dazwischen keine Zone.</summary>
public class TabellenbedarfTests
{
    private static readonly PunkteRegel DREI_EINS_NULL = new() { PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 };

    private static readonly TabellenZone[] LIGA_ZONEN =
    [
        new(null, 1, 2, null, "Aufstieg", ZonenFarbe.Gruen),
        new(null, 9, 10, null, "Abstieg", ZonenFarbe.Rot)
    ];

    /// <summary>Teams 1…n in der Reihenfolge der Angaben (Punkte, offene Spiele).</summary>
    private static Tabellenstand[] Tabelle(params (int Punkte, int Offen)[] teams) =>
        [.. teams.Select((t, i) => new Tabellenstand(i + 1, t.Punkte, t.Offen))];

    private static Tabellenstand[] EinSpielOffen(params int[] punkte) => Tabelle([.. punkte.Select(p => (p, 1))]);

    private static Tabellenbedarf Bedarf(int team, Tabellenstand[] tabelle, IReadOnlyList<TabellenZone>? zonen = null, int? gruppe = null) =>
        TabellenbedarfRechner.Berechnen(team, tabelle, zonen ?? LIGA_ZONEN, gruppe, DREI_EINS_NULL);

    // ── K1 Ohne Ziel ──────────────────────────────────────────────────────

    [Fact]
    public void Mittelfeld_dessen_Zone_kein_Ergebnis_mehr_aendert_ist_ohne_Ziel()
    {
        var tabelle = EinSpielOffen(60, 55, 40, 38, 36, 34, 32, 30, 20, 15);

        Bedarf(5, tabelle).Should().Be(Tabellenbedarf.OhneZiel);
    }

    [Fact]
    public void Rechnerisch_abgestiegenes_Team_ist_ohne_Ziel()
    {
        var tabelle = EinSpielOffen(60, 55, 40, 38, 36, 34, 32, 30, 20, 15);

        Bedarf(10, tabelle).Should().Be(Tabellenbedarf.OhneZiel);
    }

    [Fact]
    public void Ohne_Ziel_geht_dem_Remis_und_dem_Sieg_vor()
    {
        // Das Spitzenteam hat den Aufstieg sicher: es braucht weder ein Remis noch einen Sieg, sondern nichts mehr.
        var tabelle = EinSpielOffen(60, 55, 40, 38, 36, 34, 32, 30, 20, 15);

        Bedarf(1, tabelle).Should().Be(Tabellenbedarf.OhneZiel);
    }

    // ── K2 Remis reicht ───────────────────────────────────────────────────

    [Fact]
    public void Ein_Remis_reicht_wenn_es_die_beste_erreichbare_Zone_sichert()
    {
        // Team 2 (40) bleibt bei einem Remis (41) vor Team 3 (höchstens 40); bei einer Niederlage zieht Team 3 gleich.
        var tabelle = EinSpielOffen(50, 40, 37, 30, 25, 20, 15, 10, 5, 1);

        Bedarf(2, tabelle).Should().Be(Tabellenbedarf.RemisReicht);
    }

    // ── K3 Sieg nötig ─────────────────────────────────────────────────────

    [Fact]
    public void Ein_Sieg_ist_noetig_wenn_die_beste_erreichbare_Zone_nach_einem_Remis_verloren_ist()
    {
        // Team 3 (37) erreicht Rang 2 (Aufstieg) nur mit einem Sieg (40 ≥ Team 2 mit höchstens 40 … gleich zählt als vorbei).
        var tabelle = EinSpielOffen(50, 40, 37, 30, 25, 20, 15, 10, 5, 1);

        Bedarf(3, tabelle).Should().Be(Tabellenbedarf.SiegNoetig);
    }

    // ── K4 Abstiegskampf ──────────────────────────────────────────────────

    [Fact]
    public void Abstiegskampf_bei_hoechstens_drei_Punkten_ueber_dem_ersten_Abstiegsplatz()
    {
        // Team 8 (26) liegt zwei Punkte vor Rang 9 (24); fünf Spiele sind noch offen, nichts ist entschieden.
        var tabelle = Tabelle((40, 5), (38, 5), (36, 5), (34, 5), (32, 5), (30, 5), (28, 5), (26, 5), (24, 5), (22, 5));

        Bedarf(8, tabelle).Should().Be(Tabellenbedarf.Abstiegskampf);
    }

    [Fact]
    public void Kein_Abstiegskampf_bei_mehr_als_zehn_offenen_Spielen()
    {
        var tabelle = Tabelle((40, 11), (38, 11), (36, 11), (34, 11), (32, 11), (30, 11), (28, 11), (26, 11), (24, 11), (22, 11));

        Bedarf(8, tabelle).Should().Be(Tabellenbedarf.Offen);
    }

    [Fact]
    public void Kein_Abstiegskampf_bei_mehr_als_drei_Punkten_Luft()
    {
        // Team 3 (36) liegt zwölf Punkte vor Rang 9 (24).
        var tabelle = Tabelle((40, 5), (38, 5), (36, 5), (34, 5), (32, 5), (30, 5), (28, 5), (26, 5), (24, 5), (22, 5));

        Bedarf(3, tabelle).Should().Be(Tabellenbedarf.Offen);
    }

    [Fact]
    public void Kein_Abstiegskampf_wenn_der_Klassenerhalt_gesichert_ist()
    {
        // Team 8 hat fünf Punkte mehr als Rang 9, aber nur noch ein Spiel: selbst nach einer Niederlage bleiben alle dahinter.
        var tabelle = Tabelle((40, 1), (38, 1), (36, 1), (34, 1), (32, 1), (30, 1), (28, 1), (26, 1), (20, 1), (15, 1));

        Bedarf(8, tabelle).Should().NotBe(Tabellenbedarf.Abstiegskampf);
    }

    [Fact]
    public void Ohne_Abstiegszone_gibt_es_keinen_Abstiegskampf()
    {
        TabellenZone[] nurAufstieg = [new(null, 1, 2, null, "Aufstieg", ZonenFarbe.Gruen)];
        var tabelle = Tabelle((40, 5), (38, 5), (36, 5), (34, 5), (32, 5), (30, 5), (28, 5), (26, 5), (24, 5), (22, 5));

        Bedarf(8, tabelle, nurAufstieg).Should().Be(Tabellenbedarf.Offen);
    }

    // ── K6 Zonen und Gruppen ──────────────────────────────────────────────

    [Fact]
    public void Eine_Zone_unter_Vorbehalt_gilt_nie_als_sicher()
    {
        // Rang 3 steht fest, aber ob die beste Dritte weiterkommt, entscheidet erst der Vergleich über alle Gruppen.
        TabellenZone[] mitVorbehalt =
        [
            new(null, 1, 2, 7, "Achtelfinale", ZonenFarbe.Gruen),
            new(null, 3, 3, 7, "Achtelfinale", ZonenFarbe.Amber, AnzahlBeste: 2)
        ];
        var tabelle = EinSpielOffen(20, 10, 5, 0);

        Bedarf(3, tabelle, mitVorbehalt).Should().Be(Tabellenbedarf.Offen);
    }

    [Fact]
    public void Dieselbe_Lage_in_einer_Zone_ohne_Vorbehalt_ist_ohne_Ziel()
    {
        TabellenZone[] ohneVorbehalt =
        [
            new(null, 1, 2, 7, "Achtelfinale", ZonenFarbe.Gruen),
            new(null, 3, 3, 8, "Playoff", ZonenFarbe.Blau)
        ];
        var tabelle = EinSpielOffen(20, 10, 5, 0);

        Bedarf(3, tabelle, ohneVorbehalt).Should().Be(Tabellenbedarf.OhneZiel);
    }

    [Fact]
    public void Runde_ohne_Zonen_ist_offen()
    {
        var tabelle = EinSpielOffen(60, 55, 40, 38, 36, 34, 32, 30, 20, 15);

        Bedarf(5, tabelle, zonen: []).Should().Be(Tabellenbedarf.Offen);
    }

    [Fact]
    public void Zonen_einer_anderen_Gruppe_zaehlen_nicht()
    {
        TabellenZone[] nurGruppeEins = [new(GruppeIndex: 1, 1, 2, 7, "Achtelfinale", ZonenFarbe.Gruen)];
        var tabelle = EinSpielOffen(20, 10, 5, 0);

        Bedarf(3, tabelle, nurGruppeEins, gruppe: 2).Should().Be(Tabellenbedarf.Offen);
        Bedarf(3, tabelle, nurGruppeEins, gruppe: 1).Should().NotBe(Tabellenbedarf.Offen);
    }

    [Fact]
    public void Team_ohne_offenes_Spiel_oder_nicht_in_der_Tabelle_ist_offen()
    {
        var tabelle = Tabelle((60, 0), (55, 1));

        Bedarf(1, tabelle).Should().Be(Tabellenbedarf.Offen);
        Bedarf(99, tabelle).Should().Be(Tabellenbedarf.Offen);
    }
}
