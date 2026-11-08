using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>
/// Berger-Tabellen-Algorithmus (Kreismethode, <see cref="BergerTabelle"/>) für die Liga.
/// Mit MitRueckspiel=true wird ein doppelter Durchgang (Hin- und Rückrunde) generiert.
/// Gruppenphasen plant <see cref="GruppenSpielplan"/> je Gruppe — der Plan, gegen den die Kaskade prüft (#620).
/// </summary>
public class RoundRobinGenerator : ISpielplanGenerator
{
    public IReadOnlyList<Rundenart> UnterstützteRundenarten =>
        [Rundenart.Liga];

    public SpielplanErgebnis Generieren(SpielplanKontext kontext)
    {
        int n = kontext.Mannschaften.Count;
        if (n < 2) throw new InvalidOperationException(
            $"Für Round-Robin werden mindestens 2 Mannschaften benötigt (waren: {n}).");

        var spiele = Hinrunde(kontext);
        var paare  = kontext.Runde.MitRueckspiel ? RueckrundeAnhaengen(kontext, spiele) : [];

        return new SpielplanErgebnis { Runde = kontext.Runde, Spiele = spiele, HinRueckspielPaare = paare };
    }

    private static List<Spiel> Hinrunde(SpielplanKontext kontext)
    {
        var teams = kontext.Mannschaften;
        return [.. BergerTabelle.Paarungen(teams.Count).Select((p, i) =>
            Erstellen(kontext, teams[p.Heim].Id, teams[p.Gast].Id, kontext.StartSpielNummer + i, p.Spieltag))];
    }

    // Rückrunde: Heim/Auswärts getauscht, Spieltage um die Hinrunde versetzt, nummeriert im Anschluss an die Hinrunde
    private static List<(int HinspielIndex, int RueckspielIndex)> RueckrundeAnhaengen(
        SpielplanKontext kontext, List<Spiel> spiele)
    {
        int spieltage = BergerTabelle.Spieltage(kontext.Mannschaften.Count);
        int hinspiele = spiele.Count;
        var paare     = new List<(int HinspielIndex, int RueckspielIndex)>();

        for (int hinIdx = 0; hinIdx < hinspiele; hinIdx++)
        {
            var hin = spiele[hinIdx];
            var rueckspiel = Erstellen(kontext, hin.AuswaertsMannschaftId!.Value, hin.HeimMannschaftId!.Value,
                kontext.StartSpielNummer + hinspiele + hinIdx, hin.Spieltag!.Value + spieltage);
            rueckspiel.IstRueckspiel = true;
            spiele.Add(rueckspiel);
            paare.Add((hinIdx, spiele.Count - 1));
        }
        return paare;
    }

    private static Spiel Erstellen(SpielplanKontext k, int heimId, int auswId, int spielNr, int spieltag) =>
        new()
        {
            TurnierInstanzId      = k.Runde.TurnierInstanzId,
            TurnierRundeId        = k.Runde.Id,
            Spielnummer           = spielNr,
            Spieltag              = spieltag,
            HeimMannschaftId      = heimId,
            AuswaertsMannschaftId = auswId,
            Status                = SpielStatus.Geplant,
        };
}
