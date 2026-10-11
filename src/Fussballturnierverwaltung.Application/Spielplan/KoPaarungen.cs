using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Paarungen einer K.o.-Runde aus ihren Spielen lesen — ohne Datenbank.</summary>
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

    /// <summary>Paarungsnummer (1-basiert) je Spiel-ID: Hinspiele bzw. Einzelspiele in Spielnummer-Reihenfolge,
    /// ein Rückspiel übernimmt die Nummer seines Hinspiels (Issue #346) — unabhängig von der Listenposition,
    /// die bei nach Spieltag sortierter Liste Hin- und Rückspiel nicht nebeneinander führt.</summary>
    public static IReadOnlyDictionary<int, int> PaarungsNummern(IEnumerable<Spiel> spiele)
    {
        var liste = spiele.ToList();
        var nummern = liste.Where(s => !s.IstRueckspiel)
            .OrderBy(s => s.Spielnummer)
            .Select((s, i) => (s.Id, Nr: i + 1))
            .ToDictionary(x => x.Id, x => x.Nr);
        foreach (var rueck in liste.Where(s => s.IstRueckspiel).OrderBy(s => s.Spielnummer))
            nummern[rueck.Id] = rueck.HinspielId is int hinId && nummern.TryGetValue(hinId, out var nr)
                ? nr
                : nummern.Count + 1;
        return nummern;
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
