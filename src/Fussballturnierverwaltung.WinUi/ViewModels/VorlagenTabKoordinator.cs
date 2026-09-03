using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>
/// Tab-Container-Mechanik für <see cref="TurniervorlagenViewModel"/> — Vorlagenliste/-auswahl/-filterung,
/// aktives-Tab-Tracking, IRibbonAware-Weiterleitung ans aktive Kind. Pendant zum WPF-Original
/// <c>TurnierTabKoordinator</c> (Instanzen), aber ohne externen Singleton-Kontext-Service: Turniervorlagen
/// hat nur einen Menüpunkt, die Auswahl kann rein lokal gehalten werden (anders als bei Instanzen, wo zwei
/// unabhängige Menüpunkte "Turnierdefinition"/"Turnierdurchführung" dieselbe Auswahl teilen müssen).
/// </summary>
public partial class VorlagenTabKoordinator : ObservableObject
{
    private readonly TurnierVorlageService _service;

    public ViewModelBase[] Tabs { get; }

    [ObservableProperty] private ObservableCollection<TurnierVorlage> _vorlagen = [];
    [ObservableProperty] private ObservableCollection<TurnierVorlage> _vorlagenGefiltert = [];
    [ObservableProperty] private string? _suchbegriff;
    [ObservableProperty] private TurnierVorlage? _ausgewaehlteVorlage;
    [ObservableProperty] private int _aktiverTabIndex;

    public ViewModelBase AktivesTab => Tabs[AktiverTabIndex];

    /// <summary>Feuert bei jeder Ribbon-relevanten Änderung des aktiven Kinds oder bei Tab-Wechsel.</summary>
    public event EventHandler? AktivesTabZustandGeaendert;

    /// <summary>Feuert bei jeder Änderung der ausgewählten Vorlage (inkl. Neuladen nach Speichern/Löschen)
    /// — <see cref="TurniervorlagenViewModel"/> reicht den neuen Wert direkt an beide Kind-ViewModels weiter,
    /// da hier (anders als beim WPF-Vorbild) kein geteilter Kontext-Service existiert.</summary>
    public event Action<TurnierVorlage?>? AusgewaehlteVorlageGesetzt;

    public VorlagenTabKoordinator(TurnierVorlageService service, ViewModelBase[] tabs)
    {
        _service = service;
        Tabs = tabs;
    }

    private bool _initialisierungAktiv;

    public async Task AktivierenAsync()
    {
        _initialisierungAktiv = true;
        await VorlagenNeuLadenAsync(AusgewaehlteVorlage?.Id);
        _initialisierungAktiv = false;

        AktiviereAktivenTab(vorherigesTab: null);
        AusgewaehlteVorlageGesetzt?.Invoke(AusgewaehlteVorlage);
    }

    /// <summary>Lädt die Vorlagenliste neu und wählt, sofern noch vorhanden, die Vorlage mit
    /// <paramref name="behalteId"/> erneut aus — genutzt beim initialen Laden sowie nach Speichern/Löschen
    /// im Turnierkopf-Tab.</summary>
    public async Task VorlagenNeuLadenAsync(int? behalteId)
    {
        var liste = await _service.AlleAbrufenAsync();
        Vorlagen.Clear();
        foreach (var v in liste.OrderBy(v => v.Name)) Vorlagen.Add(v);
        AktualisiereVorlagenGefiltert();

        AusgewaehlteVorlage = behalteId is { } id ? Vorlagen.FirstOrDefault(v => v.Id == id) : null;
    }

    partial void OnAusgewaehlteVorlageChanged(TurnierVorlage? value)
    {
        if (_initialisierungAktiv) return;
        AusgewaehlteVorlageGesetzt?.Invoke(value);
    }

    partial void OnAktiverTabIndexChanged(int value)
    {
        if (_initialisierungAktiv) return;
        AktiviereAktivenTab(_vorherigesTab);
    }

    partial void OnSuchbegriffChanged(string? value) => AktualisiereVorlagenGefiltert();

    private void AktualisiereVorlagenGefiltert()
    {
        var begriff = Suchbegriff;
        var treffer = string.IsNullOrWhiteSpace(begriff)
            ? Vorlagen
            : Vorlagen.Where(v => v.Name.Contains(begriff, StringComparison.OrdinalIgnoreCase));
        VorlagenGefiltert = new ObservableCollection<TurnierVorlage>(treffer);
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
                               nameof(IHatUngespeicherteAenderungen.HatUngespeicherteAenderungen))
        {
            AktivesTabZustandGeaendert?.Invoke(this, EventArgs.Empty);
        }
    }
}
