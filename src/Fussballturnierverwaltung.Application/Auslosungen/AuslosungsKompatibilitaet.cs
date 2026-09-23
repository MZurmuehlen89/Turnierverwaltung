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
    /// gelten als kompatibel (z.B. passt Zufall zu jeder Rundenart außer TopfLiga).</summary>
    public static string? InkompatibilitaetsGrund(Rundenart rundenart, Auslosungsart art) => (art, rundenart) switch
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
            "Auslosungsart 'Topf-Zuordnung' ist für die UCL-Ligaphase gedacht, " +
            "nicht für eine klassische Gruppenphase. " +
            "Verwenden Sie 'Nach Töpfen' für eine topfbasierte Gruppenauslosung.",

        // Issue #347: nur die Topf-Zuordnung liefert den Ligaphasen-Spielplan (jedes Team 2 Gegner je Topf);
        // jede andere Art erzeugt nur eine einzige Paarungsrunde. Manuell bleibt für die Vorbelegung erlaubt.
        (not (Auslosungsart.TopfZuordnung or Auslosungsart.Manuell), Rundenart.TopfLiga) =>
            "Eine TopfLiga (UCL-Ligaphase) wird ausschließlich per 'Topf-Zuordnung' ausgelost — " +
            "nur so erhält jedes Team aus jedem Topf zwei Gegner.",

        _ => null
    };

    public static bool IstKompatibel(Rundenart rundenart, Auslosungsart art) =>
        InkompatibilitaetsGrund(rundenart, art) is null;
}
