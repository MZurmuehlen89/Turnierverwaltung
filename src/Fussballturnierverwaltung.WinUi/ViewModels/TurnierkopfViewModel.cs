using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Diagnose;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
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

/// <summary>"Turnierkopf"-Tab von <see cref="TurnierlisteViewModel"/> (Issue #297) — Basisdaten,
/// Standard-Regelwerk, Statuswechsel und die Befehle Neu/Speichern/Abbrechen/Löschen einer
/// <see cref="TurnierInstanz"/> (Vorlage- oder Turnier-Status identisch behandelt). Auslosungsfilter,
/// Titelverteidiger/Gastgeber, Quoten und die Statusauswahl liegen in den Kind-ViewModels <see cref="Filter"/>,
/// <see cref="TitelGastgeber"/>, <see cref="Quoten"/> (#434) und <see cref="Status"/> (#448); dieses ViewModel
/// befüllt sie, hört auf ihre Änderungen und entscheidet, was aus einem Filterwechsel folgt. Runden/Übergangsregeln/Vorbelegung liegen
/// in den separaten Tabs, die Instanzliste samt Status-Filter in <see cref="TurnierTabKoordinator"/>.</summary>
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

    [ObservableProperty] private string? _fehlermeldung;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatFormularFehler))]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    private IReadOnlyList<Feldfehler> _formularFehler = [];

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

    // Feuert auch, wenn ein Turnier geladen wird: die Sichtbarkeit der Quoten folgt dem Filter immer, alles
    // Weitere nur einer Änderung durch den Nutzer.
    private void OnFilterGeaendert()
    {
        QuotenSichtbarkeitMelden();
        if (_populiertGerade > 0) return;
        AenderungMerken();
        SammlungsAbgleich.Verzoegert(TitelGastgeberFilterAnwenden);
    }

    // Der Filter wird erst beim Ausführen gelesen — bis der verzögerte Abgleich läuft, kann er sich noch ändern.
    private void TitelGastgeberFilterAnwenden() => TitelGastgeber.FilterAnwenden(Filter.Wert);

    // ── Status-Anzeige/-Auswahl ───────────────────────────────────────────────
    public TurnierStatusViewModel Status { get; }

    /// <summary>Vom Composite gesetzt: der Status, mit dem "Neu" das Formular vorbelegt — er folgt dem
    /// Turniere/Vorlagen-Umschalter der Liste, sonst entstünde in der Vorlagen-Ansicht ein Turnier, das dort
    /// nach dem Speichern gar nicht auftaucht (#382).</summary>
    public TurnierStatus StatusFuerNeueInstanz { get; set; } = TurnierStatus.Geplant;

    /// <summary>Von <see cref="Status"/> aufgerufen, wenn der Nutzer bei einem bestehenden Turnier einen anderen
    /// Status wählt. <c>false</c>, wenn es beim bisherigen Status bleibt (abgelehnt oder gescheitert).</summary>
    private async Task<bool> StatusWechselnAsync(TurnierStatus neuerStatus)
    {
        // Die Instanz vor dem Warten festhalten: bis der Schutz frei ist, kann ein Turnierwechsel
        // AktuelleInstanz bereits umgesetzt haben.
        if (AktuelleInstanz is not { } instanz) return false;
        Fehlermeldung = null;
        try
        {
            if (!await AenderungBestaetigtAsync()) return false;

            using var schutz = await DbContextSchutz.BetretenAsync();
            using var vorgang = _logger.Vorgang($"Turnierkopf: Status ändern (Instanz {instanz.Id}, {neuerStatus})", "Status wird geändert …");
            await _service.StatusAendernAsync(instanz.Id, neuerStatus);
            InstanzGeaendert?.Invoke(instanz);
            return true;
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
            return false;
        }
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

    private void QuotenSichtbarkeitMelden() => Quoten.SichtbarkeitSetzen(VerbandsQuotenSichtbar, LandesQuotenSichtbar);

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
        [new RibbonAktion { Header = "Aus Vorlage erstellen", AutomationId = "Befehlsleiste.AusVorlageErstellen", Befehl = AusVorlageErstellenCommand, IconGlyph = "" }];

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
        Filter.Geaendert += OnFilterGeaendert;
        Quoten = quoten;
        Quoten.Geaendert += AenderungMerken;
        TitelGastgeber = titelGastgeber;
        TitelGastgeber.Geaendert += AenderungMerken;
        Status = new TurnierStatusViewModel(StatusWechselnAsync);
        Status.EingabeGeaendert += AenderungMerken;
    }

    /// <summary>Lädt Verbände/Länder/Regelwerke einmalig — vom Composite (<see cref="TurnierlisteViewModel"/>)
    /// beim Navigieren aufgerufen, bevor die Instanzauswahl das Formular erstmals befüllt.</summary>
    public async Task StammdatenLadenAsync()
    {
        using var vorgang = _logger.Vorgang("Turnierkopf: Stammdaten laden", "Stammdaten werden geladen …");
        await Filter.StammdatenLadenAsync();
        Quoten.StammdatenSetzen(Filter.Verbaende, Filter.Laender);
        using (_logger.Vorgang("Turnierkopf: Regelwerke abfragen"))
            Regelwerke = new ObservableCollection<Regelwerk>(await _regelwerkService.AlleAbrufenAsync());
        await TitelGastgeber.LadenAsync();
    }

    /// <summary>Vom Composite gesetzt, wenn sich die geteilte Instanzauswahl ändert. Der Lade-Schutz
    /// (<see cref="BefuellenBeginnen"/>) umspannt die gesamte Methode inkl. der nachfolgenden
    /// async-Ladeschritte, damit während des Ladens ausgelöste Zwischen-Änderungen nicht fälschlich
    /// IsDirty setzen.</summary>
    public async Task InstanzSetzenAsync(TurnierInstanz? instanz)
    {
        using var vorgang = _logger.Vorgang($"Turnierkopf: Turnier setzen (Instanz {instanz?.Id})");
        var instanzGewechselt = AktuelleInstanz?.Id != instanz?.Id || IstNeuModus;
        AktuelleInstanz = instanz;
        BefuellenBeginnen();
        try
        {
            FormularZeigen(instanz);
            if (instanz is null) AufklappzustandSetzen();
            else await DetailLadenAsync(instanz, instanzGewechselt);
        }
        finally
        {
            BefuellenBeenden();
        }
    }

    /// <summary>Während das Formular befüllt wird, gilt nichts als Nutzeränderung — auch der Auslosungsfilter
    /// und die Statusauswahl reagieren so lange nicht auf Rückschreibungen ihrer ComboBoxen.</summary>
    private void BefuellenBeginnen()
    {
        _populiertGerade++;
        Filter.ReaktionenAussetzen();
        Status.ReaktionenAussetzen();
    }

    private void BefuellenBeenden()
    {
        Status.ReaktionenFortsetzen();
        Filter.ReaktionenFortsetzen();
        _populiertGerade--;
    }

    // Alles, was ohne Datenbankzugriff feststeht.
    private void FormularZeigen(TurnierInstanz? instanz)
    {
        FormularAktiv = instanz is not null;
        IstNeuModus = false;
        if (instanz is null)
        {
            FormularLeeren();
            Status.Leeren();
        }
        else FormularBefuellen(instanz);
        IsDirty = false;
        Fehlermeldung = null;
        AktualisiereFormularFehler();
    }

    private void FormularLeeren()
    {
        Name = Beschreibung = Variante = SaisonOderJahr = ZielMannschaftsanzahl = Startdatum = Enddatum = "";
        Kategorie = TurnierKategorie.Benutzerdefiniert;
        Filter.Setzen(null, null, MannschaftsartFilter.Alle);
        StandardRegelwerk = _keinStandardRegelwerk;
        TitelGastgeber.TitelverteidigerSetzen(Filter.Wert, null);
        LogoDaten = null;
        Quoten.Leeren();
        TitelGastgeber.GastgeberSetzen(Filter.Wert, []);
    }

    // Verband und Land setzt der Filter atomar (siehe AuslosungsfilterViewModel.AuslosungsfilterSetzen) —
    // sequenzielle Zuweisung führte zu sporadisch leeren Feldern beim Wechsel zwischen Turniervorlagen.
    private void FormularBefuellen(TurnierInstanz instanz)
    {
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
        Status.GespeichertenSetzen(instanz.Status);
        LogoDaten = instanz.Logo;
    }

    // Gastgeber, Quoten und die Eingangskapazität der ersten Runde stehen nicht in der Turnierliste.
    private async Task DetailLadenAsync(TurnierInstanz instanz, bool aufklappzustandSetzen)
    {
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
            if (aufklappzustandSetzen) AufklappzustandSetzen();
            // Die Quoten kommen erst jetzt dazu – ihre Prüfung gehört zum geladenen Stand (#595).
            AktualisiereFormularFehler();
        }
    }

    // ── Ribbon-Befehle ────────────────────────────────────────────────────────

    [RelayCommand]
    private void Neu()
    {
        BefuellenBeginnen();
        try
        {
            AktuelleInstanz = null;
            FormularAktiv = true;
            IstNeuModus = true;
            FormularLeeren();
            Status.AlsEingabeSetzen(StatusFuerNeueInstanz);
            Fehlermeldung = null;
            AufklappzustandSetzen();
        }
        finally
        {
            BefuellenBeenden();
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
            using var vorgang = _logger.Vorgang($"Turnierkopf: speichern (Instanz {AktuelleInstanz?.Id})", "Turnier wird gespeichert …");
            var gespeichert = IstNeuModus ? await ErstellenAsync() : await AktualisierenAsync();
            if (gespeichert is null) return;

            IsDirty = false;
            InstanzGeaendert?.Invoke(gespeichert);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    // Quoten kommen erst nach dem ersten Speichern dazu — sie brauchen die Id des Turniers.
    private Task<TurnierInstanz> ErstellenAsync() => _service.ErstellenAsync(new TurnierInstanzErstellenEingabe(
        Name: Name, Beschreibung: Beschreibung, SaisonOderJahr: SaisonOderJahr, Kategorie: Kategorie,
        ZielMannschaftsanzahl: ParseZielAnzahl(ZielMannschaftsanzahl), Startdatum: ParseJahr(Startdatum),
        Enddatum: ParseJahr(Enddatum), Status: Status.Wert,
        VerbandId: Filter.VerbandId, LandId: Filter.LandId, MannschaftsartFilter: Filter.Mannschaftsart,
        StandardRegelwerkId: StandardRegelwerk.Wert?.Id, Logo: LogoDaten,
        TitelverteidigerId: TitelGastgeber.TitelverteidigerId, Variante: Variante,
        GastgeberIds: TitelGastgeber.GastgeberIds()));

    private async Task<TurnierInstanz?> AktualisierenAsync()
    {
        if (AktuelleInstanz is not { } instanz) return null;

        // Erst alles prüfen, dann speichern: ungültige Quoten lassen auch die Basisdaten ungespeichert.
        var quoten = Quoten.GepruefteEingaben();
        // Der Status ändert sich nur über die Statusauswahl (StatusWechselnAsync), nicht über „Speichern“.
        var eingabe = new TurnierInstanzVollAktualisierenEingabe(
            Name: Name, Beschreibung: Beschreibung, SaisonOderJahr: SaisonOderJahr, Kategorie: Kategorie,
            ZielMannschaftsanzahl: ParseZielAnzahl(ZielMannschaftsanzahl), Startdatum: ParseJahr(Startdatum),
            Enddatum: ParseJahr(Enddatum), Status: instanz.Status,
            VerbandId: Filter.VerbandId, LandId: Filter.LandId, MannschaftsartFilter: Filter.Mannschaftsart,
            StandardRegelwerkId: StandardRegelwerk.Wert?.Id, Logo: LogoDaten,
            TitelverteidigerId: TitelGastgeber.TitelverteidigerId, Variante: Variante,
            GastgeberIds: TitelGastgeber.GastgeberIds());
        await _service.VollAktualisierenAsync(instanz.Id, eingabe);
        await Quoten.ErsetzenAsync(instanz.Id, quoten);
        return instanz;
    }

    [RelayCommand(CanExecute = nameof(KannAbbrechen))]
    private async Task Abbrechen()
    {
        using var schutz = await DbContextSchutz.BetretenAsync();
        if (!KannAbbrechen()) return;
        using var vorgang = _logger.Vorgang($"Turnierkopf: abbrechen (Instanz {AktuelleInstanz?.Id})", "Änderungen werden verworfen …");
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
            using var vorgang = _logger.Vorgang($"Turnierkopf: Turnier löschen (Instanz {instanz.Id})", "Turnier wird gelöscht …");
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

        FormularFehler =
        [
            .. EingabeFehler(),
            .. _validator.BasisdatenValidieren(
                Name, SaisonOderJahr, ParseZielAnzahl(ZielMannschaftsanzahl), ParseJahr(Startdatum), ParseJahr(Enddatum), LogoDaten).Meldungen,
            .. Quoten.MeldungenAktualisieren(),
        ];
    }

    /// <summary>Texte, die sich nicht in Zahlen wandeln lassen; Feldschlüssel wie im <see cref="TurnierValidator"/>.</summary>
    private IEnumerable<Feldfehler> EingabeFehler()
    {
        if (!string.IsNullOrWhiteSpace(ZielMannschaftsanzahl) && ParseZielAnzahl(ZielMannschaftsanzahl) is null)
            yield return new("Mannschaftsanzahl muss eine Zahl ≥ 2 sein.", nameof(TurnierInstanz.ZielMannschaftsanzahl));
        if (!string.IsNullOrWhiteSpace(Startdatum) && ParseJahr(Startdatum) is null)
            yield return new("Startjahr muss eine Jahreszahl zwischen 1800 und 2100 sein.", nameof(TurnierInstanz.Startdatum));
        if (!string.IsNullOrWhiteSpace(Enddatum) && ParseJahr(Enddatum) is null)
            yield return new("Endjahr muss eine Jahreszahl zwischen 1800 und 2100 sein.", nameof(TurnierInstanz.Enddatum));
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(FormularAktiv) or nameof(IstNeuModus)) QuotenSichtbarkeitMelden();
        if (_formFelder.Contains(e.PropertyName!)) AenderungMerken();
    }

    /// <summary>Der Nutzer hat im Formular etwas geändert — in einem eigenen Feld oder in einem der Bereiche.
    /// Während das Formular befüllt wird, zählt nichts als Änderung.</summary>
    private void AenderungMerken()
    {
        if (_populiertGerade > 0) return;
        IsDirty = true;
        AktualisiereFormularFehler();
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
