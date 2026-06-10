using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Wpf.Converters;
using Fussballturnierverwaltung.Wpf.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

public record VorlagenKategorieOption(string Anzeige, VorlagenKategorie? Wert);

public partial class TurnierVorlageViewModel : ViewModelBase, IRibbonAware
{
    private readonly TurnierVorlageService _service;
    private int _populiertGerade;

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

    public TurnierVorlageViewModel(TurnierVorlageService service)
    {
        _service = service;
    }

    public override async Task AktivierenAsync() => await LadenAsync();

    private async Task LadenAsync()
    {
        IstLadend = true;
        try
        {
            var vorlagen = await _service.AlleAbrufenAsync();
            Vorlagen = new ObservableCollection<TurnierVorlage>(vorlagen);
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
            AnzahlMannschaften = "8";
            AusgewaehlteKategorie = KategorieOptionen.First(o => o.Wert == VorlagenKategorie.Benutzerdefiniert);
            IstHistorisch = false;
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

            if (IstNeuModus)
            {
                var eingabe = new TurnierVorlageErstellenEingabe(
                    Name, Beschreibung, anzahl, kategorie, IstHistorisch, gueltigVon, gueltigBis);
                var neu = await _service.ErstellenAsync(eingabe);
                await LadenAsync();
                Ausgewaehltes = Vorlagen.FirstOrDefault(v => v.Id == neu.Id);
            }
            else if (Ausgewaehltes is not null)
            {
                var id = Ausgewaehltes.Id;
                var eingabe = new TurnierVorlageAktualisierenEingabe(
                    Name, Beschreibung, anzahl, kategorie, IstHistorisch, gueltigVon, gueltigBis);
                await _service.AktualisierenAsync(id, eingabe);
                await LadenAsync();
                Ausgewaehltes = Vorlagen.FirstOrDefault(v => v.Id == id);
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
        if (e.PropertyName == nameof(Ausgewaehltes))
            FormularAusFuellenMit(Ausgewaehltes);
        if (_populiertGerade == 0 && _formFelder.Contains(e.PropertyName!))
            IsDirty = true;
    }

    private bool KannSpeichern() => FormularAktiv && IsDirty;
    private bool KannAbbrechen() => FormularAktiv && IsDirty;
    private bool KannLoeschen() => Ausgewaehltes is not null && !IstNeuModus;

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
