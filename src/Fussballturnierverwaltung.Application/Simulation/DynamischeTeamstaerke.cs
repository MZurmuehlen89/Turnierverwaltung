using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>
/// Dynamische Teamstärke (#359): Ein Elo-Modell nach dem Vorbild der World Football Elo Ratings verschiebt die Stärke der Teams
/// im Turnierverlauf nach Erfolgen und Überraschungen. Die Elo-Zahl bleibt intern (nirgends angezeigt oder gespeichert); in die
/// Simulation geht nur die <b>Abweichung</b> vom Startwert, als Zuschlag auf die <see cref="Turnierform"/> – sonst zählte die
/// Stärke aus den Ratings doppelt. Rein und zustandslos: der Stand folgt allein aus den Ergebnissen davor, ein gelöschtes oder
/// überschriebenes Ergebnis wirkt damit von selbst zurück.
/// <para>Eichung (Messung im Issue): <c>Elo = 1500 + 18 · (Gesamtrating − 75)</c>, Heimbonus 60, geeicht auf das tatsächliche Update
/// <c>G · (W − W_e)</c> – eine Eichung nur auf <c>W − W_e</c> ließ starke Teams durch den Tordifferenz-Faktor systematisch
/// aufsteigen. Wirkung fest 0,25; in Ligen gilt K 20, die Realismus-Bänder sind nur dafür gemessen.</para>
/// </summary>
public static class DynamischeTeamstaerke
{
    public const double STEIGUNG = 18;
    public const double HEIMBONUS = 60;
    public const double WIRKUNG = 0.25;

    /// <summary>Ein Elo-Punkt als Exponent auf die Torerwartung: ein Gesamtrating-Punkt (= <see cref="STEIGUNG"/> Elo-Punkte) wirkt
    /// in der Engine mit <c>STAERKE_SPREIZUNG / 75</c> (Rating-Verhältnis ≈ 1/75 je Punkt, hoch Spreizung) auf die eigene und
    /// gegenläufig auf die gegnerische Torerwartung – genau das leistet eine <see cref="Turnierform"/>.</summary>
    public const double VOLLE_WIRKUNG = SimulationsEngine.STAERKE_SPREIZUNG / 75.0 / STEIGUNG;

    /// <summary>Startwert eines Teams: Mittelpunkt 1500 bei Gesamtrating 75.</summary>
    public static double Startwert(Mannschaft mannschaft) => 1500 + STEIGUNG * (double)(mannschaft.GesamtRating - 75m);

    /// <summary>K nach Gewicht des Wettbewerbs (eloratings.net).</summary>
    public static double KFaktor(TurnierKategorie kategorie) => kategorie switch
    {
        TurnierKategorie.Weltmeisterschaft => 60,
        TurnierKategorie.Kontinentalmeisterschaft => 50,
        TurnierKategorie.Vereinspokal => 30,
        _ => 20
    };

    /// <summary>Tordifferenz-Faktor G: 1 bei Remis und einem Tor, 1,5 bei zwei, ab drei (11 + N) / 8.</summary>
    public static double Tordifferenzfaktor(int differenz) => Math.Abs(differenz) switch
    {
        <= 1 => 1.0,
        2 => 1.5,
        int n => (11 + n) / 8.0
    };

    /// <summary>Gewinnerwartung des Heimteams bei der Elo-Differenz <paramref name="differenz"/> (Heim minus Gast, inkl. Heimbonus).</summary>
    public static double Erwartung(double differenz) => 1.0 / (1.0 + Math.Pow(10, -differenz / 400.0));

    /// <summary>Die Turnierform um den Zuschlag aus der Abweichung beider Teams (Elo-Punkte) ergänzt.</summary>
    public static Turnierform Anwenden(Turnierform form, double abweichungHeim, double abweichungGast) =>
        new(form.Heim + WIRKUNG * VOLLE_WIRKUNG * abweichungHeim, form.Gast + WIRKUNG * VOLLE_WIRKUNG * abweichungGast);

