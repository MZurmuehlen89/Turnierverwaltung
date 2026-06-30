using Fussballturnierverwaltung.Wpf.ViewModels;

namespace Fussballturnierverwaltung.Wpf.Navigation;

public interface INavigationService
{
    ViewModelBase? AktuellesViewModel { get; }
    event EventHandler? AktuellesViewModelGeaendert;
    void NavigiereZu<TViewModel>(Action<TViewModel>? konfigurator = null) where TViewModel : ViewModelBase;
}
