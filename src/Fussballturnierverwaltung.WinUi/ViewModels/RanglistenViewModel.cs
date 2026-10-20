using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Ranglisten.Eingaben;
using Fussballturnierverwaltung.Application.Ranglisten.Import;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.Helpers;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
using Fussballturnierverwaltung.WinUi.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

public partial class RanglistenViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly RanglisteService _service;
    private readonly MannschaftService _mannschaftService;
    private readonly VerbandService _verbandService;
    private readonly RanglistenImportService _importService;
    private readonly RanglistenQuelleService _quelleService;
    private readonly LandService _landService;
    private readonly IDialogService _dialogService;
    private int _populiertGerade;

    // ── Liste ─────────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RanglistenGefiltert))]
    private ObservableCollection<Rangliste> _ranglisten = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RanglistenGefiltert))]
    private string _suchbegriff = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private Rangliste? _ausgewaehltes;

    // ── Formular-Zustand ──────────────────────────────────────────────────────
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

    // ── Basisdaten ────────────────────────────────────────────────────────────
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _datum = "";
    [ObservableProperty] private string _quelle = "";

    // Profigrenze (Spielklasse) der Ligapyramide für „Heimrecht für Unterklassige“ (#393). NumberBox liefert double,
    // leer = NaN = keine Grenze (strikt Klasse gegen Klasse).
    [ObservableProperty] private double _profigrenze = double.NaN;

    // Abschnitte nur zur Ansicht — gepflegt werden sie mit dem neuen Ranglisten-Editor (#477), bis dahin per Seed.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatAbschnitte))]
    private IReadOnlyList<AbschnittsZeile> _abschnitte = [];

    public bool HatAbschnitte => Abschnitte.Count > 0;

    // ── Einträge ──────────────────────────────────────────────────────────────
    public RanglistenEintraegeViewModel Eintragsliste { get; } = new();

    // ── Berechnete Listen ─────────────────────────────────────────────────────
    public IEnumerable<Rangliste> RanglistenGefiltert => Ranglisten
        .Where(r => string.IsNullOrWhiteSpace(Suchbegriff)
            || r.Name.Contains(Suchbegriff, StringComparison.OrdinalIgnoreCase));

    // Wird in OnPropertyChanged mit RanglistenGefiltert nachgezogen (alle Quellen automatisch).
    public string AnzahlText => DatensatzAnzahl.Text(
        RanglistenGefiltert.Count(), Ranglisten.Count, !string.IsNullOrWhiteSpace(Suchbegriff),
        "Rangliste", "Ranglisten");

    private static readonly HashSet<string> _formFelder = [nameof(Name), nameof(Datum), nameof(Quelle), nameof(Profigrenze)];

    // ── IRibbonAware ──────────────────────────────────────────────────────────
    public string RibbonTabTitel => "Ranglisten";
    public ICommand NeuBefehl => NeuCommand;
    public ICommand SpeichernBefehl => SpeichernCommand;
    public ICommand AbbrechenBefehl => AbbrechenCommand;
    public ICommand LoeschenBefehl => LoeschenCommand;

    public IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen =>
    [
        new RibbonGruppe
        {
            Header = "Import",
            Aktionen =
            [
                new RibbonAktion { Header = "Aus dem Internet importieren", Befehl = ImportAusInternetCommand, IconGlyph = "" }
            ]
        }
    ];

    public RanglistenViewModel(
        RanglisteService service,
        MannschaftService mannschaftService,
        VerbandService verbandService,
        RanglistenImportService importService,
        RanglistenQuelleService quelleService,
        LandService landService,
        IDialogService dialogService)
    {
        _service = service;
        _mannschaftService = mannschaftService;
        _verbandService = verbandService;
        _importService = importService;
        _quelleService = quelleService;
        _landService = landService;
        _dialogService = dialogService;
        Eintragsliste.Geaendert += (_, _) => { if (_populiertGerade == 0) IsDirty = true; };
    }

    public override async Task AktivierenAsync() => await LadenAsync();

    private async Task LadenAsync()
    {
        IstLadend = true;
        try
        {
            var ranglisten   = await _service.AlleAbrufenAsync();
            var mannschaften = await _mannschaftService.AlleAbrufenMitLandAsync();
            var verbaende    = await _verbandService.AlleAbrufenAsync();
            Ranglisten = new ObservableCollection<Rangliste>(ranglisten);
            Eintragsliste.StammdatenSetzen(mannschaften, verbaende);
        }
        finally
        {
            IstLadend = false;
        }
    }

    partial void OnAusgewaehltesChanged(Rangliste? value) => FormularAusFuellenMit(value);

    private void FormularAusFuellenMit(Rangliste? rangliste)
    {
        _populiertGerade++;
        try
        {
            if (rangliste is null)
            {
                FormularAktiv = false;
                IstNeuModus = false;
                Name = Quelle = "";
                Profigrenze = double.NaN;
                Abschnitte = [];
                Datum = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
                Eintragsliste.Laden([]);
            }
            else
            {
                FormularAktiv = true;
                IstNeuModus = false;
                Name = rangliste.Name;
                Datum = rangliste.Datum.ToString("yyyy-MM-dd");
                Quelle = rangliste.Quelle;
                Profigrenze = rangliste.ProfigrenzeSpielklasse ?? double.NaN;
                Abschnitte = AbschnittsUebersicht.Fuer(rangliste);
                Eintragsliste.Laden(
                    rangliste.Eintraege.OrderBy(e => e.Rang).Select(e => new RanglistenEintragZeile
                    {
                        Mannschaft = e.Mannschaft,
                        Rang       = e.Rang,
                        Punkte     = e.Punkte
                    }));
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
            Ausgewaehltes = null;
            FormularAktiv = true;
            IstNeuModus = true;
            Name = Quelle = "";
            Profigrenze = double.NaN;
            Abschnitte = [];
            Datum = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
            Eintragsliste.Laden([]);
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
            var datum = ParseDatum(Datum);
            var eintraege = Eintragsliste.Eintraege
                .Select(z => new RanglistenEintragEingabe(z.Mannschaft.Id, z.Rang, z.Punkte))
                .ToList();

            Rangliste? gespeichert;
            if (IstNeuModus)
            {
                var eingabe = new RanglisteErstellenEingabe(Name, datum, Quelle, eintraege, ProfigrenzeSpielklasse: ProfigrenzeOderNull());
                var neu = await _service.ErstellenAsync(eingabe);
                await LadenAsync();
                gespeichert = Ranglisten.FirstOrDefault(r => r.Id == neu.Id);
            }
            else if (Ausgewaehltes is not null)
            {
                var id = Ausgewaehltes.Id;
                var eingabe = new RanglisteAktualisierenEingabe(
                    Name, datum, Quelle, eintraege, ProfigrenzeSpielklasse: ProfigrenzeOderNull());
                await _service.AktualisierenAsync(id, eingabe);
                await LadenAsync();
                gespeichert = Ranglisten.FirstOrDefault(r => r.Id == id);
            }
            else
            {
                return;
            }

            // Explizit statt nur über die Ausgewaehltes-Zuweisung: EF Core gibt beim Neuladen
            // innerhalb desselben DbContext-Scopes für bereits getrackte Entitäten dieselbe
            // Instanz zurück (Identity Resolution) — die Zuweisung wäre dann referenzgleich und
            // löste kein PropertyChanged aus, wodurch FormularAusFuellenMit nie liefe.
            Ausgewaehltes = gespeichert;
            FormularAusFuellenMit(gespeichert);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(KannAbbrechen))]
    private void Abbrechen() => FormularAusFuellenMit(IstNeuModus ? null : Ausgewaehltes);

    [RelayCommand(CanExecute = nameof(KannLoeschen))]
    private async Task Loeschen()
    {
        if (Ausgewaehltes is null) return;

        var bestaetigt = await _dialogService.BestaetigenAsync(
            "Rangliste löschen", $"Soll '{Ausgewaehltes.Name}' wirklich gelöscht werden?");
        if (!bestaetigt) return;

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

    // ── Import (Issue #120/#285) ──────────────────────────────────────────────
    // ViewModels erzeugen die ContentDialogs direkt (analog zum WPF-Original, das hier direkt ein
    // Window öffnet statt über View-Code-Behind) — IDialogService.AnzeigenAsync übernimmt nur das
    // Setzen des XamlRoot, den das ViewModel selbst nicht kennt.

    [RelayCommand]
    private async Task ImportAusInternet()
    {
        var dialog = new RanglistenImportDialog(_importService, _quelleService, _service, _mannschaftService, _landService, _dialogService);
        await _dialogService.AnzeigenAsync(dialog);
        if (dialog.ImportAbgeschlossen)
            await LadenAsync();
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(RanglistenGefiltert)) OnPropertyChanged(nameof(AnzahlText));
        if (_populiertGerade == 0 && e.PropertyName is not null && _formFelder.Contains(e.PropertyName))
            IsDirty = true;
    }

    private bool KannSpeichern() => FormularAktiv && IsDirty;
    private bool KannAbbrechen() => FormularAktiv && IsDirty;

    public bool HatUngespeicherteAenderungen => FormularAktiv && IsDirty;
    private bool KannLoeschen() => Ausgewaehltes is not null && !IstNeuModus;

    private int? ProfigrenzeOderNull() => double.IsNaN(Profigrenze) ? null : (int)Math.Round(Profigrenze);

    private static DateOnly ParseDatum(string text)
        => DateOnly.TryParse(text.Trim(), out var datum) ? datum : DateOnly.FromDateTime(DateTime.Today);
}
