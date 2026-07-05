using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
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

public record VorlagenKategorieOption(string Anzeige, VorlagenKategorie? Wert);

// ViewModel-Zeile für das Runden-DataGrid; jede Eigenschaft ist observable
// damit Inline-Editing im DataGrid sofort den Dirty-Zustand der Vorlage setzt.
public partial class VorlagenRundeZeile : ObservableObject
{
    public int? DbId { get; init; }

    [ObservableProperty] private string _name = RundenartZuTextConverter.RundenartAnzeigeName(Rundenart.Liga);
    [ObservableProperty] private Rundenart _rundenart = Rundenart.Liga;

    partial void OnRundenartChanged(Rundenart oldValue, Rundenart newValue)
    {
        if (Name == RundenartZuTextConverter.RundenartAnzeigeName(oldValue))
            Name = RundenartZuTextConverter.RundenartAnzeigeName(newValue);
    }
    [ObservableProperty] private int _eingangsMannschaftsanzahl = 8;
    [ObservableProperty] private int _ausgangsMannschaftsanzahl = 4;
    [ObservableProperty] private bool _mitRueckspiel;
    [ObservableProperty] private bool _auswaertstorRegelAktiv;
    [ObservableProperty] private RegelwerkOption _regelwerk = new("— Wählen —", null);
}

