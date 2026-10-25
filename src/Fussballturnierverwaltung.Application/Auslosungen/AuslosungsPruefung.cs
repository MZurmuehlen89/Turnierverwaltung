using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>Prüft eine Auslosung vor dem Losen (<see cref="AuslosungsValidator"/>) — in der ersten Runde der Instanz
/// samt Verbands- und Landesquoten.</summary>
public class AuslosungsPruefung(
    IVerbandsQuoteRepository verbandsQuoteRepository,
    ILandesQuoteRepository landesQuoteRepository,
    AuslosungsValidator validator)
{
    /// <summary>Wirft eine <see cref="ValidationException"/>, wenn die Auslosung so nicht gelost werden kann.</summary>
    public async Task PruefenAsync(Auslosung auslosung, IReadOnlyList<TurnierRunde> alleRunden, CancellationToken ct)
    {
        var alleEintraege = auslosung.Lostoepfe
            .SelectMany(l => l.Eintraege)
            .ToList();

        // Verbandsquoten gelten laut bestätigter Domänen-Annahme nur für die initiale Turnierrunde
        // (Zulassung/erste Auslosung), nicht kumulativ für Folgerunden mit eigenem Losvorgang (z.B. eine
        // separate K.o.-Auslosung nach der Gruppenphase) — siehe [[project_verbandsquote_scope]].
        IReadOnlyList<VerbandsQuote>? verbandsQuoten = null;
        IReadOnlyList<LandesQuote>? landesQuoten = null;
        var ersteRunde = alleRunden.OrderBy(r => r.Reihenfolge).FirstOrDefault();
        if (ersteRunde is not null && ersteRunde.Id == auslosung.TurnierRundeId)
        {
            verbandsQuoten = await verbandsQuoteRepository.AlleNachTurnierInstanzIdAsync(auslosung.TurnierInstanzId, ct);
            landesQuoten = await landesQuoteRepository.AlleNachTurnierInstanzIdAsync(auslosung.TurnierInstanzId, ct);
        }

        ValidationException.WerfenWennUngueltig(
            validator.Validieren(auslosung, alleEintraege, verbandsQuoten, landesQuoten));
    }
}
