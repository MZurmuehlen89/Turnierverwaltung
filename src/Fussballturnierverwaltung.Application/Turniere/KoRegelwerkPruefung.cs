using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Application.Turniere;

/// <summary>#624: Prüft an der Konfiguration, dass jede K.-o.-Runde ein wirksames Regelwerk mit Entscheidungsverfahren
/// hat (Regel: <see cref="KoRegelwerk"/>). Wirft <see cref="ValidationException"/>; Bestand wird nicht nachgeprüft.</summary>
public class KoRegelwerkPruefung(IRegelwerkRepository regelwerke, ITurnierRundeRepository runden)
{
    /// <summary>Runden aus Rundenformular/-Editor: eigenes Regelwerk je Runde, sonst der Standard des Turniers.</summary>
    public async Task RundenPruefenAsync(
        IReadOnlyList<TurnierRundeEingabe> eingaben, int? standardRegelwerkId, CancellationToken ct = default)
    {
        if (!eingaben.Any(e => KoRegelwerk.IstKoRundenart(e.Rundenart))) return;

        var standard = await LadenAsync(standardRegelwerkId, ct);
        var fehler = new List<string>();
        foreach (var e in eingaben.Where(e => KoRegelwerk.IstKoRundenart(e.Rundenart)))
        {
            var meldung = KoRegelwerk.Fehler(e.Name, e.Rundenart, await LadenAsync(e.RegelwerkId, ct), standard);
            if (meldung is not null) fehler.Add(meldung);
        }
        Werfen(fehler);
    }

    /// <summary>Eine einzelne Runde (Name/Regelwerk ändern): wirksam ist ihr Regelwerk oder der Standard ihres Turniers.</summary>
    public async Task RundePruefenAsync(
        TurnierRunde runde, int? neuesRegelwerkId, CancellationToken ct = default)
    {
        if (!KoRegelwerk.IstKoRundenart(runde.Rundenart)) return;

        var standardId = await runden.StandardRegelwerkIdDerInstanzAsync(runde.TurnierInstanzId, ct);
        var meldung = KoRegelwerk.Fehler(runde.Name, runde.Rundenart,
            await LadenAsync(neuesRegelwerkId, ct), await LadenAsync(standardId, ct));
        Werfen(meldung is null ? [] : [meldung]);
    }

    /// <summary>Neuer Standard eines Turniers: gilt für alle K.-o.-Runden ohne eigenes Regelwerk.</summary>
    public async Task StandardPruefenAsync(int turnierInstanzId, int? standardRegelwerkId, CancellationToken ct = default)
    {
        var koOhneEigenes = (await runden.AlleNachInstanzAsync(turnierInstanzId, ct))
            .Where(r => KoRegelwerk.IstKoRundenart(r.Rundenart) && r.RegelwerkId is null).ToList();
        if (koOhneEigenes.Count == 0) return;

        var standard = await LadenAsync(standardRegelwerkId, ct);
        Werfen([.. koOhneEigenes
            .Select(r => KoRegelwerk.Fehler(r.Name, r.Rundenart, null, standard))
            .OfType<string>()]);
    }

    /// <summary>Regelwerkpflege: verliert ein Regelwerk sein letztes Entscheidungsverfahren, darf keine K.-o.-Runde
    /// es wirksam nutzen — die Meldung nennt Turnier und Runde.</summary>
    public async Task RegelwerkAenderungPruefenAsync(
        int regelwerkId, int verfahrenAnzahl, CancellationToken ct = default)
    {
        if (verfahrenAnzahl > 0) return;

        var nutzer = await runden.KoRundenMitWirksamemRegelwerkAsync(regelwerkId, ct);
        Werfen([.. nutzer.Select(r =>
            $"Das Regelwerk braucht ein Entscheidungsverfahren: K.-o.-Runde '{r.Name}' im Turnier " +
            $"'{r.TurnierInstanz.Name}' nutzt es.")]);
    }

    private async Task<Regelwerk?> LadenAsync(int? id, CancellationToken ct) =>
        id is { } wert ? await regelwerke.FindenNachIdMitAllenRegelnAsync(wert, ct) : null;

    private static void Werfen(IReadOnlyList<string> fehler)
    {
        if (fehler.Count > 0)
            throw new ValidationException(ValidationErgebnis.MitFehlern([.. fehler]));
    }
}
