using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>Bausteine für Auslosungen in Tests: Mannschaften, Lostopf-Einträge, Lostöpfe und die Auslosung selbst.</summary>
internal static class AuslosungTestdaten
{
    /// <summary>Ohne Angabe bekommt jede Mannschaft ihr eigenes Land — so greift „Kein gleiches Land“ nie
    /// unbeabsichtigt.</summary>
    public static Mannschaft Team(int id, int? landId = null) =>
        new() { Id = id, Name = $"Team {id}", LandId = landId ?? id };

    public static LostopfEintrag Eintrag(Mannschaft mannschaft) =>
        new() { Id = mannschaft.Id, MannschaftId = mannschaft.Id, Mannschaft = mannschaft };

    public static Lostopf Topf(int reihenfolge, params LostopfEintrag[] eintraege) =>
        new() { Id = reihenfolge, Reihenfolge = reihenfolge, Name = $"Topf {reihenfolge}", Eintraege = [.. eintraege] };

    /// <summary>Ein Topf mit den Mannschaften <paramref name="ersteId"/> … in aufsteigender Reihenfolge.</summary>
    public static Lostopf TopfMitTeams(int reihenfolge, int ersteId, int anzahl) =>
        Topf(reihenfolge, [.. Enumerable.Range(ersteId, anzahl).Select(id => Eintrag(Team(id)))]);

    public static Auslosung NeueAuslosung(
        Rundenart rundenart, Auslosungsart auslosungsart, IEnumerable<Lostopf> lostoepfe,
        int? gruppenAnzahl = null, int gegnerJeTopf = 2)
    {
        var auslosung = new Auslosung
        {
            Id               = 1,
            TurnierInstanzId = 42,
            TurnierRundeId   = 7,
            TurnierRunde     = new TurnierRunde
            {
                Id = 7, Name = rundenart.ToString(), Rundenart = rundenart,
                GruppenAnzahl = gruppenAnzahl, RegelwerkId = 1, GegnerJeTopf = gegnerJeTopf
            },
            Auslosungsart = auslosungsart,
            Status        = AuslosungsStatus.Vorbereitung
        };
        foreach (var topf in lostoepfe) auslosung.Lostoepfe.Add(topf);
        return auslosung;
    }
}
