using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Wpf.Converters;
using Fussballturnierverwaltung.Wpf.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

public record TurnierStatusOption(string Anzeige, TurnierStatus? Wert);
public record TurnierVorlageOption(string Anzeige, TurnierVorlage? Wert);
public record RegelwerkOption(string Anzeige, Regelwerk? Wert);

public class RundenEintrag
{
    public string Name { get; set; } = "";
    public Rundenart Rundenart { get; set; } = Rundenart.Liga;
    public int EingangsMannschaften { get; set; } = 4;
    public int AusgangsMannschaften { get; set; } = 4;
    public int RegelwerkId { get; set; }
    public string RegelwerkName { get; set; } = "";
    public bool AuswaertstorRegelAktiv { get; set; }

    public string Anzeige =>
        $"{Name} · {RundenartZuTextConverter.RundenartAnzeigeName(Rundenart)} · " +
        $"{EingangsMannschaften}→{AusgangsMannschaften}" +
        (AuswaertstorRegelAktiv ? " · Auswärtstor" : "");
}

public partial class TurnierInstanzViewModel : ViewModelBase, IRibbonAware
{
    private readonly TurnierInstanzService _service;
    private readonly TurnierVorlageService _vorlageService;
    private readonly RegelwerkService _regelwerkService;
    private int _populiertGerade;

    private static readonly TurnierStatusOption _alleStatus    = new("— Alle —", null);
    private static readonly TurnierVorlageOption _keineVorlage = new("— Keine —", null);
    private static readonly RegelwerkOption _keinRegelwerk     = new("— Wählen —", null);

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
    private TurnierInstanz? _ausgewaehltes;

    // ── Formular-Zustand ───────────────────────────────────────────────────────
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
    [NotifyPropertyChangedFor(nameof(RundenbauenAktiv))]
    [NotifyPropertyChangedFor(nameof(RundenAnzeigeAktiv))]
    private bool _istNeuModus;

    [ObservableProperty] private bool _istLadend;
    [ObservableProperty] private string? _fehlermeldung;

    // ── Basisdaten ────────────────────────────────────────────────────────────
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _saisonOderJahr = "";
    [ObservableProperty] private TurnierVorlageOption _ausgewaehlteVorlage = _keineVorlage;
    [ObservableProperty] private string _startdatum = "";
    [ObservableProperty] private string _enddatum = "";
    [ObservableProperty] private TurnierStatusOption _ausgewaehlterStatus = new("Geplant", TurnierStatus.Geplant);

