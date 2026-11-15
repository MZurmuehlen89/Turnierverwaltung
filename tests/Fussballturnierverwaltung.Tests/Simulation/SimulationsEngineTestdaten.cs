using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Simulation;

internal static class SimulationsEngineTestdaten
{
    public static Mannschaft Team(int id, int off = 70, int mid = 70, int def = 70, int heim = 75, int ausw = 65) =>
        new()
        {
            Id = id, Name = $"Team {id}", Kurzname = $"T{id}",
            OffensivRating = off, MittelfeldRating = mid, DefensivRating = def,
            Heimstaerke = heim, Auswaertsstaerke = ausw
        };

    public static EntscheidungsVerfahren Verfahren(VerfahrenArt art, int rang = 1) =>
        new() { VerfahrenArt = art, Reihenfolge = rang };

    /// <summary>Verfahren in der angegebenen Reihenfolge, Rang 1, 2, …</summary>
    public static EntscheidungsVerfahren[] VerfahrenKette(params VerfahrenArt[] arten) =>
        [.. arten.Select((art, i) => Verfahren(art, rang: i + 1))];

    /// <summary>Zufallswerte einer Torbeschreibung bei <see cref="VorgegebenerZufall"/>: Stil, kein Konter, Zone.</summary>
    public static readonly double[] TorstilOhneKonter = [0.0, 0.5, 0.0];

    // Gleich starke Teams, deren Torerwartung allein am Ortsfaktor hängt: das Heimteam spielt daheim mit 100 %
    // (λ = 1,95), das Gastteam auswärts mit 50 % (λ = 0,975). So fällt eine vertauschte Heim-/Gastseite auf.
    public static Mannschaft HeimTeam() => Team(1, heim: 100, ausw: 50);
    public static Mannschaft GastTeam() => Team(2, heim: 100, ausw: 50);

    /// <summary>Stand als nullbares Paar – ein Tupel aus <c>int</c> wäre für FluentAssertions nie gleich.</summary>
    public static (int?, int?) Stand(int heim, int gast) => (heim, gast);
}
