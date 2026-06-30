using Fussballturnierverwaltung.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Fussballturnierverwaltung.Wpf.Navigation;

public class NavigationService : INavigationService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private IServiceScope? _aktiverScope;

    public ViewModelBase? AktuellesViewModel { get; private set; }
    public event EventHandler? AktuellesViewModelGeaendert;

    public NavigationService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public void NavigiereZu<TViewModel>(Action<TViewModel>? konfigurator = null) where TViewModel : ViewModelBase
    {
        _aktiverScope?.Dispose();
        _aktiverScope = _scopeFactory.CreateScope();
        var vm = _aktiverScope.ServiceProvider.GetRequiredService<TViewModel>();
        konfigurator?.Invoke(vm);
        AktuellesViewModel = vm;
        AktuellesViewModelGeaendert?.Invoke(this, EventArgs.Empty);
        _ = vm.AktivierenAsync();
    }
}
