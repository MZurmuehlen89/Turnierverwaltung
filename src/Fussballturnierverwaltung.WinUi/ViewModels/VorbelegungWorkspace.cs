using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Diagnose;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Turnierbaum;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.Dialoge;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>
/// Workspace für die Vorbelegung einer Runde mit <see cref="Auslosungsart.Manuell"/> (Issue #297, Nachfolger
/// von #251 auf Instanzebene) — bewusst eine reine <see cref="ObservableObject"/>-Helferklasse statt
/// ViewModelBase (Precedent: <see cref="TurnierTabKoordinator"/>), gehostet von <see cref="VorbelegungViewModel"/>
/// — eine Instanz je aktuell bearbeiteter Runde, kein DI-Lifetime (per <c>new</c> erzeugt).
///
/// 1:1 aus Wpf/ViewModels/ManuellePaarungWorkspaceViewModel.cs portiert und arbeitet direkt gegen
/// <see cref="Domain.Entitaeten.Spiele.Spiel"/>/<see cref="Auslosung"/> statt gegen die mit #294 entfallene
/// <c>VorlagenPaarung</c>-Entität — funktioniert für Vorlage- UND Turnier-Status identisch (der einzige
/// Status-abhängige Punkt ist die Teilnehmerpool-Quelle in <see cref="LadenAsync"/>: eine Vorlage hat noch
/// keine echten Teilnehmer, daher generische Platzhalter statt echter Mannschaften für die erste Runde ohne
/// eingehende Übergangsregeln). Kein DB-Schreibzugriff vor "Abschließen" — die <see cref="Auslosung"/>-Entität
/// wird erst dabei lazy angelegt.
/// </summary>
public partial class VorbelegungWorkspace : ObservableObject
{
    private readonly AuslosungService _auslosungService;
    private readonly MannschaftService _mannschaftService;
    private readonly SpielplanService _spielplanService;
    private readonly TurnierBaumService _baumService;
    private readonly UebergangsregelService _uebergangsregelService;
    private readonly IDialogService _dialogService;
    private readonly ILogger _logger;
    private readonly DbContextReentrancySchutz _dbContextSchutz;

    private readonly TurnierInstanz _instanz;
    private readonly IReadOnlyList<TurnierRunde> _instanzRunden;

    /// <summary>Bereits vorhandene Manuell-Auslosung dieser Runde (inkl. Ergebnisse/Regeln), oder null,
    /// solange noch nichts abgeschlossen wurde — dann existiert noch keine <see cref="Auslosung"/>-Zeile.</summary>
    private Auslosung? _auslosung;

    public TurnierRunde Runde { get; }

    /// <summary>Raster, verfügbare Teilnehmer und Platzieren von Hand — ohne Datenbank.</summary>
    public Platzbelegung Belegung { get; }

