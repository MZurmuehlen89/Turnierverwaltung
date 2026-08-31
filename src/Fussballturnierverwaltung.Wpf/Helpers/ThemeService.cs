using ControlzEx.Theming;
using Fussballturnierverwaltung.Application.Einstellungen;
using Fussballturnierverwaltung.Domain.Enums;
using MaterialDesignThemes.Wpf;

namespace Fussballturnierverwaltung.Wpf.Helpers;

public class ThemeService(EinstellungenService einstellungenService)
{
    private readonly PaletteHelper _paletteHelper = new();

    public ThemeModus AktuellerModus { get; private set; } = ThemeModus.Hell;

    public void AnwendenAusEinstellungen(System.Windows.Application app)
    {
        Anwenden(einstellungenService.Laden().ThemeModus, app);
    }

    public void Umschalten(System.Windows.Application app)
    {
        var neuerModus = AktuellerModus == ThemeModus.Hell ? ThemeModus.Dunkel : ThemeModus.Hell;
        Anwenden(neuerModus, app);

        var einstellungen = einstellungenService.Laden();
        einstellungen.ThemeModus = neuerModus;
        einstellungenService.Speichern(einstellungen);
    }

    private void Anwenden(ThemeModus modus, System.Windows.Application app)
    {
        var theme = _paletteHelper.GetTheme();
        theme.SetBaseTheme(modus == ThemeModus.Hell ? BaseTheme.Light : BaseTheme.Dark);
        _paletteHelper.SetTheme(theme);

        ThemeManager.Current.ChangeTheme(app, modus == ThemeModus.Hell ? "Light.Blue" : "Dark.Blue");

        AktuellerModus = modus;
    }
}
