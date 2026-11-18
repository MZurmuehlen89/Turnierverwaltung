using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;
using static Fussballturnierverwaltung.GuiTests.MiniTurnier;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Spielbericht aus dem Spielplan (#302, #534): „Ausführlich simulieren“ simuliert ein Gruppenspiel im Reiter
/// „Simulation“, „Spielverlauf ansehen“ zeigt später dasselbe Protokoll, ohne neu zu simulieren — und das eines schnell
/// simulierten Spiels, dessen Ereignisse die Simulation immer mitspeichert (#171). Geprüft werden Invarianten: Der
/// Stand im Spielkopf ist der des Spielplans, die Tore der Zeitleiste ergeben ihn, der Zeitstrahl zeigt je Seite
/// dieselben Ereignisse wie die Zeitleiste.</summary>
public sealed class SpielberichtTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    [Fact]
    public void AusfuehrlichUndSpielverlauf_ZeigenDasErgebnisDesSpielplans()
    {
        var (turnier, auslosung) = GruppenauslosungVorbereiten(app.Hauptfenster);
        auslosung.Durchfuehren();
        var spielplan = turnier.Spielplan();
        spielplan.RundeWaehlen(GRUPPENPHASE);
        var spiele = spielplan.Spiele();
        var (erstes, zweites) = (spiele[0], spiele[1]);

        spielplan.SpielWaehlen(erstes.Heim, erstes.Auswaerts);
        var ausfuehrlich = turnier.AusfuehrlichSimulieren().BerichtAbwarten(erstes.Heim, erstes.Auswaerts);
        BerichtStimmig(ausfuehrlich, ErgebnisImSpielplan(turnier, erstes));

        // Zuerst das zweite Spiel ansehen: Der Reiter zeigt danach nicht mehr den Bericht des ersten, das erneute
        // Ansehen des ersten muss ihn also wirklich laden (sonst bestünde der Vergleich schon mit dem alten Bericht).
        spielplan = turnier.Spielplan();
        spielplan.RundeSimulieren(SPIELE_GRUPPENPHASE - 1);
        spielplan.SpielWaehlen(zweites.Heim, zweites.Auswaerts);
        var schnell = turnier.SpielverlaufAnsehen().BerichtAbwarten(zweites.Heim, zweites.Auswaerts);
        BerichtStimmig(schnell, ErgebnisImSpielplan(turnier, zweites));

        turnier.Spielplan().SpielWaehlen(erstes.Heim, erstes.Auswaerts);
        var angesehen = turnier.SpielverlaufAnsehen().BerichtAbwarten(erstes.Heim, erstes.Auswaerts);
        angesehen.Should().BeEquivalentTo(ausfuehrlich, "„Spielverlauf ansehen“ zeigt das gespeicherte Protokoll");
        ErgebnisImSpielplan(turnier, erstes).Should().Be(ausfuehrlich.Kopf.Tore, "Ansehen simuliert nicht neu");
    }

    // ── Großchancen (#431, K8, K10) ───────────────────────────────────────

    /// <summary>Ausführlich simulierte Spiele zeigen vergebene Großchancen als Zeile „Großchance“ im Spielbericht, ohne den
    /// Stand zu verändern (<see cref="BerichtStimmig"/>), und die Statistik „Großchancen“ passt zu den Toren: je Mannschaft
    /// höchstens so viele Tore aus Großchancen wie Tore überhaupt, höchstens so viele vergebene Elfmeter wie Großchancen.
    /// Gesucht wird das erste der Spiele, dessen Bericht eine Großchance enthält (der Zufall ist über Zufall:Seed fest);
    /// gespielt werden höchstens <see cref="MAX_SPIELE_BIS_GROSSCHANCE"/>. Nicht gezielt geprüft: ein Elfmeter im Spiel
    /// (0,3 je Spiel, kommt selten vor) – dafür stehen die Unit-Tests in <c>SpielablaufTests</c>.</summary>
    [Fact]
    public void Grosschance_StehtImSpielberichtUndPasstZurStatistik()
    {
        var (turnier, auslosung) = GruppenauslosungVorbereiten(app.Hauptfenster);
        auslosung.Durchfuehren();
        var spiele = AlleSpieleDerGruppenphase(turnier);

        Spielbericht? mitGrosschance = null;
        foreach (var spiel in spiele.Take(MAX_SPIELE_BIS_GROSSCHANCE))
        {
            var spielplan = turnier.Spielplan();
            spielplan.RundeWaehlen(GRUPPENPHASE);
            spielplan.SpielWaehlen(spiel.Heim, spiel.Auswaerts);
            var bericht = turnier.AusfuehrlichSimulieren().BerichtAbwarten(spiel.Heim, spiel.Auswaerts);
            BerichtStimmig(bericht, ErgebnisImSpielplan(turnier, spiel));
            if (bericht.HeimEreignisse.Concat(bericht.AuswaertsEreignisse).Contains(GROSSCHANCE))
            {
                mitGrosschance = bericht;
                break;
            }
        }

        mitGrosschance.Should().NotBeNull($"unter den ersten {MAX_SPIELE_BIS_GROSSCHANCE} ausführlich simulierten Spielen " +
                                          "ist eines mit vergebener Großchance (je Team rund 1,3 je Spiel)");

        var statistik = turnier.Statistik();
        var grosschancen = statistik.Tabelle("Großchancen");
        var tore = statistik.Tabelle("Tore und Zu-Null").ToDictionary(z => z.Mannschaft);
        grosschancen.Should().NotBeEmpty("ausführlich simulierte Spiele haben Chancendaten");
        grosschancen.Should().OnlyContain(z => tore.ContainsKey(z.Mannschaft));
        foreach (var zeile in grosschancen)
        {
            zeile.Zahl("Sp").Should().BeGreaterThan(0, "nur Spiele mit Chancendaten zählen – {0}", zeile.Mannschaft);
            zeile.Zahl("Tore").Should().BeLessThanOrEqualTo(zeile.Zahl("GC"), "Tore aus Großchancen ≤ Großchancen – {0}", zeile.Mannschaft);
            zeile.Zahl("Elf. vg.").Should().BeLessThanOrEqualTo(zeile.Zahl("GC"), "vergebene Elfmeter ≤ Großchancen – {0}", zeile.Mannschaft);
            zeile.Zahl("Tore").Should().BeLessThanOrEqualTo(tore[zeile.Mannschaft].Zahl("Tore"),
                "Tore aus Großchancen ≤ alle Tore – {0}", zeile.Mannschaft);
        }
        grosschancen.Sum(z => z.Zahl("GC")).Should().BeGreaterThan(0);
    }

    private const int MAX_SPIELE_BIS_GROSSCHANCE = 6;
    private const string GROSSCHANCE = "Großchance";

    private static IReadOnlyList<SpielZeile> AlleSpieleDerGruppenphase(TurnierdurchfuehrungSeite turnier)
    {
        var spielplan = turnier.Spielplan();
        spielplan.RundeWaehlen(GRUPPENPHASE);
        return spielplan.Spiele();
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();

    private static (int Heim, int Auswaerts)? ErgebnisImSpielplan(TurnierdurchfuehrungSeite turnier, SpielZeile spiel)
    {
        var spielplan = turnier.Spielplan();
        spielplan.RundeWaehlen(GRUPPENPHASE);
        return Spielstand.Tore(spielplan.Spiele().Single(s => s.Heim == spiel.Heim && s.Auswaerts == spiel.Auswaerts).Ergebnis);
    }

    private static void BerichtStimmig(Spielbericht bericht, (int Heim, int Auswaerts)? imSpielplan)
    {
        bericht.Kopf.Tore.Should().NotBeNull().And.Be(imSpielplan, "der Spielkopf zeigt das Ergebnis des Spielplans");
        bericht.ToreAusEreignissen.Should().Be(bericht.Kopf.Tore!.Value, "die Tore der Zeitleiste ergeben den Stand");
        bericht.HeimMarker.Should().BeEquivalentTo(bericht.HeimImZeitstrahl,
            "der Zeitstrahl zeigt die Ereignisse der Heimmannschaft");
        bericht.AuswaertsMarker.Should().BeEquivalentTo(bericht.AuswaertsImZeitstrahl,
            "der Zeitstrahl zeigt die Ereignisse der Auswärtsmannschaft");
    }
}
