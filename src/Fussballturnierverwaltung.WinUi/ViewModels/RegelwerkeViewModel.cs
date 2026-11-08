using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Diagnose;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
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

public record RankingSetOption(string Anzeige, RankingRegelSet? Wert);

/// <summary>Ein Kriterium des gewählten Ranking-Regelsets in der Reihenfolge, in der es die Tabelle entscheidet (#354).</summary>
public record RangfolgeKriterium(string Nummer, string Name, string Beschreibung);

public partial class RegelwerkeViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly RegelwerkService _service;
    private readonly RegelwerkValidator _validator;
    private readonly IDialogService _dialogService;
    private readonly ILogger<RegelwerkeViewModel> _logger;
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

    /// <summary>Live-Prüfung mit demselben Validator wie beim Speichern (#602); sperrt Speichern, solange sie Fehler
    /// enthält.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    private IReadOnlyList<Feldfehler> _formularFehler = [];

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RangfolgeKriterien))]
    [NotifyPropertyChangedFor(nameof(HatKeinRankingSet))]
    [NotifyPropertyChangedFor(nameof(DirektVergleichHinweis))]
    [NotifyPropertyChangedFor(nameof(HatDirektVergleichHinweis))]
    private RankingSetOption _ausgewaehltesRankingSet = _keinRankingSet;

    // ── Berechnete Listen ─────────────────────────────────────────────────────
    public IEnumerable<Regelwerk> RegelwerkeGefiltert => Regelwerke
        .Where(r => string.IsNullOrWhiteSpace(Suchbegriff)
            || r.Name.Contains(Suchbegriff, StringComparison.OrdinalIgnoreCase));

    // Wird in OnPropertyChanged mit RegelwerkeGefiltert nachgezogen (alle Quellen automatisch).
    public string AnzahlText => DatensatzAnzahl.Text(
        RegelwerkeGefiltert.Count(), Regelwerke.Count, !string.IsNullOrWhiteSpace(Suchbegriff),
        "Regelwerk", "Regelwerke");

    public IEnumerable<VerfahrenArt> VerfuegbareVerfahren =>
        Enum.GetValues<VerfahrenArt>().Except(AusgewaehlteVerfahren);

    public IReadOnlyList<RankingSetOption> RankingSetOptionen =>
        [_keinRankingSet, .. RankingRegelSets.Select(s => new RankingSetOption(s.Name, s))];

    // Nur zur Anzeige (#354): bisher zeigte die Seite nur den Namen des Sets, nicht, wonach es sortiert. Die Sets
    // kommen samt Einträgen und Regeln aus AlleRankingRegelSetsAbrufenAsync. Null-sicher, weil eine ComboBox beim
    // ersten Laden null zurückschreiben kann.
    public IReadOnlyList<RangfolgeKriterium> RangfolgeKriterien =>
        [.. (AusgewaehltesRankingSet?.Wert?.Eintraege ?? [])
            .OrderBy(e => e.Reihenfolge)
            .Select((e, i) => new RangfolgeKriterium($"{i + 1}.", e.RankingRegel.Name, e.RankingRegel.Beschreibung))];

    public bool HatKeinRankingSet => AusgewaehltesRankingSet?.Wert is null;

    /// <summary>Wie der direkte Vergleich gleichstehender Teams rechnet (#628) – nur bei Sets, die ihn enthalten.</summary>
    public string? DirektVergleichHinweis => DirekterVergleich.Hinweis(AusgewaehltesRankingSet?.Wert);

    public bool HatDirektVergleichHinweis => DirektVergleichHinweis is not null;

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

    public RegelwerkeViewModel(
        RegelwerkService service, RegelwerkValidator validator, IDialogService dialogService,
        ILogger<RegelwerkeViewModel> logger)
    {
        _service = service;
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
        AktualisiereFormularFehler();
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
        AktualisiereFormularFehler();
    }

    [RelayCommand(CanExecute = nameof(KannSpeichern))]
    private async Task Speichern()
    {
        // Als Vorgang, weil die Seite danach neu lädt: Ladeanzeige und „Bereit“ für GUI-Tests folgen nur offenen
        // Vorgängen (#597, Muster aus #535)
        using var vorgang = _logger.Vorgang($"Regelwerke: speichern ({Name})", "Regelwerk wird gespeichert …");
        Fehlermeldung = null;
        try
        {
            var eingabe = Eingabe();
            if (IstNeuModus)
            {
                var neu = await _service.ErstellenAsync(eingabe);
                await LadenAsync();
                Ausgewaehltes = Regelwerke.FirstOrDefault(r => r.Id == neu.Id);
            }
            else if (Ausgewaehltes is not null)
            {
                var id = Ausgewaehltes.Id;
                await _service.AktualisierenAsync(id, new RegelwerkAktualisierenEingabe(
                    eingabe.Name, eingabe.Beschreibung, eingabe.GueltigVon, eingabe.GueltigBis,
                    eingabe.RankingRegelSetId, eingabe.PunkteFuerSieg, eingabe.PunkteFuerUnentschieden,
                    eingabe.PunkteFuerNiederlage, eingabe.EntscheidungsVerfahrenReihenfolge));
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
        using var vorgang = _logger.Vorgang($"Regelwerke: löschen ({Ausgewaehltes.Name})", "Regelwerk wird gelöscht …");
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
        VerfahrenGeaendert();
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
        VerfahrenGeaendert();
    }

    [RelayCommand(CanExecute = nameof(KannVerfahrenHoch))]
    private void VerfahrenHoch()
    {
        if (MarkiertesVerfahren is null) return;
        var idx = AusgewaehlteVerfahren.IndexOf(MarkiertesVerfahren.Value);
        if (idx <= 0) return;
        AusgewaehlteVerfahren.Move(idx, idx - 1);
        VerfahrenGeaendert();
    }

    [RelayCommand(CanExecute = nameof(KannVerfahrenRunter))]
    private void VerfahrenRunter()
    {
        if (MarkiertesVerfahren is null) return;
        var idx = AusgewaehlteVerfahren.IndexOf(MarkiertesVerfahren.Value);
        if (idx < 0 || idx >= AusgewaehlteVerfahren.Count - 1) return;
        AusgewaehlteVerfahren.Move(idx, idx + 1);
        VerfahrenGeaendert();
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(RegelwerkeGefiltert)) OnPropertyChanged(nameof(AnzahlText));
        if (_populiertGerade == 0 && _formFelder.Contains(e.PropertyName!))
        {
            IsDirty = true;
            AktualisiereFormularFehler();
        }
    }

    // ── Live-Prüfung (#602) ───────────────────────────────────────────────────

    private void VerfahrenGeaendert()
    {
        if (_populiertGerade != 0) return;
        IsDirty = true;
        AktualisiereFormularFehler();
    }

    private RegelwerkErstellenEingabe Eingabe() => new(
        Name, Beschreibung, ParseJahr(GueltigVon), ParseJahr(GueltigBis),
        AusgewaehltesRankingSet.Wert?.Id,
        PunkteSieg, PunkteUnentschieden, PunkteNiederlage,
        [.. AusgewaehlteVerfahren]);

    private void AktualisiereFormularFehler()
    {
        if (!FormularAktiv) { FormularFehler = []; return; }
        FormularFehler =
        [
            .. JahrPruefen(GueltigVon, "Gültig von", nameof(RegelwerkErstellenEingabe.GueltigVon)),
            .. JahrPruefen(GueltigBis, "Gültig bis", nameof(RegelwerkErstellenEingabe.GueltigBis)),
            .. _validator.Validieren(Eingabe()).Meldungen,
        ];
    }

    /// <summary>Ein Jahr, das <see cref="ParseJahr"/> nicht versteht, ginge beim Speichern still verloren (leer statt
    /// des Eingegebenen) – darum ist es ein Fehler.</summary>
    private static IEnumerable<Feldfehler> JahrPruefen(string text, string bezeichnung, string feld)
    {
        if (!string.IsNullOrWhiteSpace(text) && ParseJahr(text) is null)
            yield return new($"{bezeichnung}: ein Jahr zwischen 1800 und 2100 eingeben (oder leer lassen).", feld);
    }

    private bool KannSpeichern() => FormularAktiv && IsDirty && FormularFehler.Count == 0;
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
