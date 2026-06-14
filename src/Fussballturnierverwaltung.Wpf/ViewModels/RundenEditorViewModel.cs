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
    [ObservableProperty] private Rundenart _rundenart = Rundenart.Liga;
    [ObservableProperty] private string _eingang = "4";
    [ObservableProperty] private string _ausgang = "4";
    [ObservableProperty] private RegelwerkOption _regelwerk = _keinRegelwerk;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AuswaertstorAktivierbar))]
    private bool _mitRueckspiel;
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

    // ── Validierung ───────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatRundenFehler))]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    private IReadOnlyList<string> _rundenFehler = [];

    public bool HatRundenFehler => RundenFehler.Count > 0;

    // ── Berechnete Eigenschaften ──────────────────────────────────────────────
    public bool AuswaertstorAktivierbar => MitRueckspiel;
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
        var letzte   = Runden.LastOrDefault();
        var eingang  = letzte?.AusgangsMannschaften ?? 4;
        var eintrag  = new RundenEintrag
        {
            EingangsMannschaften = eingang,
            AusgangsMannschaften = Math.Max(1, eingang / 2)
        };
        Runden.Add(eintrag);
        AusgewaehlteRunde = eintrag;
        AktualisiereRundenFehler();
        IsDirty = true;
    }

    [RelayCommand(CanExecute = nameof(KannEntfernen))]
    private void RundeEntfernen()
    {
        if (AusgewaehlteRunde is null) return;
        var idx = Runden.IndexOf(AusgewaehlteRunde);
        Runden.Remove(AusgewaehlteRunde);
        AusgewaehlteRunde = Runden.Count > 0 ? Runden[Math.Max(0, idx - 1)] : null;
        AktualisiereRundenFehler();
        IsDirty = true;
    }

    [RelayCommand(CanExecute = nameof(KannHoch))]
    private void RundeHoch()
    {
        if (AusgewaehlteRunde is null) return;
        var idx = Runden.IndexOf(AusgewaehlteRunde);
        if (idx <= 0) return;
        Runden.Move(idx, idx - 1);
        AktualisiereRundenFehler();
        IsDirty = true;
    }

    [RelayCommand(CanExecute = nameof(KannRunter))]
    private void RundeRunter()
    {
        if (AusgewaehlteRunde is null) return;
        var idx = Runden.IndexOf(AusgewaehlteRunde);
        if (idx < 0 || idx >= Runden.Count - 1) return;
        Runden.Move(idx, idx + 1);
        AktualisiereRundenFehler();
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
                    MitRueckspiel          = r.MitRueckspiel,
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
        AktualisiereRundenFehler();
        IsDirty = true;
    }

    partial void OnRundenartChanged(Rundenart value)
    {
        if (_populiertGerade > 0 || AusgewaehlteRunde is null) return;
        AusgewaehlteRunde.Rundenart = value;
        // RundenEintrag.OnRundenartChanged may have auto-updated the name — sync to form field
        _populiertGerade++;
        try { Name = AusgewaehlteRunde.Name; }
        finally { _populiertGerade--; }
        AktualisiereRundenFehler();
        IsDirty = true;
    }

    partial void OnMitRueckspielChanged(bool value)
    {
        if (_populiertGerade > 0 || AusgewaehlteRunde is null) return;
        AusgewaehlteRunde.MitRueckspiel = value;
        if (!value) AuswaertstorRegelAktiv = false;
        IsDirty = true;
    }

    partial void OnEingangChanged(string value)
    {
        if (_populiertGerade > 0 || AusgewaehlteRunde is null) return;
        AusgewaehlteRunde.EingangsMannschaften = ParseAnzahl(value, 4);
        AktualisiereRundenFehler();
        IsDirty = true;
    }

    partial void OnAusgangChanged(string value)
    {
        if (_populiertGerade > 0 || AusgewaehlteRunde is null) return;
        AusgewaehlteRunde.AusgangsMannschaften = ParseAnzahl(value, 4);
        AktualisiereRundenFehler();
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

    // ── Validierung ───────────────────────────────────────────────────────────

    private void AktualisiereRundenFehler() => RundenFehler = BerechneRundenFehler();

    private IReadOnlyList<string> BerechneRundenFehler()
    {
        if (Runden.Count == 0) return [];

        var fehler = new List<string>();

        for (int i = 0; i < Runden.Count; i++)
        {
            var r = Runden[i];
            bool istLetzte = i == Runden.Count - 1;
            string label = string.IsNullOrWhiteSpace(r.Name) ? $"Runde {i + 1}" : $"\"{r.Name}\"";

            if (string.IsNullOrWhiteSpace(r.Name))
                fehler.Add($"Runde {i + 1}: Name darf nicht leer sein.");

            if (r.EingangsMannschaften < 2)
                fehler.Add($"{label}: Eingang muss mindestens 2 sein.");

            if (r.AusgangsMannschaften < 1)
                fehler.Add($"{label}: Ausgang muss mindestens 1 sein.");

            if (r.AusgangsMannschaften > r.EingangsMannschaften)
                fehler.Add($"{label}: Ausgang ({r.AusgangsMannschaften}) darf den Eingang ({r.EingangsMannschaften}) nicht überschreiten.");

            if (r.Rundenart is Rundenart.KoRunde && r.EingangsMannschaften % 2 != 0)
                fehler.Add($"{label}: K.o.-Runden benötigen eine gerade Eingangsanzahl (aktuell: {r.EingangsMannschaften}).");

            if (istLetzte && r.AusgangsMannschaften != 1)
                fehler.Add($"{label} (letzte Runde): Ausgang muss 1 sein — ein Turnier hat genau einen Sieger.");

            if (!istLetzte)
            {
                var naechste = Runden[i + 1];
                if (r.AusgangsMannschaften != naechste.EingangsMannschaften)
                {
                    string naechsteLabel = string.IsNullOrWhiteSpace(naechste.Name)
                        ? $"Runde {i + 2}" : $"\"{naechste.Name}\"";
                    fehler.Add($"Übergang {label} -> {naechsteLabel}: Ausgang ({r.AusgangsMannschaften}) != Eingang der Folgerunde ({naechste.EingangsMannschaften}).");
                }
            }
        }

        return fehler;
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
            MitRueckspiel          = value.MitRueckspiel;
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
        AktualisiereRundenFehler();
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
            MitRueckspiel          = false;
            AuswaertstorRegelAktiv = false;
        }
        finally { _populiertGerade--; }
    }

    private IReadOnlyList<TurnierRundeEingabe> RundenListeZuEingaben()
        => Runden.Select(r => new TurnierRundeEingabe(
            r.Name, r.Rundenart, r.EingangsMannschaften, r.AusgangsMannschaften,
            r.RegelwerkId, r.MitRueckspiel, r.AuswaertstorRegelAktiv)).ToList();

    private static RundenEintrag RundeZuEintrag(TurnierRunde r) => new()
    {
        Name                   = r.Name,
        Rundenart              = r.Rundenart,
        EingangsMannschaften   = r.EingangsMannschaftsanzahl,
        AusgangsMannschaften   = r.AusgangsMannschaftsanzahl,
        RegelwerkId            = r.RegelwerkId,
        RegelwerkName          = r.Regelwerk?.Name ?? "",
        MitRueckspiel          = r.MitRueckspiel,
        AuswaertstorRegelAktiv = r.AuswaertstorRegelAktiv
    };

    private bool KannSpeichern()          => IsDirty && AusgewaehlteInstanz is not null && !HatRundenFehler;
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
