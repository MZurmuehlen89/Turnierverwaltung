using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>Was die paarweisen Regeln von einem Teilnehmer wissen müssen: sein Land und seine Gruppe aus der
/// vorangegangenen Gruppenphase (siehe <see cref="GruppenHerkunft"/>). Beides fehlt bei Platzhaltern ohne
/// eindeutige Herkunft. <see cref="KandidatenGruppen"/>: bei einem Kombinationsplatz wie „3. Gruppe A/D/E/F“ die
/// Gruppen, aus denen er kommen kann — solange der Qualifikant noch nicht feststeht (Vorbelegung, #489).</summary>
public readonly record struct RegelHerkunft(int? LandId, string? Gruppe, IReadOnlyList<string>? KandidatenGruppen = null)
{
    /// <summary>Herkunft eines Platzhalters ohne aufgelöste Mannschaft: die Gruppe bzw. die Kandidaten aus seinem Namen.</summary>
    public static RegelHerkunft AusPlatzhalter(int? landId, string? platzhalterName) =>
        new(landId, GruppenHerkunft.AusPlatzhalter(platzhalterName)?.Gruppe, Kombinationstabelle.KandidatenGruppen(platzhalterName));

    /// <summary>Ob die Gruppe nur möglicherweise dieselbe ist — einer der beiden ist ein Kombinationsplatz.</summary>
    public static bool NurMoeglich(RegelHerkunft a, RegelHerkunft b) =>
        a.KandidatenGruppen is not null || b.KandidatenGruppen is not null;
}

/// <summary>Die Regeln, die zwei Teilnehmer als Gegner oder in derselben Gruppe ausschließen: „Kein gleiches
/// Land“ und „Keine gleiche Gruppe“ (#331). Gemeinsam genutzt von den Losverfahren (<see cref="RegelKontext"/>)
/// und der Vorbelegung von Hand. Ob eine Regel bei einer Runde überhaupt wirkt, entscheidet für beide
/// <see cref="AuslosungsRegelKompatibilitaet"/>.</summary>
public static class PaarungsRegeln
{
    /// <summary>Die Regel, gegen die die beiden Teilnehmer zusammen verstoßen, oder <c>null</c>. Ein Teilnehmer
    /// ohne bekanntes Land bzw. ohne bekannte Gruppe verstößt gegen die jeweilige Regel nie.</summary>
    public static AuslosungsRegelArt? VerletzteRegel(
        bool keinGleichesLand, bool keineGleicheGruppe, RegelHerkunft a, RegelHerkunft b)
    {
        if (keinGleichesLand && a.LandId.HasValue && a.LandId == b.LandId)
            return AuslosungsRegelArt.KeinGleichesLand;
        if (keineGleicheGruppe && KannGleicheGruppeSein(a, b))
            return AuslosungsRegelArt.KeineGleicheGruppe;
        return null;
    }

    /// <summary>Gleiche feste Gruppe — oder die feste Gruppe des einen gehört zu den Kandidaten des anderen
    /// („1. Gruppe A“ gegen „3. Gruppe A/D/E/F“, #489). Zwei Kombinationsplätze verstoßen nie: Sie stehen für
    /// verschiedene Teams derselben Platzierung, also für verschiedene Gruppen.</summary>
    private static bool KannGleicheGruppeSein(RegelHerkunft a, RegelHerkunft b) =>
        (a.Gruppe is not null && (a.Gruppe == b.Gruppe || b.KandidatenGruppen?.Contains(a.Gruppe) == true))
        || (b.Gruppe is not null && a.KandidatenGruppen?.Contains(b.Gruppe) == true);
}
