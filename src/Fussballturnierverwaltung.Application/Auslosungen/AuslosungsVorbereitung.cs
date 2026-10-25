using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Application.Zufall;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>Was vor jedem Losen geschieht (<see cref="AuslosungService"/>): Gruppenköpfe einlegen, Auslosung
/// prüfen, wirksame Regeln ermitteln.</summary>
public class AuslosungsVorbereitung(
    IAuslosungRepository repository,
    IVerbandsQuoteRepository verbandsQuoteRepository,
    ILandesQuoteRepository landesQuoteRepository,
    ITurnierRundeRepository rundeRepository,
    AuslosungsValidator validator,
    IRanglisteRepository ranglisteRepository)
{
    // Was vor jedem Losen geschieht: Gruppenköpfe einlegen, Auslosung prüfen, wirksame Regeln ermitteln.
    public async Task<LosVorbereitung> VorbereitenAsync(Auslosung auslosung, CancellationToken ct)
    {
        if (auslosung.Status == AuslosungsStatus.Abgeschlossen)
            throw new InvalidOperationException("Auslosung wurde bereits durchgeführt. Bitte zuerst zurücksetzen.");

        // Ein Gastgeber, der erst nach dem Anlegen der Töpfe eingetragen wurde, kommt spätestens hier hinein (#487).
        var gruppenkoepfe = await GruppenkoepfeEinlegenAsync(auslosung, ct);
        if (gruppenkoepfe.OhnePlatz.Count > 0)
            throw new InvalidOperationException(OhnePlatzMeldung(gruppenkoepfe.OhnePlatz));
        if (gruppenkoepfe.Geaendert) await repository.AktualisierenAsync(auslosung, ct);

        var alleEintraege = auslosung.Lostoepfe
            .SelectMany(l => l.Eintraege)
            .ToList();

        // Verbandsquoten gelten laut bestätigter Domänen-Annahme nur für die initiale Turnierrunde
        // (Zulassung/erste Auslosung), nicht kumulativ für Folgerunden mit eigenem Losvorgang (z.B. eine
        // separate K.o.-Auslosung nach der Gruppenphase) — siehe [[project_verbandsquote_scope]].
        IReadOnlyList<VerbandsQuote>? verbandsQuoten = null;
        IReadOnlyList<LandesQuote>? landesQuoten = null;
        var alleRunden = await rundeRepository.AlleNachInstanzAsync(auslosung.TurnierInstanzId, ct);
        var ersteRunde = alleRunden.OrderBy(r => r.Reihenfolge).FirstOrDefault();
        if (ersteRunde is not null && ersteRunde.Id == auslosung.TurnierRundeId)
        {
            verbandsQuoten = await verbandsQuoteRepository.AlleNachTurnierInstanzIdAsync(auslosung.TurnierInstanzId, ct);
            landesQuoten = await landesQuoteRepository.AlleNachTurnierInstanzIdAsync(auslosung.TurnierInstanzId, ct);
        }

        ValidationException.WerfenWennUngueltig(
            validator.Validieren(auslosung, alleEintraege, verbandsQuoten, landesQuoten));

        var (regeln, gruppeJeMannschaft) = await RegelnErmittelnAsync(auslosung, alleRunden, ct);
        return new LosVorbereitung(regeln, gruppeJeMannschaft, gruppenkoepfe.Koepfe);
    }

    // Die wirksamen Regeln (#331) und — nur wenn „Keine gleiche Gruppe“ wirkt und echte Mannschaften in den Töpfen
    // liegen — deren Gruppe aus der letzten vorangegangenen Gruppenphase. Platzhalter („1. Gruppe A“) tragen ihre
    // Gruppe selbst im Namen.
    private async Task<(RegelKontext Regeln, IReadOnlyDictionary<int, string> GruppeJeMannschaft)> RegelnErmittelnAsync(
        Auslosung auslosung, IReadOnlyList<TurnierRunde> alleRunden, CancellationToken ct)
    {
        bool Wirkt(AuslosungsRegelArt regel) =>
            AuslosungsRegelKompatibilitaet.Wirkt(regel, auslosung, auslosung.TurnierRunde?.Rundenart);

        var regeln = new RegelKontext(
            Wirkt(AuslosungsRegelArt.KeinGleichesLand),
            Wirkt(AuslosungsRegelArt.KeineGleicheGruppe),
            Wirkt(AuslosungsRegelArt.HeimrechtNachLos),
            Wirkt(AuslosungsRegelArt.HeimrechtNachSetzliste),
            Wirkt(AuslosungsRegelArt.FreilosMoeglich),
            Wirkt(AuslosungsRegelArt.Konfoederationsverteilung),
            auslosung.TurnierRunde?.MitRueckspiel ?? false,
            Wirkt(AuslosungsRegelArt.HeimrechtFuerUnterklassige)
                ? await SpielklassenLadenAsync(auslosung.TurnierRunde?.StandardRanglisteId, ct)
                : null);

        var gruppeJeMannschaft = new Dictionary<int, string>();
        if (!regeln.KeineGleicheGruppe ||
            !auslosung.Lostoepfe.SelectMany(l => l.Eintraege).Any(e => e.MannschaftId.HasValue))
            return (regeln, gruppeJeMannschaft);

        var aktuelleRunde = alleRunden.FirstOrDefault(r => r.Id == auslosung.TurnierRundeId);
        var gruppenphase  = aktuelleRunde is null ? null : alleRunden
            .Where(r => r.Rundenart == Rundenart.Gruppenphase && r.Reihenfolge < aktuelleRunde.Reihenfolge)
            .MaxBy(r => r.Reihenfolge);
        if (gruppenphase is null) return (regeln, gruppeJeMannschaft);

        var gruppenAuslosung = await repository.FindenAbgeschlosseneNachRundeAsync(
            auslosung.TurnierInstanzId, gruppenphase.Id, ct);
        foreach (var e in gruppenAuslosung?.Ergebnisse ?? [])
            if (e.HeimMannschaftId is { } id)
                gruppeJeMannschaft[id] = GruppenHerkunft.Buchstabe(e.PaarungsNummer);

        return (regeln, gruppeJeMannschaft);
    }

    // Spielklassen aus der Standard-Rangliste der Runde (#393) — dieselbe Rangliste wie für die Lostöpfe (#349).
    // Ohne Standard-Rangliste kennt kein Team seine Klasse, dann entscheidet überall das Los.
    private async Task<Spielklassen> SpielklassenLadenAsync(int? ranglisteId, CancellationToken ct) =>
        ranglisteId is int id && await ranglisteRepository.MitAbschnittenAsync(id, ct) is { } rangliste
            ? Spielklassen.Aus(rangliste)
            : Spielklassen.Keine;

    // Gastgeber und Titelverteidiger als feste Gruppenköpfe (#487), nur in der Gruppenphase.
    /// <summary>Gastgeber und Titelverteidiger mit fester Gruppe (#487) gehören in jede Gruppenauslosung: das Programm
    /// legt die fehlenden selbst in Topf 1, statt sich darauf zu verlassen, dass jemand sie von Hand hineinlegt. Sind
    /// die Töpfe schon voll, nennt es die übrigen in <c>OhnePlatz</c>. Speichert nicht — das übernimmt der Aufrufer.</summary>
    public async Task<(IReadOnlyDictionary<int, int> Koepfe, bool Geaendert, IReadOnlyList<string> OhnePlatz)>
        GruppenkoepfeEinlegenAsync(Auslosung auslosung, CancellationToken ct)
    {
        if (auslosung.TurnierRunde is not { Rundenart: Rundenart.Gruppenphase } runde ||
            runde.GastgeberGruppen.Count == 0 && runde.TitelverteidigerGruppe is null)
            return (new Dictionary<int, int>(), false, []);

        var kandidaten = await repository.GruppenkopfKandidatenAsync(auslosung.TurnierInstanzId, ct);
        var koepfe     = Gruppenkoepfe.Bestimmen(runde, kandidaten.GastgeberIds, kandidaten.TitelverteidigerId);
        var (geaendert, ohnePlatz) = Gruppenkoepfe.Einlegen(koepfe, auslosung.Lostoepfe, runde.EingangsMannschaftsanzahl);
        return (koepfe, geaendert, [.. ohnePlatz.Select(id => kandidaten.Namen.GetValueOrDefault(id, $"Mannschaft {id}"))]);
    }

    private static string OhnePlatzMeldung(IReadOnlyList<string> ohnePlatz) =>
        (ohnePlatz.Count == 1
            ? $"{ohnePlatz[0]} führt als Gastgeber bzw. Titelverteidiger eine Gruppe fest an und gehört"
            : $"{string.Join(", ", ohnePlatz)} führen als Gastgeber bzw. Titelverteidiger je eine Gruppe fest an und gehören") +
        " in die Lostöpfe, die sind aber schon voll. Bitte ein Team herausnehmen – der Gruppenkopf kommt dann beim " +
        "Auslosen von selbst in Topf 1.";
}
