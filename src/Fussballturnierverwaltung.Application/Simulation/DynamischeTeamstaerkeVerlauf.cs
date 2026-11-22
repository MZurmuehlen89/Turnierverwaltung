using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>
/// Die Wertungen eines Turniers während eines Simulationsvorgangs (#359): hält je Spiel, was die dynamische Teamstärke aus
/// seinem Ergebnis macht, und liefert für das nächste Spiel die Turnierform mit dem Stand aus allen Spielen davor. Gelesen wird
/// der Bestand einmal beim Anlegen; was der Vorgang simuliert, kommt über <see cref="Setzen"/> dazu (ein erneut simuliertes
/// Spiel überschreibt seinen alten Eintrag). Die Rechnung selbst steckt in <see cref="DynamischeTeamstaerke"/>.
/// </summary>
public sealed class DynamischeTeamstaerkeVerlauf
{
    private readonly double _k;
    private readonly IReadOnlyCollection<int> _gastgeberIds;
    private readonly Dictionary<int, WertungsSpiel[]> _wertungenJeSpiel = [];

    public DynamischeTeamstaerkeVerlauf(TurnierKategorie kategorie, IReadOnlyCollection<int> gastgeberIds, IEnumerable<Spiel> spiele)
    {
        _k = DynamischeTeamstaerke.KFaktor(kategorie);
        _gastgeberIds = gastgeberIds;

        foreach (var spiel in spiele)
        {
            if (spiel.Ergebnis is not { } ergebnis || spiel.HeimMannschaft is null || spiel.AuswaertsMannschaft is null)
                continue;
            _wertungenJeSpiel[spiel.Id] = DynamischeTeamstaerke.Wertungen(
                Termin(spiel), ergebnis.HeimToreGesamt, ergebnis.AuswaertsToreGesamt, Wiederholungsergebnis.Aus(ergebnis)).ToArray();
        }
    }

    /// <summary>Die Turnierform des Spiels, ergänzt um den Stand der dynamischen Teamstärke vor diesem Spiel.</summary>
    public Turnierform Anwenden(Turnierform form, Spiel spiel)
    {
        var termin = Termin(spiel);
        var abweichung = DynamischeTeamstaerke.Abweichungen(_wertungenJeSpiel.Values.SelectMany(w => w), _k, termin.Schluessel);
        return DynamischeTeamstaerke.Anwenden(form,
            abweichung.GetValueOrDefault(termin.HeimId), abweichung.GetValueOrDefault(termin.GastId));
    }

    /// <summary>Merkt das Ergebnis, das der Vorgang für das Spiel gewürfelt hat.</summary>
    public void Setzen(Spiel spiel, SimulationsErgebnis ergebnis) =>
        _wertungenJeSpiel[spiel.Id] = DynamischeTeamstaerke.Wertungen(
            Termin(spiel),
            ergebnis.HeimToreRegulaer + (ergebnis.HeimToreVerlaengerung ?? 0),
            ergebnis.AuswaertsToreRegulaer + (ergebnis.AuswaertsToreVerlaengerung ?? 0),
            ergebnis.Wiederholung).ToArray();

    private Austragung Termin(Spiel spiel)
    {
        var runde = spiel.TurnierRunde;
        var ortsvorteil = SimulationsEngine.SpielortvorteilErmitteln(
            runde.NeutralerSpielort, spiel.HeimMannschaftId, spiel.AuswaertsMannschaftId,
            runde.NeutralerSpielort ? _gastgeberIds : []);
        return new Austragung(
            new Wertungsschluessel(runde.Reihenfolge, spiel.Spieltag ?? 0, spiel.Spielnummer),
            spiel.HeimMannschaft!.Id, spiel.AuswaertsMannschaft!.Id,
            DynamischeTeamstaerke.Startwert(spiel.HeimMannschaft), DynamischeTeamstaerke.Startwert(spiel.AuswaertsMannschaft),
            ortsvorteil, runde.NeutralerSpielort);
    }
}
