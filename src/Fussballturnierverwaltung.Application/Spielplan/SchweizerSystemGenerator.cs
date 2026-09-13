using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>
/// Schweizer System: generiert für jeden Spieltag eine neue Runde basierend auf der
/// aktuellen Tabelle. Vermeidet Rückspiele gegen bereits gespielte Gegner (Backtracking).
/// Bei ungerader Teamanzahl erhält das niedrigstplatzierte Team ohne vorheriges Freilos
/// (Rotation) ein Freilos. Farbausgleich: das Team mit weniger bisherigen Heimspielen bekommt
/// den Heimplatz. Die Paarungslogik selbst (Rang-Nachbar-Backtracking) ist eine bewusste
/// Vereinfachung gegenüber dem vollständigen FIDE-Dutch-System (explizite Punktgruppen,
/// Downfloater, Transpositionen) — siehe Issue #330.
/// </summary>
public class SchweizerSystemGenerator : ISpielplanGenerator
{
    public IReadOnlyList<Rundenart> UnterstützteRundenarten => [Rundenart.SchweizerSystem];

    public SpielplanErgebnis Generieren(SpielplanKontext kontext)
    {
        var teams = kontext.Mannschaften.ToList();
        int n = teams.Count;

        if (n < 2) throw new InvalidOperationException(
            $"Schweizer System benötigt mindestens 2 Mannschaften (waren: {n}).");

        // Normalize forbidden pairs: (min(a,b), max(a,b)) for order-independent lookup
        var verboten = kontext.BereitsGespielt is null
            ? new HashSet<(int, int)>()
            : kontext.BereitsGespielt
                .Select(p => (Math.Min(p.TeamAId, p.TeamBId), Math.Max(p.TeamAId, p.TeamBId)))
                .ToHashSet();

        // Odd number of teams: niedrigstplatziertes Team ohne vorheriges Freilos sitzt aus
        // (Rotation) — Fallback auf absolut letztes Team, falls alle verbleibenden Kandidaten
        // schon einmal ausgesetzt haben.
        if (n % 2 == 1)
        {
            var freilosHistorie = kontext.FreilosHistorie ?? (IReadOnlySet<int>)new HashSet<int>();
            var freilosTeam = teams.AsEnumerable().Reverse().FirstOrDefault(t => !freilosHistorie.Contains(t.Id))
                            ?? teams[^1];
            teams = teams.Where(t => t.Id != freilosTeam.Id).ToList();
        }

        // Try strict Swiss pairing (no rematch), fall back to best-effort if all opponents met
        var paarungen = SchweizerPaaren(teams, verboten)
                     ?? SchweizerPaaren(teams, [])!;

        var heimBilanz = kontext.HeimBilanz ?? new Dictionary<int, int>();

        int spieltag = kontext.Spieltag ?? 1;
        int spielNr  = kontext.StartSpielNummer;
        var spiele   = new List<Spiel>();

        foreach (var (a, b) in paarungen)
        {
            // Farbausgleich: das Team mit weniger bisherigen Heimspielen bekommt den Heimplatz;
            // bei Gleichstand bleibt das ranghöhere Team (a) Heim.
            var (heim, auswaerts) = heimBilanz.GetValueOrDefault(a.Id) <= heimBilanz.GetValueOrDefault(b.Id)
                ? (a, b) : (b, a);

            spiele.Add(new Spiel
            {
                TurnierInstanzId      = kontext.Runde.TurnierInstanzId,
                TurnierRundeId        = kontext.Runde.Id,
                Spielnummer           = spielNr++,
                Spieltag              = spieltag,
                HeimMannschaftId      = heim.Id,
                AuswaertsMannschaftId = auswaerts.Id,
                Status                = SpielStatus.Geplant,
                SimulationsModus      = SimulationsModus.SchnellSimulation
            });
        }

        return new SpielplanErgebnis { Runde = kontext.Runde, Spiele = spiele };
    }

    /// <summary>
    /// Rekursives Backtracking: paart teams[0] mit dem erstbesten Partner ohne Rematch,
    /// löst das Restproblem rekursiv. Gibt null zurück wenn keine zulässige Paarung existiert.
    /// </summary>
    private static List<(Mannschaft, Mannschaft)>? SchweizerPaaren(
        List<Mannschaft> teams,
        HashSet<(int, int)> verboten)
    {
        if (teams.Count == 0) return [];

        var erster     = teams[0];
        var kandidaten = teams.Skip(1).ToList();

        for (int j = 0; j < kandidaten.Count; j++)
        {
            var zweiter = kandidaten[j];
            var key     = (Math.Min(erster.Id, zweiter.Id), Math.Max(erster.Id, zweiter.Id));

            if (verboten.Contains(key)) continue;

            var restTeams     = kandidaten.Where((_, i) => i != j).ToList();
            var restPaarungen = SchweizerPaaren(restTeams, verboten);

            if (restPaarungen is not null)
            {
                var result = new List<(Mannschaft, Mannschaft)> { (erster, zweiter) };
                result.AddRange(restPaarungen);
                return result;
            }
        }

        return null;
    }
}
