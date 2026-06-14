using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Wpf.Ribbon;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

public partial class RundenEditorViewModel : ViewModelBase, IRibbonAware
{
    private readonly TurnierInstanzService _service;
    private readonly RegelwerkService _regelwerkService;
    private readonly TurnierVorlageService _vorlageService;
    private int _populiertGerade;

    // Preserved turnier metadata for VollAktualisierenAsync on save
    private string _instanzName = "";
    private string _instanzSaison = "";
    private DateOnly? _instanzStart;
    private DateOnly? _instanzEnd;
    private TurnierStatus _instanzStatus = TurnierStatus.Geplant;
    private int? _vorlageId;

    private static readonly RegelwerkOption _keinRegelwerk = new("— Wählen —", null);

    // ── Linke Spalte: Turnierliste (Geplant) ──────────────────────────────────
    [ObservableProperty]
    private ObservableCollection<TurnierInstanz> _geplanteTurniere = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RundenEditorAktiv))]
    [NotifyPropertyChangedFor(nameof(KeinTurnierGewaehlt))]
    [NotifyPropertyChangedFor(nameof(FormularSichtbar))]
    [NotifyCanExecuteChangedFor(nameof(NeueRundeCommand))]
    [NotifyCanExecuteChangedFor(nameof(VorlageRundenLadenCommand))]
    private TurnierInstanz? _ausgewaehlteInstanz;

    // ── Mittlere Spalte: Vorlagenauswahl + Rundenliste ─────────────────────────
    [ObservableProperty]
    private ObservableCollection<TurnierVorlage> _turnierVorlagen = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VorlageRundenLadenCommand))]
    private TurnierVorlage? _ausgewaehlteVorlagenquelle;

    [ObservableProperty]
    private ObservableCollection<RundenEintrag> _runden = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RundeHochCommand))]
    [NotifyCanExecuteChangedFor(nameof(RundeRunterCommand))]
    [NotifyCanExecuteChangedFor(nameof(RundeEntfernenCommand))]
    [NotifyPropertyChangedFor(nameof(FormularSichtbar))]
    private RundenEintrag? _ausgewaehlteRunde;

    // ── Rechte Spalte: Detailformular ─────────────────────────────────────────
    [ObservableProperty] private string _name = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AuswaertstorAktivierbar))]
    private Rundenart _rundenart = Rundenart.Liga;
    [ObservableProperty] private string _eingang = "4";
    [ObservableProperty] private string _ausgang = "4";
    [ObservableProperty] private RegelwerkOption _regelwerk = _keinRegelwerk;
    [ObservableProperty] private bool _auswaertstorRegelAktiv;

    // ── Optionen ─────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RegelwerkOptionen))]
    private IReadOnlyList<Regelwerk> _regelwerke = [];

    public IReadOnlyList<RegelwerkOption> RegelwerkOptionen =>
        [_keinRegelwerk, .. Regelwerke.Select(r => new RegelwerkOption(r.Name, r))];

    public IReadOnlyList<Rundenart> RundenartOptionen => Enum.GetValues<Rundenart>().ToList();

    // ── Zustand ───────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    private bool _isDirty;

    [ObservableProperty] private bool _istLadend;
    [ObservableProperty] private string? _fehlermeldung;

    // ── Berechnete Eigenschaften ──────────────────────────────────────────────
    public bool AuswaertstorAktivierbar => Rundenart == Rundenart.KoRundeMitHinUndRueckspiel;
    public bool RundenEditorAktiv       => AusgewaehlteInstanz is not null;
    public bool KeinTurnierGewaehlt     => AusgewaehlteInstanz is null;
    public bool FormularSichtbar        => AusgewaehlteInstanz is not null && AusgewaehlteRunde is not null;

    // ── IRibbonAware ───────────────────────────────────────────────────────────
    public string RibbonTabTitel  => "Runden";
    public ICommand NeuBefehl      => NeueRundeCommand;
    public ICommand SpeichernBefehl => SpeichernCommand;
    public ICommand AbbrechenBefehl => AbbrechenCommand;
    public ICommand? LoeschenBefehl => null;

    public RundenEditorViewModel(
        TurnierInstanzService service,
        RegelwerkService regelwerkService,
        TurnierVorlageService vorlageService)
    {
        _service = service;
        _regelwerkService = regelwerkService;
        _vorlageService = vorlageService;
    }

    public override async Task AktivierenAsync() => await LadenAsync();

    private async Task LadenAsync()
    {
        IstLadend = true;
        try
        {
            var alleInstanzen = await _service.AlleAbrufenAsync();
            var regelwerke    = await _regelwerkService.AlleAbrufenAsync();
            var vorlagen      = await _vorlageService.AlleAbrufenAsync();

            GeplanteTurniere = new ObservableCollection<TurnierInstanz>(
                alleInstanzen
                    .Where(i => i.Status == TurnierStatus.Geplant)
                    .OrderByDescending(i => i.SaisonOderJahr)
                    .ThenBy(i => i.Name));
            Regelwerke       = regelwerke.ToList();
            TurnierVorlagen  = new ObservableCollection<TurnierVorlage>(vorlagen.OrderBy(v => v.Name));
        }
        finally
        {
            IstLadend = false;
        }
    }

    // ── Runden-Befehle ────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(KannNeueRunde))]
    private void NeueRunde()
    {
        var eintrag = new RundenEintrag();
        Runden.Add(eintrag);
        AusgewaehlteRunde = eintrag;
        IsDirty = true;
    }

    [RelayCommand(CanExecute = nameof(KannEntfernen))]
    private void RundeEntfernen()
    {
        if (AusgewaehlteRunde is null) return;
        var idx = Runden.IndexOf(AusgewaehlteRunde);
        Runden.Remove(AusgewaehlteRunde);
        AusgewaehlteRunde = Runden.Count > 0 ? Runden[Math.Max(0, idx - 1)] : null;
        IsDirty = true;
    }

    [RelayCommand(CanExecute = nameof(KannHoch))]
    private void RundeHoch()
    {
        if (AusgewaehlteRunde is null) return;
        var idx = Runden.IndexOf(AusgewaehlteRunde);
        if (idx <= 0) return;
        Runden.Move(idx, idx - 1);
        IsDirty = true;
    }

    [RelayCommand(CanExecute = nameof(KannRunter))]
    private void RundeRunter()
    {
        if (AusgewaehlteRunde is null) return;
        var idx = Runden.IndexOf(AusgewaehlteRunde);
        if (idx < 0 || idx >= Runden.Count - 1) return;
        Runden.Move(idx, idx + 1);
        IsDirty = true;
    }

    // ── Vorlage laden ─────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(KannVorladeRundenLaden))]
    private async Task VorlageRundenLaden()
    {
        if (AusgewaehlteVorlagenquelle is null) return;
        IstLadend = true;
        Fehlermeldung = null;
        try
        {
            var vorlage = await _vorlageService.FindenNachIdMitRundenAsync(AusgewaehlteVorlagenquelle.Id);
            if (vorlage is null) return;
            var runden = vorlage.VorlagenRunden
                .OrderBy(r => r.Reihenfolge)
                .Select(r => new RundenEintrag
                {
                    Name                   = r.Name,
                    Rundenart              = r.Rundenart,
                    EingangsMannschaften   = r.EingangsMannschaftsanzahl,
                    AusgangsMannschaften   = r.AusgangsMannschaftsanzahl,
                    RegelwerkId            = r.RegelwerkId ?? 0,
                    RegelwerkName          = r.Regelwerk?.Name
                                            ?? RegelwerkOptionen.FirstOrDefault(o => o.Wert?.Id == r.RegelwerkId)?.Wert?.Name
                                            ?? "",
                    AuswaertstorRegelAktiv = r.AuswaertstorRegelAktiv
                })
                .ToList();
            RundenZuruecksetzen(runden);
            _vorlageId = AusgewaehlteVorlagenquelle.Id;
            IsDirty = true;
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

    // ── Ribbon-Befehle ────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(KannSpeichern))]
    private async Task Speichern()
    {
        if (AusgewaehlteInstanz is null) return;
        Fehlermeldung = null;
        try
        {
            var vollEingabe = new TurnierInstanzVollAktualisierenEingabe(
                _instanzName, _instanzSaison, _vorlageId, _instanzStart, _instanzEnd, _instanzStatus);
            await _service.VollAktualisierenAsync(AusgewaehlteInstanz.Id, vollEingabe);
            await _service.RundenErsetzenAsync(AusgewaehlteInstanz.Id, RundenListeZuEingaben());
            IsDirty = false;
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(KannAbbrechen))]
    private async Task Abbrechen()
    {
        await LadeRundenFuerInstanzAsync(AusgewaehlteInstanz);
    }

    // ── Reaktive Formular → Eintrag Synchronisation ───────────────────────────

    partial void OnNameChanged(string value)
    {
        if (_populiertGerade > 0 || AusgewaehlteRunde is null) return;
        AusgewaehlteRunde.Name = value;
        IsDirty = true;
    }

    partial void OnRundenartChanged(Rundenart value)
    {
        if (_populiertGerade > 0 || AusgewaehlteRunde is null) return;
        AusgewaehlteRunde.Rundenart = value;
        if (!AuswaertstorAktivierbar) AuswaertstorRegelAktiv = false;
        IsDirty = true;
    }

    partial void OnEingangChanged(string value)
    {
        if (_populiertGerade > 0 || AusgewaehlteRunde is null) return;
        AusgewaehlteRunde.EingangsMannschaften = ParseAnzahl(value, 4);
        IsDirty = true;
    }

    partial void OnAusgangChanged(string value)
    {
        if (_populiertGerade > 0 || AusgewaehlteRunde is null) return;
        AusgewaehlteRunde.AusgangsMannschaften = ParseAnzahl(value, 4);
        IsDirty = true;
    }

    partial void OnRegelwerkChanged(RegelwerkOption value)
    {
        if (_populiertGerade > 0 || AusgewaehlteRunde is null) return;
        AusgewaehlteRunde.RegelwerkId   = value.Wert?.Id ?? 0;
        AusgewaehlteRunde.RegelwerkName = value.Wert?.Name ?? "";
        IsDirty = true;
    }

    partial void OnAuswaertstorRegelAktivChanged(bool value)
    {
        if (_populiertGerade > 0 || AusgewaehlteRunde is null) return;
        AusgewaehlteRunde.AuswaertstorRegelAktiv = value;
        IsDirty = true;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    partial void OnAusgewaehlteInstanzChanged(TurnierInstanz? value)
        => _ = LadeRundenFuerInstanzAsync(value);

    private async Task LadeRundenFuerInstanzAsync(TurnierInstanz? instanz)
    {
        if (instanz is null)
        {
            RundenZuruecksetzen([]);
            return;
        }
        IstLadend = true;
        try
        {
            var detail = await _service.FindenNachIdMitRundenAsync(instanz.Id);
            if (detail is not null)
            {
                _instanzName   = detail.Name;
                _instanzSaison = detail.SaisonOderJahr;
                _instanzStart  = detail.Startdatum;
                _instanzEnd    = detail.Enddatum;
                _instanzStatus = detail.Status;
                _vorlageId     = detail.TurnierVorlageId;
            }
            RundenZuruecksetzen(
                (detail?.Runden ?? []).OrderBy(r => r.Reihenfolge).Select(RundeZuEintrag));
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

    partial void OnAusgewaehlteRundeChanged(RundenEintrag? value)
    {
        if (_populiertGerade > 0) return;
        if (value is null) { FormularZuruecksetzen(); return; }
        _populiertGerade++;
        try
        {
            Name                   = value.Name;
            Rundenart              = value.Rundenart;
            Eingang                = value.EingangsMannschaften.ToString();
            Ausgang                = value.AusgangsMannschaften.ToString();
            Regelwerk              = RegelwerkOptionen.FirstOrDefault(o => o.Wert?.Id == value.RegelwerkId)
                                     ?? _keinRegelwerk;
            AuswaertstorRegelAktiv = value.AuswaertstorRegelAktiv;
        }
        finally { _populiertGerade--; }
    }

    private void RundenZuruecksetzen(IEnumerable<RundenEintrag> runden)
    {
        _populiertGerade++;
        try
        {
            Runden            = new ObservableCollection<RundenEintrag>(runden);
            AusgewaehlteRunde = null;
            FormularZuruecksetzen();
            IsDirty       = false;
            Fehlermeldung = null;
        }
        finally { _populiertGerade--; }
    }

    private void FormularZuruecksetzen()
    {
        _populiertGerade++;
        try
        {
            Name                   = "";
            Rundenart              = Rundenart.Liga;
            Eingang                = "4";
            Ausgang                = "4";
            Regelwerk              = _keinRegelwerk;
            AuswaertstorRegelAktiv = false;
        }
        finally { _populiertGerade--; }
    }

    private IReadOnlyList<TurnierRundeEingabe> RundenListeZuEingaben()
        => Runden.Select(r => new TurnierRundeEingabe(
            r.Name, r.Rundenart, r.EingangsMannschaften, r.AusgangsMannschaften,
            r.RegelwerkId, r.AuswaertstorRegelAktiv)).ToList();

    private static RundenEintrag RundeZuEintrag(TurnierRunde r) => new()
    {
        Name                   = r.Name,
        Rundenart              = r.Rundenart,
        EingangsMannschaften   = r.EingangsMannschaftsanzahl,
        AusgangsMannschaften   = r.AusgangsMannschaftsanzahl,
        RegelwerkId            = r.RegelwerkId,
        RegelwerkName          = r.Regelwerk?.Name ?? "",
        AuswaertstorRegelAktiv = r.AuswaertstorRegelAktiv
    };

    private bool KannSpeichern()          => IsDirty && AusgewaehlteInstanz is not null;
    private bool KannAbbrechen()          => IsDirty && AusgewaehlteInstanz is not null;
    private bool KannNeueRunde()          => RundenEditorAktiv;
    private bool KannEntfernen()          => AusgewaehlteRunde is not null;
    private bool KannHoch()               => AusgewaehlteRunde is not null && Runden.IndexOf(AusgewaehlteRunde) > 0;
    private bool KannRunter()             => AusgewaehlteRunde is not null
                                            && Runden.IndexOf(AusgewaehlteRunde) < Runden.Count - 1;
    private bool KannVorladeRundenLaden() => AusgewaehlteVorlagenquelle is not null && RundenEditorAktiv;

    private static int ParseAnzahl(string text, int fallback)
        => int.TryParse(text.Trim(), out var n) && n >= 1 ? n : fallback;
}
