using System.Windows.Media;
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
        // Dark Mode vorerst deaktiviert (Nutzerentscheidung nach mehreren Korrekturrunden zu #270/#268 —
        // wiederkehrende native-Control-Fallstricke wie SystemColors-Bindungen und das ComboBox-Popup-
        // Template, siehe feedback_wpf_darktheme_systemcolors und project_theme_konsistenz_270 im
        // Memory). Gespeicherter ThemeModus wird bewusst ignoriert, damit die App auch dann hell startet,
        // wenn zuvor lokal Dunkel gewählt/gespeichert wurde — kein Weg mehr, das über die UI zu ändern
        // (Ribbon-Umschalter ist ausgeblendet, siehe MainWindow.xaml). Restlicher Theme-Code bleibt
        // unangetastet: Dark Mode ist ein eigenes, sauber geplantes Thema für eine spätere Sitzung, kein
        // gelöschtes Feature.
        Anwenden(ThemeModus.Hell, app);
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

        // RowHoverAccentBrush (Issue #270): projekteigener Hover-Ton, kein MaterialDesign-Brush und
        // daher nicht automatisch durch PaletteHelper.SetTheme theme-reaktiv — hier analog dazu manuell
        // je Modus gesetzt. Werte visuell gegen die laufende App abgeglichen (Mockup-Artifact zu #270).
        app.Resources["RowHoverAccentBrush"] = new SolidColorBrush(
            modus == ThemeModus.Hell
                ? (Color)ColorConverter.ConvertFromString("#DCE9FA")
                : (Color)ColorConverter.ConvertFromString("#243447"));

        AktuellerModus = modus;
    }
}
