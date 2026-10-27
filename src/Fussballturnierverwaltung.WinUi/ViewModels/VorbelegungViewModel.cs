using CommunityToolkit.Mvvm.ComponentModel;
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
using Fussballturnierverwaltung.WinUi.Ribbon;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Eine Runde in der Rundenliste des "Vorbelegung"-Tabs (Issue #297) mit ihrem aktuellen
/// Paarungsfestlegungs-Status. <see cref="IstEditierbarHier"/> ist false, wenn die Runde bereits eine echte
/// Auslosung (Zufall/NachSetzliste/…) hat — diese wird weiterhin in Turnierdurchführung → Auslosung verwaltet
/// (#288), hier nur als Hinweis angezeigt.</summary>
public partial class RundenVorbelegungsEintrag : ObservableObject
{
    public required TurnierRunde Runde { get; init; }
    [ObservableProperty] private bool _istEditierbarHier;
    [ObservableProperty] private string _statusText = "";
}

/// <summary>"Vorbelegung"-Tab von <see cref="TurnierlisteViewModel"/> (Issue #297, ersetzt den entfallenen
/// "Paarungen"-Tab/<c>VorlagenPaarungenViewModel</c>) — dritter/vierter Tab neben Turnierkopf/Runden/
/// Übergänge. Für Runden mit von vornherein feststehenden Paarungen (<c>PaarungFestVorgegeben</c>,
/// <see cref="Auslosungsart.Manuell"/>) wird die Paarungsfestlegung hier vorgenommen (Details siehe
/// <see cref="VorbelegungWorkspace"/>). Runden mit einer echten Auslosung bleiben Turnierdurchführung → Auslosung
/// (#288) vorbehalten und werden hier nur read-only mit Hinweis angezeigt. Arbeitet — anders als der
/// entfallene "Paarungen"-Tab — vollständig unabhängig von <see cref="RundenViewModel"/> (lädt Runden/
/// Auslosungen selbst, analog WPF-Original <c>PaarungsfestlegungViewModel</c>).</summary>
public partial class VorbelegungViewModel : ViewModelBase, IRibbonAware
{
    private readonly TurnierInstanzService _instanzService;
    private readonly AuslosungService _auslosungService;
    private readonly MannschaftService _mannschaftService;
    private readonly SpielplanService _spielplanService;
    private readonly TurnierBaumService _baumService;
    private readonly UebergangsregelService _uebergangsregelService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<VorbelegungViewModel> _logger;

    private TurnierInstanz? _instanz;
    private IReadOnlyList<TurnierRunde> _instanzRunden = [];
    private VorbelegungsTeilnehmerpool? _teilnehmerpool;

    [ObservableProperty] private string? _fehlermeldung;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatKeineRunden))]
    [NotifyPropertyChangedFor(nameof(ZeigtRundenBereich))]
    private bool _keinTurnierGewaehlt = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatKeineRunden))]
    [NotifyPropertyChangedFor(nameof(HatKeineFestVorgegebenenRunden))]
    [NotifyPropertyChangedFor(nameof(ZeigtRundenBereich))]
    private ObservableCollection<RundenVorbelegungsEintrag> _runden = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatKeineRunden))]
    [NotifyPropertyChangedFor(nameof(HatKeineFestVorgegebenenRunden))]
    [NotifyPropertyChangedFor(nameof(ZeigtRundenBereich))]
    private bool _hatUeberhauptRunden;

    public bool HatKeineRunden => !KeinTurnierGewaehlt && !HatUeberhauptRunden;
    public bool HatKeineFestVorgegebenenRunden => !KeinTurnierGewaehlt && HatUeberhauptRunden && Runden.Count == 0;

    /// <summary>Turnier gewählt, hat Runden, und mindestens eine davon ist als Vorbelegung-Kandidat
    /// sichtbar — der eigentliche Runden-/Workspace-Bereich ist dann an der Reihe (statt eines der drei
    /// Platzhalter-Hinweise oben).</summary>
    public bool ZeigtRundenBereich => !KeinTurnierGewaehlt && !HatKeineRunden && !HatKeineFestVorgegebenenRunden;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RundeNichtEditierbarHier))]
    [NotifyPropertyChangedFor(nameof(ZeigeWorkspace))]
    private RundenVorbelegungsEintrag? _ausgewaehlteRunde;

    /// <summary>Runde ist ausgewählt, hat aber bereits eine echte (nicht-manuelle) Auslosung — die
    /// Paarungsfestlegung bleibt dann Turnierdurchführung → Auslosung (#288) vorbehalten.</summary>
    public bool RundeNichtEditierbarHier => AusgewaehlteRunde is not null && !AusgewaehlteRunde.IstEditierbarHier;

    public bool ZeigeWorkspace => AusgewaehlteRunde is not null && AusgewaehlteRunde.IstEditierbarHier;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RibbonZusatzGruppen))]
    private VorbelegungWorkspace? _workspace;

    /// <summary>Unterdrückt die Verwerfen-Rückfrage bzw. den Wechsel-Hook, während die Rundenauswahl vom Code
    /// selbst gesetzt wird (Instanzwechsel, Rücksprung nach "Nein").</summary>
    private bool _rundenAuswahlOhneRueckfrage;
    private bool _rundenRuecksprungAktiv;

    /// <summary>Reiht Abschließen, Zurücksetzen und das Laden einer Runde hinter einen Turnierwechsel ein und
    /// umgekehrt — über den vom <see cref="TurnierlisteViewModel"/> gesetzten gemeinsamen Schutz (#416): ohne
    /// ihn kollidierte ein laufendes Zurücksetzen mit dem Laden nach dem Turnierwechsel und brach mitten in der
    /// Kaskade ab, ohne dass die Oberfläche etwas zeigte. Wird an jeden <see cref="VorbelegungWorkspace"/>
    /// weitergereicht. Nicht wiedereintrittsfähig — <see cref="InstanzSetzenAsync"/> läuft bereits darunter.</summary>
    public DbContextReentrancySchutz DbContextSchutz { get; set; } = new();

    public VorbelegungViewModel(
        TurnierInstanzService instanzService,
        AuslosungService auslosungService,
        MannschaftService mannschaftService,
        SpielplanService spielplanService,
        TurnierBaumService baumService,
        UebergangsregelService uebergangsregelService,
        IDialogService dialogService,
        ILogger<VorbelegungViewModel> logger)
    {
        _logger = logger;
        _instanzService = instanzService;
        _auslosungService = auslosungService;
        _mannschaftService = mannschaftService;
        _spielplanService = spielplanService;
        _baumService = baumService;
        _uebergangsregelService = uebergangsregelService;
        _dialogService = dialogService;
    }

    /// <summary>Vom Composite gesetzt, wenn sich die geteilte Instanzauswahl ändert — unabhängig von
    /// <see cref="RundenViewModel"/>, lädt Runden/Auslosungen selbst (analog WPF-Original
    /// <c>PaarungsfestlegungViewModel.AktivierenAsync</c>).</summary>
    public async Task InstanzSetzenAsync(TurnierInstanz? instanz)
    {
        using var vorgang = _logger.Vorgang($"Vorbelegung: Turnier setzen (Instanz {instanz?.Id})");
        KeinTurnierGewaehlt = instanz is null;
        _rundenAuswahlOhneRueckfrage = true;
        try { AusgewaehlteRunde = null; }
        finally { _rundenAuswahlOhneRueckfrage = false; }

        if (instanz is null)
        {
            _instanz = null;
            _instanzRunden = [];
            _teilnehmerpool = null;
            Runden = [];
            HatUeberhauptRunden = false;
            return;
        }

        Fehlermeldung = null;
        try
        {
            _instanz = await _instanzService.FindenNachIdMitRundenAsync(instanz.Id);
            if (_instanz is null)
            {
                Runden = [];
                HatUeberhauptRunden = false;
                return;
            }

            _instanzRunden = _instanz.Runden.OrderBy(r => r.Reihenfolge).ToList();
            _teilnehmerpool = new VorbelegungsTeilnehmerpool(_mannschaftService, _uebergangsregelService, _instanz, _instanzRunden);
            HatUeberhauptRunden = _instanzRunden.Count > 0;
            var auslosungen = await _auslosungService.AlleNachInstanzAbrufenAsync(_instanz.Id);

            // Issue #320: GroupBy statt ToDictionary — Neuanlage einer zweiten Auslosung für dieselbe Runde
            // ist jetzt in AuslosungService.ErstellenAsync unterbunden, aber vor diesem Fix bereits entstandene
            // Alt-Daten sollen die Ansicht nicht mit einer ArgumentException zum Absturz bringen (die Manuell-
            // Auslosung, sofern vorhanden, gewinnt — das ist die für diesen Tab relevante).
            var auslosungNachRundeId = auslosungen
                .GroupBy(a => a.TurnierRundeId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.Auslosungsart == Auslosungsart.Manuell).First());

            // Nur der Haken trennt Vorbelegung und Durchführung (#482): eine Manuell-Auslosung einer Runde ohne
            // Haken ist eine Ziehung von Hand der Durchführung (auch der Altfall „Haken nachträglich entfernt“, #320).
            var sichtbareRunden = _instanzRunden.Where(r => r.PaarungFestVorgegeben);

            Runden = new ObservableCollection<RundenVorbelegungsEintrag>(
                sichtbareRunden.Select(r => EintragErstellen(r, auslosungNachRundeId.GetValueOrDefault(r.Id))));
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    private static RundenVorbelegungsEintrag EintragErstellen(TurnierRunde runde, Auslosung? auslosung) => new()
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

    /// <summary>Rückfrage, bevor eine begonnene, noch nicht abgeschlossene Vorbelegung verloren geht (Runden-,
    /// Turnier- oder Menüwechsel) — gespeichert wird ausschließlich über "Paarung abschließen", ein Wechsel
    /// verwirft die platzierten Teams. Ohne begonnene Vorbelegung synchron <c>true</c>.</summary>
    public async Task<bool> VerwerfenBestaetigtAsync()
    {
        if (Workspace is not { HatUngespeicherteAenderungen: true } workspace) return true;

        return await _dialogService.BestaetigenAsync(
            "Vorbelegung verwerfen",
            $"Für '{workspace.Runde.Name}' sind bereits {workspace.Belegung.TeamsZugewiesen} Team(s) platziert, die Paarung " +
            "ist aber noch nicht abgeschlossen. Die Vorbelegung geht dabei verloren. Wirklich fortfahren?");
    }

    partial void OnAusgewaehlteRundeChanged(RundenVorbelegungsEintrag? oldValue, RundenVorbelegungsEintrag? newValue)
    {
        if (_rundenRuecksprungAktiv) return;
        _ = RundenWechselAsync(oldValue, newValue);
    }

    private async Task RundenWechselAsync(RundenVorbelegungsEintrag? oldValue, RundenVorbelegungsEintrag? newValue)
    {
        // Läuft ohne begonnene Vorbelegung synchron durch (abgeschlossener Task) — wie vor der Rückfrage.
        if (!_rundenAuswahlOhneRueckfrage && !await VerwerfenBestaetigtAsync())
        {
            _rundenRuecksprungAktiv = true;
            try { AusgewaehlteRunde = oldValue; }
            finally { _rundenRuecksprungAktiv = false; }
            return;
        }

        if (Workspace is not null) WorkspaceAbhaengen(Workspace);

        if (newValue is null || !newValue.IstEditierbarHier || _instanz is null || _teilnehmerpool is null)
        {
            Workspace = null;
            return;
        }

        var workspace = new VorbelegungWorkspace(
            _auslosungService, _spielplanService, _baumService, _dialogService,
            _logger, DbContextSchutz, _teilnehmerpool, _instanz.Id, newValue.Runde);
        workspace.Geaendert += OnWorkspaceGeaendert;
        workspace.PropertyChanged += OnWorkspacePropertyChanged;
        Workspace = workspace;
        _ = workspace.GeschuetztLadenAsync();
    }

    /// <summary>Mit dem Workspace geht auch seine Meldung — er kann noch laden, während
    /// schon die nächste Runde gewählt ist, und meldete sonst in die Anzeige einer anderen Runde hinein.</summary>
    private void WorkspaceAbhaengen(VorbelegungWorkspace workspace)
    {
        workspace.Geaendert -= OnWorkspaceGeaendert;
        workspace.PropertyChanged -= OnWorkspacePropertyChanged;
        Fehlermeldung = null;
    }

    /// <summary>Die Seite hat eine Fehlerzeile an diesem ViewModel — der Workspace
    /// selbst ist an keiner Stelle der Oberfläche gebunden (der Arbeitsbereich bindet an seine
    /// <see cref="VorbelegungWorkspace.Belegung"/>). Ohne die Weitergabe blieb ein gescheitertes Laden,
    /// Abschließen oder Zurücksetzen ohne jeden Hinweis (#453).</summary>
    private void OnWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not VorbelegungWorkspace workspace) return;
        if (e.PropertyName == nameof(VorbelegungWorkspace.Fehlermeldung)) Fehlermeldung = workspace.Fehlermeldung;
    }

    /// <summary>Nach Abschließen/Zurücksetzen im Workspace: nur den Status der aktuell ausgewählten
    /// Runde in der Liste aktualisieren — die übrigen Runden können davon nicht betroffen sein.</summary>
    private void OnWorkspaceGeaendert()
    {
        if (AusgewaehlteRunde is null || Workspace is null) return;
        AusgewaehlteRunde.IstEditierbarHier = true;
        AusgewaehlteRunde.StatusText = Workspace.IstAbgeschlossen ? "Abgeschlossen" : "In Bearbeitung";
    }

    // ── IRibbonAware — eigene Datensatz-lose Aktionen (Precedent: WPF SimulationsViewModel, Issue #173) ──
    public string RibbonTabTitel => "Vorbelegung";
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
                    new RibbonAktion { Header = "Paarung abschließen", AutomationId = "Befehlsleiste.PaarungAbschliessen", Befehl = Workspace.AbschliessenCommand, IconGlyph = "" },
                    new RibbonAktion { Header = "Zurücksetzen", AutomationId = "Befehlsleiste.VorbelegungZuruecksetzen", Befehl = Workspace.ZuruecksetzenCommand, IconGlyph = "" },
                ],
            },
        ];
}
