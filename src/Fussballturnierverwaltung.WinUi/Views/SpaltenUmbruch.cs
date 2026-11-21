namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>#247: Die Entscheidung des <see cref="SpaltenPanel"/>, ob die Karten nebeneinander oder untereinander stehen –
/// als reine Funktion ohne Oberfläche, damit die Unit-Tests sie erreichen (die Datei ist in das Testprojekt verknüpft).</summary>
public static class SpaltenUmbruch
{
    public const double ABSTAND = 12;

    /// <summary>Zusätzliche Breite, die ein Panel im Untereinander-Modus haben muss, bevor es zurück nebeneinander
    /// wechselt: Untereinander blendet der umgebende Bildlauf einen senkrechten Balken ein, der Breite kostet. Ohne die
    /// Hysterese kippte das Layout an der Grenze bei jedem Durchlauf zwischen beiden Modi.</summary>
    public const double HYSTERESE = 24;

    public static double Spaltenbreite(double breite, int anzahl) => (breite - ABSTAND * (anzahl - 1)) / anzahl;

    public static bool Untereinander(double breite, int anzahl, double minSpaltenbreite, bool bisherUntereinander)
    {
        if (anzahl < 2) return false;
        double schwelle = minSpaltenbreite + (bisherUntereinander ? HYSTERESE : 0);
        return Spaltenbreite(breite, anzahl) < schwelle;
    }
}
