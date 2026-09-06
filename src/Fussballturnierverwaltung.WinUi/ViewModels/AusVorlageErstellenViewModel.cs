using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Datenhaltung des "Aus Vorlage erstellen"-Dialogs (Issue #297-Nachzug) — nutzt den seit #294
/// bestehenden <see cref="TurnierKlonService"/>, der bislang in keiner UI verdrahtet war (Scope-Lücke aus
/// #294, siehe Memory-Notiz project_vorlage_instanz_vereinheitlichung). Bietet zusätzlich zum eigentlichen
/// Vorlage→Turnier-Zweck auch "als weitere Vorlage klonen" an, da <see cref="TurnierKlonService.KlonenAsync"/>
/// beide Fälle mit demselben strukturellen Kopiervorgang abdeckt (löst nebenbei #46).</summary>
public partial class AusVorlageErstellenViewModel : ObservableObject
{
    private readonly TurnierInstanzService _instanzService;
    private readonly TurnierKlonService _klonService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ErstellenCommand))]
    [NotifyPropertyChangedFor(nameof(KannErstellen))]
    private ObservableCollection<TurnierInstanz> _vorlagen = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ErstellenCommand))]
    [NotifyPropertyChangedFor(nameof(KannErstellen))]
    private TurnierInstanz? _ausgewaehlteVorlage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ErstellenCommand))]
    [NotifyPropertyChangedFor(nameof(KannErstellen))]
    private string _name = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ErstellenCommand))]
    [NotifyPropertyChangedFor(nameof(KannErstellen))]
    private string _saisonOderJahr = "";

    [ObservableProperty] private bool _alsVorlageAnlegen;
    [ObservableProperty] private bool _istLadend;
    [ObservableProperty] private string? _fehlermeldung;

    public bool KeineVorlagenVorhanden => !IstLadend && Vorlagen.Count == 0;

    /// <summary>Plain-bool-Pendant zu <see cref="ErstellenCommand"/>s CanExecute — <c>IRelayCommand</c> hat
    /// keine bindbare CanExecute-Property (nur eine Methode), <c>ContentDialog.IsPrimaryButtonEnabled</c>
    /// braucht daher diesen eigenen, mitgeführten Wert (analog <c>RanglistenImportDialog.VorschauVorhanden</c>).</summary>
    public bool KannErstellen =>
        AusgewaehlteVorlage is not null && !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(SaisonOderJahr);

    /// <summary>Ergebnis nach erfolgreichem Klonen — vom Dialog gelesen, um die geteilte Instanzliste des
    /// Composites neu zu laden und die neue Instanz auszuwählen.</summary>
    public TurnierInstanz? ErstellteInstanz { get; private set; }

    public AusVorlageErstellenViewModel(TurnierInstanzService instanzService, TurnierKlonService klonService)
    {
        _instanzService = instanzService;
        _klonService = klonService;
    }

    public async Task LadenAsync()
    {
        IstLadend = true;
        Fehlermeldung = null;
        try
        {
            var alle = await _instanzService.AlleAbrufenAsync();
            Vorlagen = new ObservableCollection<TurnierInstanz>(
                alle.Where(i => i.Status == TurnierStatus.Vorlage).OrderBy(i => i.Name));
            AusgewaehlteVorlage = Vorlagen.FirstOrDefault();
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
        finally { IstLadend = false; OnPropertyChanged(nameof(KeineVorlagenVorhanden)); }
    }

    partial void OnAusgewaehlteVorlageChanged(TurnierInstanz? value) => Name = value?.Name ?? "";

    [RelayCommand(CanExecute = nameof(KannErstellen))]
    public async Task ErstellenAsync()
    {
        if (AusgewaehlteVorlage is null) return;
        Fehlermeldung = null;
        try
        {
            var zielStatus = AlsVorlageAnlegen ? TurnierStatus.Vorlage : TurnierStatus.Geplant;
            ErstellteInstanz = await _klonService.KlonenAsync(AusgewaehlteVorlage.Id, Name, SaisonOderJahr, zielStatus);
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }
}
