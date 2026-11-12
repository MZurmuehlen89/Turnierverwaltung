namespace Fussballturnierverwaltung.Application.Turnierbaum;

/// <summary>Punkt auf der Zeichenfläche des Turnierbaums, in Pixeln.</summary>
public readonly record struct Punkt(double X, double Y);

/// <summary>Verbindungslinien zwischen den Karten des Turnierbaums — reine Geometrie, das Zeichnen macht die
/// Ansicht.</summary>
public static class Verbindungslinien
{
    /// <summary>Linie der Gesamtübersicht von einer Kind- zu ihrer Elternkarte als Treppe mit vier Punkten: waagerecht
    /// aus der Kindkarte bis zur Mitte, senkrecht auf Höhe der Elternkarte, waagerecht hinein. Kinder liegen weiter
    /// außen: links vom Elternknoten verbindet die Linie die rechte Kante des Kindes mit der linken des Elternknotens,
    /// rechts davon gespiegelt. Die Y-Werte sind die Kartenmitten.</summary>
    public static IReadOnlyList<Punkt> ZwischenKarten(double kindX, double kindMitteY, double elternX,
        double elternMitteY, double kartenBreite)
    {
        bool kindLinks = kindX < elternX;
        double x1 = kindLinks ? kindX + kartenBreite : kindX;
        double x2 = kindLinks ? elternX : elternX + kartenBreite;
        double mitteX = (x1 + x2) / 2;

        return [new(x1, kindMitteY), new(mitteX, kindMitteY), new(mitteX, elternMitteY), new(x2, elternMitteY)];
    }
}
