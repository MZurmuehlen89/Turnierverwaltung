using Fussballturnierverwaltung.Application.Spielplan.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Turniere;

/// <summary>Eine wählbare Möglichkeit, die Lücke einer Zielrunde zu füllen: Regeln (Runden über die 1-basierte
/// Reihenfolge, wie <see cref="UebergangsregelPositionsEingabe"/>) und woher sie stammen (#167).</summary>
public sealed record VorschlagsOption(
    IReadOnlyList<UebergangsregelPositionsEingabe> Regeln,
    IReadOnlyList<string> Herkuenfte);

/// <summary>Vorschlag für die eingehenden Regeln einer Zielrunde. Mehrere <see cref="Optionen"/> = mehrdeutig, keine
/// ist vorgewählt. <see cref="Hinweis"/> nennt den Grund, wenn die Automatik hier nicht reichte (#167).</summary>
public sealed record UebergangsVorschlag(
    int ZielReihenfolge,
    IReadOnlyList<VorschlagsOption> Optionen,
    string? Hinweis);

/// <summary>Schlägt für jede Folgerunde mit Lücke im Eingang Übergangsregeln vor (#167). Rein und zustandslos.
/// Rechenregel (Quelle B): die Plätze der unmittelbar vorangehenden Runde, die noch keine Regel vergeben hat, werden
/// der Reihe nach verteilt, bis der Eingang der Zielrunde gedeckt ist. Der Vorlagenabgleich (Quelle A) kommt hinzu.</summary>
public static class UebergangsVorschlaege
{
    private const string HERKUNFT_RECHENREGEL = "Rechenregel";

    /// <summary>Ein Platz der Quellrunde, den eine Regel weitergeben kann. Bei Rundenart Gruppenphase ist
    /// <see cref="Beste"/> ein Platz der gruppenübergreifenden Rangfolge („die n besten Dritten“, ohne Gruppe).</summary>
    private readonly record struct Platz(int? Gruppe, int Nummer, bool Beste = false);

    /// <param name="runden">Runden in Reihenfolge; Position + 1 ist die Reihenfolge der Regeln.</param>
    /// <param name="vorhandeneRegeln">Bereits angelegte (auch ungespeicherte) Regeln.</param>
    public static IReadOnlyList<UebergangsVorschlag> Berechnen(
        IReadOnlyList<TurnierRunde> runden, IReadOnlyList<UebergangsregelPositionsEingabe> vorhandeneRegeln)
    {
        var ergebnis = new List<UebergangsVorschlag>();
        for (int i = 1; i < runden.Count; i++)
            if (VorschlagFuer(runden, i, vorhandeneRegeln) is { } vorschlag)
                ergebnis.Add(vorschlag);
        return ergebnis;
    }

    private static UebergangsVorschlag? VorschlagFuer(
        IReadOnlyList<TurnierRunde> runden, int zielIndex, IReadOnlyList<UebergangsregelPositionsEingabe> vorhandeneRegeln)
    {
        var ziel = runden[zielIndex];
        int zielReihenfolge = zielIndex + 1;
        int luecke = ziel.EingangsMannschaftsanzahl
                     - vorhandeneRegeln.Where(r => r.ZielReihenfolge == zielReihenfolge).Sum(Mannschaften);
        int quelleIndex = VorgaengerIndex(runden, zielIndex);
        if (luecke <= 0 || quelleIndex < 0) return null;

        var quelle = runden[quelleIndex];
        int quelleReihenfolge = quelleIndex + 1;
        var vergeben = vorhandeneRegeln
            .Where(r => r.QuellReihenfolge == quelleReihenfolge)
            .SelectMany(PlaetzeDerRegel)
            .ToHashSet();
        var frei = Plaetze(quelle, ziel.Rundenart).Where(p => !vergeben.Contains(p)).Take(luecke).ToList();
        if (frei.Count == 0) return null;

        var regeln = RegelnAus(frei, quelleReihenfolge, zielReihenfolge);
        return new UebergangsVorschlag(zielReihenfolge, [new VorschlagsOption(regeln, [HERKUNFT_RECHENREGEL])],
            HinweisFuer(quelle));
    }

    /// <summary>Die unmittelbar vorangehende Runde; Finale und Spiel um Platz 3 sind parallele Geschwister und
    /// speisen keine Folgerunde (wie <c>TeilnehmerPoolHilfsmittel.VorgaengerrundeErmitteln</c>).</summary>
    private static int VorgaengerIndex(IReadOnlyList<TurnierRunde> runden, int zielIndex)
    {
        for (int j = zielIndex - 1; j >= 0; j--)
            if (runden[j].Rundenart is not (Rundenart.Finale or Rundenart.SpielUmPlatzDrei))
                return j;
        return -1;
    }

