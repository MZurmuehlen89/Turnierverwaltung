using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>Welche Auslosungsart und wie viele Lostöpfe eine Auslosung anbietet bzw. vorschlägt (#347, #496).
/// Gilt für die Auslosung, nicht für die Vorbelegung — „Manuell“ ist hier die Ziehung von Hand (#482).</summary>
public static class AuslosungsartVorgabe
{
    /// <summary>Die zur Rundenart passenden Arten in Enum-Reihenfolge; ohne Rundenart alle.</summary>
    public static IReadOnlyList<Auslosungsart> KompatibleArten(Rundenart? rundenart) =>
        Enum.GetValues<Auslosungsart>()
            .Where(a => rundenart is null || AuslosungsKompatibilitaet.IstKompatibel(rundenart.Value, a))
            .ToList();

    /// <summary>Art, mit der die Auslosung von <paramref name="runde"/> startet: zuerst die von der Vorlage
    /// vorgegebene Art der Runde, sonst die bisherige Auswahl, solange sie zur Rundenart passt, sonst die erste
    /// kompatible Art (z.B. Topf-Zuordnung bei einer TopfLiga).</summary>
    public static Auslosungsart StartFuer(TurnierRunde? runde, Auslosungsart bisher)
    {
        if (runde is null) return bisher;
        if (runde.VorgegebeneAuslosungsart is { } vorgabe && AuslosungsKompatibilitaet.IstKompatibel(runde.Rundenart, vorgabe))
            return vorgabe;
        if (AuslosungsKompatibilitaet.IstKompatibel(runde.Rundenart, bisher))
            return bisher;
        return KompatibleArten(runde.Rundenart).DefaultIfEmpty(Auslosungsart.Zufall).First();
    }

    /// <summary>Zufall/Benutzerdefiniert kennen keine Topf-Struktur und sind auf genau 1 Lostopf begrenzt (Issue #134).</summary>
    public static bool ErlaubtMehrereToepfe(Auslosungsart art) =>
        art is not (Auslosungsart.Zufall or Auslosungsart.Benutzerdefiniert);

    /// <summary>Anzahl Lostöpfe nach dem Wechsel auf <paramref name="art"/>: 1, wo nur ein Topf erlaubt ist. Arten
    /// mit Topf-Struktur bekommen statt eines einzelnen Topfs die Zahl, die sie brauchen; zwei oder mehr gewählte
    /// Töpfe bleiben stehen. Die Topf-Zuordnung braucht 4 gleich große Töpfe wie die UCL-Ligaphase (#347), bei
    /// einem Gegner je Topf 6 wie die UECL-Ligaphase (#469); „Nach Töpfen“ in der Gruppenphase einen Topf je
    /// Gruppenplatz (Teams ÷ Gruppen), in der K.-o.-Runde wie „Gruppensieger vs. Zweite“ zwei, die gegeneinander
    /// gelost werden (#564). „Nach Setzliste“ ebenso — Topf 1 ist dort die Setzliste (#563), mit nur einem Topf
    /// wäre jedes Team gesetzt.</summary>
    public static int LostoepfeNachWechsel(Auslosungsart art, int bisher, TurnierRunde? runde)
    {
        if (!ErlaubtMehrereToepfe(art)) return 1;
        if (bisher >= 2) return bisher;
        return (art, runde) switch
        {
            (Auslosungsart.TopfZuordnung, _) => runde?.GegnerJeTopf == 1 ? 6 : 4,
            (Auslosungsart.NachToepfen or Auslosungsart.NachSetzliste,
                { Rundenart: Rundenart.Gruppenphase, GruppenAnzahl: > 0 and var gruppen })
                when runde.EingangsMannschaftsanzahl >= 2 * gruppen => runde.EingangsMannschaftsanzahl / gruppen,
            (Auslosungsart.NachToepfen or Auslosungsart.NachSetzliste or Auslosungsart.GruppensiegerGegenGruppenzweite,
                { Rundenart: Rundenart.KoRunde }) => 2,
            _ => bisher
        };
    }
}
