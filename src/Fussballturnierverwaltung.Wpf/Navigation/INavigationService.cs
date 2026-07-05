using Fussballturnierverwaltung.Wpf.ViewModels;

namespace Fussballturnierverwaltung.Wpf.Navigation;

public interface INavigationService
{
    ViewModelBase? AktuellesViewModel { get; }
    event EventHandler? AktuellesViewModelGeaendert;

    /// <summary>
    /// Navigiert zum angegebenen ViewModel. Hat das aktuelle ViewModel ungespeicherte
    /// Änderungen (<see cref="IHatUngespeicherteAenderungen"/>), wird vorher per Dialog
    /// nachgefragt (Speichern/Verwerfen/Abbrechen). Bei "Abbrechen" oder fehlgeschlagenem
    /// Speichern findet keine Navigation statt.
    /// </summary>
    Task NavigiereZuAsync<TViewModel>(Action<TViewModel>? konfigurator = null) where TViewModel : ViewModelBase;
}
