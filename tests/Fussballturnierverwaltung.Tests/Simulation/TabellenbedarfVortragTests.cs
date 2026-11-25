using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>#355 K6: Der Tabellenbedarf (#176) startet mit dem Punktevortrag. Wie in <see cref="TabellenbedarfVerlaufTests"/>:
/// vier Teams (A–D = 1–4), drei Spieltage, nach zwei Spieltagen A 6, B 3, C 3, D 0; Zonen Rang 1 „Aufstieg“, Rang 4 „Abstieg“.
/// D startet in der Runde mit 20 Punkten Vortrag – dann steht Rang 1 schon vor dem Spiel A–D fest.</summary>
public class TabellenbedarfVortragTests
{
    private const int A = 1, B = 2, C = 3, D = 4;

    private static readonly TurnierRunde RUNDE = new() { Id = 1, Rundenart = Rundenart.Liga };

    private static TabellenbedarfGrundlage Grundlage(IReadOnlyDictionary<int, int>? vortraege) => new(
        [new TabellenZone(null, 1, 1, null, "Aufstieg", ZonenFarbe.Gruen), new TabellenZone(null, 4, 4, null, "Abstieg", ZonenFarbe.Rot)],
        new PunkteRegel { PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 },
        vortraege);

    private static Spiel Spiel(int id, int spieltag, int heim, int gast, (int Heim, int Gast)? ergebnis = null) => new()
    {
        Id = id, TurnierRundeId = 1, TurnierRunde = RUNDE, Spielnummer = id, Spieltag = spieltag,
        HeimMannschaftId = heim, AuswaertsMannschaftId = gast,
        Status = ergebnis is null ? SpielStatus.Geplant : SpielStatus.Abgeschlossen,
        Ergebnis = ergebnis is { } e ? new SpielErgebnis { HeimToreRegulaer = e.Heim, AuswaertsToreRegulaer = e.Gast } : null
    };

    private static List<Spiel> NachZweiSpieltagen() =>
    [
        Spiel(1, 1, A, B, (1, 0)), Spiel(2, 1, C, D, (1, 0)),
        Spiel(3, 2, A, C, (1, 0)), Spiel(4, 2, B, D, (1, 0)),
        Spiel(5, 3, A, D), Spiel(6, 3, B, C)
    ];

    private static TabellenbedarfVerlauf Verlauf(IReadOnlyList<Spiel> spiele, IReadOnlyDictionary<int, int>? vortraege) =>
        new(spiele, new Dictionary<int, TabellenbedarfGrundlage> { [1] = Grundlage(vortraege) });

    [Fact]
    public void Bedarf_ohneVortrag_DieZoneStehtNochNichtFest()
    {
        var spiele = NachZweiSpieltagen();

        Verlauf(spiele, vortraege: null).BedarfFuer(spiele[4]).Gast.Should().NotBe(Tabellenbedarf.OhneZiel);
    }

    [Fact]
    public void Bedarf_mitVortrag_DieZoneStehtSchonFest_OhneZiel()
    {
        var spiele = NachZweiSpieltagen();

        // D: 20 Punkte Vortrag, kein anderes Team kommt über 9 – Rang 1 steht fest.
        Verlauf(spiele, new Dictionary<int, int> { [D] = 20 }).BedarfFuer(spiele[4]).Gast.Should().Be(Tabellenbedarf.OhneZiel);
    }

    [Fact]
    public void Bedarf_mitVortragFuerAndereTeams_AendertDieUebrigenNicht()
    {
        var spiele = NachZweiSpieltagen();

        // Gleicher Vortrag für alle Teams verschiebt nichts: derselbe Bedarf wie ohne.
        var mitGleichemVortrag = Verlauf(spiele, new Dictionary<int, int> { [A] = 10, [B] = 10, [C] = 10, [D] = 10 });
        var ohne = Verlauf(spiele, vortraege: null);

        mitGleichemVortrag.BedarfFuer(spiele[5]).Should().Be(ohne.BedarfFuer(spiele[5]));
    }
}
