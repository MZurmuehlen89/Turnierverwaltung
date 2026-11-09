using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Diagnose;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Stammdaten.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.Helpers;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Reiter „Verbände“ der Seite Länder &amp; Verbände (#606).</summary>
public partial class VerbandEditorViewModel : ObservableObject
{
    private readonly VerbandService _verbandService;
    private readonly VerbandValidator _verbandValidator;
    private readonly IDialogService _dialogService;
    private readonly ILogger<VerbandEditorViewModel> _logger;
    private Func<Task>? _neuLaden;
    private int _populiertGerade;

    [ObservableProperty] private string? _fehlermeldung;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerbaendeGefiltert))]
    private ObservableCollection<Verband> _verbaende = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerbaendeGefiltert))]
    private string _suchbegriff = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private Verband? _ausgewaehlterVerband;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private bool _formularAktiv;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    private bool _isDirty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private bool _istNeuModus;

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _kurzname = "";
    [ObservableProperty] private byte[]? _logoDaten;
    [ObservableProperty] private RanglistenOption? _rangliste = RanglistenOption.Keine;
    [ObservableProperty] private RanglistenOption? _vereinsRangliste = RanglistenOption.Keine;

    /// <summary>Live-Prüfung des Verbandsformulars (#602); sperrt Speichern, solange sie Fehler enthält.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    private IReadOnlyList<Feldfehler> _fehler = [];

    // Ranglisten zur Auswahl (#572), „keine“ zuerst.
    [ObservableProperty] private IReadOnlyList<RanglistenOption> _ranglistenOptionen = [RanglistenOption.Keine];

    // ── Berechnete Listen ─────────────────────────────────────────────────────
    public IEnumerable<Verband> VerbaendeGefiltert => Verbaende
        .Where(v => string.IsNullOrWhiteSpace(Suchbegriff)
            || v.Name.Contains(Suchbegriff, StringComparison.OrdinalIgnoreCase)
            || v.Kurzname.Contains(Suchbegriff, StringComparison.OrdinalIgnoreCase));

    // Wird in OnPropertyChanged mit VerbaendeGefiltert nachgezogen (alle Quellen automatisch).
    public string AnzahlText => DatensatzAnzahl.Text(
        VerbaendeGefiltert.Count(), Verbaende.Count, !string.IsNullOrWhiteSpace(Suchbegriff),
        "Verband", "Verbände");

    private static readonly HashSet<string> _formFelder =
    [
        nameof(Name), nameof(Kurzname), nameof(LogoDaten), nameof(Rangliste),
        nameof(VereinsRangliste)
    ];

    public VerbandEditorViewModel(
        VerbandService verbandService, VerbandValidator verbandValidator, IDialogService dialogService,
        ILogger<VerbandEditorViewModel> logger)
    {
        _verbandService = verbandService;
        _verbandValidator = verbandValidator;
        _dialogService = dialogService;
        _logger = logger;
    }

    /// <summary>Lädt nach dem Speichern oder Löschen die ganze Seite neu – Länder zeigen den Verband.</summary>
    public void NeuLadenUeber(Func<Task> neuLaden) => _neuLaden = neuLaden;

    // Ohne Rahmen bliebe die Liste nach dem Speichern still veraltet – darum laut scheitern statt nichts tun.
    private Task NeuLadenAsync() =>
        (_neuLaden ?? throw new InvalidOperationException("Neuladen nicht festgelegt (NeuLadenUeber).")).Invoke();

    public async Task LadenAsync(IReadOnlyList<RanglistenOption> ranglistenOptionen)
    {
        var verbaende = await _verbandService.AlleAbrufenAsync();
        RanglistenOptionen = ranglistenOptionen;
        Verbaende = new ObservableCollection<Verband>(verbaende.OrderBy(v => v.Name));
    }

    public void MeldungVerwerfen() => Fehlermeldung = null;

    public bool HatUngespeicherteAenderungen => FormularAktiv && IsDirty;

    partial void OnAusgewaehlterVerbandChanged(Verband? value) => FormularAusFuellenMit(value);

    private void FormularAusFuellenMit(Verband? verband)
    {
        _populiertGerade++;
        try
        {
            if (verband is null)
            {
                FormularAktiv = false;
                IstNeuModus = false;
                Name = Kurzname = "";
                LogoDaten = null;
                Rangliste = RanglistenOption.Keine;
                VereinsRangliste = RanglistenOption.Keine;
            }
            else
            {
                FormularAktiv = true;
                IstNeuModus = false;
                Name = verband.Name;
                Kurzname = verband.Kurzname;
                LogoDaten = verband.Logo;
                Rangliste = RanglistenOption.Fuer(RanglistenOptionen, verband.RanglisteId);
                VereinsRangliste = RanglistenOption.Fuer(RanglistenOptionen, verband.VereinsRanglisteId);
            }
            IsDirty = false;
            Fehlermeldung = null;
        }
        finally
        {
            _populiertGerade--;
        }
        AktualisiereFehler();
    }

    // ── Ribbon-Befehle ────────────────────────────────────────────────────────

    [RelayCommand]
    private void Neu()
    {
        _populiertGerade++;
        try
        {
            AusgewaehlterVerband = null;
            FormularAktiv = true;
            IstNeuModus = true;
            Name = Kurzname = "";
            LogoDaten = null;
            Rangliste = RanglistenOption.Keine;
            VereinsRangliste = RanglistenOption.Keine;
            Fehlermeldung = null;
        }
        finally
        {
            _populiertGerade--;
        }
        IsDirty = true;
        AktualisiereFehler();
    }

    [RelayCommand]
    private async Task LogoWaehlenAsync()
    {
        var daten = await _dialogService.BilddateiWaehlenAsync();
        if (daten is not null) LogoDaten = daten;
    }

    [RelayCommand]
    private void LogoEntfernen() => LogoDaten = null;

    [RelayCommand(CanExecute = nameof(KannSpeichern))]
    private async Task Speichern()
    {
        // Als Vorgang, weil die Seite danach neu lädt: Ladeanzeige und „Bereit“ für GUI-Tests folgen nur offenen
        // Vorgängen (#597, Muster aus #535)
        using var vorgang = _logger.Vorgang($"Verbände: speichern ({Name})", "Verband wird gespeichert …");
        Fehlermeldung = null;
        try
        {
            if (IstNeuModus)
            {
                var neu = await _verbandService.ErstellenAsync(ErstellenEingabe());
                await NeuLadenAsync();
                AusgewaehlterVerband = Verbaende.FirstOrDefault(v => v.Id == neu.Id);
            }
            else if (AusgewaehlterVerband is not null)
            {
                var id = AusgewaehlterVerband.Id;
                await _verbandService.AktualisierenAsync(id, AktualisierenEingabe());
                await NeuLadenAsync();
                AusgewaehlterVerband = Verbaende.FirstOrDefault(v => v.Id == id);
            }
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(KannAbbrechen))]
    private void Abbrechen() => FormularAusFuellenMit(IstNeuModus ? null : AusgewaehlterVerband);

    [RelayCommand(CanExecute = nameof(KannLoeschen))]
    private async Task Loeschen()
    {
        if (AusgewaehlterVerband is null) return;
        Fehlermeldung = await LoeschPruefung.SperreErmittelnAsync(_logger, "Verbände", () => _verbandService.LoeschSperreAsync(AusgewaehlterVerband.Id));
        if (Fehlermeldung is not null) return;
        var bestaetigt = await _dialogService.BestaetigenAsync(
            "Verband löschen", $"Soll '{AusgewaehlterVerband.Name}' wirklich gelöscht werden?");
        if (!bestaetigt) return;
        using var vorgang = _logger.Vorgang($"Verbände: löschen ({AusgewaehlterVerband.Name})", "Verband wird gelöscht …");
        Fehlermeldung = null;
        try
        {
            await _verbandService.LoeschenAsync(AusgewaehlterVerband.Id);
            await NeuLadenAsync();
            AusgewaehlterVerband = null;
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand]
    private void FilterZuruecksetzen() => Suchbegriff = "";

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(VerbaendeGefiltert)) OnPropertyChanged(nameof(AnzahlText));
        if (_populiertGerade != 0) return;
        if (_formFelder.Contains(e.PropertyName!))
        {
            IsDirty = true;
            AktualisiereFehler();
        }
    }

    // ── Live-Prüfung (#602) ───────────────────────────────────────────────────

    private VerbandErstellenEingabe ErstellenEingabe() => new(
        Name, Kurzname, LogoDaten, Rangliste?.Id, VereinsRangliste?.Id);

    private VerbandAktualisierenEingabe AktualisierenEingabe() => new(
        Name, Kurzname, LogoDaten, Rangliste?.Id, VereinsRangliste?.Id);

    // Erstellen und Bearbeiten prüfen dieselben Regeln – die Live-Prüfung nimmt darum immer die Erstellen-Eingabe.
    private void AktualisiereFehler() =>
        Fehler = FormularAktiv ? _verbandValidator.Validieren(ErstellenEingabe()).Meldungen : [];

    private bool KannSpeichern() => FormularAktiv && IsDirty && Fehler.Count == 0;

    private bool KannAbbrechen() => FormularAktiv && IsDirty;

    private bool KannLoeschen() => AusgewaehlterVerband is not null && !IstNeuModus;
}
