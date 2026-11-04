using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Diagnose;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Ranglisten.Eingaben;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.Helpers;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

public partial class RanglistenViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly RanglisteService _service;
    private readonly MannschaftService _mannschaftService;
    private readonly VerbandService _verbandService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<RanglistenViewModel> _logger;
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
    // CalendarDatePicker liefert DateTimeOffset?; leer gespeichert = heute.
    [ObservableProperty] private DateTimeOffset? _datum = DateTimeOffset.Now.Date;
    [ObservableProperty] private string _quelle = "";

    // ── Ligapyramide ──────────────────────────────────────────────────────────
    public LigapyramideViewModel Ligapyramide { get; } = new();

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

    private static readonly HashSet<string> _formFelder = [nameof(Name), nameof(Datum), nameof(Quelle)];

    // ── IRibbonAware ──────────────────────────────────────────────────────────
    public string RibbonTabTitel => "Ranglisten";
    public ICommand NeuBefehl => NeuCommand;
    public ICommand SpeichernBefehl => SpeichernCommand;
    public ICommand AbbrechenBefehl => AbbrechenCommand;
    public ICommand LoeschenBefehl => LoeschenCommand;

    public IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen => [];

    public RanglistenViewModel(
        RanglisteService service,
        MannschaftService mannschaftService,
        VerbandService verbandService,
        IDialogService dialogService,
        ILogger<RanglistenViewModel> logger)
    {
        _service = service;
        _mannschaftService = mannschaftService;
        _verbandService = verbandService;
        _dialogService = dialogService;
        _logger = logger;
        Eintragsliste.Geaendert += (_, _) => { if (_populiertGerade == 0) IsDirty = true; };
        Ligapyramide.Geaendert += (_, _) =>
        {
            if (_populiertGerade == 0) IsDirty = true;
            SpeichernCommand.NotifyCanExecuteChanged();
        };
        Ligapyramide.AbschnitteGeaendert += (_, _) => Eintragsliste.AbschnitteSetzen(Ligapyramide.Uebersicht());
        Eintragsliste.Eintraege.CollectionChanged += (_, _) => Ligapyramide.EintragsAnzahlSetzen(Eintragsliste.Eintraege.Count);
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
            Ligapyramide.StammdatenSetzen(mannschaften);
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
                Ligapyramide.Laden(null);
                Datum = DateTimeOffset.Now.Date;
                Eintragsliste.Laden([], []);
            }
            else
            {
                FormularAktiv = true;
                IstNeuModus = false;
                Name = rangliste.Name;
                Datum = new DateTimeOffset(rangliste.Datum.ToDateTime(TimeOnly.MinValue));
                Quelle = rangliste.Quelle;
                Ligapyramide.Laden(rangliste);
                Eintragsliste.Laden(
                    rangliste.Eintraege.OrderBy(e => e.Rang).Select(e => new RanglistenEintragZeile
                    {
                        Mannschaft = e.Mannschaft,
                        Rang       = e.Rang,
                        Punkte     = e.Punkte
                    }),
                    Ligapyramide.Uebersicht());
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
            Ligapyramide.Laden(null);
            Datum = DateTimeOffset.Now.Date;
            Eintragsliste.Laden([], []);
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
        // Als Vorgang, weil die Seite danach neu lädt: Ladeanzeige und „Bereit“ für GUI-Tests folgen nur offenen
        // Vorgängen – ohne ihn arbeitete ein Test schon im Formular, das das Neuladen gleich wieder überschrieb (#535)
        using var vorgang = _logger.Vorgang($"Ranglisten: speichern ({Name})", "Rangliste wird gespeichert …");
        Fehlermeldung = null;
        try
        {
            var datum = DateOnly.FromDateTime((Datum ?? DateTimeOffset.Now).Date);
            var eintraege = Eintragsliste.Eintraege
                .Select(z => new RanglistenEintragEingabe(z.Mannschaft.Id, z.Rang, z.Punkte))
                .ToList();

            Rangliste? gespeichert;
            if (IstNeuModus)
            {
                var eingabe = new RanglisteErstellenEingabe(Name, datum, Quelle, eintraege,
                    ProfigrenzeSpielklasse: Ligapyramide.ProfigrenzeOderNull(), Abschnitte: Ligapyramide.Eingaben(),
                    PokalsiegerId: Ligapyramide.PokalsiegerId);
                var neu = await _service.ErstellenAsync(eingabe);
                await LadenAsync();
                gespeichert = Ranglisten.FirstOrDefault(r => r.Id == neu.Id);
            }
            else if (Ausgewaehltes is not null)
            {
                var id = Ausgewaehltes.Id;
                var eingabe = new RanglisteAktualisierenEingabe(
                    Name, datum, Quelle, eintraege, ProfigrenzeSpielklasse: Ligapyramide.ProfigrenzeOderNull(),
                    Abschnitte: Ligapyramide.Eingaben(), PokalsiegerId: Ligapyramide.PokalsiegerId);
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

        using var vorgang = _logger.Vorgang($"Ranglisten: löschen ({Ausgewaehltes.Name})", "Rangliste wird gelöscht …");
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

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(RanglistenGefiltert)) OnPropertyChanged(nameof(AnzahlText));
        if (_populiertGerade == 0 && e.PropertyName is not null && _formFelder.Contains(e.PropertyName))
            IsDirty = true;
    }

    private bool KannSpeichern() => FormularAktiv && IsDirty && Ligapyramide.Fehler.Count == 0;
    private bool KannAbbrechen() => FormularAktiv && IsDirty;

    public bool HatUngespeicherteAenderungen => FormularAktiv && IsDirty;
    private bool KannLoeschen() => Ausgewaehltes is not null && !IstNeuModus;
}
