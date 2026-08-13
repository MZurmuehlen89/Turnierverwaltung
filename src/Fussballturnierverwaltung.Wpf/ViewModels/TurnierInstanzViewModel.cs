using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Wpf.Converters;
using Fussballturnierverwaltung.Wpf.Navigation;
using Fussballturnierverwaltung.Wpf.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

public record TurnierStatusOption(string Anzeige, TurnierStatus? Wert);
public record TurnierVorlageOption(string Anzeige, TurnierVorlage? Wert);
public record RegelwerkOption(string Anzeige, Regelwerk? Wert);
public record VerbandOption(string Anzeige, Verband? Wert);
public record TeamfilterOption(string Anzeige, MannschaftsartFilter Wert);

public partial class RundenEintrag : ObservableObject
{
    // 0 = noch nicht gespeichert (neue Runde). Nur beim Laden aus der DB gesetzt (siehe RundeZuEintrag),
    // steuert u.a. ob im Runden-Editor Übergangsregeln für diese Runde bearbeitet werden können (Issue #169).
    public int Id { get; set; }

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Anzeige))] private string _name = RundenartZuTextConverter.RundenartAnzeigeName(Rundenart.Liga);
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Anzeige))] private Rundenart _rundenart = Rundenart.Liga;

    partial void OnRundenartChanged(Rundenart oldValue, Rundenart newValue)
    {
        if (Name == RundenartZuTextConverter.RundenartAnzeigeName(oldValue))
            Name = RundenartZuTextConverter.RundenartAnzeigeName(newValue);
    }
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Anzeige))] private int _eingangsMannschaften = 4;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Anzeige))] private int _ausgangsMannschaften = 4;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Anzeige))] private int? _gruppenAnzahl;

    // Snapshot der beim Laden aus der DB gültigen Werte — Vergleichsbasis, um zu erkennen ob eine bereits
    // gespeicherte Runde ihre Form seither geändert hat (Issue #181: Warnung wenn diese Runde bereits als
    // Quellrunde für Übergangsregeln dient).
    public int UrspruenglichEingangsMannschaften { get; set; }
    public int UrspruenglichAusgangsMannschaften { get; set; }
    public int? UrspruenglichGruppenAnzahl { get; set; }
    [ObservableProperty] private int _regelwerkId;
    [ObservableProperty] private string _regelwerkName = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Anzeige))] private bool _mitRueckspiel;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Anzeige))] private bool _auswaertstorRegelAktiv;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Anzeige))] private bool _eingangUeberUebergangsregeln;

    public string Anzeige =>
        $"{Name} · {RundenartZuTextConverter.RundenartAnzeigeName(Rundenart)} · " +
        $"{EingangsMannschaften}→{AusgangsMannschaften}" +
        (Rundenart == Rundenart.Gruppenphase && GruppenAnzahl.HasValue ? $" · {GruppenAnzahl}G" : "") +
        (MitRueckspiel ? " · H+R" : "") +
        (AuswaertstorRegelAktiv ? " · Auswärtstor" : "") +
        (EingangUeberUebergangsregeln ? " · Eingang über Übergangsregeln" : "");
}

