using FluentAssertions;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Tests.Spielplan;
using static Fussballturnierverwaltung.Tests.Spielplan.SpielplanTestdaten;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>Die Mannschaften hinter den Platzhaltern einer Folgerunde (#483). Grundlage ist die Gruppenphase aus
/// <see cref="SpielplanTestdaten.GruppenphaseUndKoSetupAnlegen"/>: Gruppe A (Team A/B) gewinnt Team A, Gruppe B
/// (Team C/D) gewinnt Team C.</summary>
public class QualifikantenAufloesungTests
{
    [Fact]
    public async Task Aufloesen_GespielteGruppen_LiefertDieMannschaftenSamtLand()
    {
        await using var ctx = NeuerContext();
        GruppenphaseUndKoSetupAnlegen(ctx, out _, out int koRundeId);
        var dienste = new SpielplanDienste(ctx);

        var qualifikanten = await dienste.Qualifikanten.AufloesenAsync(koRundeId, ["Sieger Gruppe A", "2. Gruppe B"]);

        qualifikanten.AlleFest.Should().BeTrue();
        qualifikanten.Fuer("Sieger Gruppe A")!.Name.Should().Be("Team A");
        qualifikanten.Fuer("2. Gruppe B")!.Name.Should().Be("Team D");
        qualifikanten.Fuer("2. Gruppe B")!.Land.Should().NotBeNull("Land und Verband speisen die Auslosungsregeln");
    }

    [Fact]
    public async Task Aufloesen_GruppeNochOffen_ZaehltIhrePlatzhalterAlsOffen()
    {
        await using var ctx = NeuerContext();
        GruppenphaseUndKoSetupAnlegen(ctx, out _, out int koRundeId);
        // Das einzige Spiel der Gruppe B ist noch nicht gespielt — ihr Sieger steht nicht fest.
        ctx.SpielErgebnisse.Remove(ctx.SpielErgebnisse.Single(e => e.SpielId == 2));
        ctx.Spiele.Single(s => s.Id == 2).Status = SpielStatus.Geplant;
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        var qualifikanten = await dienste.Qualifikanten.AufloesenAsync(
            koRundeId, ["1. Gruppe A", "2. Gruppe A", "1. Gruppe B", "2. Gruppe B"]);

        qualifikanten.AlleFest.Should().BeFalse();
        qualifikanten.AnzahlOffen.Should().Be(2);
        qualifikanten.Fuer("1. Gruppe A")!.Name.Should().Be("Team A");
        qualifikanten.Fuer("1. Gruppe B").Should().BeNull();
    }

    [Fact]
    public async Task Aufloesen_UnbekannterPlatzhalter_BleibtOffen()
    {
        await using var ctx = NeuerContext();
        GruppenphaseUndKoSetupAnlegen(ctx, out _, out int koRundeId);
        var dienste = new SpielplanDienste(ctx);

        var qualifikanten = await dienste.Qualifikanten.AufloesenAsync(koRundeId, ["Sieger Gruppe A", "Wildcard"]);

        qualifikanten.AnzahlOffen.Should().Be(1);
        qualifikanten.Fuer("Wildcard").Should().BeNull();
    }

    [Fact]
    public async Task Aufloesen_DoppelteNamen_ZaehlenEinmal()
    {
        await using var ctx = NeuerContext();
        GruppenphaseUndKoSetupAnlegen(ctx, out _, out int koRundeId);
        var dienste = new SpielplanDienste(ctx);

        var qualifikanten = await dienste.Qualifikanten.AufloesenAsync(
            koRundeId, ["Sieger Gruppe A", "Sieger Gruppe A", "2. Gruppe C"]);

        qualifikanten.JePlatzhalter.Should().HaveCount(1);
        qualifikanten.AnzahlOffen.Should().Be(1, "eine Gruppe C gibt es nicht");
    }

    [Fact]
    public async Task Aufloesen_OhneNamen_LiefertKeineQualifikanten()
    {
        await using var ctx = NeuerContext();
        var dienste = new SpielplanDienste(ctx);

        var qualifikanten = await dienste.Qualifikanten.AufloesenAsync(turnierRundeId: 99, []);

        qualifikanten.AlleFest.Should().BeTrue();
        qualifikanten.JePlatzhalter.Should().BeEmpty();
    }
}
