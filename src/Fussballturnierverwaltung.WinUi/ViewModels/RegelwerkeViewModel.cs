using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

public record RankingSetOption(string Anzeige, RankingRegelSet? Wert);

public partial class RegelwerkeViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly RegelwerkService _service;
    private readonly IDialogService _dialogService;
    private int _populiertGerade;

    private static readonly RankingSetOption _keinRankingSet = new("— Keines —", null);

    // ── Liste ─────────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RegelwerkeGefiltert))]
    private ObservableCollection<Regelwerk> _regelwerke = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RegelwerkeGefiltert))]
    private string _suchbegriff = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private Regelwerk? _ausgewaehltes;

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
    [ObservableProperty] private string _beschreibung = "";
    [ObservableProperty] private string _gueltigVon = "";
    [ObservableProperty] private string _gueltigBis = "";

    // ── Punktesystem ──────────────────────────────────────────────────────────
    [ObservableProperty] private int _punkteSieg = 3;
    [ObservableProperty] private int _punkteUnentschieden = 1;
    [ObservableProperty] private int _punkteNiederlage = 0;

    // ── Entscheidungsverfahren ────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerfuegbareVerfahren))]
    private ObservableCollection<VerfahrenArt> _ausgewaehlteVerfahren = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VerfahrenHinzufuegenCommand))]
    private VerfahrenArt? _neuesVerfahren;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VerfahrenEntfernenCommand))]
    [NotifyCanExecuteChangedFor(nameof(VerfahrenHochCommand))]
    [NotifyCanExecuteChangedFor(nameof(VerfahrenRunterCommand))]
    private VerfahrenArt? _markiertesVerfahren;

    // ── Ranking-Regelset ──────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RankingSetOptionen))]
    private ObservableCollection<RankingRegelSet> _rankingRegelSets = [];

    [ObservableProperty] private RankingSetOption _ausgewaehltesRankingSet = _keinRankingSet;

    // ── Berechnete Listen ─────────────────────────────────────────────────────
    public IEnumerable<Regelwerk> RegelwerkeGefiltert => Regelwerke
        .Where(r => string.IsNullOrWhiteSpace(Suchbegriff)
            || r.Name.Contains(Suchbegriff, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<VerfahrenArt> VerfuegbareVerfahren =>
        Enum.GetValues<VerfahrenArt>().Except(AusgewaehlteVerfahren);

    public IReadOnlyList<RankingSetOption> RankingSetOptionen =>
        [_keinRankingSet, .. RankingRegelSets.Select(s => new RankingSetOption(s.Name, s))];

    private static readonly HashSet<string> _formFelder =
    [
        nameof(Name), nameof(Beschreibung), nameof(GueltigVon), nameof(GueltigBis),
        nameof(PunkteSieg), nameof(PunkteUnentschieden), nameof(PunkteNiederlage),
        nameof(AusgewaehltesRankingSet)
    ];

    // ── IRibbonAware ──────────────────────────────────────────────────────────
    public string RibbonTabTitel => "Regelwerke";
    public ICommand? NeuBefehl => NeuCommand;
    public ICommand? SpeichernBefehl => SpeichernCommand;
    public ICommand? AbbrechenBefehl => AbbrechenCommand;
    public ICommand? LoeschenBefehl => LoeschenCommand;

    public RegelwerkeViewModel(RegelwerkService service, IDialogService dialogService)
    {
        _service = service;
        _dialogService = dialogService;
    }

    public override async Task AktivierenAsync() => await LadenAsync();

    private async Task LadenAsync()
    {
        IstLadend = true;
        try
        {
            var regelwerke = await _service.AlleAbrufenAsync();
            var rankingSets = await _service.AlleRankingRegelSetsAbrufenAsync();
            Regelwerke = new ObservableCollection<Regelwerk>(regelwerke);
            RankingRegelSets = new ObservableCollection<RankingRegelSet>(rankingSets);
        }
        finally
        {
            IstLadend = false;
        }
    }

    partial void OnAusgewaehltesChanged(Regelwerk? value) => FormularAusFuellenMit(value);

    private void FormularAusFuellenMit(Regelwerk? regelwerk)
    {
        _populiertGerade++;
        try
        {
            if (regelwerk is null)
            {
                FormularAktiv = false;
                IstNeuModus = false;
                Name = Beschreibung = GueltigVon = GueltigBis = "";
                PunkteSieg = 3;
                PunkteUnentschieden = 1;
                PunkteNiederlage = 0;
                AusgewaehlteVerfahren = [];
                AusgewaehltesRankingSet = _keinRankingSet;
            }
            else
            {
                FormularAktiv = true;
                IstNeuModus = false;
                Name = regelwerk.Name;
                Beschreibung = regelwerk.Beschreibung;
                GueltigVon = regelwerk.GueltigVon?.Year.ToString() ?? "";
                GueltigBis = regelwerk.GueltigBis?.Year.ToString() ?? "";
                var p = regelwerk.PunkteRegeln.FirstOrDefault();
                PunkteSieg = p?.PunkteFuerSieg ?? 3;
                PunkteUnentschieden = p?.PunkteFuerUnentschieden ?? 1;
                PunkteNiederlage = p?.PunkteFuerNiederlage ?? 0;
                AusgewaehlteVerfahren = new ObservableCollection<VerfahrenArt>(
                    regelwerk.Entscheidungsverfahren
                        .OrderBy(e => e.Reihenfolge)
                        .Select(e => e.VerfahrenArt));
                AusgewaehltesRankingSet = RankingSetOptionen
                    .FirstOrDefault(o => o.Wert?.Id == regelwerk.RankingRegelSetId)
                    ?? _keinRankingSet;
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
            Name = Beschreibung = GueltigVon = GueltigBis = "";
            PunkteSieg = 3;
            PunkteUnentschieden = 1;
            PunkteNiederlage = 0;
            AusgewaehlteVerfahren = [];
            NeuesVerfahren = null;
            AusgewaehltesRankingSet = _keinRankingSet;
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
            var gueltigVon = ParseJahr(GueltigVon);
            var gueltigBis = ParseJahr(GueltigBis);
            var verfahren = AusgewaehlteVerfahren.ToList();

            if (IstNeuModus)
            {
                var eingabe = new RegelwerkErstellenEingabe(
                    Name, Beschreibung, gueltigVon, gueltigBis,
                    AusgewaehltesRankingSet.Wert?.Id,
                    PunkteSieg, PunkteUnentschieden, PunkteNiederlage,
                    verfahren);
                var neu = await _service.ErstellenAsync(eingabe);
                await LadenAsync();
                Ausgewaehltes = Regelwerke.FirstOrDefault(r => r.Id == neu.Id);
            }
            else if (Ausgewaehltes is not null)
            {
                var id = Ausgewaehltes.Id;
                var eingabe = new RegelwerkAktualisierenEingabe(
                    Name, Beschreibung, gueltigVon, gueltigBis,
                    AusgewaehltesRankingSet.Wert?.Id,
                    PunkteSieg, PunkteUnentschieden, PunkteNiederlage,
                    verfahren);
                await _service.AktualisierenAsync(id, eingabe);
                await LadenAsync();
                Ausgewaehltes = Regelwerke.FirstOrDefault(r => r.Id == id);
            }
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
            "Regelwerk löschen", $"Soll '{Ausgewaehltes.Name}' wirklich gelöscht werden?");
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

    // ── Verfahren-Befehle ─────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(KannVerfahrenHinzufuegen))]
    private void VerfahrenHinzufuegen()
    {
        if (NeuesVerfahren is null) return;
        AusgewaehlteVerfahren.Add(NeuesVerfahren.Value);
        MarkiertesVerfahren = NeuesVerfahren;
        NeuesVerfahren = null;
        OnPropertyChanged(nameof(VerfuegbareVerfahren));
        if (_populiertGerade == 0) IsDirty = true;
    }

    [RelayCommand(CanExecute = nameof(KannVerfahrenEntfernen))]
    private void VerfahrenEntfernen()
    {
        if (MarkiertesVerfahren is null) return;
        var idx = AusgewaehlteVerfahren.IndexOf(MarkiertesVerfahren.Value);
        AusgewaehlteVerfahren.RemoveAt(idx);
        MarkiertesVerfahren = AusgewaehlteVerfahren.Count > 0
            ? AusgewaehlteVerfahren[Math.Max(0, idx - 1)]
            : null;
        OnPropertyChanged(nameof(VerfuegbareVerfahren));
        if (_populiertGerade == 0) IsDirty = true;
    }

    [RelayCommand(CanExecute = nameof(KannVerfahrenHoch))]
    private void VerfahrenHoch()
    {
        if (MarkiertesVerfahren is null) return;
        var idx = AusgewaehlteVerfahren.IndexOf(MarkiertesVerfahren.Value);
        if (idx <= 0) return;
        AusgewaehlteVerfahren.Move(idx, idx - 1);
        if (_populiertGerade == 0) IsDirty = true;
    }

    [RelayCommand(CanExecute = nameof(KannVerfahrenRunter))]
    private void VerfahrenRunter()
    {
        if (MarkiertesVerfahren is null) return;
        var idx = AusgewaehlteVerfahren.IndexOf(MarkiertesVerfahren.Value);
        if (idx < 0 || idx >= AusgewaehlteVerfahren.Count - 1) return;
        AusgewaehlteVerfahren.Move(idx, idx + 1);
        if (_populiertGerade == 0) IsDirty = true;
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
    private bool KannLoeschen() => Ausgewaehltes is not null && !IstNeuModus;
    private bool KannVerfahrenHinzufuegen() => NeuesVerfahren.HasValue;
    private bool KannVerfahrenEntfernen() => MarkiertesVerfahren.HasValue;
    private bool KannVerfahrenHoch() => MarkiertesVerfahren.HasValue
        && AusgewaehlteVerfahren.IndexOf(MarkiertesVerfahren.Value) > 0;
    private bool KannVerfahrenRunter() => MarkiertesVerfahren.HasValue
        && AusgewaehlteVerfahren.IndexOf(MarkiertesVerfahren.Value) < AusgewaehlteVerfahren.Count - 1;

    private static DateOnly? ParseJahr(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        return int.TryParse(text.Trim(), out var jahr) && jahr >= 1800 && jahr <= 2100
            ? new DateOnly(jahr, 1, 1)
            : null;
    }
}
