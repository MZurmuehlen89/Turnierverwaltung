using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turnierbaum;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Turnierbaum;

/// <summary>Ein Knoten des geplanten Turnierbaums: Runde (1 = erste K.-o.-Runde), Spiel (<c>null</c> bei einem
/// Platzhalter für eine Runde ohne Spiele) und die Verweise auf Eltern- und Kindknoten.</summary>
public sealed class PlanKnoten(int runde, int? spielId)
{
    public int Runde { get; } = runde;
    public int? SpielId { get; } = spielId;
    public PlanKnoten? Eltern { get; private set; }
    public PlanKnoten? Links { get; private set; }
    public PlanKnoten? Rechts { get; private set; }

    internal void LinksAnhaengen(PlanKnoten kind)
    {
        Links = kind;
        kind.Eltern = this;
    }

    internal void RechtsAnhaengen(PlanKnoten kind)
    {
        Rechts = kind;
        kind.Eltern = this;
    }

    /// <summary>Übernimmt gespeicherte Verweise unverändert — auch solche, die nicht zueinander passen.</summary>
    internal void Verweisen(PlanKnoten? eltern, PlanKnoten? links, PlanKnoten? rechts)
    {
        Eltern = eltern;
        Links = links;
        Rechts = rechts;
    }
}

/// <summary>Der Turnierbaum, wie er sich aus den K.-o.-Runden eines Turniers und deren Spielen ergibt — reine
/// Berechnung ohne Datenbank. <see cref="TurnierBaumService"/> speichert ihn.</summary>
public sealed class TurnierBaumPlan
{
    private TurnierBaumPlan(IReadOnlyList<IReadOnlyList<PlanKnoten>> ebenen, PlanKnoten? wurzel)
    {
        Ebenen = ebenen;
        Wurzel = wurzel;
    }

    /// <summary>Die Knoten je K.-o.-Runde, in der Reihenfolge der Runden.</summary>
    public IReadOnlyList<IReadOnlyList<PlanKnoten>> Ebenen { get; }

    /// <summary>Der Knoten des Finales; <c>null</c>, solange die letzte Ebene nicht aus genau einem Knoten besteht.</summary>
    public PlanKnoten? Wurzel { get; }

    public IEnumerable<PlanKnoten> AlleKnoten => Ebenen.SelectMany(e => e);

    public bool HatSpiele => AlleKnoten.Any(k => k.SpielId.HasValue);

    /// <summary>Baut den Baum aus den K.-o.-Runden (in Reihenfolge) und allen Spielen je Runde.</summary>
    public static TurnierBaumPlan Erstellen(
        IReadOnlyList<TurnierRunde> koRunden, IReadOnlyList<IReadOnlyList<Spiel>> spieleProRunde)
    {
        var knotenSpieleProRunde = koRunden.Select((runde, i) => KnotenSpiele(runde, spieleProRunde[i])).ToList();
        var ebenen = koRunden.Select((runde, i) => EbeneAnlegen(runde, i + 1, knotenSpieleProRunde[i])).ToList();

        // Issue #385: Das Spiel um Platz 3 liegt meist zwischen Halbfinale und Finale, gehört aber nicht in die
        // Baumkette — es bekommt keine Kinder, und die nächste Ebene (Finale) verknüpft sich mit der letzten
        // Ebene davor, die kein Spiel um Platz 3 ist.
        int? kindIndex = null;
        for (int e = 0; e < ebenen.Count; e++)
        {
            if (koRunden[e].Rundenart == Rundenart.SpielUmPlatzDrei) continue;
            if (kindIndex is int i)
                EbenenVerknuepfen(ebenen[i], KnotenJeSpielId(ebenen[i], spieleProRunde[i]), ebenen[e], knotenSpieleProRunde[e]);
            kindIndex = e;
        }

        // Wurzelknoten = einziger Knoten der letzten Runde (ohne Spiel um Platz 3, Issue #385)
        var letzteEbene = ebenen[kindIndex ?? ebenen.Count - 1];
        return new TurnierBaumPlan(ebenen, letzteEbene.Count == 1 ? letzteEbene[0] : null);
    }

    /// <summary>Liest einen gespeicherten Baum (mit geladenen Knoten) als Plan, um ihn mit einem neu berechneten
    /// zu vergleichen.</summary>
    public static TurnierBaumPlan AusBestand(TurnierBaum baum)
    {
        var planKnotenJeId = baum.Knoten.ToDictionary(k => k.Id, k => new PlanKnoten(k.Runde, k.SpielId));
        PlanKnoten? PlanKnoten(int? id) => id is int i ? planKnotenJeId.GetValueOrDefault(i) : null;

        foreach (var knoten in baum.Knoten)
            planKnotenJeId[knoten.Id].Verweisen(
                PlanKnoten(knoten.ElternKnotenId), PlanKnoten(knoten.LinkerKindKnotenId), PlanKnoten(knoten.RechterKindKnotenId));

        var ebenen = planKnotenJeId.Values.GroupBy(k => k.Runde).OrderBy(g => g.Key).Select(g => g.ToList()).ToList();
        return new TurnierBaumPlan(ebenen, PlanKnoten(baum.WurzelKnotenId));
    }

