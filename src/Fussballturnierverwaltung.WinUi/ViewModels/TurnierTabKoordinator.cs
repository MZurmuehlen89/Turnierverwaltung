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
                .Where(i => string.IsNullOrWhiteSpace(begriff) || i.Name.Contains(begriff, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>Feuert bei jeder Ribbon-relevanten Änderung des aktiven Kinds oder bei Tab-Wechsel.</summary>
    public event EventHandler? AktivesTabZustandGeaendert;

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
        AusgewaehlteInstanzGesetzt?.Invoke(AusgewaehlteInstanz);
    }

    /// <summary>Lädt die Instanzliste neu und wählt, sofern noch vorhanden, die Instanz mit
    /// <paramref name="behalteId"/> erneut aus — genutzt beim initialen Laden sowie nach Speichern/Löschen
    /// im Turnierkopf-Tab.</summary>
    public async Task InstanzenNeuLadenAsync(int? behalteId)
    {
        var liste = await _service.AlleAbrufenAsync();
        Instanzen.Clear();
        foreach (var i in liste.OrderBy(i => i.Name)) Instanzen.Add(i);
        OnPropertyChanged(nameof(InstanzenGefiltert));

        AusgewaehlteInstanz = behalteId is { } id ? Instanzen.FirstOrDefault(i => i.Id == id) : null;
    }

    partial void OnAusgewaehlteInstanzChanged(TurnierInstanz? value)
    {
        if (_turnierAuswahlService is not null) _turnierAuswahlService.TurnierInstanzId = value?.Id;
        if (_initialisierungAktiv) return;
        AusgewaehlteInstanzGesetzt?.Invoke(value);
    }

    partial void OnAktiverTabIndexChanged(int value)
    {
        if (_initialisierungAktiv) return;
        AktiviereAktivenTab(_vorherigesTab);
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
}
