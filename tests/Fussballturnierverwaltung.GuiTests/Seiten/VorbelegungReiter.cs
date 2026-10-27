using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Reiter „Vorbelegung“ der Turnierliste: die Paarungen einer Runde mit fest vorgegebener Paarung von Hand
/// setzen. Ziehen per Maus lässt sich über UIA nicht verlässlich auslösen; die App setzt ein Team auch per Doppelklick
/// in den nächsten freien Platz und entfernt es per Rechtsklick-Menü – beides geht mit echten Mausklicks.</summary>
public sealed class VorbelegungReiter(Window fenster, Hauptfenster hauptfenster)
{
    /// <summary>So zeigt die App einen freien Platz.</summary>
    public const string FREI = "…";

    /// <summary>Die Plätze in Anzeigereihenfolge, im K.-o.-Modus je Paarung Heim und Auswärts.</summary>
    public IReadOnlyList<string> Plaetze => [.. fenster.FindAllById("Vorbelegung.Platz").Select(p => p.NameOrEmpty())];

    public IReadOnlyList<string> Verfuegbar =>
        fenster.FindById("Vorbelegung.Verfuegbar").ReadAllItems(e => e.TextOf("Vorbelegung.TeilnehmerName"));

    public bool Abgeschlossen => fenster.TryFindById("Befehlsleiste.VorbelegungZuruecksetzen") is { IsEnabled: true };

    private string Fehlermeldung => fenster.TryFindById("Vorbelegung.Fehlermeldung")?.NameOrEmpty() ?? string.Empty;

    /// <summary>Wählt die Runde und wartet, bis ihre Plätze dastehen.</summary>
    public void RundeWaehlen(string runde)
    {
        fenster.FindById("Vorbelegung.Runden")
            .ReadAllItems(e => e.TextOf("Vorbelegung.RundenName") == runde ? e : null)
            .Single()
            .Activate();
        hauptfenster.WarteBisRuhig();
        Wait.Until(() => Plaetze.Count > 0, Wait.DefaultTimeout, $"Plätze der Runde '{runde}'");
    }

    /// <summary>Setzt <paramref name="team"/> per Doppelklick in den nächsten freien Platz.</summary>
    public void Platzieren(string team)
    {
        int belegt = Plaetze.Count(p => p != FREI);
        var eintrag = fenster.FindById("Vorbelegung.Verfuegbar")
            .ReadAllItems(e => e.TextOf("Vorbelegung.TeilnehmerName") == team ? e : null)
            .Single();
        // Der Doppelklick liest das gewählte Element der Liste, darum zuerst wählen
        eintrag.Patterns.SelectionItem.Pattern.Select();
        eintrag.DoubleClick();
        Wait.Until(() => Plaetze.Count(p => p != FREI) == belegt + 1 && Plaetze.Contains(team), Wait.DefaultTimeout,
            $"'{team}' platziert");
    }

    /// <summary>Entfernt <paramref name="team"/> über das Rechtsklick-Menü seines Platzes.</summary>
    public void Entfernen(string team)
    {
        fenster.FindAllById("Vorbelegung.Platz").Single(p => p.NameOrEmpty() == team).RightClick();
        var eintrag = Wait.UntilFound(
            () => fenster.FindFirstDescendant(cf => cf.ByName($"'{team}' entfernen").And(cf.ByControlType(ControlType.MenuItem))),
            Wait.DefaultTimeout, $"Menüpunkt „'{team}' entfernen“");
        eintrag.Activate();
        Wait.Until(() => !Plaetze.Contains(team), Wait.DefaultTimeout, $"'{team}' entfernt");
    }

    /// <summary>„Paarung abschließen“: speichert die Paarungen und erzeugt den Spielplan der Runde.</summary>
    public void Abschliessen()
    {
        fenster.ActivateById("Befehlsleiste.PaarungAbschliessen");
        Wait.Until(() => Abgeschlossen || Fehlermeldung.Length > 0, Wait.DefaultTimeout, "Paarung abgeschlossen");
        if (Fehlermeldung.Length > 0)
            throw new InvalidOperationException($"Paarung nicht abgeschlossen: {Fehlermeldung}");
        hauptfenster.WarteBisRuhig();
    }

    /// <summary>„Zurücksetzen“: verwirft die abgeschlossene Paarung samt Spielplan, alle Plätze sind danach frei.</summary>
    public void Zuruecksetzen()
    {
        fenster.ActivateById("Befehlsleiste.VorbelegungZuruecksetzen");
        Wait.Until(() => !Abgeschlossen && Plaetze.Count > 0 && Plaetze.All(p => p == FREI), Wait.DefaultTimeout,
            "Vorbelegung zurückgesetzt");
        hauptfenster.WarteBisRuhig();
    }
}
