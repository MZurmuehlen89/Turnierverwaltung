using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>"Turnierkopf"-Tab von <see cref="TurniervorlagenViewModel"/> (#284) — Basisdaten, Turnier-
/// einstellungen und Gültigkeit einer Turniervorlage. Runden/Übergangsregeln liegen im separaten
/// <see cref="VorlagenRundenViewModel"/>-Tab. Formular-Grundmuster analog <c>RegelwerkeViewModel</c>.</summary>
public partial class TurnierkopfViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly TurnierVorlageService _service;
    private readonly TurnierVorlageValidator _validator;
    private readonly IDialogService _dialogService;
    private int _populiertGerade;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private TurnierVorlage? _aktuelleVorlage;

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

    [ObservableProperty] private string? _fehlermeldung;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatFormularFehler))]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    private IReadOnlyList<string> _formularFehler = [];

    public bool HatFormularFehler => FormularFehler.Count > 0;

    // ── Basisdaten ────────────────────────────────────────────────────────────
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _beschreibung = "";
    [ObservableProperty] private string _anzahlMannschaften = "8";
    [ObservableProperty] private VorlagenKategorie _kategorie = VorlagenKategorie.Benutzerdefiniert;
    [ObservableProperty] private bool _istHistorisch;
    [ObservableProperty] private string _gueltigVon = "";
    [ObservableProperty] private string _gueltigBis = "";
    [ObservableProperty] private byte[]? _logoDaten;

    public IEnumerable<VorlagenKategorie> KategorieOptionen => Enum.GetValues<VorlagenKategorie>();

    private static readonly HashSet<string> _formFelder =
    [
        nameof(Name), nameof(Beschreibung), nameof(AnzahlMannschaften), nameof(Kategorie),
        nameof(IstHistorisch), nameof(GueltigVon), nameof(GueltigBis), nameof(LogoDaten)
    ];

    /// <summary>Feuert nach erfolgreichem Speichern (mit der neuen/aktualisierten Vorlage) oder Löschen
    /// (mit null) — der Composite lädt daraufhin die geteilte Vorlagenliste neu.</summary>
    public event Action<TurnierVorlage?>? VorlageGeaendert;

    // ── IRibbonAware ──────────────────────────────────────────────────────────
    public string RibbonTabTitel => "Turnierkopf";
    public ICommand? NeuBefehl => NeuCommand;
    public ICommand? SpeichernBefehl => SpeichernCommand;
    public ICommand? AbbrechenBefehl => AbbrechenCommand;
    public ICommand? LoeschenBefehl => LoeschenCommand;

    public TurnierkopfViewModel(TurnierVorlageService service, TurnierVorlageValidator validator, IDialogService dialogService)
    {
        _service = service;
        _validator = validator;
        _dialogService = dialogService;
    }

    /// <summary>Vom Composite gesetzt, wenn sich die geteilte Vorlagenauswahl ändert.</summary>
    public void VorlageSetzen(TurnierVorlage? vorlage)
    {
        AktuelleVorlage = vorlage;
        _populiertGerade++;
        try
        {
            if (vorlage is null)
            {
                FormularAktiv = false;
                IstNeuModus = false;
                Name = Beschreibung = GueltigVon = GueltigBis = "";
                AnzahlMannschaften = "8";
                Kategorie = VorlagenKategorie.Benutzerdefiniert;
                IstHistorisch = false;
                LogoDaten = null;
            }
            else
            {
                FormularAktiv = true;
                IstNeuModus = false;
                Name = vorlage.Name;
                Beschreibung = vorlage.Beschreibung;
                AnzahlMannschaften = vorlage.ErforderlicheMannschaftsanzahl.ToString();
                Kategorie = vorlage.VorlagenKategorie;
                IstHistorisch = vorlage.IstHistorisch;
                GueltigVon = vorlage.GueltigVon?.Year.ToString() ?? "";
                GueltigBis = vorlage.GueltigBis?.Year.ToString() ?? "";
                LogoDaten = vorlage.Logo;
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
            AktuelleVorlage = null;
            FormularAktiv = true;
            IstNeuModus = true;
            Name = Beschreibung = GueltigVon = GueltigBis = "";
            AnzahlMannschaften = "8";
            Kategorie = VorlagenKategorie.Benutzerdefiniert;
            IstHistorisch = false;
            LogoDaten = null;
            Fehlermeldung = null;
        }
        finally
        {
            _populiertGerade--;
        }
        IsDirty = true;
        AktualisiereFormularFehler();
    }

    [RelayCommand]
    private async Task LogoWaehlenAsync()
    {
        var daten = await _dialogService.BilddateiWaehlenAsync();
        if (daten is not null) LogoDaten = daten;
    }

    [RelayCommand]
    private void LogoEntfernen() => LogoDaten = null;

    [RelayCommand(CanExecute = nameof(KannSpeichern))]
    private async Task Speichern()
    {
        Fehlermeldung = null;
        try
        {
            var anzahl = ParseAnzahl(AnzahlMannschaften);
            var gueltigVon = ParseJahr(GueltigVon);
            var gueltigBis = ParseJahr(GueltigBis);

            TurnierVorlage ergebnis;
            if (IstNeuModus)
            {
                var eingabe = new TurnierVorlageErstellenEingabe(
                    Name, Beschreibung, anzahl, Kategorie, IstHistorisch, gueltigVon, gueltigBis, LogoDaten);
                ergebnis = await _service.ErstellenAsync(eingabe);
            }
            else if (AktuelleVorlage is not null)
            {
                var eingabe = new TurnierVorlageAktualisierenEingabe(
                    Name, Beschreibung, anzahl, Kategorie, IstHistorisch, gueltigVon, gueltigBis, LogoDaten);
                ergebnis = await _service.AktualisierenAsync(AktuelleVorlage.Id, eingabe);
            }
            else
            {
                return;
            }

            IsDirty = false;
            VorlageGeaendert?.Invoke(ergebnis);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(KannAbbrechen))]
    private void Abbrechen() => VorlageSetzen(IstNeuModus ? null : AktuelleVorlage);

    [RelayCommand(CanExecute = nameof(KannLoeschen))]
    private async Task Loeschen()
    {
        if (AktuelleVorlage is null) return;
        var bestaetigt = await _dialogService.BestaetigenAsync(
            "Turniervorlage löschen", $"Soll '{AktuelleVorlage.Name}' wirklich gelöscht werden?");
        if (!bestaetigt) return;

        Fehlermeldung = null;
        try
        {
            await _service.LoeschenAsync(AktuelleVorlage.Id);
            VorlageGeaendert?.Invoke(null);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    // ── Validierung ───────────────────────────────────────────────────────────

    private void AktualisiereFormularFehler()
    {
        if (!FormularAktiv) { FormularFehler = []; return; }

        var fehler = new List<string>();
        var anzahlText = AnzahlMannschaften.Trim();
        if (!int.TryParse(anzahlText, out var anzahl))
        {
            fehler.Add("Erforderliche Mannschaftsanzahl muss eine gültige Zahl sein.");
            anzahl = 2;
        }

        if (!string.IsNullOrWhiteSpace(GueltigVon) && ParseJahr(GueltigVon) is null)
            fehler.Add("Gültig von muss eine Jahreszahl zwischen 1800 und 2100 sein.");
        if (!string.IsNullOrWhiteSpace(GueltigBis) && ParseJahr(GueltigBis) is null)
            fehler.Add("Gültig bis muss eine Jahreszahl zwischen 1800 und 2100 sein.");

        fehler.AddRange(_validator.BasisdatenValidieren(Name, anzahl, ParseJahr(GueltigVon), ParseJahr(GueltigBis), LogoDaten).Fehler);

        FormularFehler = fehler;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_populiertGerade == 0 && _formFelder.Contains(e.PropertyName!))
        {
            IsDirty = true;
            AktualisiereFormularFehler();
        }
    }

    private bool KannSpeichern() => FormularAktiv && IsDirty && !HatFormularFehler;
    private bool KannAbbrechen() => FormularAktiv && IsDirty;

    public bool HatUngespeicherteAenderungen => FormularAktiv && IsDirty;
    private bool KannLoeschen() => AktuelleVorlage is not null && !IstNeuModus;

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
