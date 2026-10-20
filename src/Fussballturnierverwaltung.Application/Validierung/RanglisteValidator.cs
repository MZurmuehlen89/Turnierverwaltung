using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Ranglisten.Eingaben;

namespace Fussballturnierverwaltung.Application.Validierung;

public class RanglisteValidator
{
    public ValidationErgebnis Validieren(RanglisteErstellenEingabe eingabe)
    {
        var fehler = new List<string>();

        if (string.IsNullOrWhiteSpace(eingabe.Name))
            fehler.Add("Ranglisten-Name darf nicht leer sein.");

        var doppelteTeams = eingabe.Eintraege
            .GroupBy(e => e.MannschaftId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (doppelteTeams.Any())
            fehler.Add($"Eine oder mehrere Mannschaften sind mehrfach in der Rangliste enthalten (MannschaftId: {string.Join(", ", doppelteTeams)}).");

        var doppelteRaenge = eingabe.Eintraege
            .GroupBy(e => e.Rang).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (doppelteRaenge.Any())
            fehler.Add($"Rang(e) mehrfach vergeben: {string.Join(", ", doppelteRaenge)}.");

        if (eingabe.Eintraege.Any(e => e.Rang < 1))
            fehler.Add("Rang muss mindestens 1 sein.");

        if (eingabe.Eintraege.Any(e => e.Punkte is < 0))
            fehler.Add("Punkte dürfen nicht negativ sein.");

        if (eingabe.ProfigrenzeSpielklasse is < 1)
            fehler.Add("Die Profigrenze muss mindestens Spielklasse 1 sein.");

        fehler.AddRange(AbschnittsRegeln.Fehler(eingabe.Abschnitte ?? []));

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }

    public ValidationErgebnis Validieren(RanglisteAktualisierenEingabe eingabe)
    {
        var erstellen = new RanglisteErstellenEingabe(
            eingabe.Name, eingabe.Datum, eingabe.Quelle, eingabe.Eintraege, ProfigrenzeSpielklasse: eingabe.ProfigrenzeSpielklasse,
            Abschnitte: eingabe.Abschnitte);
        return Validieren(erstellen);
    }
}
