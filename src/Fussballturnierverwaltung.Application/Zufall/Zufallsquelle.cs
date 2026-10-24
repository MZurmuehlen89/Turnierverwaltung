namespace Fussballturnierverwaltung.Application.Zufall;

/// <summary>
/// Einzige Quelle für Zufall in Auslosung, Spielplan und Simulation (#519). Ohne festen Seed ist jeder Vorgang
/// zufällig wie bisher. Mit festem Seed (Konfiguration <c>Zufall:Seed</c>, für GUI-Tests, #494) beginnt jeder
/// Vorgang mit derselben Zufallsfolge, damit sich ein fehlgeschlagener Lauf reproduzieren lässt.
/// Jeder Vorgang bekommt ein eigenes <see cref="Random"/>: so teilen sich keine zwei Threads eine Instanz.
/// </summary>
public sealed class Zufallsquelle(int? festerSeed = null)
{
    public int? FesterSeed => festerSeed;

    public int SeedZiehen() => festerSeed ?? Random.Shared.Next();

    public Random Erzeugen() => new(SeedZiehen());
}
