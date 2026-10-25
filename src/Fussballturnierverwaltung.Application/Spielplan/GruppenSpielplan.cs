using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Ein Teilnehmerplatz einer Gruppe: feste Mannschaft oder (noch) Platzhalter.</summary>
public readonly record struct GruppenSlot(int? MannschaftId, string? PlatzhalterName);

/// <summary>Ein geplantes Gruppenspiel in Spielnummer-Reihenfolge; <see cref="HinspielIndex"/> zeigt bei
/// Rückspielen auf den Listenindex des zugehörigen Hinspiels.</summary>
public sealed record GeplantesGruppenSpiel(
    GruppenSlot Heim, GruppenSlot Auswaerts, int GruppenNummer, int Spieltag, bool IstRueckspiel, int? HinspielIndex);

/// <summary>Spielplan einer Gruppenphase über Teilnehmerplätze statt Mannschaften, damit auch Gruppen mit noch
/// offenen Platzhaltern einen Spielplan bekommen (Issue #395) — ohne Datenbank.</summary>
public static class GruppenSpielplan
{
    /// <summary>Gruppen-Auslosungen speichern einen Eintrag pro Team (nur Heim-Seite, PaarungsNummer =
    /// Gruppe). Innerhalb der Gruppe nach Id sortiert, damit Generierung und Kaskade unabhängig von der
    /// Ladereihenfolge denselben Plan bauen.</summary>
    public static List<IReadOnlyList<GruppenSlot>> AusAuslosung(IEnumerable<AuslosungsErgebnis> ergebnisse) =>
        [.. ergebnisse
            .GroupBy(e => e.PaarungsNummer)
            .OrderBy(g => g.Key)
            .Select(g => (IReadOnlyList<GruppenSlot>)[.. g
                .OrderBy(e => e.Id)
                .Select(e => new GruppenSlot(e.HeimMannschaftId, e.HeimPlatzhalterName))])];

    /// <summary>Reine Funktion: Berger-Round-Robin je Gruppe, sortiert nach Spieltag und Gruppe — exakt die
    /// Reihenfolge, in der die Generierung die Spielnummern vergibt. Die Kaskade baut damit den Soll-Spielplan
    /// nach und vergleicht Spiel mit Spiel (#395).</summary>
    public static List<GeplantesGruppenSpiel> Planen(
        IReadOnlyList<IReadOnlyList<GruppenSlot>> gruppen, bool mitRueckspiel)
    {
        var paarungen = new List<Paarung>();
        for (int gi = 0; gi < gruppen.Count; gi++)
            paarungen.AddRange(GruppePlanen(gruppen[gi], gi, mitRueckspiel, ersteNr: paarungen.Count));

        return InSpielnummerReihenfolge(paarungen);
    }

    /// <summary>Eine Paarung vor der Sortierung; <see cref="Nr"/> ist die laufende Nummer in Planungsreihenfolge,
    /// über die ein Rückspiel sein Hinspiel benennt.</summary>
    private readonly record struct Paarung(
        int GruppenIndex, int Spieltag, int Nr, GruppenSlot Heim, GruppenSlot Auswaerts, int? HinspielNr);

    private static List<Paarung> GruppePlanen(
        IReadOnlyList<GruppenSlot> gruppe, int gruppenIndex, bool mitRueckspiel, int ersteNr)
    {
        var plaetze = gruppe.Select(s => (GruppenSlot?)s).ToList();
        if (plaetze.Count < 2) return [];
        if (plaetze.Count % 2 == 1) plaetze.Add(null); // Freilos

        int spieltage = plaetze.Count - 1;
        var hinspiele = BergerTabelle.BergerPaarungen(plaetze)
            .Select((p, i) => new Paarung(gruppenIndex, p.Spieltag, ersteNr + i, p.Heim, p.Auswaerts, null))
            .ToList();
        if (!mitRueckspiel) return hinspiele;

#pragma warning disable S2234 // Rückspiel: Heim und Auswärts bewusst getauscht
        var rueckspiele = hinspiele.Select((h, i) => new Paarung(
            gruppenIndex, h.Spieltag + spieltage, ersteNr + hinspiele.Count + i, h.Auswaerts, h.Heim, h.Nr));
#pragma warning restore S2234
        return [.. hinspiele, .. rueckspiele];
    }

    private static List<GeplantesGruppenSpiel> InSpielnummerReihenfolge(IReadOnlyList<Paarung> paarungen)
    {
        var sortiert = paarungen
            .OrderBy(x => x.Spieltag)
            .ThenBy(x => x.GruppenIndex)
            .ThenBy(x => x.Nr)
            .ToList();
        var indexJeNr = sortiert.Select((x, i) => (x.Nr, i)).ToDictionary(x => x.Nr, x => x.i);

        return [.. sortiert.Select(x => new GeplantesGruppenSpiel(
            x.Heim, x.Auswaerts, x.GruppenIndex + 1, x.Spieltag, x.HinspielNr.HasValue,
            x.HinspielNr is int hinNr ? indexJeNr[hinNr] : null))];
    }

    /// <summary>Plausibilitätsprüfung vor dem Spiel-zu-Spiel-Vergleich der Kaskade: gleiche Anzahl, gleiche
    /// Hin-/Rückspiel-Lage, feste Teams an derselben Stelle und noch offene Platzhalter mit demselben Namen.
    /// Bereits aufgelöste Platzhalter-Seiten sind nicht prüfbar (der Name wird beim Auflösen entfernt).</summary>
    public static bool PasstZu(IReadOnlyList<GeplantesGruppenSpiel> geplant, IReadOnlyList<Spiel> spiele)
    {
        if (geplant.Count != spiele.Count) return false;

        static bool SeitePasst(GruppenSlot soll, int? mannschaftId, string? platzhalterName) =>
            soll.MannschaftId is int festId
                ? mannschaftId == festId
                : mannschaftId.HasValue || string.Equals(platzhalterName, soll.PlatzhalterName ?? "?", StringComparison.OrdinalIgnoreCase);

        return geplant.Zip(spiele).All(x =>
            x.First.IstRueckspiel == x.Second.IstRueckspiel &&
            SeitePasst(x.First.Heim, x.Second.HeimMannschaftId, x.Second.HeimPlatzhalterName) &&
            SeitePasst(x.First.Auswaerts, x.Second.AuswaertsMannschaftId, x.Second.AuswaertsPlatzhalterName));
    }
}