public partial class TurnierInstanzViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly TurnierInstanzService _service;
    private readonly VerbandService _verbandService;
    private readonly RegelwerkService _regelwerkService;
    private int _populiertGerade;

    private static readonly TurnierStatusOption _alleStatus   = new("— Alle —", null);
    private static readonly VerbandOption _keinVerband        = new("— Kein Filter —", null);
    private static readonly RegelwerkOption _keinRegelwerk    = new("— Kein Standard —", null);

    // ── Liste ──────────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstanzenGefiltert))]
    private ObservableCollection<TurnierInstanz> _instanzen = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstanzenGefiltert))]
    private string _suchbegriff = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstanzenGefiltert))]
    private TurnierStatusOption _statusFilter = _alleStatus;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    [NotifyPropertyChangedFor(nameof(RundenSichtbar))]
    [NotifyPropertyChangedFor(nameof(StatusAnzeige))]
    private TurnierInstanz? _ausgewaehltes;

    // ── Formular-Zustand ───────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    [NotifyPropertyChangedFor(nameof(RundenSichtbar))]
    private bool _formularAktiv;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    private bool _isDirty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    [NotifyPropertyChangedFor(nameof(RundenSichtbar))]
    private bool _istNeuModus;

    [ObservableProperty] private bool _istLadend;
    [ObservableProperty] private string? _fehlermeldung;

    // ── Basisdaten ────────────────────────────────────────────────────────────
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _saisonOderJahr = "";
    [ObservableProperty] private string _startdatum = "";
    [ObservableProperty] private string _enddatum = "";

    // ── Auslosungsfilter ──────────────────────────────────────────────────────
    [ObservableProperty] private IReadOnlyList<VerbandOption> _verbandOptionen = [];
    [ObservableProperty] private VerbandOption _verbandFilter = _keinVerband;
    [ObservableProperty] private MannschaftsartFilter _teamFilter = MannschaftsartFilter.Alle;

    public IReadOnlyList<TeamfilterOption> TeamfilterOptionen =>
        [new("Alle Mannschaften", MannschaftsartFilter.Alle),
         new("Nur Vereine", MannschaftsartFilter.Verein),
         new("Nur Nationalmannschaften", MannschaftsartFilter.Nationalmannschaft)];

    // ── Standard-Regelwerk ────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StandardRegelwerkOptionen))]
    private IReadOnlyList<Regelwerk> _regelwerke = [];

    [ObservableProperty] private RegelwerkOption _standardRegelwerk = _keinRegelwerk;

    public IReadOnlyList<RegelwerkOption> StandardRegelwerkOptionen =>
        [_keinRegelwerk, .. Regelwerke.Select(r => new RegelwerkOption(r.Name, r))];

    // ── Berechnete Eigenschaften ──────────────────────────────────────────────
    public bool RundenSichtbar => FormularAktiv && !IstNeuModus;

    public string StatusAnzeige => Ausgewaehltes is not null
        ? TurnierStatusZuTextConverter.StatusAnzeigeName(Ausgewaehltes.Status)
        : "";

    public IEnumerable<TurnierInstanz> InstanzenGefiltert => Instanzen
        .Where(i =>
            (string.IsNullOrWhiteSpace(Suchbegriff)
                || i.Name.Contains(Suchbegriff, StringComparison.OrdinalIgnoreCase)
                || i.SaisonOderJahr.Contains(Suchbegriff, StringComparison.OrdinalIgnoreCase))
            && (StatusFilter.Wert is null || i.Status == StatusFilter.Wert));

    public IReadOnlyList<TurnierStatusOption> StatusFilterOptionen =>
        [_alleStatus, .. Enum.GetValues<TurnierStatus>()
            .Select(s => new TurnierStatusOption(TurnierStatusZuTextConverter.StatusAnzeigeName(s), s))];

    private static readonly HashSet<string> _formFelder =
        [nameof(Name), nameof(SaisonOderJahr), nameof(Startdatum), nameof(Enddatum),
         nameof(VerbandFilter), nameof(TeamFilter), nameof(StandardRegelwerk)];

    // ── IRibbonAware ───────────────────────────────────────────────────────────
    public string RibbonTabTitel => "Turnierkopf";
    public ICommand NeuBefehl => NeuCommand;
    public ICommand SpeichernBefehl => SpeichernCommand;
    public ICommand AbbrechenBefehl => AbbrechenCommand;
    public ICommand LoeschenBefehl => LoeschenCommand;

    public TurnierInstanzViewModel(TurnierInstanzService service, VerbandService verbandService, RegelwerkService regelwerkService)
    {
        _service = service;
        _verbandService = verbandService;
        _regelwerkService = regelwerkService;
    }

    public override async Task AktivierenAsync()
    {
        IstLadend = true;
        try
        {
            var verbände   = await _verbandService.AlleAbrufenAsync();
            VerbandOptionen = [_keinVerband, .. verbände.Select(v => new VerbandOption(v.Name, v))];
            Regelwerke = await _regelwerkService.AlleAbrufenAsync();
            await LadenAsync();
        }
        finally { IstLadend = false; }
    }

    private async Task LadenAsync()
    {
        var instanzen = await _service.AlleAbrufenAsync();
        Instanzen = new ObservableCollection<TurnierInstanz>(instanzen);
    }

    private void FormularAusFuellenMit(TurnierInstanz? instanz)
    {
        _populiertGerade++;
        try
        {
            if (instanz is null)
            {
                FormularAktiv      = false;
                IstNeuModus        = false;
                Name = SaisonOderJahr = Startdatum = Enddatum = "";
                VerbandFilter      = _keinVerband;
                TeamFilter         = MannschaftsartFilter.Alle;
                StandardRegelwerk  = _keinRegelwerk;
            }
            else
            {
                FormularAktiv     = true;
                IstNeuModus       = false;
                Name              = instanz.Name;
                SaisonOderJahr    = instanz.SaisonOderJahr;
                Startdatum        = instanz.Startdatum?.Year.ToString() ?? "";
                Enddatum          = instanz.Enddatum?.Year.ToString() ?? "";
                VerbandFilter     = VerbandOptionen.FirstOrDefault(o => o.Wert?.Id == instanz.VerbandId)
                                    ?? _keinVerband;
                TeamFilter        = instanz.MannschaftsartFilter;
                StandardRegelwerk = StandardRegelwerkOptionen.FirstOrDefault(
                                        o => o.Wert?.Id == instanz.StandardRegelwerkId)
                                    ?? _keinRegelwerk;
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
            Ausgewaehltes         = null;
            FormularAktiv         = true;
            IstNeuModus           = true;
            Name = SaisonOderJahr = Startdatum = Enddatum = "";
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
            var startdatum = ParseJahr(Startdatum);
            var enddatum   = ParseJahr(Enddatum);

            var verbandId      = VerbandFilter.Wert?.Id;
            var standardRwId   = StandardRegelwerk.Wert?.Id;
            if (IstNeuModus)
            {
                var eingabe = new TurnierInstanzErstellenEingabe(
                    Name, SaisonOderJahr, null, startdatum, enddatum,
                    verbandId, TeamFilter, standardRwId);
                var neu = await _service.ErstellenAsync(eingabe);
                await LadenAsync();
                Ausgewaehltes = Instanzen.FirstOrDefault(i => i.Id == neu.Id);
            }
            else if (Ausgewaehltes is not null)
            {
                var id = Ausgewaehltes.Id;
                var eingabe = new TurnierInstanzVollAktualisierenEingabe(
                    Name, SaisonOderJahr, null, startdatum, enddatum, Ausgewaehltes.Status,
                    verbandId, TeamFilter, standardRwId);
                await _service.VollAktualisierenAsync(id, eingabe);
                await LadenAsync();
                Ausgewaehltes = Instanzen.FirstOrDefault(i => i.Id == id);
                IsDirty = false;
            }
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(KannAbbrechen))]
    private void Abbrechen()
    {
        if (IstNeuModus)
            FormularAusFuellenMit(null);
        else
            _ = BeiAuswaehlungsAenderungAsync();
    }

    [RelayCommand(CanExecute = nameof(KannLoeschen))]
    private async Task Loeschen()
    {
        if (Ausgewaehltes is null) return;
        var ergebnis = MessageBox.Show(
            $"Soll '{Ausgewaehltes.Name}' wirklich gelöscht werden?\n\nDabei werden alle Spiele, Auslosungen und Simulationsläufe mitgelöscht.",
            "Turnier löschen", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ergebnis != MessageBoxResult.Yes) return;
        Fehlermeldung = null;
        try
        {
            var id = Ausgewaehltes.Id;
            await _service.LoeschenAsync(id);
            await LadenAsync();
            Ausgewaehltes = null;
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(Ausgewaehltes))
            _ = BeiAuswaehlungsAenderungAsync();
        if (_populiertGerade == 0 && _formFelder.Contains(e.PropertyName!))
            IsDirty = true;
    }

    private async Task BeiAuswaehlungsAenderungAsync()
    {
        if (Ausgewaehltes is null)
        {
            FormularAusFuellenMit(null);
            return;
        }
        IstLadend = true;
        try
        {
            var detail = await _service.FindenNachIdMitRundenAsync(Ausgewaehltes.Id);
            FormularAusFuellenMit(detail);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally
        {
            IstLadend = false;
        }
    }

    private bool KannSpeichern() => FormularAktiv && IsDirty;
    private bool KannAbbrechen() => FormularAktiv && IsDirty;

    public bool HatUngespeicherteAenderungen => FormularAktiv && IsDirty;
    private bool KannLoeschen()  => Ausgewaehltes is not null && !IstNeuModus;

    private static DateOnly? ParseJahr(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        return int.TryParse(text.Trim(), out var jahr) && jahr >= 1800 && jahr <= 2100
            ? new DateOnly(jahr, 1, 1)
            : null;
    }
}
