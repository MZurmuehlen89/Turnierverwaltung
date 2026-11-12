namespace Fussballturnierverwaltung.Application.Turnierbaum;

/// <summary>Vertikale Streckung der Kartenabstände in der Gesamtübersicht (≥ 1): Ein breiter Baum wird beim
/// Einpassen auf die Fensterbreite verkleinert und bliebe sonst ein flaches Band in der Mitte — die Zeilenabstände
/// wachsen deshalb, bis der Baum auch die Höhe ausfüllt. Gestreckt wird alles unterhalb des Spaltenkopfs
/// (<paramref name="Kopf"/>), die Karten selbst behalten ihre Größe. Alle Werte in Pixeln.</summary>
public readonly record struct GesamtbaumStreckung(double Kopf, double Faktor)
{
    public double Strecken(double y) => Kopf + (y - Kopf) * Faktor;

    /// <summary>Oberkante einer Karte, deren Mitte gestreckt wird.</summary>
    public double KartenOben(double kartenY, double kartenHoehe) =>
        Strecken(kartenY + kartenHoehe / 2) - kartenHoehe / 2;

    /// <summary>Höhe der Zeichenfläche: der Inhalt zwischen Kopf und unterem Rand wird gestreckt.</summary>
    public double FlaechenHoehe(double baumHoehe, double rand) => Kopf + (baumHoehe - Kopf - rand) * Faktor + rand;

    /// <summary>Faktor, mit dem ein Baum der Höhe <paramref name="baumHoehe"/> die <paramref name="verfuegbareHoehe"/>
    /// ausfüllt, begrenzt auf 1 bis <paramref name="maxFaktor"/>.</summary>
    public static double FaktorZumEinpassen(double verfuegbareHoehe, double baumHoehe, double kopf, double rand,
        double maxFaktor)
    {
        double inhaltsHoehe = baumHoehe - kopf - rand;
        return inhaltsHoehe > 0
            ? Math.Clamp((verfuegbareHoehe - kopf - rand) / inhaltsHoehe, 1.0, maxFaktor)
            : 1.0;
    }
}
