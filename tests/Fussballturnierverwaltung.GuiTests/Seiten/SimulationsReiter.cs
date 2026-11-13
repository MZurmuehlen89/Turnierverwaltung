using FlaUI.Core.AutomationElements;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Der Spielbericht einer Turnierpartie: Spielkopf, die Ereignisse der Zeitleiste je Seite (Art-Text wie
/// „Tor“, „Gelbe Karte“, „Elfmeter verwandelt“) und die Marker des kompakten Zeitstrahls je Seite — der zeigt alle
/// Ereignisse außer den Schüssen des Elfmeterschießens.</summary>
public sealed record Spielbericht(
    SpielKopfAnzeige Kopf,
    IReadOnlyList<string> HeimEreignisse, IReadOnlyList<string> AuswaertsEreignisse,
    IReadOnlyList<string> HeimMarker, IReadOnlyList<string> AuswaertsMarker)
{
    private static readonly string[] _torArten = ["Tor", "Golden Goal", "Silver Goal"];
    private static readonly string[] _elfmeterschuesse = ["Elfmeter verwandelt", "Elfmeter verschossen"];

    /// <summary>Tore laut Zeitleiste (ohne Elfmeterschießen).</summary>
    public (int Heim, int Auswaerts) ToreAusEreignissen =>
        (HeimEreignisse.Count(_torArten.Contains), AuswaertsEreignisse.Count(_torArten.Contains));

    public IReadOnlyList<string> HeimOhneElfmeterschiessen => [.. HeimEreignisse.Where(e => !_elfmeterschuesse.Contains(e))];

    public IReadOnlyList<string> AuswaertsOhneElfmeterschiessen =>
        [.. AuswaertsEreignisse.Where(e => !_elfmeterschuesse.Contains(e))];
}

/// <summary>Reiter „Simulation“ der Turnierdurchführung: zeigt den Spielbericht einer Partie, nach „Ausführlich
/// simulieren“ oder „Spielverlauf ansehen“ aus dem Spielplan (#302).</summary>
public sealed class SimulationsReiter(Window fenster, Hauptfenster hauptfenster)
{
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

    private Spielbericht? Lesen()
    {
        if (fenster.TryFindById("SpielKopf.Score") is null) return null;
        var kopf = new SpielKopfAnzeige(
            fenster.TextOf("SpielKopf.HeimName"),
            fenster.TextOf("SpielKopf.AuswaertsName"),
            fenster.TextOf("SpielKopf.Score"),
            fenster.TextOf("SpielKopf.Entscheidung"),
            fenster.TextOf("SpielKopf.Zwischenstaende"));
        return new Spielbericht(kopf, Namen("Spielbericht.HeimEreignis"), Namen("Spielbericht.AuswaertsEreignis"),
            Namen("Zeitstrahl.HeimMarker"), Namen("Zeitstrahl.AuswaertsMarker"));
    }

    private IReadOnlyList<string> Namen(string automationId) =>
        [.. fenster.FindAllById(automationId).Select(e => e.NameOrEmpty())];
}