    /// <summary>Aktueller Wert minus Startwert je Mannschaft nach allen Spielen, die in der Turnierreihenfolge vor
    /// <paramref name="vor"/> liegen. Mannschaften ohne Spiel davor fehlen (Abweichung 0). Die Reihenfolge der Eingabe ist
    /// gleichgültig – gewertet wird nach <see cref="Wertungsschluessel"/>, nicht nach Erfassungszeit oder Listenposition.</summary>
    public static Dictionary<int, double> Abweichungen(IEnumerable<WertungsSpiel> spiele, double k, Wertungsschluessel vor)
    {
        var abweichung = new Dictionary<int, double>();
        foreach (var spiel in spiele.Where(s => s.Schluessel.CompareTo(vor) < 0).OrderBy(s => s.Schluessel))
        {
            abweichung.TryGetValue(spiel.HeimId, out double heim);
            abweichung.TryGetValue(spiel.GastId, out double gast);

            double bonus = spiel.Ortsvorteil switch
            {
                Spielortvorteil.Heim => HEIMBONUS,
                Spielortvorteil.Auswaerts => -HEIMBONUS,
                _ => 0
            };
            double differenz = spiel.HeimStart + heim - (spiel.GastStart + gast) + bonus;
            int tore = spiel.HeimTore - spiel.GastTore;
            double ergebnis = tore > 0 ? 1.0 : tore == 0 ? 0.5 : 0.0;

            double aenderung = k * Tordifferenzfaktor(tore) * (ergebnis - Erwartung(differenz));
            abweichung[spiel.HeimId] = heim + aenderung;
            abweichung[spiel.GastId] = gast - aenderung;
        }
        return abweichung;
    }

    /// <summary>Die Wertungen eines Spiels: das Spiel selbst und, falls es eines gab, sein Wiederholungsspiel als eigenes Spiel
    /// mit seinem tatsächlichen Heimrecht. Tore inkl. Verlängerung; ein Elfmeterschießen oder Los zählt nicht als Tor, das
    /// Spiel bleibt ein Remis.</summary>
    public static IEnumerable<WertungsSpiel> Wertungen(Austragung austragung, int heimTore, int gastTore, Wiederholungsergebnis? wiederholung)
    {
        yield return new WertungsSpiel(austragung.Schluessel, austragung.HeimId, austragung.GastId,
            austragung.HeimStart, austragung.GastStart, heimTore, gastTore, austragung.Ortsvorteil);

        if (wiederholung is null) yield break;

        var spaeter = austragung.Schluessel with { Teil = 1 };
        if (SimulationsEngine.HeimrechtTauscht(austragung.Ortsvorteil, austragung.NeutralerSpielort))
#pragma warning disable S2234 // Heim und Gast sind im Wiederholungsspiel mit Absicht vertauscht (Heimrecht-Tausch, #547)
            yield return new WertungsSpiel(spaeter, austragung.GastId, austragung.HeimId, austragung.GastStart, austragung.HeimStart,
                wiederholung.AuswaertsGesamt, wiederholung.HeimGesamt, Spielortvorteil.Heim);
#pragma warning restore S2234
        else
            yield return new WertungsSpiel(spaeter, austragung.HeimId, austragung.GastId, austragung.HeimStart, austragung.GastStart,
                wiederholung.HeimGesamt, wiederholung.AuswaertsGesamt, austragung.Ortsvorteil);
    }
}

/// <summary>Stelle eines Spiels in der Turnierreihenfolge: Runde, Spieltag, Spielnummer; <see cref="Teil"/> 1 ist das
/// Wiederholungsspiel nach dem ersten Spiel.</summary>
public readonly record struct Wertungsschluessel(int Runde, int Spieltag, int Spielnummer, int Teil = 0) : IComparable<Wertungsschluessel>
{
    public int CompareTo(Wertungsschluessel other) =>
        (Runde, Spieltag, Spielnummer, Teil).CompareTo((other.Runde, other.Spieltag, other.Spielnummer, other.Teil));
}

/// <summary>Ein gewertetes Spiel: Teams mit ihren Startwerten, Tore und wer den Ortsvorteil hatte.</summary>
public readonly record struct WertungsSpiel(
    Wertungsschluessel Schluessel, int HeimId, int GastId, double HeimStart, double GastStart,
    int HeimTore, int GastTore, Spielortvorteil Ortsvorteil);

/// <summary>Teams, Startwerte und Ort eines Spiels – alles außer dem Ergebnis.</summary>
public readonly record struct Austragung(
    Wertungsschluessel Schluessel, int HeimId, int GastId, double HeimStart, double GastStart,
    Spielortvorteil Ortsvorteil, bool NeutralerSpielort);
