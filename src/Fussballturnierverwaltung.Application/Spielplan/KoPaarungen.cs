using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;

namespace Fussballturnierverwaltung.Application.Spielplan;

public static class KoPaarungen
{
    public static Dictionary<int, Spiel> RueckspielJeHinspiel(IEnumerable<Spiel> spiele) =>
        spiele.Where(s => s.IstRueckspiel && s.HinspielId.HasValue)
            .GroupBy(s => s.HinspielId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

    /// <summary>Das entscheidende Spiel jeder Paarung einer K.o.-Runde in Paarungsreihenfolge (Issue #346):
    /// bei Hin-/Rückspiel das Rückspiel (trägt den Gesamtsieger), sonst das Einzelspiel. Ein Hinspiel ohne
    /// zugehöriges Rückspiel (Altbestand vor #346) steht für sich selbst.</summary>
    public static List<Spiel> EntscheidungsspieleJePaarung(IReadOnlyList<Spiel> spiele)
    {
        var rueckspielJeHinspiel = RueckspielJeHinspiel(spiele);
        return spiele
            .Where(s => !s.IstRueckspiel)
            .OrderBy(s => s.Spielnummer)
            .Select(h => rueckspielJeHinspiel.GetValueOrDefault(h.Id) ?? h)
            .ToList();
    }

    /// <summary>Liefert je nach <paramref name="platzierung"/> (1 = Sieger, 2 = Verlierer) die Mannschafts-ID
    /// aus einem entschiedenen K.o.-Spiel, oder <c>null</c> falls noch kein Ergebnis vorliegt.</summary>
    public static int? SiegerOderVerliererId(Spiel spiel, int platzierung)
    {
        var sieger = spiel.Ergebnis?.SiegerMannschaftId;
        if (sieger is null) return null;

        return platzierung switch
        {
            1 => sieger,
            2 => sieger == spiel.HeimMannschaftId ? spiel.AuswaertsMannschaftId : spiel.HeimMannschaftId,
            _ => null
        };
    }
}
