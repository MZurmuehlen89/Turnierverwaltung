using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Diagnose;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.Converters;
using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.Helpers;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
using Fussballturnierverwaltung.WinUi.Views;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

public record TurnierStatusOption(string Anzeige, TurnierStatus Wert);

/// <summary>Kopfzeile eines aufklappbaren Turnierkopf-Bereichs (Issue #399): Titel plus Kurzfassung des
/// Inhalts, damit auch zugeklappt erkennbar bleibt, ob etwas festgelegt ist.</summary>
public record AbschnittKopf(string Titel, string Zusammenfassung);

/// <summary>"Turnierkopf"-Tab von <see cref="TurnierlisteViewModel"/> (Issue #297) — Basisdaten,
/// Auslosungsfilter (Verband/Land/Mannschaftsart), Standard-Regelwerk, Status-Anzeige/-Auswahl und
/// Verband-Quoten einer <see cref="TurnierInstanz"/> (Vorlage- oder Turnier-Status identisch behandelt).
/// Runden/Übergangsregeln/Vorbelegung liegen in den separaten Tabs. Struktur analog WPF-Original
/// <c>TurnierInstanzViewModel</c>, hier auf ein reines Formular-ViewModel zugeschnitten (die Instanzliste
/// samt Status-Filter lebt in <see cref="TurnierTabKoordinator"/>).</summary>
public partial class TurnierkopfViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly TurnierInstanzService _service;
    private readonly TurnierValidator _validator;
    private readonly IDialogService _dialogService;
    private readonly RegelwerkService _regelwerkService;
    private readonly TurnierKlonService _klonService;
    private readonly ILogger<TurnierkopfViewModel> _logger;
    private int _populiertGerade;

    private static readonly RegelwerkOption _keinStandardRegelwerk = new("— Kein Standard —", null);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private TurnierInstanz? _aktuelleInstanz;

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatFormularFehler))]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    private IReadOnlyList<string> _formularFehler = [];

    public bool HatFormularFehler => FormularFehler.Count > 0;

    // ── Basisdaten ────────────────────────────────────────────────────────────
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _beschreibung = "";
    [ObservableProperty] private string _variante = "";
    [ObservableProperty] private string _saisonOderJahr = "";
    [ObservableProperty] private string _zielMannschaftsanzahl = "";
    [ObservableProperty] private TurnierKategorie _kategorie = TurnierKategorie.Benutzerdefiniert;
    [ObservableProperty] private string _startdatum = "";
    [ObservableProperty] private string _enddatum = "";
    [ObservableProperty] private byte[]? _logoDaten;

    public IEnumerable<TurnierKategorie> KategorieOptionen => Enum.GetValues<TurnierKategorie>();

    // ── Auslosungsfilter (Verband/Land/Mannschaftsart) ───────────────────────
    public AuslosungsfilterViewModel Filter { get; }

    // ── Standard-Regelwerk ────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StandardRegelwerkOptionen))]
    private ObservableCollection<Regelwerk> _regelwerke = [];

    [ObservableProperty] private RegelwerkOption _standardRegelwerk = _keinStandardRegelwerk;

    public IReadOnlyList<RegelwerkOption> StandardRegelwerkOptionen =>
        [_keinStandardRegelwerk, .. Regelwerke.Select(r => new RegelwerkOption(r.Name, r))];

    // ── Titelverteidiger & Gastgeber (Issue #349/#352) ───────────────────────
    public TitelGastgeberViewModel TitelGastgeber { get; }

    // Der Filter wird erst beim Ausführen gelesen — bis der verzögerte Abgleich läuft, kann er sich noch ändern.
    private void TitelGastgeberFilterAnwenden() => TitelGastgeber.FilterAnwenden(Filter.Wert);

    // ── Status-Anzeige/-Auswahl ───────────────────────────────────────────────
    [ObservableProperty] private TurnierStatusOption _status = new(TurnierStatusZuTextConverter.StatusAnzeigeName(TurnierStatus.Geplant), TurnierStatus.Geplant);

    public IReadOnlyList<TurnierStatusOption> StatusOptionen =>
        Enum.GetValues<TurnierStatus>().Select(s => new TurnierStatusOption(TurnierStatusZuTextConverter.StatusAnzeigeName(s), s)).ToList();

    /// <summary>Vom Composite gesetzt: der Status, mit dem "Neu" das Formular vorbelegt — er folgt dem
    /// Turniere/Vorlagen-Umschalter der Liste, sonst entstünde in der Vorlagen-Ansicht ein Turnier, das dort
    /// nach dem Speichern gar nicht auftaucht (#382).</summary>
    public TurnierStatus StatusFuerNeueInstanz { get; set; } = TurnierStatus.Geplant;

    /// <summary>Bei einer NEUEN, noch nicht gespeicherten Instanz ist die Statuswahl Teil der
    /// Erstellungs-Eingabe (kein Server-Aufruf möglich, es gibt noch keine Id) — bei einer bestehenden
    /// Instanz wirkt sie sofort über <see cref="TurnierInstanzService.StatusAendernAsync"/>, unabhängig
    /// vom Formular-Dirty-Status der Basisdaten (Issue #297: "Status-Anzeige/-Auswahl").</summary>
    partial void OnStatusChanged(TurnierStatusOption value)
    {
        if (_populiertGerade > 0) return;
        if (IstNeuModus) { IsDirty = true; return; }
        if (AktuelleInstanz is not null && value.Wert != AktuelleInstanz.Status)
            _ = StatusAendernAsync(value.Wert);
    }

    private async Task StatusAendernAsync(TurnierStatus neuerStatus)
    {
        // Die Instanz vor dem Warten festhalten: bis der Schutz frei ist, kann ein Turnierwechsel
        // AktuelleInstanz bereits umgesetzt haben.
        if (AktuelleInstanz is not { } instanz) return;
        Fehlermeldung = null;
        try
        {
            if (!await AenderungBestaetigtAsync())
            {
                StatusAuswahlZuruecksetzen();
                return;
            }

            using var schutz = await DbContextSchutz.BetretenAsync();
            using var vorgang = _logger.Vorgang($"Turnierkopf: Status ändern (Instanz {instanz.Id}, {neuerStatus})");
            await _service.StatusAendernAsync(instanz.Id, neuerStatus);
            InstanzGeaendert?.Invoke(instanz);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
            StatusAuswahlZuruecksetzen();
        }
    }

    private void StatusAuswahlZuruecksetzen()
    {
        if (AktuelleInstanz is null) return;
        _populiertGerade++;
        try { Status = StatusOptionen.First(o => o.Wert == AktuelleInstanz.Status); }
        finally { _populiertGerade--; }
    }

    /// <summary>Vom Composite gesetzte Rückfrage vor jeder Aktion, die <see cref="InstanzGeaendert"/> auslöst —
    /// der folgende Reload aller Reiter verwirft eine begonnene, noch nicht abgeschlossene Vorbelegung, und
    /// nach der Aktion lässt sich das nicht mehr ablehnen. "Löschen" fragt nicht zusätzlich: seine eigene
    /// Rückfrage nennt bereits, dass alles zum Turnier Gehörende verloren geht.</summary>
    public Func<Task<bool>>? VorAenderungBestaetigen { get; set; }

    private Task<bool> AenderungBestaetigtAsync() => VorAenderungBestaetigen?.Invoke() ?? Task.FromResult(true);

    /// <summary>Reiht die Aktionen dieses Reiters (Speichern, Statuswechsel, Löschen, Abbrechen) hinter einen
    /// Turnierwechsel ein und umgekehrt — über den vom <see cref="TurnierlisteViewModel"/> gesetzten gemeinsamen
    /// Schutz (#416). Nicht wiedereintrittsfähig: <see cref="InstanzSetzenAsync"/> läuft bereits unter dem
    /// Schutz, und <see cref="InstanzGeaendert"/> betritt ihn als Fire-and-Forget selbst — innerhalb einer
    /// geschützten Aktion nur auslösen, nie abwarten.</summary>
    public DbContextReentrancySchutz DbContextSchutz { get; set; } = new();

    // ── Quoten (Issue #161/#154/#189, hier #297) ─────────────────────────────
    public TurnierQuotenViewModel Quoten { get; }

    // "Verband" ist ausschließlich die Kontinentalebene — ist Verbands- oder Länderfilter gesetzt, gehören
    // zwangsläufig alle zugelassenen Teams zu genau einem Kontinentalverband, eine Verband-Quote kann dann
    // nie etwas Sinnvolles ausdrücken (Issue #154). FIFA-Ausnahme (Issue #211): "weltweit" lässt wieder alle
    // Kontinentalverbände zu. Nur für bereits gespeicherte Instanzen sichtbar (Quoten brauchen eine Id).
    private bool VerbandsQuotenSichtbar => FormularAktiv && !IstNeuModus
        && Filter.Wert is { VerbandId: null, LandId: null };

    // Landesquote ist unabhängig vom Verbandsfilter sichtbar (orthogonal zur Verband-Quote, nicht
    // exklusiv) — reales Vorbild Klub-WM hat Konföderations-Slots (Verband-Quote) UND eine Landes-Quote
    // ("max. 2 Vereine pro Land") gleichzeitig aktiv, ohne dass ein Verbandsfilter das ausschließt.
    // Nur für Vereinswettbewerbe sinnvoll (bei Nationalmannschaften ist "max. 1 Team/Land" ohnehin
    // trivial) und nur für bereits gespeicherte Instanzen (Quoten brauchen eine Id).
    private bool LandesQuotenSichtbar => FormularAktiv && !IstNeuModus
        && Filter.Wert.Art == MannschaftsartFilter.Verein;

    // ── Aufklappbare Bereiche (Issue #399) ────────────────────────────────────
    // Titelverteidiger + Gastgeber und die Quoten sind selten genutzt und liegen deshalb in aufklappbaren
    // Bereichen. Aufgeklappt wird beim Turnierwechsel, wenn der Bereich Werte enthält; danach bleibt der
    // Zustand dem Nutzer überlassen (Speichern/Abbrechen laden dasselbe Turnier neu und klappen nichts um).
    private void AufklappzustandSetzen()
    {
        TitelGastgeber.AufklappzustandSetzen();
        Quoten.AufklappzustandSetzen();
    }

    private static readonly HashSet<string> _formFelder =
    [
        nameof(Name), nameof(Beschreibung), nameof(Variante), nameof(SaisonOderJahr), nameof(ZielMannschaftsanzahl),
        nameof(Kategorie), nameof(Startdatum), nameof(Enddatum), nameof(LogoDaten), nameof(StandardRegelwerk)
    ];

    /// <summary>Feuert nach erfolgreichem Speichern (mit der neuen/aktualisierten Instanz), Statuswechsel
    /// oder Löschen (mit null) — der Composite lädt daraufhin die geteilte Instanzliste neu.</summary>
    public event Action<TurnierInstanz?>? InstanzGeaendert;

    // ── IRibbonAware ──────────────────────────────────────────────────────────
    public string RibbonTabTitel => "Turnierkopf";
    public ICommand? NeuBefehl => NeuCommand;
    public ICommand? SpeichernBefehl => SpeichernCommand;
    public ICommand? AbbrechenBefehl => AbbrechenCommand;
    public ICommand? LoeschenBefehl => LoeschenCommand;

    /// <summary>Issue #297-Nachzug: <see cref="TurnierKlonService"/> existiert seit #294, war aber in
    /// keiner UI verdrahtet (dokumentierte Scope-Lücke). "Aus Vorlage erstellen" legt eine neue Instanz an
    /// (echtes Turnier oder weitere Vorlage), löst nebenbei #46. Als Unterpunkt im "Neu"-Flyout statt
    /// eigener Ribbon-Gruppe (Nutzerwunsch aus #297-Test).</summary>
    public IReadOnlyList<RibbonAktion> NeuUnterpunkte =>
        [new RibbonAktion { Header = "Aus Vorlage erstellen", Befehl = AusVorlageErstellenCommand, IconGlyph = "" }];

    public TurnierkopfViewModel(
        TurnierInstanzService service, TurnierValidator validator, IDialogService dialogService,
        RegelwerkService regelwerkService, TurnierKlonService klonService,
        AuslosungsfilterViewModel filter, TitelGastgeberViewModel titelGastgeber, TurnierQuotenViewModel quoten,
        ILogger<TurnierkopfViewModel> logger)
    {
        _logger = logger;
        _service = service;
        _validator = validator;
        _dialogService = dialogService;
        _regelwerkService = regelwerkService;
        _klonService = klonService;
        Filter = filter;
        Filter.Geaendert += () =>
        {
            Quoten.SichtbarkeitSetzen(VerbandsQuotenSichtbar, LandesQuotenSichtbar);
            if (_populiertGerade > 0) return;
            IsDirty = true;
            AktualisiereFormularFehler();
            SammlungsAbgleich.Verzoegert(TitelGastgeberFilterAnwenden);
        };
        Quoten = quoten;
        Quoten.Geaendert += () => { if (_populiertGerade == 0) IsDirty = true; };
        TitelGastgeber = titelGastgeber;
        TitelGastgeber.Geaendert += () =>
        {
            if (_populiertGerade > 0) return;
            IsDirty = true;
            AktualisiereFormularFehler();
        };
    }

    /// <summary>Lädt Verbände/Länder/Regelwerke einmalig — vom Composite (<see cref="TurnierlisteViewModel"/>)
    /// beim Navigieren aufgerufen, bevor die Instanzauswahl das Formular erstmals befüllt.</summary>
    public async Task StammdatenLadenAsync()
    {
        using var vorgang = _logger.Vorgang("Turnierkopf: Stammdaten laden");
        await Filter.StammdatenLadenAsync();
        Quoten.StammdatenSetzen(Filter.Verbaende, Filter.Laender);
        using (_logger.Vorgang("Turnierkopf: Regelwerke abfragen"))
            Regelwerke = new ObservableCollection<Regelwerk>(await _regelwerkService.AlleAbrufenAsync());
        await TitelGastgeber.LadenAsync();
    }

    /// <summary>Vom Composite gesetzt, wenn sich die geteilte Instanzauswahl ändert. Der Guard
    /// (<see cref="_populiertGerade"/>) umspannt die gesamte Methode inkl. der nachfolgenden
    /// async-Ladeschritte, damit während des Ladens ausgelöste Zwischen-Änderungen nicht fälschlich
    /// IsDirty setzen. Verband-/Landfilter werden über <see cref="AuslosungsfilterViewModel.Setzen"/> atomar
    /// gesetzt (siehe dort) statt sequenziell — sequenzielle Zuweisung führte zu sporadisch leeren
    /// Feldern beim Wechsel zwischen Turniervorlagen.</summary>
    public async Task InstanzSetzenAsync(TurnierInstanz? instanz)
    {
        using var vorgang = _logger.Vorgang($"Turnierkopf: Turnier setzen (Instanz {instanz?.Id})");
        var instanzGewechselt = AktuelleInstanz?.Id != instanz?.Id || IstNeuModus;
        AktuelleInstanz = instanz;
        _populiertGerade++;
        Filter.ReaktionenAussetzen();
        try
        {
            if (instanz is null)
            {
                FormularAktiv = false;
                IstNeuModus = false;
                Name = Beschreibung = Variante = SaisonOderJahr = ZielMannschaftsanzahl = Startdatum = Enddatum = "";
                Kategorie = TurnierKategorie.Benutzerdefiniert;
                Filter.Setzen(null, null, MannschaftsartFilter.Alle);
                StandardRegelwerk = _keinStandardRegelwerk;
                TitelGastgeber.TitelverteidigerSetzen(Filter.Wert, null);
                Status = StatusOptionen.First(o => o.Wert == TurnierStatus.Geplant);
                LogoDaten = null;
                Quoten.Leeren();
                TitelGastgeber.GastgeberSetzen(Filter.Wert, []);
            }
            else
            {
                FormularAktiv = true;
                IstNeuModus = false;
                Name = instanz.Name;
                Beschreibung = instanz.Beschreibung;
                Variante = instanz.Variante;
                SaisonOderJahr = instanz.SaisonOderJahr;
                ZielMannschaftsanzahl = instanz.ZielMannschaftsanzahl?.ToString() ?? "";
                Kategorie = instanz.Kategorie;
                Startdatum = instanz.Startdatum?.Year.ToString() ?? "";
                Enddatum = instanz.Enddatum?.Year.ToString() ?? "";
                Filter.Setzen(instanz.VerbandId, instanz.LandId, instanz.MannschaftsartFilter);
                StandardRegelwerk = StandardRegelwerkOptionen.FirstOrDefault(o => o.Wert?.Id == instanz.StandardRegelwerkId) ?? _keinStandardRegelwerk;
                TitelGastgeber.TitelverteidigerSetzen(Filter.Wert, instanz.TitelverteidigerId);
                Status = StatusOptionen.First(o => o.Wert == instanz.Status);
                LogoDaten = instanz.Logo;
            }
            IsDirty = false;
            Fehlermeldung = null;

            AktualisiereFormularFehler();

            if (instanz is null)
            {
                AufklappzustandSetzen();
                return;
            }

            IstLadend = true;
            try
            {
                var detail = await _service.FindenNachIdMitRundenAsync(instanz.Id);
                var ersteRundeEingang = detail?.Runden.OrderBy(r => r.Reihenfolge).FirstOrDefault()?.EingangsMannschaftsanzahl;
                TitelGastgeber.GastgeberSetzen(Filter.Wert, detail?.Gastgeber.Select(g => g.MannschaftId).ToList() ?? []);
                await Quoten.LadenAsync(instanz.Id, ersteRundeEingang);
            }
            catch (Exception ex)
            {
                Fehlermeldung = ex.Message;
            }
            finally
            {
                IstLadend = false;
                if (instanzGewechselt) AufklappzustandSetzen();
            }
        }
        finally
        {
            Filter.ReaktionenFortsetzen();
            _populiertGerade--;
        }
    }

    // ── Ribbon-Befehle ────────────────────────────────────────────────────────

    [RelayCommand]
    private void Neu()
    {
        _populiertGerade++;
        Filter.ReaktionenAussetzen();
        try
        {
            AktuelleInstanz = null;
            FormularAktiv = true;
            IstNeuModus = true;
            Name = Beschreibung = Variante = SaisonOderJahr = ZielMannschaftsanzahl = Startdatum = Enddatum = "";
            Kategorie = TurnierKategorie.Benutzerdefiniert;
            Filter.Setzen(null, null, MannschaftsartFilter.Alle);
            StandardRegelwerk = _keinStandardRegelwerk;
            TitelGastgeber.TitelverteidigerSetzen(Filter.Wert, null);
            Status = StatusOptionen.First(o => o.Wert == StatusFuerNeueInstanz);
            LogoDaten = null;
            Quoten.Leeren();
            TitelGastgeber.GastgeberSetzen(Filter.Wert, []);
            Fehlermeldung = null;
            AufklappzustandSetzen();
        }
        finally
        {
            Filter.ReaktionenFortsetzen();
            _populiertGerade--;
        }
        IsDirty = true;
        AktualisiereFormularFehler();
    }

    [RelayCommand]
    private async Task AusVorlageErstellenAsync()
    {
        // Vor dem Öffnen fragen — der Erstellen-Dialog ist selbst ein ContentDialog, nur einer kann offen sein.
        if (!await AenderungBestaetigtAsync()) return;

        var dialog = new AusVorlageErstellenDialog(_service, _klonService);
        await _dialogService.AnzeigenAsync(dialog);
        if (dialog.ViewModel.ErstellteInstanz is { } neu)
            InstanzGeaendert?.Invoke(neu);
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
        if (!await AenderungBestaetigtAsync()) return;

        using var schutz = await DbContextSchutz.BetretenAsync();
        // Hat ein Turnierwechsel das Formular während des Wartens neu befüllt, gibt es nichts mehr zu speichern.
        if (!KannSpeichern()) return;

        Fehlermeldung = null;
        try
        {
            using var vorgang = _logger.Vorgang($"Turnierkopf: speichern (Instanz {AktuelleInstanz?.Id})");
            var zielAnzahl = ParseZielAnzahl(ZielMannschaftsanzahl);
            var startdatum = ParseJahr(Startdatum);
            var enddatum = ParseJahr(Enddatum);
            var verbandId = Filter.VerbandId;
            var landId = Filter.LandId;
            var standardRwId = StandardRegelwerk.Wert?.Id;

            TurnierInstanz ergebnis;
            if (IstNeuModus)
            {
                var eingabe = new TurnierInstanzErstellenEingabe(
                    Name: Name, Beschreibung: Beschreibung, SaisonOderJahr: SaisonOderJahr, Kategorie: Kategorie,
                    ZielMannschaftsanzahl: zielAnzahl, Startdatum: startdatum, Enddatum: enddatum, Status: Status.Wert,
                    VerbandId: verbandId, LandId: landId, MannschaftsartFilter: Filter.Mannschaftsart,
                    StandardRegelwerkId: standardRwId, Logo: LogoDaten, TitelverteidigerId: TitelGastgeber.TitelverteidigerId,
                    Variante: Variante, GastgeberIds: TitelGastgeber.GastgeberIds());
                ergebnis = await _service.ErstellenAsync(eingabe);
            }
            else if (AktuelleInstanz is not null)
            {
                var id = AktuelleInstanz.Id;

                var quoten = Quoten.GepruefteEingaben();

                var eingabe = new TurnierInstanzVollAktualisierenEingabe(
                    Name: Name, Beschreibung: Beschreibung, SaisonOderJahr: SaisonOderJahr, Kategorie: Kategorie,
                    ZielMannschaftsanzahl: zielAnzahl, Startdatum: startdatum, Enddatum: enddatum, Status: AktuelleInstanz.Status,
                    VerbandId: verbandId, LandId: landId, MannschaftsartFilter: Filter.Mannschaftsart,
                    StandardRegelwerkId: standardRwId, Logo: LogoDaten, TitelverteidigerId: TitelGastgeber.TitelverteidigerId,
                    Variante: Variante, GastgeberIds: TitelGastgeber.GastgeberIds());
                await _service.VollAktualisierenAsync(id, eingabe);
                await Quoten.ErsetzenAsync(id, quoten);

                ergebnis = AktuelleInstanz;
            }
            else
            {
                return;
            }

            IsDirty = false;
            InstanzGeaendert?.Invoke(ergebnis);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(KannAbbrechen))]
    private async Task Abbrechen()
    {
        using var schutz = await DbContextSchutz.BetretenAsync();
        if (!KannAbbrechen()) return;
        using var vorgang = _logger.Vorgang($"Turnierkopf: abbrechen (Instanz {AktuelleInstanz?.Id})");
        await InstanzSetzenAsync(IstNeuModus ? null : AktuelleInstanz);
    }

    [RelayCommand(CanExecute = nameof(KannLoeschen))]
    private async Task Loeschen()
    {
        // Gelöscht wird die Instanz, die die Rückfrage genannt hat — auch wenn ein Turnierwechsel
        // AktuelleInstanz umsetzt, bis der Schutz frei ist.
        if (AktuelleInstanz is not { } instanz) return;
        var bestaetigt = await _dialogService.BestaetigenAsync(
            "Turnier löschen",
            $"Soll '{instanz.Name}' wirklich gelöscht werden?\n\nDabei werden alle Spiele, Auslosungen und Simulationsläufe mitgelöscht.");
        if (!bestaetigt) return;

        using var schutz = await DbContextSchutz.BetretenAsync();
        Fehlermeldung = null;
        try
        {
            using var vorgang = _logger.Vorgang($"Turnierkopf: Turnier löschen (Instanz {instanz.Id})");
            await _service.LoeschenAsync(instanz.Id);
            InstanzGeaendert?.Invoke(null);
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

        int? zielAnzahl = null;
        var zielText = ZielMannschaftsanzahl.Trim();
        if (!string.IsNullOrEmpty(zielText))
        {
            if (int.TryParse(zielText, out var anzahl) && anzahl >= 2)
                zielAnzahl = anzahl;
            else
                fehler.Add("Mannschaftsanzahl muss eine Zahl ≥ 2 sein.");
        }

        if (!string.IsNullOrWhiteSpace(Startdatum) && ParseJahr(Startdatum) is null)
            fehler.Add("Startjahr muss eine Jahreszahl zwischen 1800 und 2100 sein.");
        if (!string.IsNullOrWhiteSpace(Enddatum) && ParseJahr(Enddatum) is null)
            fehler.Add("Endjahr muss eine Jahreszahl zwischen 1800 und 2100 sein.");

        fehler.AddRange(_validator.BasisdatenValidieren(
            Name, SaisonOderJahr, zielAnzahl, ParseJahr(Startdatum), ParseJahr(Enddatum), LogoDaten).Fehler);

        FormularFehler = fehler;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        switch (e.PropertyName)
        {
            case nameof(FormularAktiv) or nameof(IstNeuModus):
                Quoten.SichtbarkeitSetzen(VerbandsQuotenSichtbar, LandesQuotenSichtbar);
                break;
        }
        if (_populiertGerade == 0 && _formFelder.Contains(e.PropertyName!))
        {
            IsDirty = true;
            AktualisiereFormularFehler();
        }
    }

    private bool KannSpeichern() => FormularAktiv && IsDirty && !HatFormularFehler;
    private bool KannAbbrechen() => FormularAktiv && IsDirty;

    public bool HatUngespeicherteAenderungen => FormularAktiv && IsDirty;
    private bool KannLoeschen() => AktuelleInstanz is not null && !IstNeuModus;

    private static int? ParseZielAnzahl(string text)
    {
        var t = text.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        return int.TryParse(t, out var n) && n >= 2 ? n : null;
    }

    private static DateOnly? ParseJahr(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        return int.TryParse(text.Trim(), out var jahr) && jahr >= 1800 && jahr <= 2100
            ? new DateOnly(jahr, 1, 1)
            : null;
    }
}
