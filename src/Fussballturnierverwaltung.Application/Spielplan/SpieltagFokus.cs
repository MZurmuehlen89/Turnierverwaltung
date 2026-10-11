namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Regeln der Spieltag-Ansicht des Spielplans (Issue #363) — ohne Datenbank.</summary>
public static class SpieltagFokus
{
    /// <summary>Spieltag, mit dem die Ansicht einer Runde oder Gruppe öffnet: der erste mit offenen Spielen,
    /// sonst der letzte; <c>null</c>, wenn kein Spiel einen Spieltag hat.</summary>
    public static int? StartSpieltag(IEnumerable<(int? Spieltag, bool Abgeschlossen)> spiele)
    {
        var mitSpieltag = spiele.Where(s => s.Spieltag.HasValue).ToList();
        if (mitSpieltag.Count == 0) return null;

        return mitSpieltag.Where(s => !s.Abgeschlossen).Min(s => s.Spieltag)
            ?? mitSpieltag.Max(s => s.Spieltag);
    }
}
