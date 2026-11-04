using FlaUI.Core.AutomationElements;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Eine Zeile der Eintragsliste; <see cref="Abschnitt"/> ist das Trennband, mit dem ein Abschnitt der
/// Ligapyramide an dieser Zeile beginnt („Oben · Spielklasse 1“), sonst null.</summary>
public sealed record RanglistenEintrag(int Rang, string Mannschaft, string? Abschnitt = null);

/// <summary>Eine Zeile der Abschnittstabelle; „von Platz“ berechnet die Seite aus dem Abschnitt davor.</summary>
public sealed record LigaAbschnitt(string Name, int Spielklasse, int BisPlatz, int VonPlatz);

/// <summary>Seitenobjekt „Ranglisten“ (#535): links die Ranglisten, rechts der Editor mit Eintragsliste (Hinzufügen
/// per Suchbox, Markieren, Umsortieren, Suchen) und der aufklappbaren Ligapyramide (Abschnitte, Pokalsieger).</summary>
public sealed class RanglistenSeite(Window fenster, Hauptfenster hauptfenster)
{
    private const string EINTRAEGE = "Ranglisten.Eintraege";
    private const string EINTRAG_NAME = "Ranglisten.EintragName";
    private const string EINTRAG_RANG = "Ranglisten.EintragRang";

    // Was die Seite nach einer Eingabe zeigt, zieht über Ereignisse nach. Die …Abwarten-Methoden warten darum bis zum
    // erwarteten Zustand und liefern dann den tatsächlichen – ein Fehlschlag zeigt so den echten Wert.

    public StammdatenEditor Editor { get; } = new(fenster, hauptfenster, "Ranglisten", "Rangliste löschen");

    public MannschaftsAuswahl Pokalsieger { get; } = new(fenster, "Ranglisten.Pokalsieger");

    private readonly MannschaftsAuswahl _hinzufuegen = new(fenster, "Ranglisten.Hinzufuegen");

    /// <summary>Legt eine Rangliste mit <paramref name="mannschaften"/> in dieser Reihenfolge an.</summary>
    public void Anlegen(string name, params string[] mannschaften)
    {
        Editor.Neu();
        Editor.WertSetzen("Name", name);
        foreach (var mannschaft in mannschaften)
            MannschaftHinzufuegen(mannschaft);
        Editor.Speichern();
    }

    /// <summary>Fügt die Mannschaft hinter dem markierten Eintrag ein (sonst am Ende); danach ist sie markiert.</summary>
    public void MannschaftHinzufuegen(string mannschaft)
    {
        _hinzufuegen.Waehlen(mannschaft);
        Wait.Until(() => Eintraege().Any(e => e.Mannschaft == mannschaft), Wait.DefaultTimeout,
            $"'{mannschaft}' in der Rangliste");
    }

    /// <summary>Was die Liste links unter dem Namen zeigt („3 Einträge“), sobald die Rangliste dort steht.</summary>
    public string Anzahl(string name) => Editor.Eintrag(name, e => e.TextOf("Ranglisten.ListeAnzahl"));

    // ── Eintragsliste ─────────────────────────────────────────────────────────

    public IReadOnlyList<RanglistenEintrag> Eintraege() =>
        Liste.ReadAllItems(e => e.TryFindById(EINTRAG_NAME) is null
            ? null
            : new RanglistenEintrag(
                int.Parse(e.TextOf(EINTRAG_RANG)),
                e.TextOf(EINTRAG_NAME),
                e.TryFindById("Ranglisten.AbschnittBeginn")?.NameOrEmpty()));

    public IReadOnlyList<RanglistenEintrag> EintraegeAbwarten(params RanglistenEintrag[] erwartet) =>
        Abwarten(Eintraege, ist => ist.SequenceEqual(erwartet));

    /// <summary>Die Mannschaften in Listenreihenfolge, sobald sie <paramref name="erwartet"/> entsprechen.</summary>
    public IReadOnlyList<string> ReihenfolgeAbwarten(params string[] erwartet) =>
        Abwarten<IReadOnlyList<string>>(() => [.. Eintraege().Select(e => e.Mannschaft)], ist => ist.SequenceEqual(erwartet));

    /// <summary>Markiert genau diese Einträge (wie Strg + Klick).</summary>
    public void Markieren(params string[] mannschaften)
    {
        for (int i = 0; i < mannschaften.Length; i++)
        {
            string mannschaft = mannschaften[i];
            var zeile = Liste.FindItem(e => e.TextOf(EINTRAG_NAME) == mannschaft)
                ?? throw new InvalidOperationException($"'{mannschaft}' steht nicht in der Rangliste.");
            var auswahl = zeile.Patterns.SelectionItem.Pattern;
            if (i == 0) auswahl.Select();
            else auswahl.AddToSelection();
        }
        Wait.Until(() => Markierte().Order().SequenceEqual(mannschaften.Order()), Wait.DefaultTimeout,
            $"markiert: {string.Join(", ", mannschaften)}");
    }

    public IReadOnlyList<string> MarkierteAbwarten(params string[] erwartet) =>
        Abwarten(Markierte, ist => ist.SequenceEqual(erwartet));

