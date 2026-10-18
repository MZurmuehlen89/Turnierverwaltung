using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Diagnose;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Turnierbaum;
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
/// Status-abhängige Punkt ist die Teilnehmerpool-Quelle in <see cref="VorbelegungsTeilnehmerpool"/>: eine Vorlage hat noch
/// keine echten Teilnehmer, daher generische Platzhalter statt echter Mannschaften für die erste Runde ohne
/// eingehende Übergangsregeln). Kein DB-Schreibzugriff vor "Abschließen" — die <see cref="Auslosung"/>-Entität
/// wird erst dabei lazy angelegt.
/// </summary>
public partial class VorbelegungWorkspace : ObservableObject
{
    private readonly AuslosungService _auslosungService;
    private readonly SpielplanService _spielplanService;
    private readonly TurnierBaumService _baumService;
    private readonly IDialogService _dialogService;
    private readonly ILogger _logger;
    private readonly DbContextReentrancySchutz _dbContextSchutz;
    private readonly VorbelegungsTeilnehmerpool _teilnehmerpool;
    private readonly int _instanzId;

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

    // Neun Abhängigkeiten statt der angestrebten fünf (#445): Abschließen und Zurücksetzen brauchen Auslosung,
    // Spielplan-Rückfrage (mit Dialog) und Turnierbaum in einem Ablauf unter einem Schutz — sie in ein
    // Parameter-Objekt zu packen, würde nur die Zahl verstecken.
    public VorbelegungWorkspace(
        AuslosungService auslosungService,
        SpielplanService spielplanService,
        TurnierBaumService baumService,
        IDialogService dialogService,
        ILogger logger,
        DbContextReentrancySchutz dbContextSchutz,
        VorbelegungsTeilnehmerpool teilnehmerpool,
        int instanzId,
        TurnierRunde runde)
    {
        _logger = logger;
        _dbContextSchutz = dbContextSchutz;
        _auslosungService = auslosungService;
        _spielplanService = spielplanService;
        _baumService = baumService;
        _dialogService = dialogService;
        _teilnehmerpool = teilnehmerpool;
        _instanzId = instanzId;
        Runde = runde;
        // Keine Regelprüfung (#463): hier werden Platzhalter gepaart ("1. Gruppe A"), die kein Land und meist keine
        // eindeutige Gruppe haben, und die Paarungen bilden ein Reglement ab. Die Prüfung in der Platzbelegung
        // bleibt für die manuelle Auslosung mit echten Mannschaften (#482).
        Belegung = new Platzbelegung(runde.Rundenart, runde.GruppenAnzahl, regelWirkt: _ => false);
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
        Belegung.RegelmeldungLoeschen();
        try
        {
            var alle = await _auslosungService.AlleNachInstanzAbrufenAsync(_instanzId);
            var bestehende = alle.FirstOrDefault(a => a.TurnierRundeId == Runde.Id);
            _auslosung = bestehende is not null ? await _auslosungService.FindenNachIdAsync(bestehende.Id) : null;

            IstAbgeschlossen = _auslosung?.Status == AuslosungsStatus.Abgeschlossen;

            var platzhalter = await _teilnehmerpool.PlatzhalterAusVorrundenAsync(Runde);
            var teamsBenoetigt = platzhalter?.Count ?? Runde.EingangsMannschaftsanzahl;

            if (_auslosung is { Status: AuslosungsStatus.Abgeschlossen } abgeschlossene)
                Belegung.AbgeschlossenAufbauen(abgeschlossene.Ergebnisse, teamsBenoetigt);
            else
                Belegung.LeerAufbauen(
                    platzhalter ?? await _teilnehmerpool.TeilnehmerOhneVorgaengerAsync(Runde), teamsBenoetigt);
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
        finally { IstLadend = false; }
    }

    // ── Abschließen / Zurücksetzen ───────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(KannAbschliessen))]
    private async Task Abschliessen()
    {
        Fehlermeldung = null;
        Belegung.RegelmeldungLoeschen();
        try
        {
            var paarungen = Belegung.AlsPaarungsEingaben();

            // Schon die Rückfrage liest den Spielplan — der Schutz gilt ab hier bis nach dem Neuladen (#416).
            using var schutz = await _dbContextSchutz.BetretenAsync();
            if (!await ErgebnisVerlustBestaetigtAsync()) return;

            // Erst ab hier messen — die Rückfrage davor wartet auf den Nutzer.
            using var vorgang = _logger.Vorgang($"Vorbelegung: Paarung abschließen (Runde {Runde.Id}, {paarungen.Count} Einträge)");

            _auslosung ??= await _auslosungService.ErstellenAsync(new AuslosungErstellenEingabe(
                _instanzId, Runde.Id, Runde.Name, Auslosungsart.Manuell, IstAutomatisch: false));

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
        using var vorgang = _logger.Vorgang($"Vorbelegung: Turnierbaum aktualisieren (Instanz {_instanzId})");
        await _baumService.NachSpielplanAenderungAktualisierenAsync(_instanzId);
    }
}
