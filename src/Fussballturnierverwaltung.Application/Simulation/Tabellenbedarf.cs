namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>Was ein Team aus der Tabellenlage vor einem Liga- oder Gruppenspiel braucht (#176). Vorrang in dieser Reihenfolge:
/// <see cref="OhneZiel"/>, <see cref="RemisReicht"/>, <see cref="SiegNoetig"/>, <see cref="Abstiegskampf"/>, <see cref="Offen"/>.</summary>
public enum Tabellenbedarf
{
    /// <summary>Keine Besonderheit – auch jedes Spiel ohne Tabelle (K.-o., freies Einzelspiel, Runde ohne Zonen).</summary>
    Offen = 0,

    /// <summary>Die Zone steht fest, kein Ergebnis ändert sie mehr (auch rechnerisch abgestiegen).</summary>
    OhneZiel,

    /// <summary>Mit einem Remis hat das Team die beste noch erreichbare Zone sicher.</summary>
    RemisReicht,

    /// <summary>Die beste noch erreichbare Zone erreicht das Team nur mit einem Sieg.</summary>
    SiegNoetig,

    /// <summary>Wenige Spiele vor Schluss, der Klassenerhalt ist nicht gesichert und nah dran.</summary>
    Abstiegskampf
}

/// <summary>Der Bedarf beider Teams eines Spiels (#176), aus Sicht von Heim und Gast.</summary>
public readonly record struct TabellenbedarfJeTeam(Tabellenbedarf Heim, Tabellenbedarf Gast)
{
    /// <summary>Beide Teams tauschen die Seite (Wiederholungsspiel mit getauschtem Heimrecht).</summary>
#pragma warning disable S2234 // Heim und Gast werden mit Absicht vertauscht
    public TabellenbedarfJeTeam Tauschen() => new(Gast, Heim);
#pragma warning restore S2234
}

/// <summary>Stand eines Teams vor einem Spieltag: Punkte der bisher gespielten und die Zahl der noch offenen Spiele
/// (das anstehende eingeschlossen).</summary>
public readonly record struct Tabellenstand(int MannschaftId, int Punkte, int Offen);
