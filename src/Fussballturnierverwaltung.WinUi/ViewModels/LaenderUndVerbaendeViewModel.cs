using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Stammdaten.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.Helpers;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

public record KontinentOption(string Anzeige, Kontinent? Wert);

/// <summary>Auswahl der Rangliste eines Landes oder Verbands (#572); <see cref="Id"/> null = keine.</summary>
public record RanglistenOption(string Anzeige, int? Id);

public partial class LaenderUndVerbaendeViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly LandService _landService;
    private readonly VerbandService _verbandService;
    private readonly RanglisteService _ranglisteService;
    private readonly IDialogService _dialogService;
    private int _populiertGerade;

    private static readonly VerbandFilterOption AllesVerband = new("— Alle —", null);
    private static readonly KontinentOption AllesKontinent = new("— Alle —", null);
    private static readonly RanglistenOption KeineRangliste = new("— keine —", null);

    private static readonly IReadOnlyList<KontinentOption> _kontinentFormularOptionen =
        Enum.GetValues<Kontinent>()
            .Select(k => new KontinentOption(KontinentAnzeigeName(k), k))
            .ToList();

    // ── Aktiver Tab ───────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private int _aktiverTabIndex;

    [ObservableProperty] private bool _istLadend;
    [ObservableProperty] private string? _fehlermeldung;

    // ── Verbände ─────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerbaendeGefiltert))]
    [NotifyPropertyChangedFor(nameof(VerbandFilterOptionen))]
    private ObservableCollection<Verband> _verbaende = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerbaendeGefiltert))]
    private string _verbandSuchbegriff = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private Verband? _ausgewaehlterVerband;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private bool _verbandFormularAktiv;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    private bool _verbandIsDirty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private bool _verbandIstNeuModus;

    [ObservableProperty] private string _verbandName = "";
    [ObservableProperty] private string _verbandKurzname = "";
    [ObservableProperty] private byte[]? _verbandLogoDaten;
    [ObservableProperty] private RanglistenOption? _verbandRangliste = KeineRangliste;

    // ── Länder ───────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LaenderGefiltert))]
    private ObservableCollection<Land> _laender = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LaenderGefiltert))]
    private string _landSuchbegriff = "";

    // Nullable, obwohl fachlich nie "leer": Bindet eine WinUI-ComboBox TwoWay an diese Property, setzt sie
    // SelectedItem beim Austausch der ItemsSource (VerbandFilterOptionen ändert sich mit Verbaende-Reload)
    // kurzzeitig auf null zurück, bevor sie den passenden Eintrag wiederfindet — die On...Changed-Hooks
    // unten fangen das ab und stellen sofort die "Alle"-Sentinel wieder her (Fund aus #281-Test).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LaenderGefiltert))]
    private VerbandFilterOption? _landFilterVerbandOption = AllesVerband;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LaenderGefiltert))]
    private KontinentOption? _landFilterKontinentOption = AllesKontinent;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private Land? _ausgewaehltesLand;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private bool _landFormularAktiv;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    private bool _landIsDirty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private bool _landIstNeuModus;

    [ObservableProperty] private string _landName = "";
    [ObservableProperty] private string _landKurzname = "";
    [ObservableProperty] private string _landFifaCode = "";
    [ObservableProperty] private KontinentOption _landKontinentOption = _kontinentFormularOptionen[0];
    [ObservableProperty] private Verband? _landVerband;
    [ObservableProperty] private byte[]? _landFlaggeDaten;
    [ObservableProperty] private RanglistenOption? _landRangliste = KeineRangliste;

    // Ranglisten zur Auswahl an Land und Verband (#572), „keine“ zuerst.
    [ObservableProperty] private IReadOnlyList<RanglistenOption> _ranglistenOptionen = [KeineRangliste];

    // ── Berechnete Listen ─────────────────────────────────────────────────────
    public IEnumerable<Land> LaenderGefiltert => Laender
        .Where(l => string.IsNullOrWhiteSpace(LandSuchbegriff)
            || l.Name.Contains(LandSuchbegriff, StringComparison.OrdinalIgnoreCase)
            || l.Kurzname.Contains(LandSuchbegriff, StringComparison.OrdinalIgnoreCase)
            || l.FifaCode.Contains(LandSuchbegriff, StringComparison.OrdinalIgnoreCase))
        .Where(l => LandFilterVerbandOption?.Wert == null || l.VerbandId == LandFilterVerbandOption.Wert.Id)
        .Where(l => LandFilterKontinentOption?.Wert == null || l.Kontinent == LandFilterKontinentOption.Wert);

    public IEnumerable<Verband> VerbaendeGefiltert => Verbaende
        .Where(v => string.IsNullOrWhiteSpace(VerbandSuchbegriff)
            || v.Name.Contains(VerbandSuchbegriff, StringComparison.OrdinalIgnoreCase)
            || v.Kurzname.Contains(VerbandSuchbegriff, StringComparison.OrdinalIgnoreCase));

    // Werden in OnPropertyChanged mit der jeweiligen ...Gefiltert-Liste nachgezogen (alle Quellen automatisch).
    public string LaenderAnzahlText => DatensatzAnzahl.Text(
        LaenderGefiltert.Count(), Laender.Count,
        !string.IsNullOrWhiteSpace(LandSuchbegriff) || LandFilterVerbandOption?.Wert != null
            || LandFilterKontinentOption?.Wert != null,
        "Land", "Länder");

    public string VerbaendeAnzahlText => DatensatzAnzahl.Text(
        VerbaendeGefiltert.Count(), Verbaende.Count, !string.IsNullOrWhiteSpace(VerbandSuchbegriff),
        "Verband", "Verbände");

    public IReadOnlyList<VerbandFilterOption> VerbandFilterOptionen =>
        [AllesVerband, .. Verbaende.Select(v => new VerbandFilterOption(v.Name, v))];

    public IReadOnlyList<KontinentOption> KontinentFilterOptionen =>
        [AllesKontinent, .. _kontinentFormularOptionen];

    public IReadOnlyList<KontinentOption> KontinentFormularOptionen => _kontinentFormularOptionen;

    public static string KontinentAnzeigeName(Kontinent k) => k switch
    {
        Kontinent.Europa               => "Europa",
        Kontinent.Suedamerika          => "Südamerika",
        Kontinent.NordUndMittelamerika => "Nord- & Mittelamerika",
        Kontinent.Afrika               => "Afrika",
        Kontinent.Asien                => "Asien",
        Kontinent.Ozeanien             => "Ozeanien",
        _                              => k.ToString()
    };

    private static readonly HashSet<string> _landFormFelder =
    [
        nameof(LandName), nameof(LandKurzname), nameof(LandFifaCode),
        nameof(LandKontinentOption), nameof(LandVerband), nameof(LandFlaggeDaten), nameof(LandRangliste)
    ];

    private static readonly HashSet<string> _verbandFormFelder =
    [
        nameof(VerbandName), nameof(VerbandKurzname), nameof(VerbandLogoDaten), nameof(VerbandRangliste)
    ];

    // ── IRibbonAware ──────────────────────────────────────────────────────────
    public string RibbonTabTitel => "Länder & Verbände";
    public ICommand? NeuBefehl => NeuCommand;
    public ICommand? SpeichernBefehl => SpeichernCommand;
    public ICommand? AbbrechenBefehl => AbbrechenCommand;
    public ICommand? LoeschenBefehl => LoeschenCommand;

    public LaenderUndVerbaendeViewModel(
        LandService landService, VerbandService verbandService, RanglisteService ranglisteService,
        IDialogService dialogService)
    {
        _landService = landService;
        _verbandService = verbandService;
        _ranglisteService = ranglisteService;
        _dialogService = dialogService;
    }

    public override async Task AktivierenAsync() => await LadenAsync();

    private async Task LadenAsync()
    {
        IstLadend = true;
        try
        {
            var laender = await _landService.AlleAbrufenMitVerbandAsync();
            var verbaende = await _verbandService.AlleAbrufenAsync();
            var ranglisten = await _ranglisteService.AlleOhneEintraegeAbrufenAsync();

            RanglistenOptionen = [KeineRangliste, .. ranglisten.OrderBy(r => r.Name).Select(r => new RanglistenOption(r.Name, r.Id))];

            Laender = new ObservableCollection<Land>(laender.OrderBy(l => l.Name));
            Verbaende = new ObservableCollection<Verband>(verbaende.OrderBy(v => v.Name));
        }
        finally
        {
            IstLadend = false;
        }
    }

    // Null-Guards: siehe Kommentar an _landFilterVerbandOption (WinUI-ComboBox-ItemsSource-Reset).
    partial void OnLandFilterVerbandOptionChanged(VerbandFilterOption? value)
    {
        if (value is null) LandFilterVerbandOption = AllesVerband;
    }

    partial void OnLandFilterKontinentOptionChanged(KontinentOption? value)
    {
        if (value is null) LandFilterKontinentOption = AllesKontinent;
    }

    partial void OnAusgewaehltesLandChanged(Land? value) => LandFormularAusFuellenMit(value);
    partial void OnAusgewaehlterVerbandChanged(Verband? value) => VerbandFormularAusFuellenMit(value);

    private void LandFormularAusFuellenMit(Land? land)
    {
        _populiertGerade++;
        try
        {
            if (land is null)
            {
                LandFormularAktiv = false;
                LandIstNeuModus = false;
                LandName = LandKurzname = LandFifaCode = "";
                LandKontinentOption = _kontinentFormularOptionen[0];
                LandVerband = null;
                LandFlaggeDaten = null;
                LandRangliste = KeineRangliste;
            }
            else
            {
                LandFormularAktiv = true;
                LandIstNeuModus = false;
                LandName = land.Name;
                LandKurzname = land.Kurzname;
                LandFifaCode = land.FifaCode;
                LandKontinentOption = _kontinentFormularOptionen
                    .FirstOrDefault(k => k.Wert == land.Kontinent) ?? _kontinentFormularOptionen[0];
                LandVerband = Verbaende.FirstOrDefault(v => v.Id == land.VerbandId);
                LandFlaggeDaten = land.Flagge;
                LandRangliste = RanglisteOptionFuer(land.RanglisteId);
            }
            LandIsDirty = false;
            Fehlermeldung = null;
        }
        finally
        {
            _populiertGerade--;
        }
    }

    private RanglistenOption RanglisteOptionFuer(int? ranglisteId) =>
        RanglistenOptionen.FirstOrDefault(o => o.Id == ranglisteId) ?? KeineRangliste;

    private void VerbandFormularAusFuellenMit(Verband? verband)
    {
        _populiertGerade++;
        try
        {
            if (verband is null)
            {
                VerbandFormularAktiv = false;
                VerbandIstNeuModus = false;
                VerbandName = VerbandKurzname = "";
                VerbandLogoDaten = null;
                VerbandRangliste = KeineRangliste;
            }
            else
            {
                VerbandFormularAktiv = true;
                VerbandIstNeuModus = false;
                VerbandName = verband.Name;
                VerbandKurzname = verband.Kurzname;
                VerbandLogoDaten = verband.Logo;
                VerbandRangliste = RanglisteOptionFuer(verband.RanglisteId);
            }
            VerbandIsDirty = false;
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
        if (AktiverTabIndex == 0) NeuLand();
        else NeuVerband();
    }

    private void NeuLand()
    {
        _populiertGerade++;
        try
        {
            AusgewaehltesLand = null;
            LandFormularAktiv = true;
            LandIstNeuModus = true;
            LandName = LandKurzname = LandFifaCode = "";
            LandKontinentOption = _kontinentFormularOptionen[0];
            LandVerband = Verbaende.FirstOrDefault();
            LandFlaggeDaten = null;
            LandRangliste = KeineRangliste;
            Fehlermeldung = null;
        }
        finally
        {
            _populiertGerade--;
        }
        LandIsDirty = true;
    }

    [RelayCommand]
    private async Task LandFlaggeWaehlenAsync()
    {
        var daten = await _dialogService.BilddateiWaehlenAsync();
        if (daten is not null) LandFlaggeDaten = daten;
    }

    [RelayCommand]
    private void LandFlaggeEntfernen() => LandFlaggeDaten = null;

    private void NeuVerband()
    {
        _populiertGerade++;
        try
        {
            AusgewaehlterVerband = null;
            VerbandFormularAktiv = true;
            VerbandIstNeuModus = true;
            VerbandName = VerbandKurzname = "";
            VerbandLogoDaten = null;
            VerbandRangliste = KeineRangliste;
            Fehlermeldung = null;
        }
        finally
        {
            _populiertGerade--;
        }
        VerbandIsDirty = true;
    }

    [RelayCommand]
    private async Task VerbandLogoWaehlenAsync()
    {
        var daten = await _dialogService.BilddateiWaehlenAsync();
        if (daten is not null) VerbandLogoDaten = daten;
    }

    [RelayCommand]
    private void VerbandLogoEntfernen() => VerbandLogoDaten = null;

    [RelayCommand(CanExecute = nameof(KannSpeichern))]
    private async Task Speichern()
    {
        if (AktiverTabIndex == 0) await LandSpeichern();
        else await VerbandSpeichern();
    }

    private async Task LandSpeichern()
    {
        Fehlermeldung = null;
        try
        {
            if (LandIstNeuModus)
            {
                var eingabe = new LandErstellenEingabe(
                    LandName, LandKurzname, LandFifaCode,
                    LandKontinentOption.Wert ?? Kontinent.Europa,
                    LandVerband?.Id ?? 0, LandFlaggeDaten, LandRangliste?.Id);
                var neu = await _landService.ErstellenAsync(eingabe);
                await LadenAsync();
                AusgewaehltesLand = Laender.FirstOrDefault(l => l.Id == neu.Id);
            }
            else if (AusgewaehltesLand is not null)
            {
                var id = AusgewaehltesLand.Id;
                var eingabe = new LandAktualisierenEingabe(
                    LandName, LandKurzname, LandFifaCode,
                    LandKontinentOption.Wert ?? Kontinent.Europa,
                    LandVerband?.Id ?? 0, LandFlaggeDaten, LandRangliste?.Id);
                await _landService.AktualisierenAsync(id, eingabe);
                await LadenAsync();
                AusgewaehltesLand = Laender.FirstOrDefault(l => l.Id == id);
            }
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    private async Task VerbandSpeichern()
    {
        Fehlermeldung = null;
        try
        {
            if (VerbandIstNeuModus)
            {
                var eingabe = new VerbandErstellenEingabe(VerbandName, VerbandKurzname, VerbandLogoDaten, VerbandRangliste?.Id);
                var neu = await _verbandService.ErstellenAsync(eingabe);
                await LadenAsync();
                AusgewaehlterVerband = Verbaende.FirstOrDefault(v => v.Id == neu.Id);
            }
            else if (AusgewaehlterVerband is not null)
            {
                var id = AusgewaehlterVerband.Id;
                var eingabe = new VerbandAktualisierenEingabe(VerbandName, VerbandKurzname, VerbandLogoDaten, VerbandRangliste?.Id);
                await _verbandService.AktualisierenAsync(id, eingabe);
                await LadenAsync();
                AusgewaehlterVerband = Verbaende.FirstOrDefault(v => v.Id == id);
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
        if (AktiverTabIndex == 0)
            LandFormularAusFuellenMit(LandIstNeuModus ? null : AusgewaehltesLand);
        else
            VerbandFormularAusFuellenMit(VerbandIstNeuModus ? null : AusgewaehlterVerband);
    }

    [RelayCommand(CanExecute = nameof(KannLoeschen))]
    private async Task Loeschen()
    {
        if (AktiverTabIndex == 0) await LandLoeschen();
        else await VerbandLoeschen();
    }

    private async Task LandLoeschen()
    {
        if (AusgewaehltesLand is null) return;
        var bestaetigt = await _dialogService.BestaetigenAsync(
            "Land löschen", $"Soll '{AusgewaehltesLand.Name}' wirklich gelöscht werden?");
        if (!bestaetigt) return;
        Fehlermeldung = null;
        try
        {
            await _landService.LoeschenAsync(AusgewaehltesLand.Id);
            await LadenAsync();
            AusgewaehltesLand = null;
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    private async Task VerbandLoeschen()
    {
        if (AusgewaehlterVerband is null) return;
        var bestaetigt = await _dialogService.BestaetigenAsync(
            "Verband löschen", $"Soll '{AusgewaehlterVerband.Name}' wirklich gelöscht werden?");
        if (!bestaetigt) return;
        Fehlermeldung = null;
        try
        {
            await _verbandService.LoeschenAsync(AusgewaehlterVerband.Id);
            await LadenAsync();
            AusgewaehlterVerband = null;
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand]
    private void FilterZuruecksetzen()
    {
        if (AktiverTabIndex == 0)
        {
            LandSuchbegriff = "";
            LandFilterVerbandOption = AllesVerband;
            LandFilterKontinentOption = AllesKontinent;
        }
        else
        {
            VerbandSuchbegriff = "";
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(LaenderGefiltert)) OnPropertyChanged(nameof(LaenderAnzahlText));
        if (e.PropertyName == nameof(VerbaendeGefiltert)) OnPropertyChanged(nameof(VerbaendeAnzahlText));
        if (_populiertGerade != 0) return;
        if (_landFormFelder.Contains(e.PropertyName!)) LandIsDirty = true;
        if (_verbandFormFelder.Contains(e.PropertyName!)) VerbandIsDirty = true;
    }

    private bool KannSpeichern() => AktiverTabIndex == 0
        ? (LandFormularAktiv && LandIsDirty)
        : (VerbandFormularAktiv && VerbandIsDirty);

    private bool KannAbbrechen() => AktiverTabIndex == 0
        ? (LandFormularAktiv && LandIsDirty)
        : (VerbandFormularAktiv && VerbandIsDirty);

    private bool KannLoeschen() => AktiverTabIndex == 0
        ? (AusgewaehltesLand is not null && !LandIstNeuModus)
        : (AusgewaehlterVerband is not null && !VerbandIstNeuModus);

    // Unabhaengig vom aktiven Tab: auch das inaktive Formular wuerde bei Navigation verloren gehen.
    public bool HatUngespeicherteAenderungen =>
        (LandFormularAktiv && LandIsDirty) || (VerbandFormularAktiv && VerbandIsDirty);
}
