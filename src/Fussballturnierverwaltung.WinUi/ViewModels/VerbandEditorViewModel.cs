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
    private Func<Task> _neuLaden = () => Task.CompletedTask;
    private int _populiertGerade;

    private static readonly RanglistenOption KeineRangliste = LaenderUndVerbaendeViewModel.KeineRangliste;

    [ObservableProperty] private string? _fehlermeldung;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerbaendeGefiltert))]
    private ObservableCollection<Verband> _verbaende = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerbaendeGefiltert))]
    private string _verbandSuchbegriff = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private Verband? _ausgewaehlterVerband;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private bool _verbandFormularAktiv;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    private bool _verbandIsDirty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private bool _verbandIstNeuModus;

    [ObservableProperty] private string _verbandName = "";
    [ObservableProperty] private string _verbandKurzname = "";
    [ObservableProperty] private byte[]? _verbandLogoDaten;
    [ObservableProperty] private RanglistenOption? _verbandRangliste = KeineRangliste;
    [ObservableProperty] private RanglistenOption? _verbandVereinsRangliste = KeineRangliste;

    /// <summary>Live-Prüfung des Verbandsformulars (#602); sperrt Speichern, solange sie Fehler enthält.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    private IReadOnlyList<Feldfehler> _verbandFehler = [];

    // Ranglisten zur Auswahl (#572), „keine“ zuerst.
    [ObservableProperty] private IReadOnlyList<RanglistenOption> _ranglistenOptionen = [KeineRangliste];

    // ── Berechnete Listen ─────────────────────────────────────────────────────
    public IEnumerable<Verband> VerbaendeGefiltert => Verbaende
        .Where(v => string.IsNullOrWhiteSpace(VerbandSuchbegriff)
            || v.Name.Contains(VerbandSuchbegriff, StringComparison.OrdinalIgnoreCase)
            || v.Kurzname.Contains(VerbandSuchbegriff, StringComparison.OrdinalIgnoreCase));

    // Wird in OnPropertyChanged mit VerbaendeGefiltert nachgezogen (alle Quellen automatisch).
    public string VerbaendeAnzahlText => DatensatzAnzahl.Text(
        VerbaendeGefiltert.Count(), Verbaende.Count, !string.IsNullOrWhiteSpace(VerbandSuchbegriff),
        "Verband", "Verbände");

    private static readonly HashSet<string> _verbandFormFelder =
    [
        nameof(VerbandName), nameof(VerbandKurzname), nameof(VerbandLogoDaten), nameof(VerbandRangliste),
        nameof(VerbandVereinsRangliste)
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

    public async Task LadenAsync(IReadOnlyList<RanglistenOption> ranglistenOptionen)
    {
        var verbaende = await _verbandService.AlleAbrufenAsync();
        RanglistenOptionen = ranglistenOptionen;
        Verbaende = new ObservableCollection<Verband>(verbaende.OrderBy(v => v.Name));
    }

    public void MeldungVerwerfen() => Fehlermeldung = null;

    public bool HatUngespeicherteAenderungen => VerbandFormularAktiv && VerbandIsDirty;

    partial void OnAusgewaehlterVerbandChanged(Verband? value) => VerbandFormularAusFuellenMit(value);

    private RanglistenOption RanglisteOptionFuer(int? ranglisteId) =>
        RanglistenOptionen.FirstOrDefault(o => o.Id == ranglisteId) ?? KeineRangliste;

    private void VerbandFormularAusFuellenMit(Verband? verband)
    {
        _populiertGerade++;
        try
        {
            if (verband is null)
            {
                VerbandFormularAktiv = false;
                VerbandIstNeuModus = false;
                VerbandName = VerbandKurzname = "";
                VerbandLogoDaten = null;
                VerbandRangliste = KeineRangliste;
                VerbandVereinsRangliste = KeineRangliste;
            }
            else
            {
                VerbandFormularAktiv = true;
                VerbandIstNeuModus = false;
                VerbandName = verband.Name;
                VerbandKurzname = verband.Kurzname;
                VerbandLogoDaten = verband.Logo;
                VerbandRangliste = RanglisteOptionFuer(verband.RanglisteId);
                VerbandVereinsRangliste = RanglisteOptionFuer(verband.VereinsRanglisteId);
            }
            VerbandIsDirty = false;
            Fehlermeldung = null;
        }
        finally
        {
            _populiertGerade--;
        }
        AktualisiereVerbandFehler();
    }

    // ── Ribbon-Befehle ────────────────────────────────────────────────────────

    [RelayCommand]
    private void Neu()
    {
        _populiertGerade++;
        try
        {
            AusgewaehlterVerband = null;
            VerbandFormularAktiv = true;
            VerbandIstNeuModus = true;
            VerbandName = VerbandKurzname = "";
            VerbandLogoDaten = null;
            VerbandRangliste = KeineRangliste;
            VerbandVereinsRangliste = KeineRangliste;
            Fehlermeldung = null;
        }
        finally
        {
            _populiertGerade--;
        }
        VerbandIsDirty = true;
        AktualisiereVerbandFehler();
    }

    [RelayCommand]
    private async Task VerbandLogoWaehlenAsync()
    {
        var daten = await _dialogService.BilddateiWaehlenAsync();
        if (daten is not null) VerbandLogoDaten = daten;
    }

    [RelayCommand]
    private void VerbandLogoEntfernen() => VerbandLogoDaten = null;

    [RelayCommand(CanExecute = nameof(KannSpeichern))]
    private async Task Speichern()
    {
        // Als Vorgang, weil die Seite danach neu lädt: Ladeanzeige und „Bereit“ für GUI-Tests folgen nur offenen
        // Vorgängen (#597, Muster aus #535)
        using var vorgang = _logger.Vorgang($"Verbände: speichern ({VerbandName})", "Verband wird gespeichert …");
        await VerbandSpeichern();
    }

    private async Task VerbandSpeichern()
    {
        Fehlermeldung = null;
        try
        {
            if (VerbandIstNeuModus)
            {
                var neu = await _verbandService.ErstellenAsync(VerbandErstellenEingabe());
                await _neuLaden();
                AusgewaehlterVerband = Verbaende.FirstOrDefault(v => v.Id == neu.Id);
            }
            else if (AusgewaehlterVerband is not null)
            {
                var id = AusgewaehlterVerband.Id;
                await _verbandService.AktualisierenAsync(id, VerbandAktualisierenEingabe());
                await _neuLaden();
                AusgewaehlterVerband = Verbaende.FirstOrDefault(v => v.Id == id);
            }
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(KannAbbrechen))]
    private void Abbrechen() => VerbandFormularAusFuellenMit(VerbandIstNeuModus ? null : AusgewaehlterVerband);

    [RelayCommand(CanExecute = nameof(KannLoeschen))]
    private async Task Loeschen()
    {
        if (AusgewaehlterVerband is null) return;
        if (await LoeschSperreMeldenAsync(() => _verbandService.LoeschSperreAsync(AusgewaehlterVerband.Id))) return;
        var bestaetigt = await _dialogService.BestaetigenAsync(
            "Verband löschen", $"Soll '{AusgewaehlterVerband.Name}' wirklich gelöscht werden?");
        if (!bestaetigt) return;
        using var vorgang = _logger.Vorgang($"Verbände: löschen ({AusgewaehlterVerband.Name})", "Verband wird gelöscht …");
        Fehlermeldung = null;
        try
        {
            await _verbandService.LoeschenAsync(AusgewaehlterVerband.Id);
            await _neuLaden();
            AusgewaehlterVerband = null;
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    /// <summary>Zeigt den Grund, warum sich der Datensatz nicht löschen lässt; true = gesperrt.</summary>
    private async Task<bool> LoeschSperreMeldenAsync(Func<Task<string?>> sperreErmitteln)
    {
        // Als Vorgang, damit GUI-Tests erst nach der Abfrage weitermachen (Muster #597)
        using var vorgang = _logger.Vorgang("Länder & Verbände: Löschen prüfen", "Löschen wird geprüft …");
        try
        {
            Fehlermeldung = await sperreErmitteln();
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        return Fehlermeldung is not null;
    }

    [RelayCommand]
    private void FilterZuruecksetzen() => VerbandSuchbegriff = "";

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(VerbaendeGefiltert)) OnPropertyChanged(nameof(VerbaendeAnzahlText));
        if (_populiertGerade != 0) return;
        if (_verbandFormFelder.Contains(e.PropertyName!))
        {
            VerbandIsDirty = true;
            AktualisiereVerbandFehler();
        }
    }

    // ── Live-Prüfung (#602) ───────────────────────────────────────────────────

    private VerbandErstellenEingabe VerbandErstellenEingabe() => new(
        VerbandName, VerbandKurzname, VerbandLogoDaten, VerbandRangliste?.Id, VerbandVereinsRangliste?.Id);

    private VerbandAktualisierenEingabe VerbandAktualisierenEingabe() => new(
        VerbandName, VerbandKurzname, VerbandLogoDaten, VerbandRangliste?.Id, VerbandVereinsRangliste?.Id);

    // Erstellen und Bearbeiten prüfen dieselben Regeln – die Live-Prüfung nimmt darum immer die Erstellen-Eingabe.
    private void AktualisiereVerbandFehler() =>
        VerbandFehler = VerbandFormularAktiv ? _verbandValidator.Validieren(VerbandErstellenEingabe()).Meldungen : [];

    private bool KannSpeichern() => VerbandFormularAktiv && VerbandIsDirty && VerbandFehler.Count == 0;

    private bool KannAbbrechen() => VerbandFormularAktiv && VerbandIsDirty;

    private bool KannLoeschen() => AusgewaehlterVerband is not null && !VerbandIstNeuModus;
}
