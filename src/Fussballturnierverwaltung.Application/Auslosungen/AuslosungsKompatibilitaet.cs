using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>
/// Zentrale Kompatibilitäts-Matrix Rundenart × Auslosungsart (Issue #94) — vorher unabhängig
/// als serverseitige Hart-Validierung (<see cref="Fussballturnierverwaltung.Application.Validierung.AuslosungsValidator"/>)
/// und als reiner Text-Warnhinweis in der WPF-UI gepflegt, jetzt eine einzige Quelle für beides:
/// <see cref="AuslosungsValidator"/> wirft anhand von <see cref="InkompatibilitaetsGrund"/>, die
/// WPF-ComboBox blendet damit inkompatible Auslosungsarten von vornherein aus, statt sie wählbar
/// zu lassen und erst beim Speichern abzulehnen.
/// </summary>
public static class AuslosungsKompatibilitaet
{
    /// <summary>Liefert den Grund, warum <paramref name="art"/> für <paramref name="rundenart"/> nicht
    /// wählbar ist, oder <c>null</c>, wenn die Kombination kompatibel ist. Nicht erfasste Kombinationen
    /// gelten als kompatibel (z.B. passt Zufall zu jeder Rundenart außer TopfLiga).
    /// <paramref name="paarungFestVorgegeben"/>: ob die Runde ihre Paarungen in der Vorbelegung festlegt — dort
    /// ist „Manuell“ die Art jeder Runde, auch einer TopfLiga (#482).</summary>
    public static string? InkompatibilitaetsGrund(
        Rundenart rundenart, Auslosungsart art, bool paarungFestVorgegeben = false) => (art, rundenart) switch
    {
        (Auslosungsart.GruppensiegerGegenGruppenzweite,
            not (Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei)) =>
            "Auslosungsart 'Gruppensieger vs. Zweite' ist nur für K.O.-Runden geeignet, " +
            "die auf eine Gruppenphase folgen.",

        (Auslosungsart.TopfZuordnung, Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei) =>
            "Auslosungsart 'Topf-Zuordnung' weist Teams direkt Gruppenplätzen zu " +
            "und ist für K.O.-Runden nicht geeignet. " +
            "Verwenden Sie stattdessen 'Zufällig' oder 'Nach Töpfen'.",

        (Auslosungsart.TopfZuordnung, Rundenart.Gruppenphase) =>
            "Auslosungsart 'Topf-Zuordnung' ist für die Ligaphase der UEFA-Klubwettbewerbe gedacht, " +
            "nicht für eine klassische Gruppenphase. " +
            "Verwenden Sie 'Nach Töpfen' für eine topfbasierte Gruppenauslosung.",

        // Die Ziehung von Hand (#482) kennt je Team nur einen Gegner bzw. eine Gruppe, die Ligaphase braucht je
        // Team 6 oder 8 Gegner (#469). Die Vorbelegung einer TopfLiga bleibt erlaubt.
        (Auslosungsart.Manuell, Rundenart.TopfLiga) when !paarungFestVorgegeben =>
            "Eine TopfLiga (Ligaphase) lässt sich nicht von Hand ziehen — jedes Team braucht Gegner aus allen " +
            "Töpfen. Verwenden Sie 'Topf-Zuordnung'.",

        // Issue #347: nur die Topf-Zuordnung liefert den Ligaphasen-Spielplan (jedes Team Gegner aus allen Töpfen);
        // jede andere Art erzeugt nur eine einzige Paarungsrunde.
        (not (Auslosungsart.TopfZuordnung or Auslosungsart.Manuell), Rundenart.TopfLiga) =>
            "Eine TopfLiga (Ligaphase) wird ausschließlich per 'Topf-Zuordnung' ausgelost — " +
            "nur so erhält jedes Team Gegner aus allen Töpfen.",

        _ => null
    };

    public static bool IstKompatibel(Rundenart rundenart, Auslosungsart art, bool paarungFestVorgegeben = false) =>
        InkompatibilitaetsGrund(rundenart, art, paarungFestVorgegeben) is null;
}
