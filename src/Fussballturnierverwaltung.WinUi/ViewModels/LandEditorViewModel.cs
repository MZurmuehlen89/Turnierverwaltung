using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Diagnose;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Stammdaten.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.Helpers;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Reiter „Länder“ der Seite Länder &amp; Verbände (#606).</summary>
public partial class LandEditorViewModel : ObservableObject
{
    private readonly LandService _landService;
    private readonly LandValidator _landValidator;
    private readonly IDialogService _dialogService;
    private readonly ILogger<LandEditorViewModel> _logger;
    private Func<Task> _neuLaden = () => Task.CompletedTask;
    private int _populiertGerade;

    private static readonly VerbandFilterOption AllesVerband = new("— Alle —", null);
    private static readonly KontinentOption AllesKontinent = new("— Alle —", null);
    private static readonly RanglistenOption KeineRangliste = LaenderUndVerbaendeViewModel.KeineRangliste;

    private static readonly IReadOnlyList<KontinentOption> _kontinentFormularOptionen =
        Enum.GetValues<Kontinent>()
            .Select(k => new KontinentOption(KontinentAnzeigeName(k), k))
            .ToList();

    [ObservableProperty] private string? _fehlermeldung;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LaenderGefiltert))]
    private ObservableCollection<Land> _laender = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerbandFilterOptionen))]
    private IReadOnlyList<Verband> _verbaende = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LaenderGefiltert))]
    private string _landSuchbegriff = "";

    // Nullable, obwohl fachlich nie "leer": Bindet eine WinUI-ComboBox TwoWay an diese Property, setzt sie
    // SelectedItem beim Austausch der ItemsSource (VerbandFilterOptionen ändert sich mit Verbaende-Reload)
    // kurzzeitig auf null zurück, bevor sie den passenden Eintrag wiederfindet — die On...Changed-Hooks
    // unten fangen das ab und stellen sofort die "Alle"-Sentinel wieder her (Fund aus #281-Test).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LaenderGefiltert))]
    private VerbandFilterOption? _landFilterVerbandOption = AllesVerband;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LaenderGefiltert))]
    private KontinentOption? _landFilterKontinentOption = AllesKontinent;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private Land? _ausgewaehltesLand;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private bool _landFormularAktiv;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    private bool _landIsDirty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private bool _landIstNeuModus;

    [ObservableProperty] private string _landName = "";
    [ObservableProperty] private string _landKurzname = "";
    [ObservableProperty] private string _landFifaCode = "";
    [ObservableProperty] private KontinentOption _landKontinentOption = _kontinentFormularOptionen[0];
    [ObservableProperty] private Verband? _landVerband;
    [ObservableProperty] private byte[]? _landFlaggeDaten;
    [ObservableProperty] private RanglistenOption? _landRangliste = KeineRangliste;

    /// <summary>Live-Prüfung des Länderformulars (#602); sperrt Speichern, solange sie Fehler enthält.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    private IReadOnlyList<Feldfehler> _landFehler = [];

    // Ranglisten zur Auswahl (#572), „keine“ zuerst.
    [ObservableProperty] private IReadOnlyList<RanglistenOption> _ranglistenOptionen = [KeineRangliste];

    // ── Berechnete Listen ─────────────────────────────────────────────────────
    public IEnumerable<Land> LaenderGefiltert => Laender
        .Where(l => string.IsNullOrWhiteSpace(LandSuchbegriff)
            || l.Name.Contains(LandSuchbegriff, StringComparison.OrdinalIgnoreCase)
            || l.Kurzname.Contains(LandSuchbegriff, StringComparison.OrdinalIgnoreCase)
            || l.FifaCode.Contains(LandSuchbegriff, StringComparison.OrdinalIgnoreCase))
        .Where(l => LandFilterVerbandOption?.Wert == null || l.VerbandId == LandFilterVerbandOption.Wert.Id)
        .Where(l => LandFilterKontinentOption?.Wert == null || l.Kontinent == LandFilterKontinentOption.Wert);

    // Wird in OnPropertyChanged mit LaenderGefiltert nachgezogen (alle Quellen automatisch).
    public string LaenderAnzahlText => DatensatzAnzahl.Text(
        LaenderGefiltert.Count(), Laender.Count,
        !string.IsNullOrWhiteSpace(LandSuchbegriff) || LandFilterVerbandOption?.Wert != null
            || LandFilterKontinentOption?.Wert != null,
        "Land", "Länder");

    public IReadOnlyList<VerbandFilterOption> VerbandFilterOptionen =>
        [AllesVerband, .. Verbaende.Select(v => new VerbandFilterOption(v.Name, v))];

    public IReadOnlyList<KontinentOption> KontinentFilterOptionen =>
        [AllesKontinent, .. _kontinentFormularOptionen];

    public IReadOnlyList<KontinentOption> KontinentFormularOptionen => _kontinentFormularOptionen;

    public static string KontinentAnzeigeName(Kontinent k) => k switch
    {
        Kontinent.Europa               => "Europa",
        Kontinent.Suedamerika          => "Südamerika",
        Kontinent.NordUndMittelamerika => "Nord- & Mittelamerika",
        Kontinent.Afrika               => "Afrika",
        Kontinent.Asien                => "Asien",
        Kontinent.Ozeanien             => "Ozeanien",
        _                              => k.ToString()
    };

    private static readonly HashSet<string> _landFormFelder =
    [
        nameof(LandName), nameof(LandKurzname), nameof(LandFifaCode),
        nameof(LandKontinentOption), nameof(LandVerband), nameof(LandFlaggeDaten), nameof(LandRangliste)
    ];

    public LandEditorViewModel(
        LandService landService, LandValidator landValidator, IDialogService dialogService,
        ILogger<LandEditorViewModel> logger)
    {
        _landService = landService;
        _landValidator = landValidator;
        _dialogService = dialogService;
        _logger = logger;
    }

    /// <summary>Lädt nach dem Speichern oder Löschen die ganze Seite neu – Länder zeigen den Verband.</summary>
    public void NeuLadenUeber(Func<Task> neuLaden) => _neuLaden = neuLaden;

    public async Task LadenAsync(IReadOnlyList<Verband> verbaende, IReadOnlyList<RanglistenOption> ranglistenOptionen)
    {
        var laender = await _landService.AlleAbrufenMitVerbandAsync();
        RanglistenOptionen = ranglistenOptionen;
        Laender = new ObservableCollection<Land>(laender.OrderBy(l => l.Name));
        Verbaende = verbaende;
    }

    public void MeldungVerwerfen() => Fehlermeldung = null;

    public bool HatUngespeicherteAenderungen => LandFormularAktiv && LandIsDirty;

    // Null-Guards: siehe Kommentar an _landFilterVerbandOption (WinUI-ComboBox-ItemsSource-Reset).
    partial void OnLandFilterVerbandOptionChanged(VerbandFilterOption? value)
    {
        if (value is null) LandFilterVerbandOption = AllesVerband;
    }

    partial void OnLandFilterKontinentOptionChanged(KontinentOption? value)
    {
        if (value is null) LandFilterKontinentOption = AllesKontinent;
    }

    partial void OnAusgewaehltesLandChanged(Land? value) => LandFormularAusFuellenMit(value);

    private void LandFormularAusFuellenMit(Land? land)
    {
        _populiertGerade++;
        try
        {
            if (land is null)
            {
                LandFormularAktiv = false;
                LandIstNeuModus = false;
                LandName = LandKurzname = LandFifaCode = "";
                LandKontinentOption = _kontinentFormularOptionen[0];
                LandVerband = null;
                LandFlaggeDaten = null;
                LandRangliste = KeineRangliste;
            }
            else
            {
                LandFormularAktiv = true;
                LandIstNeuModus = false;
                LandName = land.Name;
                LandKurzname = land.Kurzname;
                LandFifaCode = land.FifaCode;
                LandKontinentOption = _kontinentFormularOptionen
                    .FirstOrDefault(k => k.Wert == land.Kontinent) ?? _kontinentFormularOptionen[0];
                LandVerband = Verbaende.FirstOrDefault(v => v.Id == land.VerbandId);
                LandFlaggeDaten = land.Flagge;
                LandRangliste = RanglisteOptionFuer(land.RanglisteId);
            }
            LandIsDirty = false;
            Fehlermeldung = null;
        }
        finally
        {
            _populiertGerade--;
        }
        AktualisiereLandFehler();
    }

    private RanglistenOption RanglisteOptionFuer(int? ranglisteId) =>
        RanglistenOptionen.FirstOrDefault(o => o.Id == ranglisteId) ?? KeineRangliste;

    // ── Ribbon-Befehle ────────────────────────────────────────────────────────

    [RelayCommand]
    private void Neu()
    {
        _populiertGerade++;
        try
        {
            AusgewaehltesLand = null;
            LandFormularAktiv = true;
            LandIstNeuModus = true;
            LandName = LandKurzname = LandFifaCode = "";
            LandKontinentOption = _kontinentFormularOptionen[0];
            LandVerband = Verbaende.FirstOrDefault();
            LandFlaggeDaten = null;
            LandRangliste = KeineRangliste;
            Fehlermeldung = null;
        }
        finally
        {
            _populiertGerade--;
        }
        LandIsDirty = true;
        AktualisiereLandFehler();
    }

    [RelayCommand]
    private async Task LandFlaggeWaehlenAsync()
    {
        var daten = await _dialogService.BilddateiWaehlenAsync();
        if (daten is not null) LandFlaggeDaten = daten;
    }

    [RelayCommand]
    private void LandFlaggeEntfernen() => LandFlaggeDaten = null;

    [RelayCommand(CanExecute = nameof(KannSpeichern))]
    private async Task Speichern()
    {
        // Als Vorgang, weil die Seite danach neu lädt: Ladeanzeige und „Bereit“ für GUI-Tests folgen nur offenen
        // Vorgängen (#597, Muster aus #535)
        using var vorgang = _logger.Vorgang($"Länder: speichern ({LandName})", "Land wird gespeichert …");
        await LandSpeichern();
    }

    private async Task LandSpeichern()
    {
        Fehlermeldung = null;
        try
        {
            if (LandIstNeuModus)
            {
                var neu = await _landService.ErstellenAsync(LandErstellenEingabe());
                await _neuLaden();
                AusgewaehltesLand = Laender.FirstOrDefault(l => l.Id == neu.Id);
            }
            else if (AusgewaehltesLand is not null)
            {
                var id = AusgewaehltesLand.Id;
                await _landService.AktualisierenAsync(id, LandAktualisierenEingabe());
                await _neuLaden();
                AusgewaehltesLand = Laender.FirstOrDefault(l => l.Id == id);
            }
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(KannAbbrechen))]
    private void Abbrechen() => LandFormularAusFuellenMit(LandIstNeuModus ? null : AusgewaehltesLand);

    [RelayCommand(CanExecute = nameof(KannLoeschen))]
    private async Task Loeschen()
    {
        if (AusgewaehltesLand is null) return;
        // Hängt noch etwas am Land, gleich den Grund zeigen statt erst nachzufragen (#559)
        if (await LoeschSperreMeldenAsync(() => _landService.LoeschSperreAsync(AusgewaehltesLand.Id))) return;
        var bestaetigt = await _dialogService.BestaetigenAsync(
            "Land löschen", $"Soll '{AusgewaehltesLand.Name}' wirklich gelöscht werden?");
        if (!bestaetigt) return;
        using var vorgang = _logger.Vorgang($"Länder: löschen ({AusgewaehltesLand.Name})", "Land wird gelöscht …");
        Fehlermeldung = null;
        try
        {
            await _landService.LoeschenAsync(AusgewaehltesLand.Id);
            await _neuLaden();
            AusgewaehltesLand = null;
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
    private void FilterZuruecksetzen()
    {
        LandSuchbegriff = "";
        LandFilterVerbandOption = AllesVerband;
        LandFilterKontinentOption = AllesKontinent;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(LaenderGefiltert)) OnPropertyChanged(nameof(LaenderAnzahlText));
        if (_populiertGerade != 0) return;
        if (_landFormFelder.Contains(e.PropertyName!))
        {
            LandIsDirty = true;
            AktualisiereLandFehler();
        }
    }

    // ── Live-Prüfung (#602) ───────────────────────────────────────────────────

    private LandErstellenEingabe LandErstellenEingabe() => new(
        LandName, LandKurzname, LandFifaCode, LandKontinentOption.Wert ?? Kontinent.Europa,
        LandVerband?.Id ?? 0, LandFlaggeDaten, LandRangliste?.Id);

    private LandAktualisierenEingabe LandAktualisierenEingabe() => new(
        LandName, LandKurzname, LandFifaCode, LandKontinentOption.Wert ?? Kontinent.Europa,
        LandVerband?.Id ?? 0, LandFlaggeDaten, LandRangliste?.Id);

    // Erstellen und Bearbeiten prüfen dieselben Regeln – die Live-Prüfung nimmt darum immer die Erstellen-Eingabe.
    private void AktualisiereLandFehler() =>
        LandFehler = LandFormularAktiv ? _landValidator.Validieren(LandErstellenEingabe()).Meldungen : [];

    private bool KannSpeichern() => LandFormularAktiv && LandIsDirty && LandFehler.Count == 0;

    private bool KannAbbrechen() => LandFormularAktiv && LandIsDirty;

    private bool KannLoeschen() => AusgewaehltesLand is not null && !LandIstNeuModus;
}