    [ObservableProperty] private bool _istLadend;
    [ObservableProperty] private string? _fehlermeldung;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstBearbeitbar))]
    [NotifyCanExecuteChangedFor(nameof(AbschliessenCommand))]
    [NotifyCanExecuteChangedFor(nameof(ZuruecksetzenCommand))]
    private bool _istAbgeschlossen;

    public bool IstBearbeitbar => !IstAbgeschlossen;

    /// <summary>Meldet dem Host (<see cref="VorbelegungViewModel"/>) eine Änderung, die dessen
    /// Runden-Statusliste betrifft (Abschließen/Zurücksetzen).</summary>
    public event Action? Geaendert;

    public bool HatUngespeicherteAenderungen => !IstAbgeschlossen && Belegung.TeamsZugewiesen > 0;

    public VorbelegungWorkspace(
        AuslosungService auslosungService,
        MannschaftService mannschaftService,
        SpielplanService spielplanService,
        TurnierBaumService baumService,
        UebergangsregelService uebergangsregelService,
        IDialogService dialogService,
        ILogger logger,
        DbContextReentrancySchutz dbContextSchutz,
        TurnierInstanz instanz,
        TurnierRunde runde,
        IReadOnlyList<TurnierRunde> instanzRunden)
    {
        _logger = logger;
        _dbContextSchutz = dbContextSchutz;
        _auslosungService = auslosungService;
        _mannschaftService = mannschaftService;
        _spielplanService = spielplanService;
        _baumService = baumService;
        _uebergangsregelService = uebergangsregelService;
        _dialogService = dialogService;
        _instanz = instanz;
        Runde = runde;
        _instanzRunden = instanzRunden;
        Belegung = new Platzbelegung(runde, RegelAktiv);
        Belegung.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Platzbelegung.BelegungVollstaendig)) AbschliessenCommand.NotifyCanExecuteChanged();
        };
    }

    /// <summary>Lädt die Runde unter dem gemeinsamen Schutz (#416) — für den Aufruf von außerhalb einer
    /// geschützten Aktion (Rundenwechsel). Abschließen/Zurücksetzen halten den Schutz schon und rufen
    /// <see cref="LadenAsync"/> direkt auf.</summary>
    public async Task GeschuetztLadenAsync()
    {
        using var schutz = await _dbContextSchutz.BetretenAsync();
        await LadenAsync();
    }

    private async Task LadenAsync()
    {
        using var vorgang = _logger.Vorgang($"Vorbelegung: Runde laden (Runde {Runde.Id})");
        IstLadend = true;
        Fehlermeldung = null;
        Belegung.ManuellesFehler = null;
        try
        {
            var alle = await _auslosungService.AlleNachInstanzAbrufenAsync(_instanz.Id);
            var bestehende = alle.FirstOrDefault(a => a.TurnierRundeId == Runde.Id);
            _auslosung = bestehende is not null ? await _auslosungService.FindenNachIdAsync(bestehende.Id) : null;

            IstAbgeschlossen = _auslosung?.Status == AuslosungsStatus.Abgeschlossen;

            var eingehendeRegeln = await _uebergangsregelService.AlleNachZielRundeIdAsync(Runde.Id);
            var platzhalter = TeilnehmerPoolHilfsmittel.PlatzhalterAusVorrunden(
                _instanzRunden, Runde, eingehendeRegeln, Kombinationstabelle.AusJson(Runde.KombinationstabelleJson));
            var alleTeilnehmer = platzhalter ?? await TeilnehmerOhneVorgaengerAsync();

            Belegung.SlotsAufbauen(
                alleTeilnehmer, platzhalter?.Count ?? Runde.EingangsMannschaftsanzahl, IstAbgeschlossen ? _auslosung : null);
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
        finally { IstLadend = false; }
    }

    /// <summary>Teilnehmer einer Runde ohne Vorgänger. Issue #297: "echte Auslosung mit Mannschaften nur bei
    /// Status != Vorlage" — eine Vorlage hat noch keine echten Teilnehmer, daher generische Platzhalter statt einer
    /// fachlich unpassenden echten Mannschaftsliste.</summary>
    private async Task<IReadOnlyList<TeilnehmerAnzeige>> TeilnehmerOhneVorgaengerAsync()
    {
        if (_instanz.Status == TurnierStatus.Vorlage)
            return Enumerable.Range(1, Runde.EingangsMannschaftsanzahl)
                .Select(i => TeilnehmerAnzeige.AusPlatzhalter($"Team {i}")).ToList();

        var teams = await _mannschaftService.AlleNachTurnierfilterAsync(
            _instanz.VerbandId, _instanz.LandId, _instanz.MannschaftsartFilter);
        return teams.Select(TeilnehmerAnzeige.AusMannschaft).ToList();
    }

    private bool RegelAktiv(AuslosungsRegelArt art) =>
        _auslosung?.Regeln.Any(r => r.IstAktiv && r.Regelart == art) == true;

    // ── Abschließen / Zurücksetzen ───────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(KannAbschliessen))]
    private async Task Abschliessen()
    {
        Fehlermeldung = null;
        Belegung.ManuellesFehler = null;
        try
        {
            var paarungen = Belegung.PaarungsEingaben();

            // Schon die Rückfrage liest den Spielplan — der Schutz gilt ab hier bis nach dem Neuladen (#416).
            using var schutz = await _dbContextSchutz.BetretenAsync();
            if (!await ErgebnisVerlustBestaetigtAsync()) return;

            // Erst ab hier messen — die Rückfrage davor wartet auf den Nutzer.
            using var vorgang = _logger.Vorgang($"Vorbelegung: Paarung abschließen (Runde {Runde.Id}, {paarungen.Count} Einträge)");

            _auslosung ??= await _auslosungService.ErstellenAsync(new AuslosungErstellenEingabe(
                _instanz.Id, Runde.Id, Runde.Name, Auslosungsart.Manuell, IstAutomatisch: false));

            // Der Service ersetzt dabei auch den Spielplan der Runde und zieht die Folgerunden nach (#411).
            using (_logger.Vorgang($"Vorbelegung: Paarungen speichern, Spielplan generieren (Auslosung {_auslosung.Id})"))
                await _auslosungService.ManuelleErgebnisseAbschliessenAsync(_auslosung.Id, paarungen);
            await TurnierBaumAktualisierenAsync();
            await LadenAsync();
            Geaendert?.Invoke();
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    [RelayCommand(CanExecute = nameof(KannZuruecksetzen))]
    private async Task Zuruecksetzen()
    {
        if (_auslosung is null) return;
        Fehlermeldung = null;
        try
        {
            using var schutz = await _dbContextSchutz.BetretenAsync();
            using var vorgang = _logger.Vorgang($"Vorbelegung: zurücksetzen (Runde {Runde.Id}, Auslosung {_auslosung.Id})");
            using (_logger.Vorgang($"Vorbelegung: Auslosung zurücksetzen (Auslosung {_auslosung.Id})"))
                await _auslosungService.ZuruecksetzenAsync(_auslosung.Id);
            await TurnierBaumAktualisierenAsync();
            await LadenAsync();
            Geaendert?.Invoke();
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    private bool KannAbschliessen() => IstBearbeitbar && Belegung.BelegungVollstaendig;
    private bool KannZuruecksetzen() => IstAbgeschlossen;

    private Task<bool> ErgebnisVerlustBestaetigtAsync() =>
        SpielplanErsetzenRueckfrage.ErgebnisVerlustBestaetigtAsync(
            _spielplanService, _dialogService, Runde.Id, "Paarung abschließen", "Das Abschließen", "abschließen");

    private async Task TurnierBaumAktualisierenAsync()
    {
        using var vorgang = _logger.Vorgang($"Vorbelegung: Turnierbaum aktualisieren (Instanz {_instanz.Id})");
        await _baumService.NachSpielplanAenderungAktualisierenAsync(_instanz.Id);
    }
}