    private static int Mannschaften(UebergangsregelPositionsEingabe regel) =>
        regel.AnzahlBeste ?? regel.PlatzierungBis - regel.PlatzierungVon + 1;

    // ── Plätze der Quellrunde ───────────────────────────────────────────────

    /// <summary>Die Plätze, die eine Runde der Zielart weitergeben kann, in Vergabereihenfolge.</summary>
    private static IEnumerable<Platz> Plaetze(TurnierRunde quelle, Rundenart zielart)
    {
        switch (quelle.Rundenart)
        {
            case Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei:
                int nummer = zielart == Rundenart.SpielUmPlatzDrei ? 2 : 1; // Sieger bzw. Verlierer
                return Enumerable.Range(1, quelle.EingangsMannschaftsanzahl / 2).Select(spiel => new Platz(spiel, nummer));
            case Rundenart.Gruppenphase when quelle.GruppenAnzahl is > 0 and var gruppen:
                return GruppenPlaetze(quelle.AusgangsMannschaftsanzahl, gruppen);
            case Rundenart.Liga or Rundenart.TopfLiga or Rundenart.SchweizerSystem:
                return Enumerable.Range(1, quelle.AusgangsMannschaftsanzahl).Select(nummer => new Platz(null, nummer));
            default:
                return [];
        }
    }

    private static IEnumerable<Platz> GruppenPlaetze(int ausgang, int gruppen)
    {
        int jeGruppe = ausgang / gruppen;
        for (int nummer = 1; nummer <= jeGruppe; nummer++)
            for (int gruppe = 1; gruppe <= gruppen; gruppe++)
                yield return new Platz(gruppe, nummer);
        for (int rest = 0; rest < ausgang % gruppen; rest++)
            yield return new Platz(null, jeGruppe + 1, Beste: true);
    }

    private static IEnumerable<Platz> PlaetzeDerRegel(UebergangsregelPositionsEingabe regel)
    {
        if (regel.AnzahlBeste is { } anzahl)
            return Enumerable.Range(0, anzahl).Select(_ => new Platz(null, regel.PlatzierungVon, Beste: true));
        return Enumerable.Range(regel.PlatzierungVon, regel.PlatzierungBis - regel.PlatzierungVon + 1)
            .Select(nummer => new Platz(regel.GruppeIndex, nummer));
    }

    // ── Plätze zu Regeln zusammenfassen ─────────────────────────────────────

    private static IReadOnlyList<UebergangsregelPositionsEingabe> RegelnAus(
        IReadOnlyList<Platz> plaetze, int quelle, int ziel)
    {
        var regeln = new List<UebergangsregelPositionsEingabe>();
        foreach (var gruppe in plaetze.Where(p => !p.Beste).GroupBy(p => p.Gruppe).OrderBy(g => g.Key))
            foreach (var (von, bis) in ZusammenhaengendeBereiche(gruppe.Select(p => p.Nummer).Order()))
                regeln.Add(new UebergangsregelPositionsEingabe(quelle, ziel, gruppe.Key, von, bis));
        foreach (var beste in plaetze.Where(p => p.Beste).GroupBy(p => p.Nummer).OrderBy(g => g.Key))
            regeln.Add(new UebergangsregelPositionsEingabe(quelle, ziel, null, beste.Key, beste.Key, beste.Count()));
        return regeln;
    }

    private static IEnumerable<(int Von, int Bis)> ZusammenhaengendeBereiche(IEnumerable<int> sortierteNummern)
    {
        int? von = null, bis = null;
        foreach (int nummer in sortierteNummern)
        {
            if (bis is { } ende && nummer == ende + 1)
            {
                bis = nummer;
                continue;
            }
            if (von is { } start) yield return (start, bis!.Value);
            von = bis = nummer;
        }
        if (von is { } letzter) yield return (letzter, bis!.Value);
    }

    private static string? HinweisFuer(TurnierRunde quelle) =>
        quelle.Rundenart == Rundenart.Gruppenphase && quelle.GruppenAnzahl is > 0 and var gruppen
        && quelle.AusgangsMannschaftsanzahl % gruppen != 0
            ? $"{quelle.AusgangsMannschaftsanzahl} Teams verteilen sich nicht gleichmäßig auf {gruppen} Gruppen: " +
              $"die Gruppenplatzierten und zusätzlich die {quelle.AusgangsMannschaftsanzahl % gruppen} besten " +
              $"{quelle.AusgangsMannschaftsanzahl / gruppen + 1}. Plätze."
            : null;
}
