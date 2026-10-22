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

    /// <summary>Anzahl Lostöpfe nach dem Wechsel auf <paramref name="art"/>: 1, wo nur ein Topf erlaubt ist;
    /// die Topf-Zuordnung braucht mindestens 2 gleich große Töpfe — dann 4 wie in der UCL-Ligaphase (#347).</summary>
    public static int LostoepfeNachWechsel(Auslosungsart art, int bisher)
    {
        if (!ErlaubtMehrereToepfe(art)) return 1;
        if (art == Auslosungsart.TopfZuordnung && bisher < 2) return 4;
        return bisher;
    }
}
