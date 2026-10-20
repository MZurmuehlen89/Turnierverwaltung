using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Ranglisten.Eingaben;
using Fussballturnierverwaltung.Application.Ranglisten.Import;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Auswahloption der Tabellen-Liste (Issue #225) — Beschreibung fasst Zeilen-/Spaltenzahl und
/// die erste Vorschauzeile zusammen, damit der Nutzer die richtige Tabelle ohne HTML-Kenntnis per
/// Klick erkennt.</summary>
public record TabellenAuswahlOption(RanglistenTabellenVorschau Vorschau, string Beschreibung);

/// <summary>Auswahloption einer Spalten-Liste — SpaltenIndex null steht für "— keine —" (nur bei der
/// optionalen Punkte-Spalte verwendet).</summary>
public record SpaltenAuswahlOption(int? SpaltenIndex, string Beschreibung);

/// <summary>Reiter „Quellen“ der Ranglisten-Seite (#477, vorher Dialog aus #285, #291): Liste der
/// Import-Quellen und Formular mit Webseiten-Prüfung, Tabellen- und Spaltenzuordnung samt Vorschau.</summary>
public partial class RanglistenQuellenViewModel : ObservableObject, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly RanglistenQuelleService _service;
    private readonly IDialogService _dialogService;
    private int _populiertGerade;
    private RanglistenQuelle? _bearbeitet;

    private static readonly HashSet<string> _formFelder =
    [
        nameof(Name), nameof(Url), nameof(AusgewaehlteTabelle), nameof(AusgewaehlteMannschaftSpalte),
        nameof(AusgewaehltePunkteSpalte), nameof(KopfzeileUeberspringen), nameof(FormularAktiv)
    ];

    [ObservableProperty] private ObservableCollection<RanglistenQuelle> _quellen = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private RanglistenQuelle? _ausgewaehlte;

    [ObservableProperty] private bool _formularAktiv;
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _url = "";
    [ObservableProperty] private string? _fehlermeldung;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(WebseitePruefenCommand))]
    private bool _istPruefend;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TabellenGefunden))]
    private IReadOnlyList<TabellenAuswahlOption> _tabellenOptionen = [];

    public bool TabellenGefunden => TabellenOptionen.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AusgewaehlteTabelleAuswahl))]
    [NotifyPropertyChangedFor(nameof(TabelleGewaehlt))]
    private TabellenAuswahlOption? _ausgewaehlteTabelle;

    public bool TabelleGewaehlt => AusgewaehlteTabelle is not null;

    [ObservableProperty] private IReadOnlyList<SpaltenAuswahlOption> _mannschaftSpaltenOptionen = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AusgewaehlteMannschaftSpalteAuswahl))]
    private SpaltenAuswahlOption? _ausgewaehlteMannschaftSpalte;

    [ObservableProperty] private IReadOnlyList<SpaltenAuswahlOption> _punkteSpaltenOptionen = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AusgewaehltePunkteSpalteAuswahl))]
    private SpaltenAuswahlOption? _ausgewaehltePunkteSpalte;

    [ObservableProperty] private bool _kopfzeileUeberspringen = true;

    [ObservableProperty] private QuellenTabellenVorschau? _vorschau;

    // WinUI-ComboBoxen schreiben beim Laden bzw. beim Tausch der ItemsSource null über TwoWay zurück
    // (#377, siehe RundenFormularViewModel.RundenartAuswahl). Deshalb binden sie an diese Hüllen: null wird
    // verworfen und die Anzeige danach wiederhergestellt.
    public TabellenAuswahlOption? AusgewaehlteTabelleAuswahl
    {
        get => AusgewaehlteTabelle;
        set { if (value is not null) AusgewaehlteTabelle = value; else AnzeigeWiederherstellen(nameof(AusgewaehlteTabelleAuswahl)); }
    }

    public SpaltenAuswahlOption? AusgewaehlteMannschaftSpalteAuswahl
    {
        get => AusgewaehlteMannschaftSpalte;
        set { if (value is not null) AusgewaehlteMannschaftSpalte = value; else AnzeigeWiederherstellen(nameof(AusgewaehlteMannschaftSpalteAuswahl)); }
    }

    public SpaltenAuswahlOption? AusgewaehltePunkteSpalteAuswahl
    {
        get => AusgewaehltePunkteSpalte;
        set { if (value is not null) AusgewaehltePunkteSpalte = value; else AnzeigeWiederherstellen(nameof(AusgewaehltePunkteSpalteAuswahl)); }
    }

    private void AnzeigeWiederherstellen(string eigenschaft) =>
        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(
            Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
            () => OnPropertyChanged(eigenschaft));

    // ── IRibbonAware ──────────────────────────────────────────────────────────
    public string RibbonTabTitel => "Ranglisten";
    public ICommand NeuBefehl => NeuCommand;
    public ICommand SpeichernBefehl => SpeichernCommand;
    public ICommand AbbrechenBefehl => AbbrechenCommand;
    public ICommand LoeschenBefehl => LoeschenCommand;


    public RanglistenQuellenViewModel(RanglistenQuelleService service, IDialogService dialogService)
    {
        _service = service;
        _dialogService = dialogService;
    }

    public async Task LadenAsync()
    {
        var quellen = await _service.AlleAbrufenAsync();
        Quellen = new ObservableCollection<RanglistenQuelle>(quellen);
    }

    /// <summary>Wählt die Quelle mit dieser Id aus (Sprung aus dem Import, #477).</summary>
    public void Auswaehlen(int quelleId) => Ausgewaehlte = Quellen.FirstOrDefault(q => q.Id == quelleId);

    // ── Änderungsstand ────────────────────────────────────────────────────────

    /// <summary>Neue Quelle: immer ungespeichert. Bestehende: Abweichung vom gespeicherten Stand; Tabelle und
    /// Spalten zählen erst, wenn die Webseite geprüft wurde (vorher gibt es keine Auswahl).</summary>
    public bool HatUngespeicherteAenderungen => FormularAktiv && (_bearbeitet is null || WeichtAbVon(_bearbeitet));

    private bool WeichtAbVon(RanglistenQuelle quelle) =>
        Name != quelle.Name
        || Url != quelle.Url
        || KopfzeileUeberspringen != quelle.KopfzeileUeberspringen
        || (AusgewaehlteTabelle is not null
            && (AusgewaehlteTabelle.Vorschau.Index != quelle.TabellenIndex
                || AusgewaehlteMannschaftSpalte?.SpaltenIndex != quelle.MannschaftSpalte
                || AusgewaehltePunkteSpalte?.SpaltenIndex != quelle.PunkteSpalte));

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is null || !_formFelder.Contains(e.PropertyName)) return;

        if (e.PropertyName is nameof(AusgewaehlteTabelle) or nameof(AusgewaehlteMannschaftSpalte)
            or nameof(AusgewaehltePunkteSpalte) or nameof(KopfzeileUeberspringen))
            Vorschau = AusgewaehlteTabelle is null
                ? null
                : QuellenTabellenVorschau.Erstellen(AusgewaehlteTabelle.Vorschau,
                    AusgewaehlteMannschaftSpalte?.SpaltenIndex, AusgewaehltePunkteSpalte?.SpaltenIndex, KopfzeileUeberspringen);

        ZustandGeaendertMelden();
    }

    /// <summary>Gespeicherte Zuordnung als Text, solange die Webseite nicht geprüft ist.</summary>
    public string? GespeicherteZuordnungText => _bearbeitet is null || AusgewaehlteTabelle is not null
        ? null
        : $"Gespeichert: Tabelle {_bearbeitet.TabellenIndex + 1}, Mannschaft in Spalte {_bearbeitet.MannschaftSpalte + 1}"
          + (_bearbeitet.PunkteSpalte is int p ? $", Punkte in Spalte {p + 1}" : ", ohne Punkte")
          + ". „Webseite prüfen“ zeigt die Tabelle zur Kontrolle.";

    private void ZustandGeaendertMelden()
    {
        OnPropertyChanged(nameof(GespeicherteZuordnungText));
        SpeichernCommand.NotifyCanExecuteChanged();
        AbbrechenCommand.NotifyCanExecuteChanged();
        WebseitePruefenCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(HatUngespeicherteAenderungen));
    }

    // ── Liste/Formular ────────────────────────────────────────────────────────

    // Auswahl in der Liste füllt das Formular direkt (app-weites Liste+Formular-Muster).
    partial void OnAusgewaehlteChanged(RanglistenQuelle? value)
    {
        if (_populiertGerade > 0) return;
        FormularFuellen(value);
    }

    private void FormularFuellen(RanglistenQuelle? quelle)
    {
        _bearbeitet = quelle;
        _populiertGerade++;
        try
        {
            Name = quelle?.Name ?? "";
            Url = quelle?.Url ?? "";
            KopfzeileUeberspringen = quelle?.KopfzeileUeberspringen ?? true;
            TabellenOptionen = [];
            AusgewaehlteTabelle = null;
            Fehlermeldung = null;
        }
        finally { _populiertGerade--; }
        FormularAktiv = quelle is not null;
    }

    [RelayCommand]
    private void Neu()
    {
        _populiertGerade++;
        try { Ausgewaehlte = null; }
        finally { _populiertGerade--; }
        FormularFuellen(null);
        FormularAktiv = true;
    }

    [RelayCommand(CanExecute = nameof(HatUngespeicherteAenderungen))]
    private void Abbrechen()
    {
        if (_bearbeitet is not null)
        {
            FormularFuellen(_bearbeitet);
            return;
        }
        FormularFuellen(null);
    }

    [RelayCommand(CanExecute = nameof(KannLoeschen))]
    private async Task Loeschen()
    {
        if (Ausgewaehlte is null) return;
        var bestaetigt = await _dialogService.BestaetigenAsync(
            "Quelle löschen", $"'{Ausgewaehlte.Name}' wirklich löschen?");
        if (!bestaetigt) return;

        try
        {
            await _service.LoeschenAsync(Ausgewaehlte.Id);
            await LadenAsync();
            Ausgewaehlte = null;
            FormularFuellen(null);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    private bool KannLoeschen() => Ausgewaehlte is not null;

    // ── Webseite prüfen ───────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(KannWebseitePruefen))]
    private async Task WebseitePruefen()
    {
        Fehlermeldung = null;
        IstPruefend = true;
        try
        {
            var tabellen = await _service.WebseitePruefenAsync(Url.Trim());
            if (tabellen.Count == 0)
            {
                Fehlermeldung = "Auf der Seite wurden keine Tabellen gefunden.";
                TabellenOptionen = [];
                AusgewaehlteTabelle = null;
                return;
            }

            TabellenOptionen = tabellen.Select(TabellenBeschreiben).ToList();
            AusgewaehlteTabelle = TabellenOptionen.FirstOrDefault(t => t.Vorschau.Index == _bearbeitet?.TabellenIndex)
                ?? TabellenOptionen[0];
        }
        catch (Exception ex)
        {
            Fehlermeldung = $"Seite konnte nicht geladen werden: {ex.Message}";
        }
        finally
        {
            IstPruefend = false;
        }
    }

    private bool KannWebseitePruefen() => FormularAktiv && !IstPruefend && !string.IsNullOrWhiteSpace(Url);

    // Neue Tabelle: Spaltenoptionen neu aufbauen und die gespeicherte Zuordnung vorwählen, sofern es die Tabelle
    // der gespeicherten Quelle ist — sonst die erkannte Vorgabe.
    partial void OnAusgewaehlteTabelleChanged(TabellenAuswahlOption? value)
    {
        if (value is null)
        {
            MannschaftSpaltenOptionen = [];
            PunkteSpaltenOptionen = [];
            AusgewaehlteMannschaftSpalte = null;
            AusgewaehltePunkteSpalte = null;
            return;
        }

        var vorschau = value.Vorschau;
        var spalten = SpaltenBeschreiben(vorschau);
        var gespeicherteTabelle = _bearbeitet is not null && vorschau.Index == _bearbeitet.TabellenIndex;

        MannschaftSpaltenOptionen = spalten;
        PunkteSpaltenOptionen = [new SpaltenAuswahlOption(null, "— keine —"), .. spalten];

        AusgewaehlteMannschaftSpalte = (gespeicherteTabelle
            ? spalten.FirstOrDefault(s => s.SpaltenIndex == _bearbeitet!.MannschaftSpalte)
            : null) ?? spalten.FirstOrDefault();
        AusgewaehltePunkteSpalte = (gespeicherteTabelle && _bearbeitet!.PunkteSpalte is not null
            ? PunkteSpaltenOptionen.FirstOrDefault(s => s.SpaltenIndex == _bearbeitet.PunkteSpalte)
            : null) ?? PunkteSpaltenOptionen[0];
        KopfzeileUeberspringen = gespeicherteTabelle ? _bearbeitet!.KopfzeileUeberspringen : vorschau.ErkannteKopfzeile;
    }

    private static TabellenAuswahlOption TabellenBeschreiben(RanglistenTabellenVorschau vorschau)
    {
        var ersteZeile = vorschau.VorschauZeilen.Count > 0 ? string.Join(" | ", vorschau.VorschauZeilen[0]) : "";
        var beschreibung = $"Tabelle {vorschau.Index + 1}: {vorschau.ZeilenAnzahl} Zeilen × {vorschau.SpaltenAnzahl} Spalten"
            + (string.IsNullOrWhiteSpace(ersteZeile) ? "" : $" — {ersteZeile}");
        return new TabellenAuswahlOption(vorschau, beschreibung);
    }

    private static List<SpaltenAuswahlOption> SpaltenBeschreiben(RanglistenTabellenVorschau vorschau)
    {
        var datenZeilen = vorschau.ErkannteKopfzeile ? vorschau.VorschauZeilen.Skip(1) : vorschau.VorschauZeilen;
        var optionen = new List<SpaltenAuswahlOption>();

        for (int spalte = 0; spalte < vorschau.SpaltenAnzahl; spalte++)
        {
            var beispiele = datenZeilen
                .Select(zeile => spalte < zeile.Count ? zeile[spalte] : "")
                .Where(wert => !string.IsNullOrWhiteSpace(wert))
                .Take(3)
                .ToList();

            var kopf = vorschau.ErkannteKopfzeile && spalte < vorschau.VorschauZeilen[0].Count
                ? vorschau.VorschauZeilen[0][spalte]
                : $"Spalte {spalte + 1}";

            var beschreibung = beispiele.Count > 0 ? $"{kopf} ({string.Join(", ", beispiele)})" : kopf;
            optionen.Add(new SpaltenAuswahlOption(spalte, beschreibung));
        }

        return optionen;
    }

    // ── Speichern ─────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(KannSpeichern))]
    private async Task Speichern()
    {
        Fehlermeldung = null;
        if (ZuordnungAusFormular() is not { } zuordnung)
        {
            Fehlermeldung = AusgewaehlteTabelle is null
                ? "Bitte zuerst die Webseite prüfen und eine Tabelle auswählen."
                : "Bitte die Mannschaftsspalte auswählen.";
            return;
        }
        var (tabelle, mannschaftSpalte, punkteSpalte) = zuordnung;

        try
        {
            int id;
            if (_bearbeitet is null)
            {
                var neu = await _service.ErstellenAsync(new RanglistenQuelleErstellenEingabe(
                    Name, Url, tabelle, null, mannschaftSpalte, punkteSpalte, KopfzeileUeberspringen));
                id = neu.Id;
            }
            else
            {
                id = _bearbeitet.Id;
                await _service.AktualisierenAsync(id, new RanglistenQuelleAktualisierenEingabe(
                    Name, Url, tabelle, null, mannschaftSpalte, punkteSpalte, KopfzeileUeberspringen));
            }

            await LadenAsync();
            // Formular bleibt stehen (samt geprüfter Tabelle und Vorschau), nur der Vergleichsstand wird der
            // gespeicherte. Ausgewaehlte ohne Formular-Neuaufbau setzen, sonst wäre die Prüfung weg.
            _populiertGerade++;
            try { Ausgewaehlte = Quellen.FirstOrDefault(q => q.Id == id); }
            finally { _populiertGerade--; }
            _bearbeitet = Ausgewaehlte;
            ZustandGeaendertMelden();
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    /// <summary>Tabelle und Spalten zum Speichern: aus der Prüfung, sonst – bei einer bestehenden Quelle mit
    /// unveränderter URL – die gespeicherten; <c>null</c>, wenn erst geprüft werden muss.</summary>
    private (int Tabelle, int MannschaftSpalte, int? PunkteSpalte)? ZuordnungAusFormular()
    {
        if (AusgewaehlteTabelle is not null)
            return AusgewaehlteMannschaftSpalte?.SpaltenIndex is int spalte
                ? (AusgewaehlteTabelle.Vorschau.Index, spalte, AusgewaehltePunkteSpalte?.SpaltenIndex)
                : null;
        return _bearbeitet is not null && Url == _bearbeitet.Url
            ? (_bearbeitet.TabellenIndex, _bearbeitet.MannschaftSpalte, _bearbeitet.PunkteSpalte)
            : null;
    }

    private bool KannSpeichern() => HatUngespeicherteAenderungen
        && !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Url);
}
