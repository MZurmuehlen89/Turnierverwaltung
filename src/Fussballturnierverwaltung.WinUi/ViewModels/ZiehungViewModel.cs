using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Auslosungen;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>TV-Modus: schrittweise Live-Ziehung einer bereits feststehenden Auslosung (#303, Issue #5) — Banner,
/// Fokus-Ansicht und Gesamtübersicht für Gruppenphase/K.-o.-Runde (#408) sowie Gegner-Matrix der Ligaphase (#364).
/// Reine Anzeige ohne Datenbankzugriff: Auslosung und Spielplan stehen vor dem Start fest, die Ziehung enthüllt nur
/// noch und darf jederzeit enden. Hält den gebundenen Zustand, das Warten und den Abbruch; was wann erscheint,
/// bestimmen <see cref="GruppenKoZiehungsAblauf"/> und <see cref="LigaphasenZiehungsAblauf"/>. Kind-ViewModel von
/// <see cref="AuslosungViewModel"/>.</summary>
public partial class ZiehungViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnimationLaeuftNicht))]
    [NotifyPropertyChangedFor(nameof(AnimationZeigeListenmodus))]
    [NotifyPropertyChangedFor(nameof(AnimationZeigeListenFokus))]
    [NotifyPropertyChangedFor(nameof(AnimationZeigeListenUebersicht))]
    [NotifyCanExecuteChangedFor(nameof(AnimationUeberspringenCommand))]
    private bool _animationLaeuft;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnimationWeiterCommand))]
    private bool _animationWartetAufWeiter;
    [ObservableProperty] private string? _animationBannerMannschaft;
    [ObservableProperty] private string? _animationBannerTopf;
    [ObservableProperty] private string? _animationBannerZiel;
    [ObservableProperty] private string? _animationBannerSlot;
    [ObservableProperty] private string? _animationBannerPosition;
    [ObservableProperty] private ObservableCollection<AnimationsTopf> _animationToepfe = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnimationIstGruppenmodus))]
    [NotifyPropertyChangedFor(nameof(AnimationZeigeGruppenLeiste))]
    private ObservableCollection<AnimationsGruppe> _animationGruppen = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnimationIstPaarungsmodus))]
    private ObservableCollection<AnimationsPaarung> _animationPaarungen = [];

    public bool AnimationIstGruppenmodus  => AnimationGruppen.Count > 0;
    public bool AnimationIstPaarungsmodus => AnimationPaarungen.Count > 0;

    // Ligaphase (#364): eigene Matrix-Instanzen für die Animation (Ergebnis-Matrizen bleiben unberührt).
    // Fokus = nur die Matrix des gerade gezogenen Topfs; Übersicht = alle Matrizen nach Abschluss eines Topfs.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnimationIstMatrixmodus))]
    [NotifyPropertyChangedFor(nameof(AnimationZeigeListenmodus))]
    [NotifyPropertyChangedFor(nameof(AnimationZeigeListenFokus))]
    [NotifyPropertyChangedFor(nameof(AnimationZeigeListenUebersicht))]
    private ObservableCollection<GegnerMatrixTopf> _animationMatrizen = [];

    [ObservableProperty] private GegnerMatrixTopf? _animationFokusMatrix;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnimationZeigeFokusMatrix))]
    [NotifyPropertyChangedFor(nameof(AnimationZeigeListenFokus))]
    [NotifyPropertyChangedFor(nameof(AnimationZeigeListenUebersicht))]
    private bool _animationZeigeUebersicht;

    // Gruppenphase/K.-o.-Runde (#408, Vorbild UEFA-Gruppenauslosung bzw. DFB-Pokal): Fokus = aktueller Topf plus
    // Zielgruppe bzw. aktuelle Paarung; Übersicht = alle Gruppen/Paarungen mit dem bisherigen Stand.
    [ObservableProperty] private AnimationsTopf? _animationFokusTopf;
    [ObservableProperty] private AnimationsGruppe? _animationFokusGruppe;
    [ObservableProperty] private AnimationsPaarung? _animationFokusPaarung;

    public bool AnimationZeigeListenFokus      => AnimationZeigeListenmodus && !AnimationZeigeUebersicht;
    public bool AnimationZeigeListenUebersicht => AnimationZeigeListenmodus && AnimationZeigeUebersicht;
    /// <summary>Mini-Leiste aller Gruppen nur bei mehreren Gruppen — eine Liga hat nur eine.</summary>
    public bool AnimationZeigeGruppenLeiste => AnimationGruppen.Count > 1;
    /// <summary>Beschriftung der linken Banner-Pille — während der Übersicht und der Topf-Vorstellung steht dort
    /// kein Mannschaftsname.</summary>
    [ObservableProperty] private string _animationBannerKopf = BANNER_KOPF_MANNSCHAFT;
    private const string BANNER_KOPF_MANNSCHAFT    = "MANNSCHAFT";
    private const string BANNER_KOPF_ZWISCHENSTAND = "ZWISCHENSTAND";
    private const string BANNER_KOPF_TOPF          = "NÄCHSTER TOPF";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnimationZeigeBannerGegner))]
    private ObservableCollection<AnimationsBannerGegner> _animationBannerGegner = [];

    public bool AnimationZeigeBannerGegner => AnimationBannerGegner.Count > 0;

    public bool AnimationIstMatrixmodus   => AnimationMatrizen.Count > 0;
    public bool AnimationZeigeFokusMatrix => !AnimationZeigeUebersicht;
    /// <summary>Lostöpfe + Gruppen/Paarungen nur außerhalb der Ligaphase — dort zeigt die Matrix den Topf-Inhalt
    /// selbst (abgeblendete Zeilen = noch im Topf).</summary>
    public bool AnimationZeigeListenmodus => AnimationLaeuft && !AnimationIstMatrixmodus;

    [ObservableProperty] private string? _animationFokusKopf;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnimationFortschrittText))]
    private int _animationSchrittIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnimationFortschrittText))]
    private int _animationSchrittGesamt;

    public bool AnimationLaeuftNicht => !AnimationLaeuft;
    public string AnimationFortschrittText => $"{AnimationSchrittIndex} / {AnimationSchrittGesamt}";

    private CancellationTokenSource _abbruch = new();
    private TaskCompletionSource _weiterSignal = new();

    /// <summary>Manuelle Ziehung: jeder Schritt wartet auf "Weiter" statt auf eine Verzögerung.</summary>
    private bool _manuell;

    /// <summary>Beendet eine laufende TV-Ziehung wie "Rest sofort anzeigen" — die Ziehung ist reine Anzeige,
    /// Auslosung und Spielplan stehen schon vorher fest. Die Nutzerwege fragen vorher nach (siehe
    /// <see cref="AuslosungViewModel.ZiehungBeendenNachRueckfrageAsync"/>); die Aufrufe ohne Rückfrage sind das Netz darunter,
    /// damit die Reste einer wartenden Ziehung nie ein danach geöffnetes Detail überlagern (#406). Der Zustand
    /// wird hier sofort zurückgesetzt statt erst im finally von <see cref="AbspielenAsync"/>, damit das nicht davon
    /// abhängt, wann die Fortsetzung des abgebrochenen Ablaufs läuft.</summary>
    public void Beenden()
    {
        if (!AnimationLaeuft) return;
        _abbruch.Cancel();
        AnimationsZustandZuruecksetzen();
    }

    [RelayCommand(CanExecute = nameof(AnimationWartetAufWeiter))]
    private void AnimationWeiter() => _weiterSignal.TrySetResult();

    [RelayCommand(CanExecute = nameof(AnimationLaeuft))]
    private void AnimationUeberspringen() => _abbruch.Cancel();

    // ── Rahmen einer Ziehung ──────────────────────────────────────────────────

    /// <summary>Zeigt die Ziehung an, spielt <paramref name="ablauf"/> ab und räumt danach auf. Endet die Ziehung
    /// vorzeitig ("Rest sofort anzeigen", <see cref="Beenden"/>), bricht das Warten des Ablaufs mit einer
    /// <see cref="OperationCanceledException"/> ab — der Ablauf selbst braucht dafür keine Abbruchpunkte.</summary>
    internal async Task AbspielenAsync(int schrittGesamt, bool manuell, Func<Task> ablauf)
    {
        Starten(schrittGesamt, manuell);
        try { await ablauf(); }
        catch (OperationCanceledException) { }
        finally { AnimationsZustandZuruecksetzen(); }
    }

    private void Starten(int schrittGesamt, bool manuell)
    {
        _manuell               = manuell;
        _abbruch               = new CancellationTokenSource();
        AnimationLaeuft        = true;
        AnimationSchrittGesamt = schrittGesamt;
        AnimationSchrittIndex  = 0;
    }

    private void AnimationsZustandZuruecksetzen()
    {
        AnimationLaeuft           = false;
        AnimationWartetAufWeiter  = false;
        AnimationBannerKopf       = BANNER_KOPF_MANNSCHAFT;
        BannerLeeren();
        AnimationBannerGegner     = [];
        AnimationToepfe           = [];
        AnimationGruppen          = [];
        AnimationPaarungen        = [];
        AnimationMatrizen         = [];
        AnimationFokusKopf        = null;
        AnimationFokusMatrix      = null;
        AnimationFokusTopf        = null;
        AnimationFokusGruppe      = null;
        AnimationFokusPaarung     = null;
        AnimationZeigeUebersicht  = false;
    }

    // ── Warten ────────────────────────────────────────────────────────────────

    /// <summary>Wartet auf den nächsten Schritt: bei der manuellen Ziehung auf "Weiter", sonst
    /// <paramref name="verzoegerung"/> lang.</summary>
    internal async Task WarteAufSchrittAsync(TimeSpan verzoegerung)
    {
        if (!_manuell)
        {
            await VerzoegernAsync(verzoegerung);
            return;
        }

        _weiterSignal = new TaskCompletionSource();
        AnimationWartetAufWeiter = true;
        await _weiterSignal.Task.WaitAsync(_abbruch.Token);
        AnimationWartetAufWeiter = false;
    }

    /// <summary>Wartet <paramref name="dauer"/> lang — auch bei der manuellen Ziehung ohne "Weiter".</summary>
    internal Task VerzoegernAsync(TimeSpan dauer) => Task.Delay(dauer, _abbruch.Token);

    // ── Banner ────────────────────────────────────────────────────────────────

    /// <summary>Stellt einen Topf vor, bevor daraus gezogen wird.</summary>
    internal void BannerTopfZeigen(string topfName, string? inhalt)
    {
        AnimationBannerKopf       = BANNER_KOPF_TOPF;
        AnimationBannerMannschaft = topfName;
        AnimationBannerTopf       = inhalt;
    }

    /// <summary>Zeigt die gezogene Mannschaft — Ziel, Seite und Position folgen mit den nächsten Schritten.</summary>
    internal void BannerMannschaftZeigen(string name, string topfName)
    {
        BannerLeeren();
        AnimationBannerKopf       = BANNER_KOPF_MANNSCHAFT;
        AnimationBannerMannschaft = name;
        AnimationBannerTopf       = topfName;
    }

    internal void BannerLeeren()
    {
        AnimationBannerMannschaft = null;
        AnimationBannerTopf       = null;
        AnimationBannerZiel       = null;
        AnimationBannerSlot       = null;
        AnimationBannerPosition   = null;
    }

    /// <summary>Blendet die Gesamtübersicht als eigenen Schritt ein und danach wieder aus.</summary>
    internal async Task ZwischenstandZeigenAsync(string titel, string untertitel)
    {
        AnimationZeigeUebersicht  = true;
        AnimationBannerKopf       = BANNER_KOPF_ZWISCHENSTAND;
        AnimationBannerMannschaft = titel;
        AnimationBannerTopf       = untertitel;
        await WarteAufSchrittAsync(ZiehungsTempo.Uebersicht);
        AnimationBannerKopf       = BANNER_KOPF_MANNSCHAFT;
        AnimationBannerMannschaft = null;
        AnimationBannerTopf       = null;
        AnimationZeigeUebersicht  = false;
    }
}