    public void Hoch() => fenster.ActivateById("Ranglisten.Hoch");

    public void Runter() => fenster.ActivateById("Ranglisten.Runter");

    public void AufRangVerschieben(int rang)
    {
        fenster.FindById("Ranglisten.ZielRang").Patterns.RangeValue.Pattern.SetValue(rang);
        fenster.ActivateById("Ranglisten.AufRang");
    }

    public void Entfernen() => fenster.ActivateById("Ranglisten.Entfernen");

    /// <summary>„In Liste suchen“: springt zum ersten Treffer und markiert ihn, filtert aber nicht.</summary>
    public void InListeSuchen(string text) =>
        fenster.FindById("Ranglisten.InListeSuchen").Patterns.Value.Pattern.SetValue(text);

    public bool KeinTrefferAbwarten() =>
        Wait.Within(() => fenster.TryFindById("Ranglisten.KeinTreffer") is not null, Wait.DefaultTimeout);

    private AutomationElement Liste => fenster.FindById(EINTRAEGE);

    private IReadOnlyList<string> Markierte() =>
        Liste.ReadAllItems(e => e.Patterns.SelectionItem.Pattern.IsSelected.Value ? e.TextOf(EINTRAG_NAME) : null);

    // ── Ligapyramide ──────────────────────────────────────────────────────────

    /// <summary>Klappt den Bereich auf; er ist bei jedem Ranglistenwechsel und nach dem Speichern wieder zu.</summary>
    public void LigapyramideAufklappen()
    {
        fenster.FindById("Ranglisten.Ligapyramide").Patterns.ExpandCollapse.Pattern.Expand();
        Wait.Until(() => fenster.TryFindById("Ranglisten.AbschnittHinzufuegen") is not null, Wait.DefaultTimeout,
            "Ligapyramide aufgeklappt");
    }

    /// <summary>Hängt einen Abschnitt an: eine Spielklasse tiefer als der letzte, bis zum Listenende.</summary>
    public void AbschnittHinzufuegen()
    {
        int vorher = AbschnittsFelder("Name").Count;
        fenster.ActivateById("Ranglisten.AbschnittHinzufuegen");
        Wait.Until(() => AbschnittsFelder("Name").Count == vorher + 1, Wait.DefaultTimeout, "Abschnitt angehängt");
    }

    public void AbschnittBenennen(int index, string name) =>
        AbschnittsFelder("Name")[index].Patterns.Value.Pattern.SetValue(name);

    public void AbschnittBisPlatzSetzen(int index, int bisPlatz) =>
        AbschnittsFelder("BisPlatz")[index].Patterns.RangeValue.Pattern.SetValue(bisPlatz);

    public IReadOnlyList<LigaAbschnitt> AbschnitteAbwarten(params LigaAbschnitt[] erwartet) =>
        Abwarten(Abschnitte, ist => ist.SequenceEqual(erwartet));

    /// <summary>Die Fehler der Live-Prüfung in der Meldungsleiste, sobald sie <paramref name="erwartet"/> entsprechen.</summary>
    public IReadOnlyList<string> AbschnittsFehlerAbwarten(params string[] erwartet) =>
        Abwarten<IReadOnlyList<string>>(() => new MeldungsLeiste(fenster, "Ranglisten").Fehler,
            ist => ist.SequenceEqual(erwartet));

    /// <summary>Der Überhang-Hinweis (Speichern bleibt möglich), sobald er <paramref name="erwartet"/> lautet.</summary>
    public string HinweisAbwarten(string erwartet) =>
        Abwarten(() => new MeldungsLeiste(fenster, "Ranglisten").Hinweis, ist => ist == erwartet);

    public bool SpeichernFreigabeAbwarten(bool erwartet)
    {
        Wait.Within(() => Editor.SpeichernFreigegeben == erwartet, Wait.DefaultTimeout);
        return Editor.SpeichernFreigegeben;
    }

    public void PokalsiegerEntfernen() => fenster.ActivateById("Ranglisten.PokalsiegerEntfernen");

    private IReadOnlyList<LigaAbschnitt> Abschnitte()
    {
        var namen = AbschnittsFelder("Name");
        var klassen = AbschnittsFelder("Spielklasse");
        var bis = AbschnittsFelder("BisPlatz");
        var von = AbschnittsFelder("VonPlatz");
        return [.. Enumerable.Range(0, namen.Count).Select(i => new LigaAbschnitt(
            namen[i].Patterns.Value.Pattern.Value.ValueOrDefault ?? string.Empty,
            (int)klassen[i].Patterns.RangeValue.Pattern.Value.Value,
            (int)bis[i].Patterns.RangeValue.Pattern.Value.Value,
            int.Parse(von[i].NameOrEmpty())))];
    }

    private IReadOnlyList<AutomationElement> AbschnittsFelder(string feld) => fenster.FindAllById($"Ranglisten.Abschnitt{feld}");

    private static T Abwarten<T>(Func<T> lesen, Func<T, bool> erfuellt)
    {
        Wait.Within(() => erfuellt(lesen()), Wait.DefaultTimeout);
        return lesen();
    }
}