public partial class TurnierVorlageViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly TurnierVorlageService _service;
    private readonly RegelwerkService _regelwerkService;
    private int _populiertGerade;

    private static readonly RegelwerkOption _keinRegelwerk = new("— Wählen —", null);

    private static readonly VorlagenKategorieOption _alleKategorien = new("— Alle —", null);

    // ── Liste ──────────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VorlagenGefiltert))]
    private ObservableCollection<TurnierVorlage> _vorlagen = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VorlagenGefiltert))]
    private string _suchbegriff = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VorlagenGefiltert))]
    private VorlagenKategorieOption _kategorieFilter = _alleKategorien;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private TurnierVorlage? _ausgewaehltes;

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
    private bool _istNeuModus;

    [ObservableProperty] private bool _istLadend;
    [ObservableProperty] private string? _fehlermeldung;

    // ── Formularfelder ────────────────────────────────────────────────────────
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _beschreibung = "";
    [ObservableProperty] private string _anzahlMannschaften = "8";
    [ObservableProperty] private VorlagenKategorieOption _ausgewaehlteKategorie = new(VorlagenKategorieZuTextConverter.KategorieAnzeigeName(VorlagenKategorie.Benutzerdefiniert), VorlagenKategorie.Benutzerdefiniert);
    [ObservableProperty] private bool _istHistorisch;
    [ObservableProperty] private string _gueltigVon = "";
    [ObservableProperty] private string _gueltigBis = "";

    // ── Runden ────────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RundeNachObenCommand))]
    [NotifyCanExecuteChangedFor(nameof(RundeNachUntenCommand))]
    [NotifyCanExecuteChangedFor(nameof(RundeEntfernenCommand))]
    private VorlagenRundeZeile? _ausgewaehlteRunde;

    [ObservableProperty] private ObservableCollection<VorlagenRundeZeile> _rundenZeilen = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RegelwerkOptionen))]
    private ObservableCollection<Regelwerk> _regelwerke = [];

    // ── Berechnete Listen ─────────────────────────────────────────────────────
    public IEnumerable<TurnierVorlage> VorlagenGefiltert => Vorlagen
        .Where(v =>
            (string.IsNullOrWhiteSpace(Suchbegriff) || v.Name.Contains(Suchbegriff, StringComparison.OrdinalIgnoreCase))
            && (KategorieFilter.Wert is null || v.VorlagenKategorie == KategorieFilter.Wert));

    public IReadOnlyList<VorlagenKategorieOption> KategorieFilterOptionen =>
        [_alleKategorien, .. Enum.GetValues<VorlagenKategorie>()
            .Select(k => new VorlagenKategorieOption(VorlagenKategorieZuTextConverter.KategorieAnzeigeName(k), k))];

    public IReadOnlyList<VorlagenKategorieOption> KategorieOptionen =>
        Enum.GetValues<VorlagenKategorie>()
            .Select(k => new VorlagenKategorieOption(VorlagenKategorieZuTextConverter.KategorieAnzeigeName(k), k))
            .ToList();

    public IEnumerable<Rundenart> RundenartOptionen => Enum.GetValues<Rundenart>();

    public IReadOnlyList<RegelwerkOption> RegelwerkOptionen =>
        [_keinRegelwerk, .. Regelwerke.Select(r => new RegelwerkOption(r.Name, r))];

    private static readonly HashSet<string> _formFelder =
    [
        nameof(Name), nameof(Beschreibung), nameof(AnzahlMannschaften),
        nameof(AusgewaehlteKategorie), nameof(IstHistorisch),
        nameof(GueltigVon), nameof(GueltigBis)
    ];

    // ── IRibbonAware ───────────────────────────────────────────────────────────
    public string RibbonTabTitel => "Turniervorlagen";
    public ICommand NeuBefehl => NeuCommand;
    public ICommand SpeichernBefehl => SpeichernCommand;
    public ICommand AbbrechenBefehl => AbbrechenCommand;
    public ICommand LoeschenBefehl => LoeschenCommand;

    public TurnierVorlageViewModel(TurnierVorlageService service, RegelwerkService regelwerkService)
    {
        _service = service;
        _regelwerkService = regelwerkService;
    }

    public override async Task AktivierenAsync() => await LadenAsync();

    private async Task LadenAsync()
    {
        IstLadend = true;
        try
        {
            var vorlagen   = await _service.AlleAbrufenAsync();
            var regelwerke = await _regelwerkService.AlleAbrufenAsync();
            Vorlagen   = new ObservableCollection<TurnierVorlage>(vorlagen);
            Regelwerke = new ObservableCollection<Regelwerk>(regelwerke);
        }
        finally
        {
            IstLadend = false;
        }
    }

    private void FormularAusFuellenMit(TurnierVorlage? vorlage)
    {
        _populiertGerade++;
        try
        {
            if (vorlage is null)
            {
                FormularAktiv = false;
                IstNeuModus = false;
                Name = Beschreibung = GueltigVon = GueltigBis = "";
                AnzahlMannschaften = "8";
                AusgewaehlteKategorie = KategorieOptionen.First(o => o.Wert == VorlagenKategorie.Benutzerdefiniert);
                IstHistorisch = false;
                RundenZeilen = [];
            }
            else
            {
                FormularAktiv = true;
                IstNeuModus = false;
                Name = vorlage.Name;
                Beschreibung = vorlage.Beschreibung;
                AnzahlMannschaften = vorlage.ErforderlicheMannschaftsanzahl.ToString();
                AusgewaehlteKategorie = KategorieOptionen.FirstOrDefault(o => o.Wert == vorlage.VorlagenKategorie)
                    ?? KategorieOptionen.First();
                IstHistorisch = vorlage.IstHistorisch;
                GueltigVon = vorlage.GueltigVon?.Year.ToString() ?? "";
                GueltigBis = vorlage.GueltigBis?.Year.ToString() ?? "";
                // VorlagenRunden werden async in AusgewaehltesGeaendertAsync geladen
            }
            IsDirty = false;
            Fehlermeldung = null;
        }
        finally
        {
            _populiertGerade--;
        }
    }

    partial void OnAusgewaehltesChanged(TurnierVorlage? value)
        => _ = AusgewaehltesGeaendertAsync(value);

    private async Task AusgewaehltesGeaendertAsync(TurnierVorlage? vorlage)
    {
        FormularAusFuellenMit(vorlage);
        if (vorlage is null) return;

        IstLadend = true;
        try
        {
            var mitRunden = await _service.FindenNachIdMitRundenAsync(vorlage.Id);
            _populiertGerade++;
            try
            {
                RundenZeilen = new ObservableCollection<VorlagenRundeZeile>(
                    (mitRunden?.VorlagenRunden ?? []).Select(r => new VorlagenRundeZeile
                    {
                        DbId                      = r.Id,
                        Name                      = r.Name,
                        Rundenart                 = r.Rundenart,
                        EingangsMannschaftsanzahl = r.EingangsMannschaftsanzahl,
                        AusgangsMannschaftsanzahl = r.AusgangsMannschaftsanzahl,
                        MitRueckspiel             = r.MitRueckspiel,
                        AuswaertstorRegelAktiv    = r.AuswaertstorRegelAktiv,
                        Regelwerk                 = RegelwerkOptionen.FirstOrDefault(o => o.Wert?.Id == r.RegelwerkId)
                                                    ?? _keinRegelwerk
                    }));
                AbonniereRundenAenderungen();
                IsDirty = false;
            }
            finally
            {
                _populiertGerade--;
            }
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

    // Setzt IsDirty wenn eine Zeile inline bearbeitet wird
    private void AbonniereRundenAenderungen()
    {
        foreach (var zeile in RundenZeilen)
            zeile.PropertyChanged += OnRundeZeileGeaendert;
    }

    private void OnRundeZeileGeaendert(object? sender, PropertyChangedEventArgs e)
    {
        if (_populiertGerade == 0)
            IsDirty = true;
    }

    // ── Runden-Befehle ────────────────────────────────────────────────────────

    [RelayCommand]
    private void RundeHinzufuegen()
    {
        var letzte = RundenZeilen.LastOrDefault();
        var neu = new VorlagenRundeZeile
        {
            Rundenart                 = Rundenart.Liga,
            EingangsMannschaftsanzahl = letzte?.AusgangsMannschaftsanzahl ?? 8,
            AusgangsMannschaftsanzahl = Math.Max(1, (letzte?.AusgangsMannschaftsanzahl ?? 8) / 2)
        };
        neu.PropertyChanged += OnRundeZeileGeaendert;
        RundenZeilen.Add(neu);
        AusgewaehlteRunde = neu;
        IsDirty = true;
    }

    [RelayCommand(CanExecute = nameof(KannRundeEntfernen))]
    private void RundeEntfernen()
    {
        if (AusgewaehlteRunde is null) return;
        RundenZeilen.Remove(AusgewaehlteRunde);
        AusgewaehlteRunde = RundenZeilen.LastOrDefault();
        IsDirty = true;
    }

    [RelayCommand(CanExecute = nameof(KannRundeNachOben))]
    private void RundeNachOben()
    {
        if (AusgewaehlteRunde is null) return;
        var idx = RundenZeilen.IndexOf(AusgewaehlteRunde);
        if (idx <= 0) return;
        RundenZeilen.Move(idx, idx - 1);
        IsDirty = true;
    }

    [RelayCommand(CanExecute = nameof(KannRundeNachUnten))]
    private void RundeNachUnten()
    {
        if (AusgewaehlteRunde is null) return;
        var idx = RundenZeilen.IndexOf(AusgewaehlteRunde);
        if (idx < 0 || idx >= RundenZeilen.Count - 1) return;
        RundenZeilen.Move(idx, idx + 1);
        IsDirty = true;
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
            AnzahlMannschaften = "8";
            AusgewaehlteKategorie = KategorieOptionen.First(o => o.Wert == VorlagenKategorie.Benutzerdefiniert);
            IstHistorisch = false;
            RundenZeilen = [];
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
            var anzahl = ParseAnzahl(AnzahlMannschaften);
            var gueltigVon = ParseJahr(GueltigVon);
            var gueltigBis = ParseJahr(GueltigBis);
            var kategorie = AusgewaehlteKategorie.Wert ?? VorlagenKategorie.Benutzerdefiniert;
            var rundenEingaben = RundenZeilen.Select(z => new VorlagenRundeEingabe(
                z.Name, z.Rundenart,
                z.EingangsMannschaftsanzahl, z.AusgangsMannschaftsanzahl,
                z.MitRueckspiel, z.AuswaertstorRegelAktiv, z.Regelwerk.Wert?.Id)).ToList();

            if (IstNeuModus)
            {
                var eingabe = new TurnierVorlageErstellenEingabe(
                    Name, Beschreibung, anzahl, kategorie, IstHistorisch, gueltigVon, gueltigBis);
                var neu = await _service.ErstellenAsync(eingabe);
                await _service.RundenSynchronisierenAsync(neu.Id, rundenEingaben);
                await LadenAsync();
                Ausgewaehltes = Vorlagen.FirstOrDefault(v => v.Id == neu.Id);
            }
            else if (Ausgewaehltes is not null)
            {
                var id = Ausgewaehltes.Id;
                var eingabe = new TurnierVorlageAktualisierenEingabe(
                    Name, Beschreibung, anzahl, kategorie, IstHistorisch, gueltigVon, gueltigBis);
                await _service.AktualisierenAsync(id, eingabe);
                await _service.RundenSynchronisierenAsync(id, rundenEingaben);
                await LadenAsync();
                Ausgewaehltes = Vorlagen.FirstOrDefault(v => v.Id == id);
            }

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                () => IsDirty = false,
                System.Windows.Threading.DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(KannAbbrechen))]
    private void Abbrechen() => _ = AusgewaehltesGeaendertAsync(IstNeuModus ? null : Ausgewaehltes);

    [RelayCommand(CanExecute = nameof(KannLoeschen))]
    private async Task Loeschen()
    {
        if (Ausgewaehltes is null) return;
        var ergebnis = MessageBox.Show(
            $"Soll '{Ausgewaehltes.Name}' wirklich gelöscht werden?",
            "Turniervorlage löschen", MessageBoxButton.YesNo, MessageBoxImage.Warning);
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
    private bool KannRundeEntfernen() => AusgewaehlteRunde is not null;
    private bool KannRundeNachOben() => AusgewaehlteRunde is not null && RundenZeilen.IndexOf(AusgewaehlteRunde) > 0;
    private bool KannRundeNachUnten() => AusgewaehlteRunde is not null && RundenZeilen.IndexOf(AusgewaehlteRunde) < RundenZeilen.Count - 1;

    private static int ParseAnzahl(string text)
        => int.TryParse(text.Trim(), out var n) && n >= 2 ? n : 2;

    private static DateOnly? ParseJahr(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        return int.TryParse(text.Trim(), out var jahr) && jahr >= 1800 && jahr <= 2100
            ? new DateOnly(jahr, 1, 1)
            : null;
    }
}
