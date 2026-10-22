using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Formular „Neue Auslosung“ (#496): Runde, Name, Auslosungsart und Anzahl Lostöpfe. Kind-ViewModel von
/// <see cref="AuslosungViewModel"/>, ohne Datenbankzugriff — welche Runden auslosbar sind, ermittelt das
/// Eltern-ViewModel und übergibt sie mit <see cref="Oeffnen"/>.</summary>
public partial class NeueAuslosungViewModel : ObservableObject
{
    /// <summary>Runden ohne „Paarung fest vorgegeben“, ohne vorhandene Auslosung und mit feststehenden Qualifikanten
    /// (#320, #483) — siehe <see cref="AuslosungViewModel"/>.</summary>
    [ObservableProperty] private IReadOnlyList<TurnierRunde> _rundenOptionen = [];

    /// <summary>Welche Runden fehlen, weil ihre Qualifikanten noch nicht feststehen — <c>null</c>, wenn keine.</summary>
    [ObservableProperty] private string? _rundenHinweis;

    [ObservableProperty] private string _name = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AuslosungsartHinweis))]
    [NotifyPropertyChangedFor(nameof(AuslosungsartWarnung))]
    [NotifyPropertyChangedFor(nameof(AnzahlLostoepfeBearbeitbar))]
    [NotifyPropertyChangedFor(nameof(AuslosungsartAusgewaehlt))]
    private Auslosungsart _auslosungsart = Auslosungsart.Zufall;

    /// <summary>Wrapper für die ComboBox-Bindung: <c>SelectedItem</c> statt <c>SelectedValue</c>/
    /// <c>SelectedValuePath</c> — Letzteres zeigte in #300-Tests wiederholt einen WinUI3-Fallstrick (Auswahl
    /// erschien nach Reload/Sichtbarkeitswechsel leer, obwohl der zugrunde liegende Wert korrekt war), auch
    /// nach gezielter Nachbenachrichtigung. <c>SelectedItem</c> gegen den Options-Record (Werte-Gleichheit
    /// dank <c>record</c>) ist der im Rest der App bereits bewährte, robustere Mechanismus.</summary>
    public AuslosungsartOption? AuslosungsartAusgewaehlt
    {
        get => AuslosungsartOptionen.FirstOrDefault(o => o.Wert == Auslosungsart);
        set { if (value is not null) Auslosungsart = value.Wert; }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AuslosungsartHinweis))]
    [NotifyPropertyChangedFor(nameof(AuslosungsartWarnung))]
    private TurnierRunde? _runde;

    /// <summary>Gecachte Optionsliste statt einer bei jedem Binding-Zugriff neu berechneten Liste — eine
    /// dauerhaft neue Listeninstanz pro Lesezugriff ist ein bekannter WinUI3-ComboBox-Fallstrick
    /// (siehe WinUi/CLAUDE.md, "ItemsSource pro Fokus neu berechnet"): kombiniert mit
    /// <c>SelectedValuePath</c> kann das zu einem transienten Reset führen, der die eigentlich korrekte
    /// Auswahl visuell verwaist zurücklässt (Fund aus #300-Test — "Auslosungsart erscheint leer trotz
    /// vorheriger Auswahl"). Wird explizit in <see cref="OnRundeChanged"/> aktualisiert.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AuslosungsartAusgewaehlt))]
    private IReadOnlyList<AuslosungsartOption> _auslosungsartOptionen = [];

    private string _autoName = "";

    [ObservableProperty] private int _anzahlLostoepfe = 1;

    public string AuslosungsartHinweis  => AuslosungAnzeigeTexte.AuslosungsartHinweisText(Auslosungsart);
    public string? AuslosungsartWarnung => AuslosungAnzeigeTexte.AuslosungsartWarnungText(Auslosungsart, Runde?.Rundenart);

    public bool AnzahlLostoepfeBearbeitbar => AuslosungViewModel.ErlaubtMehrereToepfe(Auslosungsart);

    public void Oeffnen(IReadOnlyList<TurnierRunde> rundenOptionen, string? rundenHinweis)
    {
        RundenOptionen  = rundenOptionen;
        RundenHinweis   = rundenHinweis;
        _autoName       = "";
        Name            = "";
        AnzahlLostoepfe = 1;
        Auslosungsart   = Auslosungsart.Zufall;
        Runde           = RundenOptionen.FirstOrDefault();
        // Explizit statt nur über OnRundeChanged: bleibt die vorausgewählte Runde dieselbe wie beim letzten
        // Mal, feuert der Hook nicht, und die Vorgabe der Runde (Issue #347) ginge durch den Reset oben verloren.
        Auslosungsart   = StartAuslosungsartFuer(Runde, Auslosungsart);
    }

    partial void OnRundeChanged(TurnierRunde? value)
    {
        // Reihenfolge bewusst: erst den Zielwert festlegen (falls die aktuelle Art in der neuen Runde nicht
        // mehr kompatibel ist), DANACH die Optionsliste (ItemsSource) neu setzen — minimiert das Zeitfenster,
        // in dem die ComboBox eine ItemsSource ohne passenden Eintrag zur aktuellen Auswahl hätte (siehe
        // Doku bei AuslosungsartOptionen).
        Auslosungsart = StartAuslosungsartFuer(value, Auslosungsart);

        AuslosungsartOptionen = AuslosungViewModel.AuslosungsartOptionenFuer(value?.Rundenart);
        // Erzwungene Nachbenachrichtigung: falls WinUI die ComboBox-Auswahl beim ItemsSource-Wechsel
        // transient zurücksetzt und der anschließende Vergleich zufällig denselben Enum-Wert ergibt,
        // unterdrückt CommunityToolkits Gleichheitsprüfung die Benachrichtigung — die ComboBox bliebe dann
        // visuell leer, obwohl Auslosungsart korrekt ist (Fund aus #300-Test).
        OnPropertyChanged(nameof(Auslosungsart));

        if (value is null) return;
        if (string.IsNullOrWhiteSpace(Name) || Name == _autoName)
        {
            _autoName = value.Name;
            Name      = value.Name;
        }
    }

    partial void OnAuslosungsartChanged(Auslosungsart value)
    {
        if (!AnzahlLostoepfeBearbeitbar)
            AnzahlLostoepfe = 1;
        // Topf-Zuordnung braucht mindestens 2 gleich große Töpfe — 4 entspricht der UCL-Ligaphase (Issue #347).
        else if (value == Auslosungsart.TopfZuordnung && AnzahlLostoepfe < 2)
            AnzahlLostoepfe = 4;
    }

    /// <summary>Auslosungsart, mit der das Formular für <paramref name="runde"/> startet (Issue #347):
    /// zuerst die von der Vorlage vorgegebene Art der Runde, sonst die bisherige Auswahl, solange sie zur
    /// Rundenart passt, sonst die erste kompatible Art (z.B. Topf-Zuordnung bei einer TopfLiga).</summary>
    private static Auslosungsart StartAuslosungsartFuer(TurnierRunde? runde, Auslosungsart bisher)
    {
        if (runde is null) return bisher;
        if (runde.VorgegebeneAuslosungsart is { } vorgabe && AuslosungsKompatibilitaet.IstKompatibel(runde.Rundenart, vorgabe))
            return vorgabe;
        if (AuslosungsKompatibilitaet.IstKompatibel(runde.Rundenart, bisher))
            return bisher;
        return AuslosungViewModel.AuslosungsartOptionenFuer(runde.Rundenart).Select(o => o.Wert).DefaultIfEmpty(Auslosungsart.Zufall).First();
    }
}
