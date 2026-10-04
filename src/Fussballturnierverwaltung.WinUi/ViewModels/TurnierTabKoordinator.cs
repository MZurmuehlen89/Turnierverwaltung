using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>
/// Tab-Container-Mechanik für <see cref="TurnierlisteViewModel"/>/<see cref="TurnierdurchfuehrungViewModel"/>
/// (#299) — Instanzliste/-auswahl/-filterung (Status-Umschalter Turniere/Vorlagen, Issue #297),
/// aktives-Tab-Tracking, IRibbonAware-Weiterleitung ans aktive Kind. Pendant zum WPF-Original
/// <c>TurnierTabKoordinator</c>. Der optionale <paramref name="turnierAuswahlService"/> (Singleton) hält die
/// Auswahl über mehrere Menüpunkte hinweg — nötig, da ViewModels bei jeder Navigation per Transient-Scope
/// neu erzeugt werden und lokaler Zustand sonst verloren ginge (analog WPFs zwei unabhängigen Menüpunkten
/// "Turnierdefinition"/"Turnierdurchführung", die dieselbe Auswahl teilen). <see cref="TurnierlisteViewModel"/>
/// nutzt ihn ebenfalls, damit die Auswahl beim Wechsel zu "Turnierdurchführung" erhalten bleibt.
/// </summary>
public partial class TurnierTabKoordinator : ObservableObject
{
    private readonly TurnierInstanzService _service;
    private readonly AusgewaehlteTurnierInstanzService? _turnierAuswahlService;
    private readonly DbContextReentrancySchutz _instanzSetzenSchutz = new();

