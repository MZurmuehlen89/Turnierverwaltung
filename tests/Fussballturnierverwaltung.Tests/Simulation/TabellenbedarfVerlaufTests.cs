using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>#176 K5 und K6: Der Bedarf kommt aus dem Stand vor dem Spieltag des Spiels; Spiele desselben Spieltags zählen nicht.
/// Vier Teams (A–D = 1–4), einfache Runde in drei Spieltagen: 1: A–B, C–D · 2: A–C, B–D · 3: A–D, B–C. Zonen: Rang 1 „Aufstieg“,
/// Rang 4 „Abstieg“. Nach zwei Spieltagen mit lauter Siegen von A und C (erst gegen B und D, dann A gegen C, B gegen D) steht es
/// A 6, B 3, C 3, D 0.</summary>
public class TabellenbedarfVerlaufTests
{
    private const int A = 1, B = 2, C = 3, D = 4;

    private static readonly TurnierRunde RUNDE = new() { Id = 1, Rundenart = Rundenart.Liga };

    private static readonly TabellenbedarfGrundlage GRUNDLAGE = new(
        [new TabellenZone(null, 1, 1, null, "Aufstieg", ZonenFarbe.Gruen), new TabellenZone(null, 4, 4, null, "Abstieg", ZonenFarbe.Rot)],
        new PunkteRegel { PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 });

    private static Spiel Spiel(int id, int spieltag, int heim, int gast, (int Heim, int Gast)? ergebnis = null) => new()
    {
        Id = id, TurnierRundeId = 1, TurnierRunde = RUNDE, Spielnummer = id, Spieltag = spieltag,
        HeimMannschaftId = heim, AuswaertsMannschaftId = gast,
        Status = ergebnis is null ? SpielStatus.Geplant : SpielStatus.Abgeschlossen,
        Ergebnis = ergebnis is { } e ? new SpielErgebnis { HeimToreRegulaer = e.Heim, AuswaertsToreRegulaer = e.Gast } : null
    };

    private static SimulationsErgebnis Ergebnis(int heim, int gast) =>
        new(heim, gast, null, null, null, null, EntscheidungArt.RegulaereSpielzeit, []);

    private static TabellenbedarfVerlauf Verlauf(IEnumerable<Spiel> spiele, bool mitZonen = true) =>
        new(spiele, mitZonen ? new Dictionary<int, TabellenbedarfGrundlage> { [1] = GRUNDLAGE } : []);

    /// <summary>Alle sechs Spiele, die ersten vier mit Ergebnis (A 6, B 3, C 3, D 0).</summary>
    private static List<Spiel> NachZweiSpieltagen() =>
    [
        Spiel(1, 1, A, B, (1, 0)), Spiel(2, 1, C, D, (1, 0)),
        Spiel(3, 2, A, C, (1, 0)), Spiel(4, 2, B, D, (1, 0)),
        Spiel(5, 3, A, D), Spiel(6, 3, B, C)
    ];

    // ── K5 Stand vor dem Spieltag ─────────────────────────────────────────

    [Fact]
    public void Bedarf_aus_dem_Stand_vor_dem_Spieltag()
    {
        var spiele = NachZweiSpieltagen();

        // B braucht gegen C einen Sieg: ein Remis lässt A (6) uneinholbar in Reichweite des Titels – nur A verlieren ließe Platz 1 offen.
        Verlauf(spiele).BedarfFuer(spiele[5]).Heim.Should().Be(Tabellenbedarf.SiegNoetig);
    }

    [Fact]
    public void Ein_Ergebnis_desselben_Spieltags_aendert_den_Bedarf_nicht()
    {
        var spiele = NachZweiSpieltagen();
        var verlauf = Verlauf(spiele);
        var vorher = verlauf.BedarfFuer(spiele[5]);

        // A schlägt D hoch: zählte das Spiel schon, hätte A neun Punkte und B nur noch Rang 2 oder 3 – ohne Ziel.
        verlauf.Setzen(spiele[4], Ergebnis(3, 0));

        verlauf.BedarfFuer(spiele[5]).Should().Be(vorher);
        vorher.Heim.Should().Be(Tabellenbedarf.SiegNoetig);
    }

    [Fact]
    public void Ein_Ergebnis_eines_frueheren_Spieltags_wirkt_auf_die_spaeteren()
    {
        var spiele = NachZweiSpieltagen();
        spiele[2] = Spiel(3, 2, A, C);
        spiele[3] = Spiel(4, 2, B, D);
        var verlauf = Verlauf(spiele);
        verlauf.BedarfFuer(spiele[5]).Heim.Should().Be(Tabellenbedarf.Abstiegskampf, "B hat noch null Punkte und zwei Spiele");

        verlauf.Setzen(spiele[2], Ergebnis(1, 0));
        verlauf.Setzen(spiele[3], Ergebnis(1, 0));

        verlauf.BedarfFuer(spiele[5]).Heim.Should().Be(Tabellenbedarf.SiegNoetig);
    }

    [Fact]
    public void Ein_neu_gewuerfeltes_Spiel_ersetzt_sein_altes_Ergebnis()
    {
        var spiele = NachZweiSpieltagen();
        var verlauf = Verlauf(spiele);
        verlauf.BedarfFuer(spiele[5]).Gast.Should().Be(Tabellenbedarf.SiegNoetig, "C hat 3 Punkte, A steht bei 6");

        verlauf.Setzen(spiele[2], Ergebnis(0, 1)); // C schlägt A statt umgekehrt: C 6, A 3

        verlauf.BedarfFuer(spiele[5]).Gast.Should().Be(Tabellenbedarf.RemisReicht, "C führt jetzt mit 6 Punkten");
    }

    // ── K6 Zonen und Gruppen ──────────────────────────────────────────────

    [Fact]
    public void Runde_ohne_Zonen_ist_offen()
    {
        var spiele = NachZweiSpieltagen();

        Verlauf(spiele, mitZonen: false).BedarfFuer(spiele[5]).Should().Be(default(TabellenbedarfJeTeam));
    }

    [Fact]
    public void Spiel_ohne_Spieltag_ist_offen()
    {
        var spiele = NachZweiSpieltagen();
        spiele[5].Spieltag = null;

        Verlauf(spiele).BedarfFuer(spiele[5]).Should().Be(default(TabellenbedarfJeTeam));
    }

    [Fact]
    public void Runde_mit_offenem_Platzhalter_ist_offen()
    {
        var spiele = NachZweiSpieltagen();
        spiele[4].HeimMannschaftId = null;

        Verlauf(spiele).BedarfFuer(spiele[5]).Should().Be(default(TabellenbedarfJeTeam));
    }

    [Fact]
    public void Spiel_einer_fremden_Runde_ist_offen()
    {
        var spiele = NachZweiSpieltagen();
        var fremd = Spiel(99, 3, A, B);
        fremd.TurnierRundeId = 2;

        Verlauf(spiele).BedarfFuer(fremd).Should().Be(default(TabellenbedarfJeTeam));
    }
}
