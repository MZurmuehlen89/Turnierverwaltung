using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>Was die paarweisen Regeln von einem Teilnehmer wissen müssen: sein Land und seine Gruppe aus der
/// vorangegangenen Gruppenphase (siehe <see cref="GruppenHerkunft"/>). Beides fehlt bei Platzhaltern ohne
/// eindeutige Herkunft.</summary>
public readonly record struct RegelHerkunft(int? LandId, string? Gruppe);

/// <summary>Die Regeln, die zwei Teilnehmer als Gegner oder in derselben Gruppe ausschließen: „Kein gleiches
/// Land“ und „Keine gleiche Gruppe“ (#331). Gemeinsam genutzt von den Losverfahren (<see cref="RegelKontext"/>)
/// und der Vorbelegung von Hand. Ob eine Regel bei einer Runde überhaupt wirkt, entscheidet der Aufrufer.</summary>
public static class PaarungsRegeln
{
    /// <summary>Die Regel, gegen die die beiden Teilnehmer zusammen verstoßen, oder <c>null</c>. Ein Teilnehmer
    /// ohne bekanntes Land bzw. ohne bekannte Gruppe verstößt gegen die jeweilige Regel nie.</summary>
    public static AuslosungsRegelArt? VerletzteRegel(
        bool keinGleichesLand, bool keineGleicheGruppe, RegelHerkunft a, RegelHerkunft b)
    {
        if (keinGleichesLand && a.LandId.HasValue && a.LandId == b.LandId)
            return AuslosungsRegelArt.KeinGleichesLand;
        if (keineGleicheGruppe && a.Gruppe is not null && a.Gruppe == b.Gruppe)
            return AuslosungsRegelArt.KeineGleicheGruppe;
        return null;
    }
}
