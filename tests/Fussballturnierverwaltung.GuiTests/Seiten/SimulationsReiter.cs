using FlaUI.Core.AutomationElements;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Der Spielbericht einer Turnierpartie: Spielkopf, die Ereignisse der Zeitleiste je Seite (Art-Text wie
/// „Tor“, „Gelbe Karte“, „Elfmeter verwandelt“) und die Marker des kompakten Zeitstrahls je Seite — der zeigt alle
/// Ereignisse außer den Schüssen des Elfmeterschießens und den Taktikwechseln (#361).</summary>
public sealed record Spielbericht(
    SpielKopfAnzeige Kopf,
    IReadOnlyList<string> HeimEreignisse, IReadOnlyList<string> AuswaertsEreignisse,
    IReadOnlyList<string> HeimMarker, IReadOnlyList<string> AuswaertsMarker)
{
    private static readonly string[] _torArten = ["Tor", "Golden Goal", "Silver Goal"];
    private static readonly string[] _nichtImZeitstrahl = ["Elfmeter verwandelt", "Elfmeter verschossen", "Taktikwechsel"];

    /// <summary>Tore laut Zeitleiste (ohne Elfmeterschießen).</summary>
    public (int Heim, int Auswaerts) ToreAusEreignissen =>
        (HeimEreignisse.Count(_torArten.Contains), AuswaertsEreignisse.Count(_torArten.Contains));

    public IReadOnlyList<string> HeimImZeitstrahl => [.. HeimEreignisse.Where(e => !_nichtImZeitstrahl.Contains(e))];

    public IReadOnlyList<string> AuswaertsImZeitstrahl => [.. AuswaertsEreignisse.Where(e => !_nichtImZeitstrahl.Contains(e))];
}

/// <summary>Die beiden Karten eines Spiels mit Wiederholungsspiel (#247): das Spiel und daneben das Wiederholungsspiel mit
/// seinem Kontext („Wiederholungsspiel“).</summary>
public sealed record Wiederholungskarten(SpielKopfAnzeige Spiel, SpielKopfAnzeige Wiederholungsspiel, string Kontext);

/// <summary>Reiter „Simulation“ der Turnierdurchführung: zeigt den Spielbericht einer Partie, nach „Ausführlich
/// simulieren“ oder „Spielverlauf ansehen“ aus dem Spielplan (#302).</summary>
public sealed class SimulationsReiter(Window fenster, Hauptfenster hauptfenster)
{
    private const string SPIEL = "Spielbericht.Spiel";
    private const string WIEDERHOLUNGSSPIEL = "Spielbericht.Wiederholungsspiel";

    /// <summary>Wartet, bis der Bericht <paramref name="heim"/> – <paramref name="auswaerts"/> mit Ergebnis zeigt und
    /// die Zeitleiste dazu geladen ist (ihre Tore passen zum Stand), höchstens <see cref="Hauptfenster.Ladezeit"/>;
    /// danach wird gelesen, was dasteht, und der Test prüft es selbst.</summary>
    public Spielbericht BerichtAbwarten(string heim, string auswaerts)
    {
        Wait.UntilFound(() => Lesen() is { } b && b.Kopf.Heim == heim && b.Kopf.Auswaerts == auswaerts
                                                && b.Kopf.Tore is not null ? b : null,
            Hauptfenster.Ladezeit, $"Spielbericht {heim} – {auswaerts}");
        hauptfenster.WarteBisRuhig();
        Wait.Within(() => Lesen() is { } b && b.Kopf.Tore == b.ToreAusEreignissen, Wait.DefaultTimeout);
        return Lesen() ?? throw new InvalidOperationException("Der Spielbericht ist verschwunden.");
    }

    /// <summary>Wartet, bis das Spiel <paramref name="heim"/> – <paramref name="auswaerts"/> mit seiner Karte und der
    /// Karte des Wiederholungsspiels dasteht (#247). Ein von Hand eingetragenes Ergebnis hat keine Ereignisse – gelesen
    /// werden darum nur die Köpfe.</summary>
    public Wiederholungskarten WiederholungsspielAbwarten(string heim, string auswaerts) =>
        Wait.UntilFound(
            () => fenster.TryFindById(SPIEL) is { } spiel && KopfLesen(spiel) is { } kopf
                  && kopf.Heim == heim && kopf.Auswaerts == auswaerts
                  && fenster.TryFindById(WIEDERHOLUNGSSPIEL) is { } wdh && KopfLesen(wdh) is { } wdhKopf
                ? new Wiederholungskarten(kopf, wdhKopf, wdh.TextOf("SpielKopf.Kontext"))
                : null,
            Hauptfenster.Ladezeit, $"Spielbericht {heim} – {auswaerts} mit Wiederholungsspiel");

    /// <summary>Ob neben dem Spiel eine Karte des Wiederholungsspiels steht.</summary>
    public bool ZeigtWiederholungsspiel => fenster.TryFindById(WIEDERHOLUNGSSPIEL) is not null;

    // Gelesen wird nur die Karte des Spiels – die des Wiederholungsspiels hat eigene Ereignisse und eine eigene Uhr.
    private Spielbericht? Lesen()
    {
        if (fenster.TryFindById(SPIEL) is not { } karte || KopfLesen(karte) is not { } kopf) return null;
        return new Spielbericht(kopf, Namen(karte, "Spielbericht.HeimEreignis"), Namen(karte, "Spielbericht.AuswaertsEreignis"),
            Namen(karte, "Zeitstrahl.HeimMarker"), Namen(karte, "Zeitstrahl.AuswaertsMarker"));
    }

    private static SpielKopfAnzeige? KopfLesen(AutomationElement karte) =>
        karte.TryFindById("SpielKopf.Score") is null
            ? null
            : new SpielKopfAnzeige(
                karte.TextOf("SpielKopf.HeimName"),
                karte.TextOf("SpielKopf.AuswaertsName"),
                karte.TextOf("SpielKopf.Score"),
                karte.TextOf("SpielKopf.Entscheidung"),
                karte.TextOf("SpielKopf.Zwischenstaende"));

    private static IReadOnlyList<string> Namen(AutomationElement karte, string automationId) =>
        [.. karte.FindAllById(automationId).Select(e => e.NameOrEmpty())];
}