    /// <summary>Gleicher Aufbau: dieselben Spiele in denselben Runden mit denselben Verknüpfungen und derselben
    /// Wurzel. Verglichen wird ohne Knoten-IDs — Platzhalter-Knoten ohne Spiel zählen über ihre Lage im Baum.</summary>
    public bool Entspricht(TurnierBaumPlan anderer) =>
        Signatur(Wurzel) == Signatur(anderer.Wurzel)
        && KnotenSignaturen().SequenceEqual(anderer.KnotenSignaturen());

    private List<string> KnotenSignaturen() =>
        AlleKnoten.Select(k => $"{Signatur(k)}^{Signatur(k.Eltern)}").Order(StringComparer.Ordinal).ToList();

    /// <summary>Der Knoten samt allem, was unter ihm hängt.</summary>
    private static string Signatur(PlanKnoten? knoten) =>
        knoten is null
            ? "-"
            : $"{knoten.Runde}:{knoten.SpielId}[{KindSignatur(knoten, knoten.Links)}|{KindSignatur(knoten, knoten.Rechts)}]";

    /// <summary>Ein Kind liegt immer in einer früheren Runde. Alles andere stammt aus beschädigten Daten und wird
    /// nicht weiterverfolgt (ein Kreis liefe sonst endlos) — ein berechneter Baum enthält so etwas nie, der
    /// Vergleich fällt also ungleich aus.</summary>
    private static string KindSignatur(PlanKnoten eltern, PlanKnoten? kind) =>
        kind is not null && kind.Runde >= eltern.Runde ? "!" : Signatur(kind);

    /// <summary>Bei Hin-/Rückspiel steht ein Knoten je Paarung — er hängt am Hinspiel.</summary>
    private static List<Spiel> KnotenSpiele(TurnierRunde runde, IReadOnlyList<Spiel> spiele) =>
        spiele.Where(s => !runde.MitRueckspiel || !s.IstRueckspiel).OrderBy(s => s.Spielnummer).ToList();

    private static List<PlanKnoten> EbeneAnlegen(TurnierRunde runde, int rundenNummer, List<Spiel> knotenSpiele) =>
        knotenSpiele.Count > 0
            ? knotenSpiele.Select(s => new PlanKnoten(rundenNummer, s.Id)).ToList()
            // Platzhalter-Knoten für Runden ohne Spiele
            : Enumerable.Range(0, runde.EingangsMannschaftsanzahl / 2).Select(_ => new PlanKnoten(rundenNummer, null)).ToList();

    private static Dictionary<int, PlanKnoten> KnotenJeSpielId(List<PlanKnoten> ebene, IReadOnlyList<Spiel> spiele)
    {
        var knotenJeSpielId = ebene.Where(k => k.SpielId.HasValue).ToDictionary(k => k.SpielId!.Value);

        // Issue #340: seit #346 zeigen Quell-IDs bei Hin-/Rückspiel auf das Rückspiel (trägt den
        // Gesamtsieger), der Knoten hängt aber am Hinspiel — Rückspiel-ID auf denselben Knoten abbilden.
        foreach (var rueckspiel in spiele.Where(s => s.IstRueckspiel && s.HinspielId.HasValue))
            if (knotenJeSpielId.TryGetValue(rueckspiel.HinspielId!.Value, out var hinspielKnoten))
                knotenJeSpielId[rueckspiel.Id] = hinspielKnoten;

        return knotenJeSpielId;
    }

    /// <summary>Issue #241: direkt aus Spiel.HeimQuellSpielId/AuswaertsQuellSpielId statt Text-Platzhalter zu
    /// erraten — pro Slot, nicht pro Übergang, damit gemischte Ebenen aus K.o.-Siegern und Direkteinstiegen nicht
    /// mehr teilweise unverknüpft bleiben.</summary>
    private static void EbenenVerknuepfen(
        List<PlanKnoten> kindEbene, Dictionary<int, PlanKnoten> kindKnotenJeSpielId,
        List<PlanKnoten> elternEbene, List<Spiel> elternSpiele)
    {
        if (!elternSpiele.Any(s => s.HeimQuellSpielId.HasValue || s.AuswaertsQuellSpielId.HasValue))
        {
            SequentiellVerknuepfen(kindEbene, elternEbene);
            return;
        }

        for (int j = 0; j < elternEbene.Count && j < elternSpiele.Count; j++)
        {
            if (elternSpiele[j].HeimQuellSpielId is int heimQuellId &&
                kindKnotenJeSpielId.TryGetValue(heimQuellId, out var links))
                elternEbene[j].LinksAnhaengen(links);
            if (elternSpiele[j].AuswaertsQuellSpielId is int auswaertsQuellId &&
                kindKnotenJeSpielId.TryGetValue(auswaertsQuellId, out var rechts))
                elternEbene[j].RechtsAnhaengen(rechts);
        }
    }

    /// <summary>Fallback für Alt-Turniere ohne jegliche Quell-ID in dieser Übergangs-Ebene: Elternknoten j
    /// bekommt die Kindknoten 2j und 2j+1.</summary>
    private static void SequentiellVerknuepfen(List<PlanKnoten> kindEbene, List<PlanKnoten> elternEbene)
    {
        for (int j = 0; j < elternEbene.Count; j++)
        {
            if (j * 2 < kindEbene.Count) elternEbene[j].LinksAnhaengen(kindEbene[j * 2]);
            if (j * 2 + 1 < kindEbene.Count) elternEbene[j].RechtsAnhaengen(kindEbene[j * 2 + 1]);
        }
    }
}
