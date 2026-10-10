namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Wartezeiten der automatischen TV-Ziehung je Art des Schritts.</summary>
internal static class ZiehungsTempo
{
    public static readonly TimeSpan Schritt    = TimeSpan.FromMilliseconds(900);
    public static readonly TimeSpan Pausen     = TimeSpan.FromMilliseconds(500);
    public static readonly TimeSpan Aufdeck    = TimeSpan.FromMilliseconds(1600);
    public static readonly TimeSpan Gegner     = TimeSpan.FromMilliseconds(350);
    public static readonly TimeSpan Uebersicht = TimeSpan.FromMilliseconds(3000);
}
