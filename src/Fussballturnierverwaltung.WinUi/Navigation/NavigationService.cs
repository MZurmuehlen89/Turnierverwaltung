using Fussballturnierverwaltung.Application.Diagnose;
using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.Helpers;
using Fussballturnierverwaltung.WinUi.Ribbon;
using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.WinUi.Navigation;

public class NavigationService : INavigationService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDialogService _dialogService;
    private readonly ILogger<NavigationService> _logger;
    private readonly SemaphoreSlim _navigationLock = new(1, 1);
    private IServiceScope? _aktiverScope;

    public ViewModelBase? AktuellesViewModel { get; private set; }
    public event EventHandler? AktuellesViewModelGeaendert;

    public NavigationService(IServiceScopeFactory scopeFactory, IDialogService dialogService,
                             ILogger<NavigationService> logger)
    {
        _scopeFactory = scopeFactory;
        _dialogService = dialogService;
        _logger = logger;
    }

    public async Task NavigiereZuAsync<TViewModel>(Action<TViewModel>? konfigurator = null) where TViewModel : ViewModelBase
    {
        if (!await DarfWegnavigierenAsync()) return;

        Ablaufprotokoll.Bedienung($"Menü → {typeof(TViewModel).Name}");

        // Navigation muss serialisiert werden: NavigiereZuAsync wird pro Klick direkt aus einem
        // RelayCommand aufgerufen. Ohne Sperre disposed ein schnell aufeinanderfolgender zweiter
        // Aufruf _aktiverScope, während der erste noch in vm.AktivierenAsync() (DB-Zugriff über
        // genau dieses Scope) hängt — die noch laufende Abfrage bricht dann mit "Verbindung ist
        // geschlossen" ab, sobald sie nach dem Dispose fortgesetzt wird.
        await _navigationLock.WaitAsync();
        try
        {
            _aktiverScope?.Dispose();
            _aktiverScope = _scopeFactory.CreateScope();
            var vm = _aktiverScope.ServiceProvider.GetRequiredService<TViewModel>();
            konfigurator?.Invoke(vm);
            AktuellesViewModel = vm;
            AktuellesViewModelGeaendert?.Invoke(this, EventArgs.Empty);

            // Das Laden der Seite als Vorgang führen: Ladeanzeige und der Ladezustand für GUI-Tests („Bereit“)
            // folgen nur offenen Vorgängen. Ohne ihn galt eine Seite als geladen, solange ihr AktivierenAsync
            // Daten ohne eigenen Vorgang holte – auf dem langsameren CI-Runner griffen Tests dann zu früh zu (#525)
            using var vorgang = _logger.Vorgang($"Seite öffnen: {typeof(TViewModel).Name}");
            await vm.AktivierenAsync();
        }
        finally
        {
            _navigationLock.Release();
        }
    }

    private async Task<bool> DarfWegnavigierenAsync()
    {
        if (AktuellesViewModel is IVerlassenBestaetigen verlassen && !await verlassen.VerlassenBestaetigenAsync())
            return false;

        if (AktuellesViewModel is not IHatUngespeicherteAenderungen { HatUngespeicherteAenderungen: true })
            return true;

        var antwort = await _dialogService.UngespeicherteAenderungenAbfragenAsync();

        if (antwort == UngespeichertAntwort.Abbrechen) return false;

        if (antwort == UngespeichertAntwort.Speichern && AktuellesViewModel is IRibbonAware { SpeichernBefehl: { } speichern })
        {
            if (speichern is CommunityToolkit.Mvvm.Input.IAsyncRelayCommand asyncSpeichern)
            {
                if (asyncSpeichern.CanExecute(null))
                    await asyncSpeichern.ExecuteAsync(null);
            }
            else if (speichern.CanExecute(null))
            {
                speichern.Execute(null);
            }

            // Speichern kann an Validierungsfehlern scheitern (Fehlermeldung bleibt im
            // ViewModel sichtbar) — in dem Fall nicht wegnavigieren, sonst geht der Kontext verloren.
            if (AktuellesViewModel is IHatUngespeicherteAenderungen { HatUngespeicherteAenderungen: true })
                return false;
        }

        return true;
    }
}
