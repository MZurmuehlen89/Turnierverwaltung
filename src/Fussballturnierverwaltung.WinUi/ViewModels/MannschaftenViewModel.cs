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
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

// Wrapper-Records vermeiden, dass eine "Alle"-Option als null-Item in der ItemsSource landet.
// SelectedItem ist immer ein nicht-null-Objekt; Wert==null bedeutet "Alle".
public record LandFilterOption(string Anzeige, Land? Wert);
public record VerbandFilterOption(string Anzeige, Verband? Wert);
public record ArtFilterOption(string Anzeige, Mannschaftsart? Wert);

public partial class MannschaftenViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly MannschaftService _mannschaftService;
    private readonly LandService _landService;
    private readonly MannschaftValidator _validator;
    private readonly IDialogService _dialogService;
    private readonly ILogger<MannschaftenViewModel> _logger;
    private int _populiertGerade;

    private static readonly LandFilterOption AllesLand = new("— Alle —", null);
    private static readonly VerbandFilterOption AllesVerband = new("— Alle —", null);
    private static readonly ArtFilterOption AllesArt = new("— Alle —", null);

    // ── Liste ────────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MannschaftenGefiltert))]
    private ObservableCollection<Mannschaft> _mannschaften = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MannschaftenGefiltert))]
    private string _suchbegriff = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private Mannschaft? _ausgewaehlte;

    // ── Formular-Zustand ─────────────────────────────────────────────────────
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

    [ObservableProperty] private bool _istLadend;
    [ObservableProperty] private string? _fehlermeldung;

    /// <summary>Live-Prüfung des Formulars mit demselben Validator wie beim Speichern (#523). Blockiert Speichern,
    /// solange sie Fehler enthält.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    private IReadOnlyList<Feldfehler> _formularFehler = [];

    // ── Formular-Felder ──────────────────────────────────────────────────────
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _kurzname = "";
    [ObservableProperty] private Mannschaftsart _ausgewaehlteArt = Mannschaftsart.Verein;
    [ObservableProperty] private Land? _ausgewaehltesLand;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GesamtRating))]
    private int _offensivRating = 70;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GesamtRating))]
    private int _mittelfeldRating = 70;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GesamtRating))]
    private int _defensivRating = 70;

    [ObservableProperty] private int _heimstaerke = 70;
    [ObservableProperty] private int _auswaertsstaerke = 65;
    [ObservableProperty] private byte[]? _wappenDaten;

    // ── Filter ───────────────────────────────────────────────────────────────
    // Nullable, obwohl fachlich nie "leer": Bindet ein WinUI-ComboBox TwoWay an diese Property,
    // setzt sie SelectedItem beim Austausch der ItemsSource (Laender/Verbaende-Reload) kurzzeitig
    // auf null zurück, bevor sie den passenden Eintrag wiederfindet — die On...Changed-Hooks unten
    // fangen das ab und stellen sofort die "Alle"-Sentinel wieder her.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MannschaftenGefiltert))]
    [NotifyPropertyChangedFor(nameof(VerbandFilterOptionen))]
    private LandFilterOption? _filterLandOption = AllesLand;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MannschaftenGefiltert))]
    private VerbandFilterOption? _filterVerbandOption = AllesVerband;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MannschaftenGefiltert))]
    private ArtFilterOption? _filterArtOption = AllesArt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MannschaftenGefiltert))]
    private int _filterMinRating;

    // ── Dropdown-Daten ────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LandFilterOptionen))]
    [NotifyPropertyChangedFor(nameof(VerbandFilterOptionen))]
    private ObservableCollection<Land> _laender = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerbandFilterOptionen))]
    private ObservableCollection<Verband> _verbaende = [];

    // ── Berechnete Eigenschaften ──────────────────────────────────────────────
    public decimal GesamtRating => Math.Round((OffensivRating + MittelfeldRating + DefensivRating) / 3m, 1);

    public IReadOnlyList<LandFilterOption> LandFilterOptionen =>
        [AllesLand, ..Laender.Select(l => new LandFilterOption(l.Name, l))];

    public IReadOnlyList<VerbandFilterOption> VerbandFilterOptionen
    {
        get
        {
            if (FilterLandOption?.Wert != null)
            {
                var verband = Verbaende.FirstOrDefault(v => v.Id == FilterLandOption.Wert.VerbandId);
                return verband != null
                    ? [AllesVerband, new VerbandFilterOption(verband.Name, verband)]
                    : [AllesVerband];
            }
            return [AllesVerband, ..Verbaende.Select(v => new VerbandFilterOption(v.Name, v))];
        }
    }

    public IReadOnlyList<ArtFilterOption> ArtFilterOptionen =>
        [AllesArt, ..Enum.GetValues<Mannschaftsart>().Select(a => new ArtFilterOption(a.ToString(), a))];

    public IEnumerable<Mannschaft> MannschaftenGefiltert => Mannschaften
        .Where(m => string.IsNullOrWhiteSpace(Suchbegriff)
            || m.Name.Contains(Suchbegriff, StringComparison.OrdinalIgnoreCase)
            || m.Kurzname.Contains(Suchbegriff, StringComparison.OrdinalIgnoreCase)
            || (m.Land?.Name.Contains(Suchbegriff, StringComparison.OrdinalIgnoreCase) ?? false))
        .Where(m => FilterLandOption?.Wert == null || m.LandId == FilterLandOption.Wert.Id)
        .Where(m => FilterVerbandOption?.Wert == null || m.Land?.VerbandId == FilterVerbandOption.Wert.Id)
        .Where(m => FilterArtOption?.Wert == null || m.Mannschaftsart == FilterArtOption.Wert)
        .Where(m => FilterMinRating == 0 || m.GesamtRating >= FilterMinRating);

    // Wird in OnPropertyChanged mit MannschaftenGefiltert nachgezogen — damit gelten automatisch alle Quellen
    // der gefilterten Liste (Filter, Suche, Neu-Laden nach Anlegen/Löschen), ohne sie einzeln aufzuzählen.
    public string AnzahlText => DatensatzAnzahl.Text(
        MannschaftenGefiltert.Count(), Mannschaften.Count,
        !string.IsNullOrWhiteSpace(Suchbegriff) || FilterLandOption?.Wert != null || FilterVerbandOption?.Wert != null
            || FilterArtOption?.Wert != null || FilterMinRating > 0,
        "Mannschaft", "Mannschaften");

    public IEnumerable<Mannschaftsart> ArtOptionen => Enum.GetValues<Mannschaftsart>();

    // ── Formularfelder die IsDirty auslösen ───────────────────────────────────
    private static readonly HashSet<string> _formFelder =
    [
        nameof(Name), nameof(Kurzname), nameof(AusgewaehlteArt), nameof(AusgewaehltesLand),
        nameof(OffensivRating), nameof(MittelfeldRating), nameof(DefensivRating),
        nameof(Heimstaerke), nameof(Auswaertsstaerke), nameof(WappenDaten)
    ];

    // ── IRibbonAware ──────────────────────────────────────────────────────────
    public string RibbonTabTitel => "Mannschaften";
    public ICommand NeuBefehl => NeuCommand;
    public ICommand SpeichernBefehl => SpeichernCommand;
    public ICommand AbbrechenBefehl => AbbrechenCommand;
    public ICommand LoeschenBefehl => LoeschenCommand;

    public MannschaftenViewModel(
        MannschaftService mannschaftService, LandService landService, MannschaftValidator validator,
        IDialogService dialogService, ILogger<MannschaftenViewModel> logger)
    {
        _mannschaftService = mannschaftService;
        _landService = landService;
        _validator = validator;
        _dialogService = dialogService;
        _logger = logger;
    }

    public override async Task AktivierenAsync() => await LadenAsync();

    private async Task LadenAsync()
    {
        IstLadend = true;
        try
        {
            var mannschaften = await _mannschaftService.AlleAbrufenMitLandAsync();
            var laender = await _landService.AlleAbrufenMitVerbandAsync();

            Mannschaften = new ObservableCollection<Mannschaft>(mannschaften.OrderBy(m => m.Name));
            Laender = new ObservableCollection<Land>(laender.OrderBy(l => l.Name));
            Verbaende = new ObservableCollection<Verband>(
                laender.Select(l => l.Verband)
                       .DistinctBy(v => v.Id)
                       .OrderBy(v => v.Name));
        }
        finally
        {
            IstLadend = false;
        }
    }

    // Wenn Land-Filter ändert: Verband-Filter zurücksetzen, da sich Verband-Liste ändert.
    // Null-Guard: siehe Kommentar an _filterLandOption (WinUI-ComboBox-ItemsSource-Reset).
    partial void OnFilterLandOptionChanged(LandFilterOption? value)
    {
        if (value is null) { FilterLandOption = AllesLand; return; }
        FilterVerbandOption = AllesVerband;
    }

    partial void OnFilterVerbandOptionChanged(VerbandFilterOption? value)
    {
        if (value is null) FilterVerbandOption = AllesVerband;
    }

    partial void OnFilterArtOptionChanged(ArtFilterOption? value)
    {
        if (value is null) FilterArtOption = AllesArt;
    }

    partial void OnAusgewaehlteChanged(Mannschaft? value) => FormularAusFuellenMit(value);

    private void FormularAusFuellenMit(Mannschaft? mannschaft)
    {
        _populiertGerade++;
        try
        {
            if (mannschaft is null)
            {
                FormularAktiv = false;
                IstNeuModus = false;
                Name = Kurzname = "";
                AusgewaehltesLand = null;
                OffensivRating = MittelfeldRating = DefensivRating = 70;
                Heimstaerke = 70;
                Auswaertsstaerke = 65;
                WappenDaten = null;
            }
            else
            {
                FormularAktiv = true;
                IstNeuModus = false;
                Name = mannschaft.Name;
                Kurzname = mannschaft.Kurzname;
                AusgewaehlteArt = mannschaft.Mannschaftsart;
                AusgewaehltesLand = Laender.FirstOrDefault(l => l.Id == mannschaft.LandId);
                OffensivRating = mannschaft.OffensivRating;
                MittelfeldRating = mannschaft.MittelfeldRating;
                DefensivRating = mannschaft.DefensivRating;
                Heimstaerke = mannschaft.Heimstaerke;
                Auswaertsstaerke = mannschaft.Auswaertsstaerke;
                WappenDaten = mannschaft.Wappen;
            }
            IsDirty = false;
            Fehlermeldung = null;
        }
        finally
        {
            _populiertGerade--;
        }
        AktualisiereFormularFehler();
    }

    // ── Ribbon-Befehle ────────────────────────────────────────────────────────

    [RelayCommand]
    private void Neu()
    {
        _populiertGerade++;
        try
        {
            Ausgewaehlte = null;
            FormularAktiv = true;
            IstNeuModus = true;
            Name = Kurzname = "";
            AusgewaehlteArt = Mannschaftsart.Verein;
            AusgewaehltesLand = Laender.FirstOrDefault();
            OffensivRating = MittelfeldRating = DefensivRating = 70;
            Heimstaerke = 70;
            Auswaertsstaerke = 65;
            WappenDaten = null;
            Fehlermeldung = null;
        }
        finally
        {
            _populiertGerade--;
        }
        IsDirty = true;
        AktualisiereFormularFehler();
    }

    [RelayCommand]
    private async Task WappenWaehlenAsync()
    {
        var daten = await _dialogService.BilddateiWaehlenAsync();
        if (daten is not null) WappenDaten = daten;
    }

    [RelayCommand]
    private void WappenEntfernen() => WappenDaten = null;

    [RelayCommand(CanExecute = nameof(KannSpeichern))]
    private async Task Speichern()
    {
        // Als Vorgang, weil die Seite danach neu lädt: Ladeanzeige und „Bereit“ für GUI-Tests folgen nur offenen
        // Vorgängen (#597, Muster aus #535)
        using var vorgang = _logger.Vorgang($"Mannschaften: speichern ({Name})", "Mannschaft wird gespeichert …");
        Fehlermeldung = null;
        try
        {
            if (IstNeuModus)
            {
                var neu = await _mannschaftService.ErstellenAsync(ErstellenEingabe());
                await LadenAsync();
                Ausgewaehlte = Mannschaften.FirstOrDefault(m => m.Id == neu.Id);
            }
            else if (Ausgewaehlte is not null)
            {
                var id = Ausgewaehlte.Id;
                await _mannschaftService.AktualisierenAsync(id, AktualisierenEingabe());
                await LadenAsync();
                Ausgewaehlte = Mannschaften.FirstOrDefault(m => m.Id == id);
            }
            IsDirty = false;
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(KannAbbrechen))]
    private void Abbrechen() => FormularAusFuellenMit(IstNeuModus ? null : Ausgewaehlte);

    [RelayCommand(CanExecute = nameof(KannLoeschen))]
    private async Task Loeschen()
    {
        if (Ausgewaehlte is null) return;

        var bestaetigt = await _dialogService.BestaetigenAsync(
            "Mannschaft löschen",
            $"Soll '{Ausgewaehlte.Name}' wirklich gelöscht werden?");
        if (!bestaetigt) return;

        using var vorgang = _logger.Vorgang($"Mannschaften: löschen ({Ausgewaehlte.Name})", "Mannschaft wird gelöscht …");
        Fehlermeldung = null;
        try
        {
            var id = Ausgewaehlte.Id;
            await _mannschaftService.LoeschenAsync(id);
            await LadenAsync();
            Ausgewaehlte = null;
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
        FilterLandOption = AllesLand;
        // FilterVerbandOption wird automatisch durch OnFilterLandOptionChanged zurückgesetzt
        FilterArtOption = AllesArt;
        FilterMinRating = 0;
    }

    // ── Validierung ───────────────────────────────────────────────────────────

    private MannschaftErstellenEingabe ErstellenEingabe() => new(
        Name, Kurzname, AusgewaehlteArt,
        AusgewaehltesLand?.Id ?? 0,
        OffensivRating, MittelfeldRating, DefensivRating,
        Heimstaerke, Auswaertsstaerke, WappenDaten);

    private MannschaftAktualisierenEingabe AktualisierenEingabe() => new(
        Name, Kurzname,
        AusgewaehltesLand?.Id ?? 0,
        OffensivRating, MittelfeldRating, DefensivRating,
        Heimstaerke, Auswaertsstaerke, WappenDaten);

    private void AktualisiereFormularFehler()
    {
        if (!FormularAktiv) { FormularFehler = []; return; }

        var ergebnis = IstNeuModus
            ? _validator.Validieren(ErstellenEingabe())
            : _validator.Validieren(AktualisierenEingabe());
        FormularFehler = ergebnis.Meldungen;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(MannschaftenGefiltert)) OnPropertyChanged(nameof(AnzahlText));
        if (_populiertGerade == 0 && _formFelder.Contains(e.PropertyName!))
        {
            IsDirty = true;
            AktualisiereFormularFehler();
        }
    }

    private bool KannSpeichern() => FormularAktiv && IsDirty && FormularFehler.Count == 0;
    private bool KannAbbrechen() => FormularAktiv && IsDirty;

    public bool HatUngespeicherteAenderungen => FormularAktiv && IsDirty;
    private bool KannLoeschen() => Ausgewaehlte is not null && !IstNeuModus;
}
