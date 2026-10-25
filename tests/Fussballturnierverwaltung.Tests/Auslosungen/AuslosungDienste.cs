using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Application.Zufall;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>Baut den <see cref="AuslosungService"/> mit seinen Bausteinen wie in der App — nicht angegebene
/// Abhängigkeiten sind leere Attrappen.</summary>
internal static class AuslosungDienste
{
    public static AuslosungService Service(
        IAuslosungRepository repository,
        ITurnierRundeRepository? runden = null,
        IPlatzhalterKaskadeService? kaskade = null,
        IRanglisteRepository? ranglisten = null,
        IVerbandsQuoteRepository? verbandsQuoten = null,
        Zufallsquelle? zufallsquelle = null)
    {
        runden ??= new FakeTurnierRundeRepository();
        var vorbereitung = new AuslosungsVorbereitung(
            repository, verbandsQuoten ?? new FakeVerbandsQuoteRepository(), new FakeLandesQuoteRepository(), runden,
            new AuslosungsValidator(), ranglisten ?? new FakeRanglisteRepository());
        return new AuslosungService(
            repository, runden, vorbereitung, kaskade ?? new FakePlatzhalterKaskadeService(),
            zufallsquelle ?? new Zufallsquelle(), NullLogger<AuslosungService>.Instance);
    }
}
