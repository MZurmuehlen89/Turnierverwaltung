using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Wpf.Ribbon;
using Fussballturnierverwaltung.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace Fussballturnierverwaltung.Wpf.Navigation;

public class NavigationService : INavigationService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SemaphoreSlim _navigationLock = new(1, 1);
    private IServiceScope? _aktiverScope;

    public ViewModelBase? AktuellesViewModel { get; private set; }
    public event EventHandler? AktuellesViewModelGeaendert;

    public NavigationService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task NavigiereZuAsync<TViewModel>(Action<TViewModel>? konfigurator = null) where TViewModel : ViewModelBase
    {
        if (!await DarfWegnavigierenAsync()) return;

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
            await vm.AktivierenAsync();
        }
        finally
        {
            _navigationLock.Release();
        }
    }

    private async Task<bool> DarfWegnavigierenAsync()
    {
        if (AktuellesViewModel is not IHatUngespeicherteAenderungen { HatUngespeicherteAenderungen: true })
            return true;

        var ergebnis = MessageBox.Show(
            "Es gibt ungespeicherte Änderungen. Speichern, Verwerfen oder Abbrechen?",
            "Ungespeicherte Änderungen",
            MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

        if (ergebnis == MessageBoxResult.Cancel) return false;

        if (ergebnis == MessageBoxResult.Yes && AktuellesViewModel is IRibbonAware { SpeichernBefehl: { } speichern })
        {
            if (speichern is IAsyncRelayCommand asyncSpeichern)
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
