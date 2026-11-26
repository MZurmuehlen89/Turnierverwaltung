using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan.Eingaben;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Turniere;

public class UebergangsVorschlagTextTests
{
    private static TurnierRunde Runde(string name, Rundenart art, int eingang, int ausgang, int? gruppen = null) =>
        new() { Name = name, Rundenart = art, EingangsMannschaftsanzahl = eingang, AusgangsMannschaftsanzahl = ausgang, GruppenAnzahl = gruppen };

    private static string Text(IReadOnlyList<TurnierRunde> runden, params UebergangsregelPositionsEingabe[] regeln) =>
        UebergangsVorschlagText.Beschreiben(new VorschlagsOption(regeln, ["Rechenregel"]), runden);

    [Fact]
    public void Gruppenphase_AlleGruppenGleich_PlatzJeGruppe_UndBesteDritte()
    {
        var runden = new[] { Runde("Gruppenphase", Rundenart.Gruppenphase, 24, 16, gruppen: 3) };

        Text(runden,
                new(1, 2, 1, 1, 2), new(1, 2, 2, 1, 2), new(1, 2, 3, 1, 2), new(1, 2, null, 3, 3, 4))
            .Should().Be("Gruppenphase: Platz 1–2 je Gruppe, 4 beste 3. Plätze");
    }

    [Fact]
    public void Gruppenphase_NurEinTeilDerGruppen_NenntDieGruppen()
    {
        var runden = new[] { Runde("Gruppenphase", Rundenart.Gruppenphase, 24, 16, gruppen: 3) };

        Text(runden, new(1, 2, 1, 1, 1), new(1, 2, 3, 1, 1)).Should().Be("Gruppenphase: Platz 1 in Gruppe A, C");
    }

    [Fact]
    public void Ko_FasstSpielnummernZusammen_SiegerUndVerlierer()
    {
        var runden = new[] { Runde("Halbfinale", Rundenart.KoRunde, 8, 4) };

        Text(runden, new(1, 2, 1, 1, 1), new(1, 2, 2, 1, 1), new(1, 2, 3, 1, 1), new(1, 2, 1, 2, 2))
            .Should().Be("Halbfinale: Sieger Spiel 1–3, Verlierer Spiel 1");
    }

    [Fact]
    public void Liga_NenntDenPlatzbereich_UndMehrereQuellrundenStehenGetrennt()
    {
        var runden = new[]
        {
            Runde("Ligaphase", Rundenart.Liga, 36, 24),
            Runde("Playoffs", Rundenart.KoRunde, 16, 8),
        };

        Text(runden, new(1, 3, null, 1, 8), new(2, 3, 1, 1, 1), new(2, 3, 2, 1, 1))
            .Should().Be("Ligaphase: Platz 1–8 · Playoffs: Sieger Spiel 1–2");
    }
}
