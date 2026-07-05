using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Stammdaten.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Wpf.Navigation;
using Fussballturnierverwaltung.Wpf.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

// Wrapper-Records vermeiden das WPF-ComboBox-Problem mit null-Items in ItemsSource.
// SelectedItem ist immer ein nicht-null-Objekt; Wert==null bedeutet "Alle".
public record LandFilterOption(string Anzeige, Land? Wert);
public record VerbandFilterOption(string Anzeige, Verband? Wert);
public record ArtFilterOption(string Anzeige, Mannschaftsart? Wert);

public partial class MannschaftenViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly MannschaftService _mannschaftService;
    private readonly LandService _landService;
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

    // ── Filter ───────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MannschaftenGefiltert))]
    [NotifyPropertyChangedFor(nameof(VerbandFilterOptionen))]
    private LandFilterOption _filterLandOption = AllesLand;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MannschaftenGefiltert))]
    private VerbandFilterOption _filterVerbandOption = AllesVerband;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MannschaftenGefiltert))]
    private ArtFilterOption _filterArtOption = AllesArt;

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
            if (FilterLandOption.Wert != null)
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
        .Where(m => FilterLandOption.Wert == null || m.LandId == FilterLandOption.Wert.Id)
        .Where(m => FilterVerbandOption.Wert == null || m.Land?.VerbandId == FilterVerbandOption.Wert.Id)
        .Where(m => FilterArtOption.Wert == null || m.Mannschaftsart == FilterArtOption.Wert)
        .Where(m => FilterMinRating == 0 || m.GesamtRating >= FilterMinRating);

    public IEnumerable<Mannschaftsart> ArtOptionen => Enum.GetValues<Mannschaftsart>();

    // ── Formularfelder die IsDirty auslösen ───────────────────────────────────
    private static readonly HashSet<string> _formFelder =
    [
        nameof(Name), nameof(Kurzname), nameof(AusgewaehlteArt), nameof(AusgewaehltesLand),
        nameof(OffensivRating), nameof(MittelfeldRating), nameof(DefensivRating),
        nameof(Heimstaerke), nameof(Auswaertsstaerke)
    ];

    // ── IRibbonAware ──────────────────────────────────────────────────────────
    public string RibbonTabTitel => "Mannschaften";
    public ICommand NeuBefehl => NeuCommand;
    public ICommand SpeichernBefehl => SpeichernCommand;
    public ICommand AbbrechenBefehl => AbbrechenCommand;
    public ICommand LoeschenBefehl => LoeschenCommand;

    public MannschaftenViewModel(MannschaftService mannschaftService, LandService landService)
    {
        _mannschaftService = mannschaftService;
        _landService = landService;
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

    // Wenn Land-Filter ändert: Verband-Filter zurücksetzen, da sich Verband-Liste ändert
    partial void OnFilterLandOptionChanged(LandFilterOption value)
    {
        FilterVerbandOption = AllesVerband;
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
            }
            IsDirty = false;
            Fehlermeldung = null;
        }
        finally
        {
            _populiertGerade--;
        }
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
            Fehlermeldung = null;
        }
        finally
        {
            _populiertGerade--;
        }
        IsDirty = true;
    }

    [RelayCommand(CanExecute = nameof(KannSpeichern))]
    private async Task Speichern()
    {
        Fehlermeldung = null;
        try
        {
            if (IstNeuModus)
            {
                var eingabe = new MannschaftErstellenEingabe(
                    Name, Kurzname, AusgewaehlteArt,
                    AusgewaehltesLand?.Id ?? 0,
                    OffensivRating, MittelfeldRating, DefensivRating,
                    Heimstaerke, Auswaertsstaerke);
                var neu = await _mannschaftService.ErstellenAsync(eingabe);
                await LadenAsync();
                Ausgewaehlte = Mannschaften.FirstOrDefault(m => m.Id == neu.Id);
            }
            else if (Ausgewaehlte is not null)
            {
                var id = Ausgewaehlte.Id;
                var eingabe = new MannschaftAktualisierenEingabe(
                    Name, Kurzname,
                    AusgewaehltesLand?.Id ?? 0,
                    OffensivRating, MittelfeldRating, DefensivRating,
                    Heimstaerke, Auswaertsstaerke);
                await _mannschaftService.AktualisierenAsync(id, eingabe);
                await LadenAsync();
                Ausgewaehlte = Mannschaften.FirstOrDefault(m => m.Id == id);
            }
            // LadenAsync ersetzt Laender durch neue Instanzen; das Land-ComboBox-Binding
            // schreibt AusgewaehltesLand deferred (DataBind-Priorität) auf null zurück und
            // triggert dadurch IsDirty=true. Background-Priorität läuft nach allen Bindings.
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                () => IsDirty = false,
                System.Windows.Threading.DispatcherPriority.Background);
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

        var ergebnis = MessageBox.Show(
            $"Soll '{Ausgewaehlte.Name}' wirklich gelöscht werden?",
            "Mannschaft löschen",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ergebnis != MessageBoxResult.Yes) return;

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

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_populiertGerade == 0 && _formFelder.Contains(e.PropertyName!))
            IsDirty = true;
    }

    private bool KannSpeichern() => FormularAktiv && IsDirty;
    private bool KannAbbrechen() => FormularAktiv && IsDirty;

    public bool HatUngespeicherteAenderungen => FormularAktiv && IsDirty;
    private bool KannLoeschen() => Ausgewaehlte is not null && !IstNeuModus;
}
