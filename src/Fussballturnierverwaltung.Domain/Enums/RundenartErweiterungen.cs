namespace Fussballturnierverwaltung.Domain.Enums;

/// <summary>Zentrale Abfragen auf <see cref="Rundenart"/> (#571).</summary>
public static class RundenartErweiterungen
{
    /// <summary>K.-o.-Rundenarten: KoRunde, Finale, Spiel um Platz 3 – je Spiel ein Sieger, Platzhalter „Sieger/Verlierer …“,
    /// Turnierbaum. Seit #570 beantworten alle drei sowohl „wird entschieden wie ein K.-o.-Spiel?“ als auch
    /// „gehört zur K.-o.-Kette?“ gleich, deshalb eine Abfrage. EF-Queries brauchen die Liste (die Erweiterungsmethode
    /// ist nicht übersetzbar).</summary>
    public static IReadOnlyList<Rundenart> KoRundenarten { get; } =
        [Rundenart.KoRunde, Rundenart.Finale, Rundenart.SpielUmPlatzDrei];

    public static bool IstKoRunde(this Rundenart art) => KoRundenarten.Contains(art);

    /// <summary>Finale und Spiel um Platz 3: parallele Geschwister desselben Halbfinales, speisen keine Folgerunde (#198).</summary>
    public static bool IstEndspiel(this Rundenart art) => art is Rundenart.Finale or Rundenart.SpielUmPlatzDrei;

    /// <summary>Finale und Spiel um Platz 3 in beliebiger Reihenfolge (#178).</summary>
    public static bool IstEndspielGeschwister(this Rundenart a, Rundenart b) => a != b && a.IstEndspiel() && b.IstEndspiel();
}
