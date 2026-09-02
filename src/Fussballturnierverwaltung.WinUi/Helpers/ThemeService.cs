using Fussballturnierverwaltung.Application.Einstellungen;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.UI.Xaml;

namespace Fussballturnierverwaltung.WinUi.Helpers;

/// <summary>Hält wie <see cref="Fussballturnierverwaltung.WinUi.Dialoge.DialogService"/> eine
/// Referenz auf das Wurzelelement, dessen <see cref="FrameworkElement.RequestedTheme"/> beim
/// Umschalten gesetzt wird — MainViewModel kennt kein UI-Element, das MainWindow setzt die
/// Referenz beim Start.</summary>
public class ThemeService
{
    private readonly EinstellungenService _einstellungenService;
    private FrameworkElement? _root;

    // Sofort im Konstruktor geladen, nicht erst bei AnwendenAusEinstellungen(): MainViewModel liest
    // AktuellerModus bereits in seinem eigenen Konstruktor, der per DI VOR MainWindow (und damit vor
    // dem RootSetzen/AnwendenAusEinstellungen-Aufruf) aufgelöst wird — sonst bliebe IstDunkelModus im
    // ViewModel auf dem Default "Hell" hängen, bis der Nutzer den Toggle einmal betätigt.
    public ThemeModus AktuellerModus { get; private set; }

    public ThemeService(EinstellungenService einstellungenService)
    {
        _einstellungenService = einstellungenService;
        AktuellerModus = _einstellungenService.Laden().ThemeModus;
    }

    public void RootSetzen(FrameworkElement root) => _root = root;

    public void AnwendenAusEinstellungen() => Anwenden(AktuellerModus);

    public void Umschalten()
    {
        var neuerModus = AktuellerModus == ThemeModus.Hell ? ThemeModus.Dunkel : ThemeModus.Hell;
        Anwenden(neuerModus);

        var einstellungen = _einstellungenService.Laden();
        einstellungen.ThemeModus = neuerModus;
        _einstellungenService.Speichern(einstellungen);
    }

    private void Anwenden(ThemeModus modus)
    {
        if (_root is not null)
            _root.RequestedTheme = modus == ThemeModus.Hell ? ElementTheme.Light : ElementTheme.Dark;
        AktuellerModus = modus;
    }
}
