using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>Wie das Ergebnis einer Auslosung gezeigt wird (#496).</summary>
public enum AuslosungsErgebnisart
{
    /// <summary>Flache Paarungsliste — auch, solange es noch keine Ergebnisse gibt.</summary>
    Paarungen,
    /// <summary>Je Mannschaft eine Zeile ohne Gegner, gruppiert nach Gruppe.</summary>
    Gruppen,
    /// <summary>Ligaphase: Gegner-Matrix je Topf statt Paarungsliste (#362).</summary>
    GegnerMatrix,
}

public static class AuslosungsErgebnisartErmittlung
{
    public static AuslosungsErgebnisart Von(Auslosung auslosung)
    {
        if (!auslosung.Ergebnisse.Any()) return AuslosungsErgebnisart.Paarungen;
        if (IstGruppenergebnis(auslosung.Ergebnisse)) return AuslosungsErgebnisart.Gruppen;
        if (auslosung.Auslosungsart == Auslosungsart.TopfZuordnung && auslosung.Lostoepfe.Count >= 2)
            return AuslosungsErgebnisart.GegnerMatrix;
        return AuslosungsErgebnisart.Paarungen;
    }

    /// <summary>Eine Gruppenauslosung speichert je Mannschaft eine Zeile ohne Gegner — die eine Erkennung für
    /// Ergebnisanzeige und TV-Modus (<see cref="ZiehungsReihenfolge"/>). Ohne Ergebnisse: nein.</summary>
    internal static bool IstGruppenergebnis(IEnumerable<AuslosungsErgebnis> ergebnisse) =>
        ergebnisse.Any() &&
        ergebnisse.All(e => !e.IstFreilos && e.AuswaertsMannschaftId == null && e.AuswaertsPlatzhalterName == null);
}
