using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>Abbildung gespeicherter <see cref="SpielEreignis"/>se auf <see cref="ZeitleistenEreignis"/> (#688, K1):
/// Reihenfolge nach Id, Heim-/Auswärtsseite über die Mannschafts-Id, Wiederholungsmarke.</summary>
public class ZeitleistenEreignisTests
{
    private const int HEIM_ID = 7;
    private const int GAST_ID = 9;

    private static SpielEreignis Ereignis(
        int id, int minute, int? mannschaftId, bool wiederholung = false, int? nachspiel = null,
        EreignisArt art = EreignisArt.Tor, string beschreibung = "") =>
        new()
        {
            Id = id, Minute = minute, Nachspielminute = nachspiel, EreignisArt = art,
            MannschaftId = mannschaftId, ImWiederholungsspiel = wiederholung, Beschreibung = beschreibung,
        };

    [Fact]
    public void Aus_SortiertNachId_NichtNachMinuteOderEingabereihenfolge()
    {
        var gespeichert = new[]
        {
            Ereignis(30, 45, HEIM_ID),
            Ereignis(10, 45, GAST_ID),
            Ereignis(20, 45, HEIM_ID),
        };

        var ergebnis = ZeitleistenEreignis.Aus(gespeichert, HEIM_ID);

        ergebnis.Select(e => e.IstHeimSeite).Should().Equal(false, true, true);
    }

    [Fact]
    public void Aus_HeimSeiteNurBeiPassenderMannschaftsId()
    {
        var gespeichert = new[]
        {
            Ereignis(1, 10, HEIM_ID),
            Ereignis(2, 20, GAST_ID),
            Ereignis(3, 30, null),
        };

        var ergebnis = ZeitleistenEreignis.Aus(gespeichert, HEIM_ID);

        ergebnis.Select(e => e.IstHeimSeite).Should().Equal(true, false, false);
    }

    [Fact]
    public void Aus_UebernimmtWiederholungsmarkeUndFelder()
    {
        var gespeichert = new[]
        {
            Ereignis(1, 90, HEIM_ID, wiederholung: false, nachspiel: 3, art: EreignisArt.GelbeKarte, beschreibung: "Foul"),
            Ereignis(2, 12, GAST_ID, wiederholung: true),
        };

        var ergebnis = ZeitleistenEreignis.Aus(gespeichert, HEIM_ID);

        ergebnis.Should().Equal(
            new ZeitleistenEreignis(90, 3, EreignisArt.GelbeKarte, true, "Foul", false),
            new ZeitleistenEreignis(12, null, EreignisArt.Tor, false, "", true));
    }
}
