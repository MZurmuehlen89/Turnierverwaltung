using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Ranglisten.Eingaben;
using Fussballturnierverwaltung.Application.Ranglisten.Import;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
using Fussballturnierverwaltung.WinUi.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

// ViewModel-Zeile für die Einträge-ListView; jede Eigenschaft ist observable, damit
// Inline-Editing (Punkte) sofort den Dirty-Zustand der Rangliste setzt. Rang wird NICHT
// manuell editiert, sondern aus der Position in der Eintraege-Liste abgeleitet (siehe
// RanglistenViewModel.AktualisiereRaenge) — Reihenfolge per Hoch/Runter-Buttons, analog
// zum bestehenden Verfahren-Muster in RegelwerkeViewModel.
public partial class RanglistenEintragZeile : ObservableObject
{
    [ObservableProperty] private Mannschaft _mannschaft = null!;
    [ObservableProperty] private int _rang;
    [ObservableProperty] private int? _punkte;

    public byte[]? Wappen => Mannschaft.Mannschaftsart == Mannschaftsart.Nationalmannschaft
        ? Mannschaft.Land?.Flagge
        : Mannschaft.Wappen;
}

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

    private static readonly VerbandFilterOption _allesVerband = new("— Alle —", null);

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

    // ── Einträge ──────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerfuegbareMannschaftenGefiltert))]
    private ObservableCollection<RanglistenEintragZeile> _eintraege = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EintragEntfernenCommand))]
    [NotifyCanExecuteChangedFor(nameof(EintragHochCommand))]
    [NotifyCanExecuteChangedFor(nameof(EintragRunterCommand))]
    private RanglistenEintragZeile? _ausgewaehlterEintrag;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerfuegbareMannschaftenGefiltert))]
    private ObservableCollection<Mannschaft> _alleMannschaften = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerbandFilterOptionen))]
    private ObservableCollection<Verband> _alleVerbaende = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerfuegbareMannschaftenGefiltert))]
    private VerbandFilterOption _mannschaftVerbandFilter = _allesVerband;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerfuegbareMannschaftenGefiltert))]
    private string _mannschaftSuchbegriff = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EintragHinzufuegenCommand))]
    private Mannschaft? _neueMannschaft;

    // ── Berechnete Listen ─────────────────────────────────────────────────────
    public IEnumerable<Rangliste> RanglistenGefiltert => Ranglisten
        .Where(r => string.IsNullOrWhiteSpace(Suchbegriff)
            || r.Name.Contains(Suchbegriff, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<VerbandFilterOption> VerbandFilterOptionen =>
        [_allesVerband, .. AlleVerbaende.Select(v => new VerbandFilterOption(v.Name, v))];

    public IEnumerable<Mannschaft> VerfuegbareMannschaftenGefiltert => AlleMannschaften
        .Where(m => Eintraege.All(e => e.Mannschaft.Id != m.Id))
        .Where(m => MannschaftVerbandFilter.Wert is null || m.Land?.VerbandId == MannschaftVerbandFilter.Wert.Id)
        .Where(m => string.IsNullOrWhiteSpace(MannschaftSuchbegriff)
            || m.Name.Contains(MannschaftSuchbegriff, StringComparison.OrdinalIgnoreCase));

    private static readonly HashSet<string> _formFelder = [nameof(Name), nameof(Datum), nameof(Quelle)];

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
                new RibbonAktion { Header = "Aus dem Internet importieren", Befehl = ImportAusInternetCommand, IconGlyph = "" },
                new RibbonAktion { Header = "Import-Quellen verwalten", Befehl = QuellenVerwaltenCommand, IconGlyph = "" }
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
            Ranglisten       = new ObservableCollection<Rangliste>(ranglisten);
            AlleMannschaften = new ObservableCollection<Mannschaft>(mannschaften);
            AlleVerbaende    = new ObservableCollection<Verband>(verbaende.OrderBy(v => v.Name));
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
                Datum = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
                Eintraege = [];
            }
            else
            {
                FormularAktiv = true;
                IstNeuModus = false;
                Name = rangliste.Name;
                Datum = rangliste.Datum.ToString("yyyy-MM-dd");
                Quelle = rangliste.Quelle;
                Eintraege = new ObservableCollection<RanglistenEintragZeile>(
                    rangliste.Eintraege.OrderBy(e => e.Rang).Select(e => new RanglistenEintragZeile
                    {
                        Mannschaft = e.Mannschaft,
                        Rang       = e.Rang,
                        Punkte     = e.Punkte
                    }));
                AbonniereEintraegeAenderungen();
                AktualisiereRaenge();
            }
            NeueMannschaft = null;
            MannschaftSuchbegriff = "";
            MannschaftVerbandFilter = _allesVerband;
            IsDirty = false;
            Fehlermeldung = null;
        }
        finally
        {
            _populiertGerade--;
        }
    }

    // Setzt IsDirty wenn Punkte inline editiert werden
    private void AbonniereEintraegeAenderungen()
    {
        foreach (var zeile in Eintraege)
            zeile.PropertyChanged += OnEintragGeaendert;
    }

    private void OnEintragGeaendert(object? sender, PropertyChangedEventArgs e)
    {
        if (_populiertGerade == 0)
            IsDirty = true;
    }

    // Rang ergibt sich immer aus der Position in der Liste (1-basiert) — keine
    // manuelle Eingabe, keine Lücken/Duplikate möglich.
    private void AktualisiereRaenge()
    {
        for (int i = 0; i < Eintraege.Count; i++)
            Eintraege[i].Rang = i + 1;
        OnPropertyChanged(nameof(VerfuegbareMannschaftenGefiltert));
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
            Datum = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
            Eintraege = [];
            NeueMannschaft = null;
            MannschaftSuchbegriff = "";
            MannschaftVerbandFilter = _allesVerband;
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
            var eintraege = Eintraege
                .Select(z => new RanglistenEintragEingabe(z.Mannschaft.Id, z.Rang, z.Punkte))
                .ToList();

            Rangliste? gespeichert;
            if (IstNeuModus)
            {
                var eingabe = new RanglisteErstellenEingabe(Name, datum, Quelle, eintraege);
                var neu = await _service.ErstellenAsync(eingabe);
                await LadenAsync();
                gespeichert = Ranglisten.FirstOrDefault(r => r.Id == neu.Id);
            }
            else if (Ausgewaehltes is not null)
            {
                var id = Ausgewaehltes.Id;
                var eingabe = new RanglisteAktualisierenEingabe(Name, datum, Quelle, eintraege);
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

    [RelayCommand]
    private async Task QuellenVerwalten()
    {
        var dialog = new RanglistenQuellenVerwaltenDialog(_quelleService, _dialogService);
        await _dialogService.AnzeigenAsync(dialog);
    }

    // ── Eintrag-Befehle ───────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(KannEintragHinzufuegen))]
    private void EintragHinzufuegen()
    {
        if (NeueMannschaft is null) return;
        var neu = new RanglistenEintragZeile { Mannschaft = NeueMannschaft, Rang = Eintraege.Count + 1 };
        neu.PropertyChanged += OnEintragGeaendert;
        Eintraege.Add(neu);
        AusgewaehlterEintrag = neu;
        NeueMannschaft = null;
        AktualisiereRaenge();
        if (_populiertGerade == 0) IsDirty = true;
    }

    [RelayCommand(CanExecute = nameof(KannEintragEntfernen))]
    private void EintragEntfernen()
    {
        if (AusgewaehlterEintrag is null) return;
        Eintraege.Remove(AusgewaehlterEintrag);
        AusgewaehlterEintrag = null;
        AktualisiereRaenge();
        if (_populiertGerade == 0) IsDirty = true;
    }

    [RelayCommand(CanExecute = nameof(KannEintragHoch))]
    private void EintragHoch()
    {
        if (AusgewaehlterEintrag is null) return;
        var idx = Eintraege.IndexOf(AusgewaehlterEintrag);
        if (idx <= 0) return;
        Eintraege.Move(idx, idx - 1);
        AktualisiereRaenge();
        if (_populiertGerade == 0) IsDirty = true;
    }

    [RelayCommand(CanExecute = nameof(KannEintragRunter))]
    private void EintragRunter()
    {
        if (AusgewaehlterEintrag is null) return;
        var idx = Eintraege.IndexOf(AusgewaehlterEintrag);
        if (idx < 0 || idx >= Eintraege.Count - 1) return;
        Eintraege.Move(idx, idx + 1);
        AktualisiereRaenge();
        if (_populiertGerade == 0) IsDirty = true;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_populiertGerade == 0 && e.PropertyName is not null && _formFelder.Contains(e.PropertyName))
            IsDirty = true;
    }

    private bool KannSpeichern() => FormularAktiv && IsDirty;
    private bool KannAbbrechen() => FormularAktiv && IsDirty;

    public bool HatUngespeicherteAenderungen => FormularAktiv && IsDirty;
    private bool KannLoeschen() => Ausgewaehltes is not null && !IstNeuModus;
    private bool KannEintragHinzufuegen() => NeueMannschaft is not null;
    private bool KannEintragEntfernen() => AusgewaehlterEintrag is not null;
    private bool KannEintragHoch() => AusgewaehlterEintrag is not null && Eintraege.IndexOf(AusgewaehlterEintrag) > 0;
    private bool KannEintragRunter() => AusgewaehlterEintrag is not null
        && Eintraege.IndexOf(AusgewaehlterEintrag) < Eintraege.Count - 1;

    private static DateOnly ParseDatum(string text)
        => DateOnly.TryParse(text.Trim(), out var datum) ? datum : DateOnly.FromDateTime(DateTime.Today);
}
