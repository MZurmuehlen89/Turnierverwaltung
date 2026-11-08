using FluentAssertions;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Turniere;

public class TurnierausgangTextTests
{
    private static readonly Mannschaft _esp = new() { Id = 1, Name = "Spanien" };
    private static readonly Mannschaft _eng = new() { Id = 2, Name = "England" };

    private static Turnierausgang Finale(params Platzierung[] plaetze) => new(TurnierausgangArt.Finale, plaetze, []);

    [Theory]
    [InlineData(PlatzierungsGrund.FinaleGewonnen, EntscheidungArt.Elfmeterschiessen, "Finale gewonnen (i.E.)")]
    [InlineData(PlatzierungsGrund.FinaleVerloren, EntscheidungArt.Verlaengerung, "Finale verloren (n.V.)")]
    [InlineData(PlatzierungsGrund.SpielUmPlatzDreiGewonnen, EntscheidungArt.RegulaereSpielzeit, "Spiel um Platz 3 gewonnen")]
    [InlineData(PlatzierungsGrund.SpielUmPlatzDreiVerloren, EntscheidungArt.GoldenGoal, "Spiel um Platz 3 verloren (G.G.)")]
    [InlineData(PlatzierungsGrund.HalbfinaleVerloren, EntscheidungArt.Elfmeterschiessen, "Halbfinale")]
    public void Erlaeuterung_KoPlatz_NenntSpielUndEntscheidung(PlatzierungsGrund grund, EntscheidungArt art, string erwartet)
    {
        var platz = new Platzierung(1, _esp, grund, art);

        TurnierausgangText.Erlaeuterung(platz, Finale(platz)).Should().Be(erwartet);
    }

    [Theory]
    [InlineData(5, 34, "Steht vorzeitig fest, noch 5 von 34 Spieltagen offen")]
    [InlineData(0, 34, "Nach 34 Spieltagen")]
    [InlineData(0, 0, "Meister")]
    public void Erlaeuterung_Meister_NenntDieSpieltage(int offen, int gesamt, string erwartet)
    {
        var platz = new Platzierung(1, _esp, PlatzierungsGrund.Meister);
        var ausgang = new Turnierausgang(TurnierausgangArt.Tabelle, [platz], [], _esp, offen, gesamt);

        TurnierausgangText.Erlaeuterung(platz, ausgang).Should().Be(erwartet);
    }

    [Fact]
    public void Hinweis_SiegerSteht_KeinHinweis()
        => TurnierausgangText.Hinweis(Finale(new Platzierung(1, _esp, PlatzierungsGrund.FinaleGewonnen))).Should().BeNull();

    [Fact]
    public void Hinweis_FinaleAngesetzt_NenntBeideFinalisten()
        => TurnierausgangText.Hinweis(new Turnierausgang(TurnierausgangArt.Finale, [], [_esp, _eng]))
            .Should().Be("Spanien oder England – das Finale ist noch nicht gespielt.");

    [Fact]
    public void Hinweis_FinalistenNochOffen_TurnierLaeuft()
        => TurnierausgangText.Hinweis(Finale()).Should().Be(TurnierausgangText.TURNIER_LAEUFT);

    [Theory]
    [InlineData(34, "Noch offen – 12 von 34 Spieltagen stehen aus. Tabellenführer: Spanien.")]
    [InlineData(0, "Noch offen. Tabellenführer: Spanien.")]
    public void Hinweis_LigaOffen_NenntTabellenfuehrer(int gesamt, string erwartet)
        => TurnierausgangText.Hinweis(new Turnierausgang(TurnierausgangArt.Tabelle, [], [], _esp, gesamt > 0 ? 12 : 0, gesamt))
            .Should().Be(erwartet);

    [Fact]
    public void Hinweis_LigaOhneErgebnis_TurnierLaeuft()
        => TurnierausgangText.Hinweis(new Turnierausgang(TurnierausgangArt.Tabelle, [], []))
            .Should().Be(TurnierausgangText.TURNIER_LAEUFT);
}
