namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Prüft, ob die ausgehenden Übergangsregeln einer Runde so viele Plätze weiterreichen, wie die Runde
/// als Ausgang hat (Issue #187) — Grundlage des Unstimmigkeits-Hinweises in der Rundenliste.</summary>
public static class UebergangsAbdeckung
{
    /// <summary>Plätze, die eine Regelzeile weiterreicht: bei einer gruppenübergreifenden Rangfolge-Regel
    /// („2 beste Gruppendritte“) deren Anzahl, sonst der Platzierungsbereich.</summary>
    public static int Plaetze(int platzierungVon, int platzierungBis, int? anzahlBeste) =>
        anzahlBeste ?? (platzierungBis - platzierungVon + 1);

    /// <summary>Hinweistext, wenn die Summe ALLER ausgehenden Regeln (über ggf. mehrere unabhängige Regeln zu
    /// verschiedenen Zielrunden hinweg) nicht die Ausgangsanzahl ergibt; <c>null</c>, wenn sie passt oder die
    /// Runde gar keine ausgehende Regel hat.</summary>
    /// <param name="plaetzeJeRegel">Je ausgehender Regel die Summe ihrer Zeilen (siehe <see cref="Plaetze"/>).</param>
    public static string? Unstimmigkeit(int ausgangsAnzahl, IReadOnlyCollection<int> plaetzeJeRegel)
    {
        if (plaetzeJeRegel.Count == 0) return null;

        var summe = plaetzeJeRegel.Sum();
        if (summe == ausgangsAnzahl) return null;
        return summe < ausgangsAnzahl
            ? $"Übergangsregeln decken nur {summe} von {ausgangsAnzahl} Plätzen ab."
            : $"Übergangsregeln decken {summe} von {ausgangsAnzahl} Plätzen ab — mehr als erwartet.";
    }
}
