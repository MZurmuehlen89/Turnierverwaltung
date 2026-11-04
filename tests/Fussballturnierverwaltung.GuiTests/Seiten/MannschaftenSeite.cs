using FlaUI.Core.AutomationElements;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Eine Zeile der Mannschaftsliste, soweit die Tests sie lesen.</summary>
public sealed record MannschaftsEintrag(string Name, string Kurzname, int Offensiv);

/// <summary>Seitenobjekt „Mannschaften“ (#523): Liste durchsuchen, Formular ausfüllen, speichern und löschen über die
/// Befehlsleiste. Die Liste zeigt alle Mannschaften des Seeds, darum filtert jeder Zugriff erst per Suchfeld auf den
/// eindeutigen Testnamen.</summary>
public sealed class MannschaftenSeite(Window fenster, Hauptfenster hauptfenster)
{
    private const string LOESCHDIALOG = "Mannschaft löschen";

    public string Name => Wert("Mannschaften.Name");
    public string Kurzname => Wert("Mannschaften.Kurzname");

    // Meldungen und Speichern-Freigabe folgen einer Eingabe nicht sofort (UIA-Baum und CanExecute ziehen über
    // Ereignisse nach). Die …Abwarten-Methoden warten darum bis zum erwarteten Zustand und liefern dann den
    // tatsächlichen – ein Fehlschlag zeigt so den echten Wert statt einer Zeitüberschreitung.

    /// <summary>Die Meldungen der Live-Prüfung, sobald sie <paramref name="erwartet"/> entsprechen (Reihenfolge egal).</summary>
    public IReadOnlyList<string> FehlerAbwarten(params string[] erwartet)
    {
        Wait.Within(() => Fehler.Order().SequenceEqual(erwartet.Order()), Wait.DefaultTimeout);
        return Fehler;
    }

    public bool SpeichernFreigabeAbwarten(bool erwartet)
    {
        Wait.Within(() => SpeichernFreigegeben == erwartet, Wait.DefaultTimeout);
        return SpeichernFreigegeben;
    }

    /// <summary>Ob die Suche nach <paramref name="name"/> keinen Eintrag mehr findet.</summary>
    public bool NichtMehrGelistetAbwarten(string name)
    {
        Suchen(name);
        return Wait.Within(() => Eintraege().All(e => e.Name != name), Wait.DefaultTimeout);
    }

    private IReadOnlyList<string> Fehler => Meldungen.Fehler;

    /// <summary>Die Meldungsleiste des Formulars.</summary>
    public MeldungsLeiste Meldungen => new(fenster, "Mannschaften");

    /// <summary>Das Formularfeld <c>Mannschaften.&lt;feld&gt;</c>, etwa für <see cref="Feldmarkierung"/>.</summary>
    public AutomationElement Feld(string feld) => fenster.FindById($"Mannschaften.{feld}");

    private bool SpeichernFreigegeben => fenster.FindById("Befehlsleiste.Speichern").IsEnabled;

    public void Neu() => fenster.ActivateById("Befehlsleiste.Neu");

    public void NameEingeben(string name) => WertSetzen("Mannschaften.Name", name);
    public void KurznameEingeben(string kurzname) => WertSetzen("Mannschaften.Kurzname", kurzname);

    public void OffensivSetzen(int wert) =>
        fenster.FindById("Mannschaften.Offensiv").Patterns.RangeValue.Pattern.SetValue(wert);

    /// <summary>Speichert und wartet, bis die Seite neu geladen hat – erkennbar daran, dass Speichern wieder gesperrt
    /// ist (keine ungespeicherten Änderungen mehr).</summary>
    public void Speichern()
    {
        fenster.ActivateById("Befehlsleiste.Speichern");
        Wait.Until(() => !SpeichernFreigegeben, Wait.DefaultTimeout, "gespeichert (Speichern wieder gesperrt)");
        hauptfenster.WarteBisRuhig();
    }

    public void Abbrechen() => fenster.ActivateById("Befehlsleiste.Abbrechen");

    /// <summary>Legt eine Mannschaft an; Art und Land bleiben auf der Vorgabe des Formulars.</summary>
    public void Anlegen(string name, string kurzname, int offensiv)
    {
        Neu();
        NameEingeben(name);
        KurznameEingeben(kurzname);
        OffensivSetzen(offensiv);
        Speichern();
    }

    /// <summary>Filtert die Liste über das Suchfeld (Name, Kurzname oder Land enthält den Begriff).</summary>
    public void Suchen(string begriff) => WertSetzen("Mannschaften.Suche", begriff);

    /// <summary>Die Einträge der aktuell gefilterten Liste.</summary>
    public IReadOnlyList<MannschaftsEintrag> Eintraege() =>
        fenster.FindById("Mannschaften.Liste").ReadAllItems(e => e.TryFindById("Mannschaften.ListeName") is null
            ? null
            : new MannschaftsEintrag(
                e.TextOf("Mannschaften.ListeName"),
                e.TextOf("Mannschaften.ListeKurzname"),
                int.Parse(e.TextOf("Mannschaften.ListeOffensiv"))));

    /// <summary>Sucht nach <paramref name="name"/> und liefert den Eintrag, sobald er in der Liste steht.</summary>
    public MannschaftsEintrag Eintrag(string name)
    {
        Suchen(name);
        return Wait.UntilFound(() => Eintraege().FirstOrDefault(e => e.Name == name), Wait.DefaultTimeout,
            $"Mannschaft '{name}' in der Liste");
    }

    /// <summary>Wählt die Mannschaft in der Liste und wartet, bis das Formular sie zeigt.</summary>
    public void Auswaehlen(string name)
    {
        Suchen(name);
        var zeile = Wait.UntilFound(
            () => fenster.FindById("Mannschaften.Liste")
                .ReadAllItems(e => e.TextOf("Mannschaften.ListeName") == name ? e : null)
                .FirstOrDefault(),
            Wait.DefaultTimeout,
            $"Mannschaft '{name}' in der Liste");
        zeile.Activate();
        Wait.Until(() => fenster.TryFindById("Mannschaften.Name") is not null && Name == name, Wait.DefaultTimeout,
            $"Formular zeigt '{name}'");
    }

    /// <summary>Löscht die gewählte Mannschaft über die Befehlsleiste und beantwortet die Rückfrage.</summary>
    public void Loeschen(bool bestaetigen)
    {
        fenster.ActivateById("Befehlsleiste.Loeschen");
        var dialog = Dialog.WaitFor(fenster, LOESCHDIALOG);
        if (bestaetigen) dialog.Confirm();
        else dialog.Close();

        Wait.Until(() => Dialog.FindOpen(fenster).All(d => d.Title != LOESCHDIALOG), Wait.DefaultTimeout,
            $"Dialog '{LOESCHDIALOG}' geschlossen");
        hauptfenster.WarteBisRuhig();
    }

    private string Wert(string automationId) =>
        fenster.FindById(automationId).Patterns.Value.Pattern.Value.ValueOrDefault ?? string.Empty;

    private void WertSetzen(string automationId, string wert) =>
        fenster.FindById(automationId).Patterns.Value.Pattern.SetValue(wert);
}