    // ── Optionslisten ─────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VorlageOptionen))]
    private ObservableCollection<TurnierVorlage> _turnierVorlagen = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RegelwerkOptionen))]
    private ObservableCollection<Regelwerk> _regelwerke = [];

    // ── Runden (Bearbeiten-Modus: read-only) ──────────────────────────────────
    [ObservableProperty] private ObservableCollection<TurnierRunde> _aktuelleRunden = [];

    // ── Runden (Neu-Modus: editierbar) ────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<RundenEintrag> _rundenListe = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RundeEntfernenCommand))]
    [NotifyCanExecuteChangedFor(nameof(RundeHochCommand))]
    [NotifyCanExecuteChangedFor(nameof(RundeRunterCommand))]
    private RundenEintrag? _markierteRunde;

    // Neue-Runde-Unterformular
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RundeHinzufuegenCommand))]
    [NotifyPropertyChangedFor(nameof(AuswaertstorAktivierbar))]
    private Rundenart _neueRundeRundenart = Rundenart.Liga;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RundeHinzufuegenCommand))]
    private string _neueRundeName = "";

    [ObservableProperty] private string _neueRundeEingang = "4";
    [ObservableProperty] private string _neueRundeAusgang = "4";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RundeHinzufuegenCommand))]
    private RegelwerkOption _neueRundeRegelwerk = _keinRegelwerk;

    [ObservableProperty] private bool _neueRundeAuswaertstorRegel;

    // ── Berechnete Eigenschaften ──────────────────────────────────────────────
    public bool RundenbauenAktiv  => IstNeuModus;
    public bool RundenAnzeigeAktiv => !IstNeuModus;
    public bool AuswaertstorAktivierbar => NeueRundeRundenart == Rundenart.KoRundeMitHinUndRueckspiel;

    public IEnumerable<TurnierInstanz> InstanzenGefiltert => Instanzen
        .Where(i =>
            (string.IsNullOrWhiteSpace(Suchbegriff)
                || i.Name.Contains(Suchbegriff, StringComparison.OrdinalIgnoreCase)
                || i.SaisonOderJahr.Contains(Suchbegriff, StringComparison.OrdinalIgnoreCase))
            && (StatusFilter.Wert is null || i.Status == StatusFilter.Wert));

    public IReadOnlyList<TurnierVorlageOption> VorlageOptionen =>
        [_keineVorlage, .. TurnierVorlagen.Select(v => new TurnierVorlageOption(v.Name, v))];

    public IReadOnlyList<RegelwerkOption> RegelwerkOptionen =>
        [_keinRegelwerk, .. Regelwerke.Select(r => new RegelwerkOption(r.Name, r))];

    public IReadOnlyList<TurnierStatusOption> StatusFilterOptionen =>
        [_alleStatus, .. Enum.GetValues<TurnierStatus>()
            .Select(s => new TurnierStatusOption(TurnierStatusZuTextConverter.StatusAnzeigeName(s), s))];

    public IReadOnlyList<TurnierStatusOption> StatusOptionen =>
        Enum.GetValues<TurnierStatus>()
            .Select(s => new TurnierStatusOption(TurnierStatusZuTextConverter.StatusAnzeigeName(s), s))
            .ToList();

    public IReadOnlyList<Rundenart> RundenartOptionen => Enum.GetValues<Rundenart>().ToList();

    private static readonly HashSet<string> _formFelder =
    [
        nameof(Name), nameof(SaisonOderJahr), nameof(AusgewaehlteVorlage),
        nameof(Startdatum), nameof(Enddatum), nameof(AusgewaehlterStatus)
    ];

    // ── IRibbonAware ───────────────────────────────────────────────────────────
    public string RibbonTabTitel => "Turniere";
    public ICommand NeuBefehl => NeuCommand;
    public ICommand SpeichernBefehl => SpeichernCommand;
    public ICommand AbbrechenBefehl => AbbrechenCommand;
    public ICommand LoeschenBefehl => LoeschenCommand;

    public TurnierInstanzViewModel(
        TurnierInstanzService service,
        TurnierVorlageService vorlageService,
        RegelwerkService regelwerkService)
    {
        _service = service;
        _vorlageService = vorlageService;
        _regelwerkService = regelwerkService;
    }

    public override async Task AktivierenAsync() => await LadenAsync();

    private async Task LadenAsync()
    {
        IstLadend = true;
        try
        {
            var instanzen  = await _service.AlleAbrufenAsync();
            var vorlagen   = await _vorlageService.AlleAbrufenAsync();
            var regelwerke = await _regelwerkService.AlleAbrufenAsync();
            Instanzen       = new ObservableCollection<TurnierInstanz>(instanzen);
            TurnierVorlagen = new ObservableCollection<TurnierVorlage>(vorlagen);
            Regelwerke      = new ObservableCollection<Regelwerk>(regelwerke);
        }
        finally
        {
            IstLadend = false;
        }
    }

    private void FormularAusFuellenMit(TurnierInstanz? instanz)
    {
        _populiertGerade++;
        try
        {
            if (instanz is null)
            {
                FormularAktiv = false;
                IstNeuModus   = false;
                Name = SaisonOderJahr = Startdatum = Enddatum = "";
                AusgewaehlteVorlage   = _keineVorlage;
                AusgewaehlterStatus   = StatusOptionen.First(o => o.Wert == TurnierStatus.Geplant);
                AktuelleRunden        = [];
            }
            else
            {
                FormularAktiv       = true;
                IstNeuModus         = false;
                Name                = instanz.Name;
                SaisonOderJahr      = instanz.SaisonOderJahr;
                Startdatum          = instanz.Startdatum?.Year.ToString() ?? "";
                Enddatum            = instanz.Enddatum?.Year.ToString() ?? "";
                AusgewaehlteVorlage = VorlageOptionen.FirstOrDefault(o => o.Wert?.Id == instanz.TurnierVorlageId)
                    ?? _keineVorlage;
                AusgewaehlterStatus = StatusOptionen.FirstOrDefault(o => o.Wert == instanz.Status)
                    ?? StatusOptionen.First();
                AktuelleRunden = new ObservableCollection<TurnierRunde>(
                    instanz.Runden.OrderBy(r => r.Reihenfolge));
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
            AusgewaehlteVorlage   = _keineVorlage;
            AusgewaehlterStatus   = StatusOptionen.First(o => o.Wert == TurnierStatus.Geplant);
            RundenListe           = [];
            MarkierteRunde        = null;
            ResetNeueRundeFormular();
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
            var vorlageId  = AusgewaehlteVorlage.Wert?.Id;
            var status     = AusgewaehlterStatus.Wert ?? TurnierStatus.Geplant;

            if (IstNeuModus)
            {
                var rundenEingaben = RundenListe.Select((r, i) => new TurnierRundeEingabe(
                    r.Name, r.Rundenart, r.EingangsMannschaften, r.AusgangsMannschaften,
                    r.RegelwerkId, r.AuswaertstorRegelAktiv)).ToList();

                var instanzEingabe = new TurnierInstanzErstellenEingabe(
                    Name, SaisonOderJahr, vorlageId, startdatum, enddatum);
                var neu = await _service.ErstellenMitRundenAsync(instanzEingabe, rundenEingaben);
                await LadenAsync();
                Ausgewaehltes = Instanzen.FirstOrDefault(i => i.Id == neu.Id);
            }
            else if (Ausgewaehltes is not null)
            {
                var id = Ausgewaehltes.Id;
                var eingabe = new TurnierInstanzVollAktualisierenEingabe(
                    Name, SaisonOderJahr, vorlageId, startdatum, enddatum, status);
                await _service.VollAktualisierenAsync(id, eingabe);
                await LadenAsync();
                Ausgewaehltes = Instanzen.FirstOrDefault(i => i.Id == id);
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

    // ── Runden-Befehle (Neu-Modus) ────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(KannRundeHinzufuegen))]
    private void RundeHinzufuegen()
    {
        var eingang  = ParseAnzahl(NeueRundeEingang, 4);
        var ausgang  = ParseAnzahl(NeueRundeAusgang, 4);
        var regelwerk = NeueRundeRegelwerk.Wert;
        RundenListe.Add(new RundenEintrag
        {
            Name                   = NeueRundeName.Trim(),
            Rundenart              = NeueRundeRundenart,
            EingangsMannschaften   = eingang,
            AusgangsMannschaften   = ausgang,
            RegelwerkId            = regelwerk?.Id ?? 0,
            RegelwerkName          = regelwerk?.Name ?? "",
            AuswaertstorRegelAktiv = NeueRundeAuswaertstorRegel
        });
        MarkierteRunde = RundenListe.Last();
        ResetNeueRundeFormular();
        if (_populiertGerade == 0) IsDirty = true;
    }

    [RelayCommand(CanExecute = nameof(KannRundeEntfernen))]
    private void RundeEntfernen()
    {
        if (MarkierteRunde is null) return;
        var idx = RundenListe.IndexOf(MarkierteRunde);
        RundenListe.RemoveAt(idx);
        MarkierteRunde = RundenListe.Count > 0 ? RundenListe[Math.Max(0, idx - 1)] : null;
        if (_populiertGerade == 0) IsDirty = true;
    }

    [RelayCommand(CanExecute = nameof(KannRundeHoch))]
    private void RundeHoch()
    {
        if (MarkierteRunde is null) return;
        var idx = RundenListe.IndexOf(MarkierteRunde);
        if (idx <= 0) return;
        RundenListe.Move(idx, idx - 1);
        if (_populiertGerade == 0) IsDirty = true;
    }

    [RelayCommand(CanExecute = nameof(KannRundeRunter))]
    private void RundeRunter()
    {
        if (MarkierteRunde is null) return;
        var idx = RundenListe.IndexOf(MarkierteRunde);
        if (idx < 0 || idx >= RundenListe.Count - 1) return;
        RundenListe.Move(idx, idx + 1);
        if (_populiertGerade == 0) IsDirty = true;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(Ausgewaehltes))
            _ = BeiAuswaehlungsAenderungAsync();
        if (e.PropertyName == nameof(NeueRundeRundenart) && !AuswaertstorAktivierbar)
            NeueRundeAuswaertstorRegel = false;
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

    private void ResetNeueRundeFormular()
    {
        NeueRundeName              = "";
        NeueRundeRundenart         = Rundenart.Liga;
        NeueRundeEingang           = "4";
        NeueRundeAusgang           = "4";
        NeueRundeRegelwerk         = _keinRegelwerk;
        NeueRundeAuswaertstorRegel = false;
    }

    private bool KannSpeichern()       => FormularAktiv && IsDirty;
    private bool KannAbbrechen()       => FormularAktiv && IsDirty;
    private bool KannLoeschen()        => Ausgewaehltes is not null && !IstNeuModus;
    private bool KannRundeHinzufuegen() =>
        !string.IsNullOrWhiteSpace(NeueRundeName) && NeueRundeRegelwerk.Wert is not null;
    private bool KannRundeEntfernen()  => MarkierteRunde is not null;
    private bool KannRundeHoch()       => MarkierteRunde is not null
        && RundenListe.IndexOf(MarkierteRunde) > 0;
    private bool KannRundeRunter()     => MarkierteRunde is not null
        && RundenListe.IndexOf(MarkierteRunde) < RundenListe.Count - 1;

    private static int ParseAnzahl(string text, int fallback)
        => int.TryParse(text.Trim(), out var n) && n >= 1 ? n : fallback;

    private static DateOnly? ParseJahr(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        return int.TryParse(text.Trim(), out var jahr) && jahr >= 1800 && jahr <= 2100
            ? new DateOnly(jahr, 1, 1)
            : null;
    }
}