    public ViewModelBase[] Tabs { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstanzenGefiltert))]
    private ObservableCollection<TurnierInstanz> _instanzen = [];
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstanzenGefiltert))]
    private string? _suchbegriff;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstanzenGefiltert))]
    private bool _nurVorlagenAnzeigen;
    [ObservableProperty] private TurnierInstanz? _ausgewaehlteInstanz;
    [ObservableProperty] private int _aktiverTabIndex;

    public ViewModelBase AktivesTab => Tabs[AktiverTabIndex];

    /// <summary>Turniere/Vorlagen-Filter (Issue #297) — Standard ist "nur Turniere" (Status ≠ Vorlage).</summary>
    public IEnumerable<TurnierInstanz> InstanzenGefiltert
    {
        get
        {
            var begriff = Suchbegriff;
            return Instanzen
                .Where(i => NurVorlagenAnzeigen ? i.Status == TurnierStatus.Vorlage : i.Status != TurnierStatus.Vorlage)
                .Where(i => string.IsNullOrWhiteSpace(begriff)
                    || i.Name.Contains(begriff, StringComparison.OrdinalIgnoreCase)
                    || i.Variante.Contains(begriff, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>Feuert bei jeder Ribbon-relevanten Änderung des aktiven Kinds oder bei Tab-Wechsel.</summary>
    public event EventHandler? AktivesTabZustandGeaendert;

    /// <summary>Feuert NUR bei einem echten Tab-Wechsel (nicht bei jeder Ribbon-relevanten Änderung, siehe
    /// <see cref="AktivesTabZustandGeaendert"/> — daran zu hängen würde eine Rückkopplungsschleife erzeugen,
    /// sobald der Reload selbst Ribbon-Properties ändert). Anders als bei WPF (jeder Tab-Wechsel ist dort eine
    /// echte <c>NavigationService</c>-Navigation mit <c>AktivierenAsync()</c>-Aufruf) ist ein WinUi-Tab-Wechsel
    /// nur ein Index-Wechsel auf bereits erzeugten VM-Instanzen — ohne dieses Event bleiben Daten, die sich in
    /// einem ANDEREN Tab geändert haben (z.B. Spielplan-Neugenerierung nach einer Auslosung), beim
    /// Zurückwechseln veraltet stehen (Fund aus #301-Testsession).</summary>
    public event EventHandler? AktiverTabGewechselt;

    /// <summary>Feuert bei jeder Änderung der ausgewählten Instanz (inkl. Neuladen nach Speichern/Löschen)
    /// — <see cref="TurnierlisteViewModel"/> reicht den neuen Wert direkt an alle Kind-ViewModels weiter,
    /// da hier (anders als beim WPF-Vorbild) kein geteilter Kontext-Service existiert.</summary>
    public event Action<TurnierInstanz?>? AusgewaehlteInstanzGesetzt;

    public TurnierTabKoordinator(TurnierInstanzService service, ViewModelBase[] tabs, AusgewaehlteTurnierInstanzService? turnierAuswahlService = null)
    {
        _service = service;
        Tabs = tabs;
        _turnierAuswahlService = turnierAuswahlService;
    }

    private bool _initialisierungAktiv;

    public async Task AktivierenAsync()
    {
        _initialisierungAktiv = true;
        var behalteId = _turnierAuswahlService?.TurnierInstanzId ?? AusgewaehlteInstanz?.Id;
        await InstanzenNeuLadenAsync(behalteId);
        _initialisierungAktiv = false;

        AktiviereAktivenTab(vorherigesTab: null);

        // Absichtlich KEIN AusgewaehlteInstanzGesetzt-Invoke hier: beide Composite-ViewModels
        // (TurnierlisteViewModel/TurnierdurchfuehrungViewModel) awaiten den initialen Reload direkt im
        // Anschluss an AktivierenAsync() explizit selbst (siehe dort). Ein zusätzlicher Invoke hier würde
        // denselben Reload ein zweites Mal auslösen — der Event-Handler läuft als Fire-and-Forget parallel
        // zum expliziten Await, der SequenziellMitDbContextSchutzAsync-Lock serialisiert die beiden Läufe
        // dann nur nacheinander, statt sie zusammenzuführen. Root Cause von #336 (Vorbelegung-Detailbereich
        // baute sich nach dem doppelten Reload nicht mehr auf).
    }

    /// <summary>Lädt die Instanzliste neu und wählt, sofern noch vorhanden, die Instanz mit
    /// <paramref name="behalteId"/> erneut aus — genutzt beim initialen Laden sowie nach Speichern/Löschen
    /// im Turnierkopf-Tab.</summary>
    public async Task InstanzenNeuLadenAsync(int? behalteId)
    {
        var liste = await _service.AlleAbrufenAsync();
        _instanzenWerdenNeuGeladen = true;
        try
        {
            Instanzen.Clear();
            foreach (var i in liste.OrderBy(i => i.Name)) Instanzen.Add(i);
            OnPropertyChanged(nameof(InstanzenGefiltert));

            AusgewaehlteInstanz = behalteId is { } id ? Instanzen.FirstOrDefault(i => i.Id == id) : null;
        }
        finally { _instanzenWerdenNeuGeladen = false; }
    }

    /// <summary>Optionale Rückfrage, bevor der Nutzer ein anderes Turnier auswählt (z.B. begonnene Vorbelegung
    /// verwerfen). <c>false</c> stellt die bisherige Auswahl wieder her, die Kind-ViewModels erfahren von dem
    /// Wechsel dann nichts. Gefragt wird nur beim Wechsel von einem Turnier auf ein anderes — nicht beim
    /// Neuladen der Liste (<see cref="InstanzenNeuLadenAsync"/>) und nicht bei einem Null-Durchlauf der
    /// Auswahl, wie ihn die ListView beim Tausch ihrer ItemsSource auslöst. Liefert der Aufruf einen bereits
    /// abgeschlossenen Task, läuft der Wechsel wie ohne Rückfrage synchron durch.</summary>
    public Func<Task<bool>>? InstanzVerlassenBestaetigen { get; set; }

    private bool _instanzRuecksprungAktiv;
    private bool _instanzenWerdenNeuGeladen;

    partial void OnAusgewaehlteInstanzChanged(TurnierInstanz? oldValue, TurnierInstanz? newValue)
    {
        if (_instanzRuecksprungAktiv) return;
        _ = InstanzWechselAbschliessenAsync(oldValue, newValue);
    }

    private async Task InstanzWechselAbschliessenAsync(TurnierInstanz? vorherige, TurnierInstanz? neue)
    {
        bool nutzerWechsel = !_initialisierungAktiv && !_instanzenWerdenNeuGeladen && vorherige is not null && neue is not null;
        if (nutzerWechsel && InstanzVerlassenBestaetigen is { } bestaetigen && !await bestaetigen())
        {
            _instanzRuecksprungAktiv = true;
            try { AusgewaehlteInstanz = vorherige; }
            finally { _instanzRuecksprungAktiv = false; }
            return;
        }

        if (_turnierAuswahlService is not null) _turnierAuswahlService.TurnierInstanzId = neue?.Id;
        if (_initialisierungAktiv) return;
        AusgewaehlteInstanzGesetzt?.Invoke(neue);
    }

    /// <summary>Optionale Rückfrage, bevor ein Reiter verlassen wird (Parameter: der bisherige Reiter) — z.B. bei
    /// laufender TV-Ziehung (#406). <c>false</c> springt auf den bisherigen Reiter zurück: der Pivot kennt kein
    /// "Changing"-Ereignis, der Wechsel ist beim Aufruf also schon sichtbar. Liefert der Aufruf einen bereits
    /// abgeschlossenen Task, läuft der Wechsel wie ohne Rückfrage synchron durch.</summary>
    public Func<ViewModelBase, Task<bool>>? TabVerlassenBestaetigen { get; set; }

    private bool _tabRuecksprungAktiv;

    partial void OnAktiverTabIndexChanged(int oldValue, int newValue)
    {
        if (_initialisierungAktiv || _tabRuecksprungAktiv) return;
        _ = TabWechselAbschliessenAsync(oldValue);
    }

    private async Task TabWechselAbschliessenAsync(int vorherigerIndex)
    {
        if (TabVerlassenBestaetigen is { } bestaetigen && !await bestaetigen(Tabs[vorherigerIndex]))
        {
            _tabRuecksprungAktiv = true;
            try { AktiverTabIndex = vorherigerIndex; }
            finally { _tabRuecksprungAktiv = false; }
            return;
        }

        AktiviereAktivenTab(_vorherigesTab);
        AktiverTabGewechselt?.Invoke(this, EventArgs.Empty);
    }

    private ViewModelBase? _vorherigesTab;

    private void AktiviereAktivenTab(ViewModelBase? vorherigesTab)
    {
        if (vorherigesTab is not null)
            vorherigesTab.PropertyChanged -= OnAktivemTabPropertyChanged;

        _vorherigesTab = Tabs[AktiverTabIndex];
        _vorherigesTab.PropertyChanged += OnAktivemTabPropertyChanged;

        AktivesTabZustandGeaendert?.Invoke(this, EventArgs.Empty);
    }

    private void OnAktivemTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IRibbonAware.RibbonTabTitel) or
                               nameof(IRibbonAware.NeuBefehl) or
                               nameof(IRibbonAware.SpeichernBefehl) or
                               nameof(IRibbonAware.AbbrechenBefehl) or
                               nameof(IRibbonAware.LoeschenBefehl) or
                               nameof(IRibbonAware.RibbonZusatzGruppen) or
                               nameof(IRibbonAware.NeuUnterpunkte) or
                               nameof(IHatUngespeicherteAenderungen.HatUngespeicherteAenderungen))
        {
            AktivesTabZustandGeaendert?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Serialisiert asynchrone DbContext-Zugriffe, die durch Instanz-/Tab-Wechsel ausgelöst
    /// werden (siehe <see cref="DbContextReentrancySchutz"/>). Gemeinsame Basis für
    /// <see cref="TurnierlisteViewModel"/> und <see cref="TurnierdurchfuehrungViewModel"/>
    /// (#189-Nebenfund) — beide reichen die ausgewählte Instanz per Fire-and-Forget-Event an mehrere
    /// Kind-ViewModels weiter, die sich denselben Scoped-DbContext teilen; bei schnellem Durchsteppen der
    /// Instanzliste (z.B. Pfeiltasten) überlappten sich sonst zwei dieser Weiterreichungen und lösten
    /// "A second operation was started on this context instance before a previous operation completed"
    /// aus (#317).</summary>
    public Task SequenziellMitDbContextSchutzAsync(Func<Task> aktion, Action<Exception>? fehlerBehandlung = null) =>
        _instanzSetzenSchutz.AusfuehrenAsync(aktion, fehlerBehandlung);

    /// <summary>Derselbe Schutz für Kind-ViewModels, deren eigene Aktionen (Auslosen, Zurücksetzen …) auf dem
    /// geteilten DbContext arbeiten: laufen sie daran vorbei, bekommt ein Turnier-/Reiterwechsel den Schutz
    /// sofort und kollidiert mit der noch laufenden Aktion (#413).</summary>
    public DbContextReentrancySchutz DbContextSchutz => _instanzSetzenSchutz;
}
