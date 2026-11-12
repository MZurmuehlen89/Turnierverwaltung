namespace Fussballturnierverwaltung.Application.Turnierbaum;

/// <summary>Punkt auf der Zeichenfläche des Turnierbaums, in Pixeln.</summary>
public readonly record struct Punkt(double X, double Y);

/// <summary>Gerades Stück einer Verbindungslinie.</summary>
public readonly record struct Segment(Punkt Von, Punkt Nach);

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

    /// <summary>Linie der rundenweisen Ansicht von den Kindkarten der linken Spalte zu ihrer Elternkarte rechts:
    /// je Kind waagerecht bis zur Mitte, dort senkrecht zwischen den beiden Kindern (nur wenn sie sichtbar auseinander
    /// liegen) und von der Mitte waagerecht in die Elternkarte. <paramref name="links"/> und
    /// <paramref name="rechts"/> sind die rechten Kanten der Kindkarten, <paramref name="nach"/> die linke Kante der
    /// Elternkarte, jeweils auf Kartenmitte; fehlt ein Kind, entfällt sein Stück. Ohne Kinder keine Linie.</summary>
    public static IReadOnlyList<Segment> ZwischenSpalten(Punkt? links, Punkt? rechts, Punkt nach)
    {
        var segmente = new List<Segment>();
        if (links is null && rechts is null) return segmente;

        double midX = ((links ?? rechts!.Value).X + nach.X) / 2;

        if (links is { } l) segmente.Add(new(l, new Punkt(midX, l.Y)));
        if (rechts is { } r) segmente.Add(new(r, new Punkt(midX, r.Y)));
        if (links is { } lv && rechts is { } rv && Math.Abs(lv.Y - rv.Y) > 0.5)
            segmente.Add(new(new Punkt(midX, lv.Y), new Punkt(midX, rv.Y)));
        segmente.Add(new(new Punkt(midX, nach.Y), nach));
        return segmente;
    }
}
