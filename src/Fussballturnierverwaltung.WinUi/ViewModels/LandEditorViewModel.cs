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

public record KontinentOption(string Anzeige, Kontinent? Wert);

/// <summary>Reiter „Länder“ der Seite Länder &amp; Verbände (#606).</summary>
public partial class LandEditorViewModel : ObservableObject
{
    private readonly LandService _landService;
    private readonly LandValidator _landValidator;
    private readonly IDialogService _dialogService;
    private readonly ILogger<LandEditorViewModel> _logger;
    private Func<Task>? _neuLaden;
    private int _populiertGerade;

    private static readonly VerbandFilterOption AllesVerband = new("— Alle —", null);
    private static readonly KontinentOption AllesKontinent = new("— Alle —", null);
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
    private string _suchbegriff = "";

    // Nullable, obwohl fachlich nie "leer": Bindet eine WinUI-ComboBox TwoWay an diese Property, setzt sie
    // SelectedItem beim Austausch der ItemsSource (VerbandFilterOptionen ändert sich mit Verbaende-Reload)
    // kurzzeitig auf null zurück, bevor sie den passenden Eintrag wiederfindet — die On...Changed-Hooks
    // unten fangen das ab und stellen sofort die "Alle"-Sentinel wieder her (Fund aus #281-Test).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LaenderGefiltert))]
    private VerbandFilterOption? _filterVerbandOption = AllesVerband;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LaenderGefiltert))]
    private KontinentOption? _filterKontinentOption = AllesKontinent;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private Land? _ausgewaehltesLand;

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
    [ObservableProperty] private string _fifaCode = "";
    [ObservableProperty] private KontinentOption _kontinentAuswahl = _kontinentFormularOptionen[0];
    [ObservableProperty] private Verband? _verband;
    [ObservableProperty] private byte[]? _flaggeDaten;
    [ObservableProperty] private RanglistenOption? _rangliste = RanglistenOption.Keine;

    /// <summary>Live-Prüfung des Länderformulars (#602); sperrt Speichern, solange sie Fehler enthält.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    private IReadOnlyList<Feldfehler> _fehler = [];

    // Ranglisten zur Auswahl (#572), „keine“ zuerst.
    [ObservableProperty] private IReadOnlyList<RanglistenOption> _ranglistenOptionen = [RanglistenOption.Keine];

    // ── Berechnete Listen ─────────────────────────────────────────────────────
    public IEnumerable<Land> LaenderGefiltert => Laender
        .Where(l => string.IsNullOrWhiteSpace(Suchbegriff)
            || l.Name.Contains(Suchbegriff, StringComparison.OrdinalIgnoreCase)
            || l.Kurzname.Contains(Suchbegriff, StringComparison.OrdinalIgnoreCase)
            || l.FifaCode.Contains(Suchbegriff, StringComparison.OrdinalIgnoreCase))
        .Where(l => FilterVerbandOption?.Wert == null || l.VerbandId == FilterVerbandOption.Wert.Id)
        .Where(l => FilterKontinentOption?.Wert == null || l.Kontinent == FilterKontinentOption.Wert);

    // Wird in OnPropertyChanged mit LaenderGefiltert nachgezogen (alle Quellen automatisch).
    public string AnzahlText => DatensatzAnzahl.Text(
        LaenderGefiltert.Count(), Laender.Count,
        !string.IsNullOrWhiteSpace(Suchbegriff) || FilterVerbandOption?.Wert != null
            || FilterKontinentOption?.Wert != null,
        "Land", "Länder");

    public IReadOnlyList<VerbandFilterOption> VerbandFilterOptionen =>
        [AllesVerband, .. Verbaende.Select(v => new VerbandFilterOption(v.Name, v))];

    public IReadOnlyList<KontinentOption> KontinentFilterOptionen =>
        [AllesKontinent, .. _kontinentFormularOptionen];

    public IReadOnlyList<KontinentOption> KontinentFormularOptionen => _kontinentFormularOptionen;

    private static string KontinentAnzeigeName(Kontinent k) => k switch
    {
        Kontinent.Europa               => "Europa",
        Kontinent.Suedamerika          => "Südamerika",
        Kontinent.NordUndMittelamerika => "Nord- & Mittelamerika",
        Kontinent.Afrika               => "Afrika",
        Kontinent.Asien                => "Asien",
        Kontinent.Ozeanien             => "Ozeanien",
        _                              => k.ToString()
    };

    private static readonly HashSet<string> _formFelder =
    [
        nameof(Name), nameof(Kurzname), nameof(FifaCode),
        nameof(KontinentAuswahl), nameof(Verband), nameof(FlaggeDaten), nameof(Rangliste)
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

    // Ohne Rahmen bliebe die Liste nach dem Speichern still veraltet – darum laut scheitern statt nichts tun.
    private Task NeuLadenAsync() =>
        (_neuLaden ?? throw new InvalidOperationException("Neuladen nicht festgelegt (NeuLadenUeber).")).Invoke();

    public async Task LadenAsync(IReadOnlyList<Verband> verbaende, IReadOnlyList<RanglistenOption> ranglistenOptionen)
    {
        var laender = await _landService.AlleAbrufenMitVerbandAsync();
        RanglistenOptionen = ranglistenOptionen;
        Laender = new ObservableCollection<Land>(laender.OrderBy(l => l.Name));
        Verbaende = verbaende;
    }

    public void MeldungVerwerfen() => Fehlermeldung = null;

    public bool HatUngespeicherteAenderungen => FormularAktiv && IsDirty;

    // Null-Guards: siehe Kommentar an _filterVerbandOption (WinUI-ComboBox-ItemsSource-Reset).
    partial void OnFilterVerbandOptionChanged(VerbandFilterOption? value)
    {
        if (value is null) FilterVerbandOption = AllesVerband;
    }

    partial void OnFilterKontinentOptionChanged(KontinentOption? value)
    {
        if (value is null) FilterKontinentOption = AllesKontinent;
    }

    partial void OnAusgewaehltesLandChanged(Land? value) => FormularAusFuellenMit(value);

    private void FormularAusFuellenMit(Land? land)
    {
        _populiertGerade++;
        try
        {
            if (land is null)
            {
                FormularAktiv = false;
                IstNeuModus = false;
                Name = Kurzname = FifaCode = "";
                KontinentAuswahl = _kontinentFormularOptionen[0];
                Verband = null;
                FlaggeDaten = null;
                Rangliste = RanglistenOption.Keine;
            }
            else
            {
                FormularAktiv = true;
                IstNeuModus = false;
                Name = land.Name;
                Kurzname = land.Kurzname;
                FifaCode = land.FifaCode;
                KontinentAuswahl = _kontinentFormularOptionen
                    .FirstOrDefault(k => k.Wert == land.Kontinent) ?? _kontinentFormularOptionen[0];
                Verband = Verbaende.FirstOrDefault(v => v.Id == land.VerbandId);
                FlaggeDaten = land.Flagge;
                Rangliste = RanglistenOption.Fuer(RanglistenOptionen, land.RanglisteId);
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
            AusgewaehltesLand = null;
            FormularAktiv = true;
            IstNeuModus = true;
            Name = Kurzname = FifaCode = "";
            KontinentAuswahl = _kontinentFormularOptionen[0];
            Verband = Verbaende.FirstOrDefault();
            FlaggeDaten = null;
            Rangliste = RanglistenOption.Keine;
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
    private async Task FlaggeWaehlenAsync()
    {
        var daten = await _dialogService.BilddateiWaehlenAsync();
        if (daten is not null) FlaggeDaten = daten;
    }

    [RelayCommand]
    private void FlaggeEntfernen() => FlaggeDaten = null;

    [RelayCommand(CanExecute = nameof(KannSpeichern))]
    private async Task Speichern()
    {
        // Als Vorgang, weil die Seite danach neu lädt: Ladeanzeige und „Bereit“ für GUI-Tests folgen nur offenen
        // Vorgängen (#597, Muster aus #535)
        using var vorgang = _logger.Vorgang($"Länder: speichern ({Name})", "Land wird gespeichert …");
        Fehlermeldung = null;
        try
        {
            if (IstNeuModus)
            {
                var neu = await _landService.ErstellenAsync(ErstellenEingabe());
                await NeuLadenAsync();
                AusgewaehltesLand = Laender.FirstOrDefault(l => l.Id == neu.Id);
            }
            else if (AusgewaehltesLand is not null)
            {
                var id = AusgewaehltesLand.Id;
                await _landService.AktualisierenAsync(id, AktualisierenEingabe());
                await NeuLadenAsync();
                AusgewaehltesLand = Laender.FirstOrDefault(l => l.Id == id);
            }
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(KannAbbrechen))]
    private void Abbrechen() => FormularAusFuellenMit(IstNeuModus ? null : AusgewaehltesLand);

    [RelayCommand(CanExecute = nameof(KannLoeschen))]
    private async Task Loeschen()
    {
        if (AusgewaehltesLand is null) return;
        // Hängt noch etwas am Land, gleich den Grund zeigen statt erst nachzufragen (#559)
        Fehlermeldung = await LoeschPruefung.SperreErmittelnAsync(_logger, "Länder", () => _landService.LoeschSperreAsync(AusgewaehltesLand.Id));
        if (Fehlermeldung is not null) return;
        var bestaetigt = await _dialogService.BestaetigenAsync(
            "Land löschen", $"Soll '{AusgewaehltesLand.Name}' wirklich gelöscht werden?");
        if (!bestaetigt) return;
        using var vorgang = _logger.Vorgang($"Länder: löschen ({AusgewaehltesLand.Name})", "Land wird gelöscht …");
        Fehlermeldung = null;
        try
        {
            await _landService.LoeschenAsync(AusgewaehltesLand.Id);
            await NeuLadenAsync();
            AusgewaehltesLand = null;
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand]
    private void FilterZuruecksetzen()
    {
        Suchbegriff = "";
        FilterVerbandOption = AllesVerband;
        FilterKontinentOption = AllesKontinent;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(LaenderGefiltert)) OnPropertyChanged(nameof(AnzahlText));
        if (_populiertGerade != 0) return;
        if (_formFelder.Contains(e.PropertyName!))
        {
            IsDirty = true;
            AktualisiereFehler();
        }
    }

    // ── Live-Prüfung (#602) ───────────────────────────────────────────────────

    private LandErstellenEingabe ErstellenEingabe() => new(
        Name, Kurzname, FifaCode, KontinentAuswahl.Wert ?? Kontinent.Europa,
        Verband?.Id ?? 0, FlaggeDaten, Rangliste?.Id);

    private LandAktualisierenEingabe AktualisierenEingabe() => new(
        Name, Kurzname, FifaCode, KontinentAuswahl.Wert ?? Kontinent.Europa,
        Verband?.Id ?? 0, FlaggeDaten, Rangliste?.Id);

    // Erstellen und Bearbeiten prüfen dieselben Regeln – die Live-Prüfung nimmt darum immer die Erstellen-Eingabe.
    private void AktualisiereFehler() =>
        Fehler = FormularAktiv ? _landValidator.Validieren(ErstellenEingabe()).Meldungen : [];

    private bool KannSpeichern() => FormularAktiv && IsDirty && Fehler.Count == 0;

    private bool KannAbbrechen() => FormularAktiv && IsDirty;

    private bool KannLoeschen() => AusgewaehltesLand is not null && !IstNeuModus;
}
