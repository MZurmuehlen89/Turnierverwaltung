using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Wpf.Navigation;
using Fussballturnierverwaltung.Wpf.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

/// <summary>Eine Runde in der Rundenliste des "Paarungen"-Tabs (Issue #251) mit ihrem aktuellen
/// Paarungsfestlegungs-Status. <see cref="IstEditierbarHier"/> ist false, wenn die Runde bereits eine
/// echte Auslosung (Zufall/NachSetzliste/…) hat — diese wird weiterhin in Turnierdurchführung → Auslosung
/// verwaltet, hier nur als Hinweis angezeigt.</summary>
public partial class RundenPaarungsEintrag : ObservableObject
{
    public TurnierRunde Runde { get; init; } = null!;

    [ObservableProperty] private bool _istEditierbarHier;
    [ObservableProperty] private string _statusText = "";
}

/// <summary>
/// "Paarungen"-Tab der Turnierdefinition (Issue #251) — dritter Tab neben "Runden" und "Rundenübergänge"
/// (siehe TurnierDefinitionViewModel/TurnierTabKoordinator). Für Runden mit von vornherein feststehenden
/// Paarungen (Auslosungsart.Manuell) wird die Paarungsfestlegung hier statt in Turnierdurchführung →
/// Auslosung vorgenommen (Details siehe ManuellePaarungWorkspaceViewModel). Runden mit einer echten
/// Auslosung bleiben unverändert in Turnierdurchführung → Auslosung und werden hier nur read-only mit
/// Hinweis angezeigt.
/// </summary>
public partial class PaarungsfestlegungViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly TurnierInstanzService _instanzService;
    private readonly AuslosungService _auslosungService;
    private readonly MannschaftService _mannschaftService;
    private readonly SpielplanService _spielplanService;
    private readonly UebergangsregelService _uebergangsregelService;
    private readonly AusgewaehlteTurnierInstanzService _turnierAuswahlService;

    private TurnierInstanz? _instanz;
    private IReadOnlyList<TurnierRunde> _instanzRunden = [];

    [ObservableProperty] private bool _istLadend;
    [ObservableProperty] private string? _fehlermeldung;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatKeineRunden))]
    private bool _keinTurnierGewaehlt = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatKeineRunden))]
    [NotifyPropertyChangedFor(nameof(HatKeineFestVorgegebenenRunden))]
    private ObservableCollection<RundenPaarungsEintrag> _runden = [];

    // Issue #251-Folgefund: unterscheidet "diese Instanz hat noch gar keine Runden" (siehe Tab "Runden")
    // von "es hat nur noch niemand eine Runde als fest vorgegeben markiert" (siehe Checkbox dort) — sonst
    // wäre die Meldung im zweiten Fall irreführend.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatKeineRunden))]
    [NotifyPropertyChangedFor(nameof(HatKeineFestVorgegebenenRunden))]
    private bool _hatUeberhauptRunden;

    public bool HatKeineRunden => !KeinTurnierGewaehlt && !HatUeberhauptRunden;
    public bool HatKeineFestVorgegebenenRunden => !KeinTurnierGewaehlt && HatUeberhauptRunden && Runden.Count == 0;

    [ObservableProperty] private RundenPaarungsEintrag? _ausgewaehlteRunde;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RibbonZusatzGruppen))]
    [NotifyPropertyChangedFor(nameof(HatUngespeicherteAenderungen))]
    private ManuellePaarungWorkspaceViewModel? _workspace;

    public PaarungsfestlegungViewModel(
        TurnierInstanzService instanzService,
        AuslosungService auslosungService,
        MannschaftService mannschaftService,
        SpielplanService spielplanService,
        UebergangsregelService uebergangsregelService,
        AusgewaehlteTurnierInstanzService turnierAuswahlService)
    {
        _instanzService = instanzService;
        _auslosungService = auslosungService;
        _mannschaftService = mannschaftService;
        _spielplanService = spielplanService;
        _uebergangsregelService = uebergangsregelService;
        _turnierAuswahlService = turnierAuswahlService;
    }

    public override async Task AktivierenAsync()
    {
        var instanzId = _turnierAuswahlService.TurnierInstanzId;
        KeinTurnierGewaehlt = instanzId is null;
        AusgewaehlteRunde = null;

        if (instanzId is null)
        {
            _instanz = null;
            _instanzRunden = [];
            Runden = [];
            HatUeberhauptRunden = false;
            return;
        }

        IstLadend = true;
        Fehlermeldung = null;
        try
        {
            _instanz = await _instanzService.FindenNachIdMitRundenAsync(instanzId.Value);
            if (_instanz is null)
            {
                Runden = [];
                HatUeberhauptRunden = false;
                return;
            }

            // Alle Runden der Instanz bleiben für die Vorgängerrunden-/Platzhalter-Ermittlung nötig
            // (TeilnehmerPoolHilfsmittel.VorgaengerrundeErmitteln), auch wenn nicht jede hier angezeigt wird.
            _instanzRunden = _instanz.Runden.OrderBy(r => r.Reihenfolge).ToList();
            HatUeberhauptRunden = _instanzRunden.Count > 0;
            var auslosungen = await _auslosungService.AlleNachInstanzAbrufenAsync(_instanz.Id);
            var auslosungNachRundeId = auslosungen.ToDictionary(a => a.TurnierRundeId);

            // Issue #251-Folgefund: nur Runden anzeigen, deren Paarung laut Rundendefinition fest
            // vorgegeben ist — sonst erschien anfangs jede Runde hier editierbar, bis anderswo eine echte
            // Auslosung angelegt wurde. Eine bereits bestehende Manuell-Auslosung zählt zusätzlich als
            // Fallback (Alt-Daten aus der Zeit vor diesem Flag, siehe #251).
            var sichtbareRunden = _instanzRunden.Where(r =>
                r.PaarungFestVorgegeben ||
                auslosungNachRundeId.GetValueOrDefault(r.Id)?.Auslosungsart == Auslosungsart.Manuell);

            Runden = new ObservableCollection<RundenPaarungsEintrag>(
                sichtbareRunden.Select(r => EintragErstellen(r, auslosungNachRundeId.GetValueOrDefault(r.Id))));
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
        finally { IstLadend = false; }
    }

    private static RundenPaarungsEintrag EintragErstellen(TurnierRunde runde, Auslosung? auslosung) => new()
    {
        Runde = runde,
        IstEditierbarHier = auslosung is null || auslosung.Auslosungsart == Auslosungsart.Manuell,
        StatusText = auslosung switch
        {
            null => "Keine feste Paarung",
            { Auslosungsart: not Auslosungsart.Manuell } => "Auslosung in Turnierdurchführung",
            { Status: AuslosungsStatus.Abgeschlossen } => "Abgeschlossen",
            _ => "In Bearbeitung"
        }
    };

    partial void OnAusgewaehlteRundeChanged(RundenPaarungsEintrag? oldValue, RundenPaarungsEintrag? newValue)
    {
        if (Workspace is not null)
        {
            Workspace.PropertyChanged -= OnWorkspacePropertyChanged;
            Workspace.Geaendert -= OnWorkspaceGeaendert;
        }

        if (newValue is null || !newValue.IstEditierbarHier || _instanz is null)
        {
            Workspace = null;
            return;
        }

        var workspace = new ManuellePaarungWorkspaceViewModel(
            _auslosungService, _mannschaftService, _spielplanService, _uebergangsregelService,
            _instanz, newValue.Runde, _instanzRunden);
        workspace.PropertyChanged += OnWorkspacePropertyChanged;
        workspace.Geaendert += OnWorkspaceGeaendert;
        Workspace = workspace;
        _ = workspace.LadenAsync();
    }

    private void OnWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ManuellePaarungWorkspaceViewModel.HatUngespeicherteAenderungen))
            OnPropertyChanged(nameof(HatUngespeicherteAenderungen));
    }

    /// <summary>Nach Abschließen/Zurücksetzen im Workspace: nur den Status der aktuell ausgewählten
    /// Runde in der Liste aktualisieren — die übrigen Runden können davon nicht betroffen sein.</summary>
    private void OnWorkspaceGeaendert()
    {
        if (AusgewaehlteRunde is null || Workspace is null) return;
        AusgewaehlteRunde.IstEditierbarHier = true;
        AusgewaehlteRunde.StatusText = Workspace.IstAbgeschlossen ? "Abgeschlossen" : "In Bearbeitung";
    }

    // ── IRibbonAware — eigene Datensatz-lose Aktionen (Precedent: SimulationsViewModel, Issue #173) ──
    public string RibbonTabTitel => "Paarungen";
    public ICommand? NeuBefehl => null;
    public ICommand? SpeichernBefehl => null;
    public ICommand? AbbrechenBefehl => null;
    public ICommand? LoeschenBefehl => null;
    public bool HatStandardAktionen => false;
    public bool HatLoeschenAktion => false;

    public IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen => Workspace is null
        ? []
        :
        [
            new RibbonGruppe
            {
                Header = "Paarung",
                Aktionen =
                [
                    new RibbonAktion
                    {
                        Header = "Paarung abschließen",
                        Befehl = Workspace.AbschliessenCommand,
                        IconVektorDatei = "check.xaml",
                    },
                    new RibbonAktion
                    {
                        Header = "Zurücksetzen",
                        Befehl = Workspace.ZuruecksetzenCommand,
                        IconVektorDatei = "refresh.xaml",
                    },
                ],
            },
        ];

    public bool HatUngespeicherteAenderungen => Workspace?.HatUngespeicherteAenderungen ?? false;
}
