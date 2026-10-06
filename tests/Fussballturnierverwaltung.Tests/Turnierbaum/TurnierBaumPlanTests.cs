using FluentAssertions;
using Fussballturnierverwaltung.Application.Turnierbaum;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turnierbaum;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Turnierbaum;

public class TurnierBaumPlanTests
{
    // ── Hilfsmethoden ─────────────────────────────────────────────────────

    private static readonly TurnierRunde _halbfinale = new()
        { Id = 1, Name = "Halbfinale", Reihenfolge = 1, Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 4 };
    private static readonly TurnierRunde _finale = new()
        { Id = 2, Name = "Finale", Reihenfolge = 2, Rundenart = Rundenart.Finale, EingangsMannschaftsanzahl = 2 };

    private static Spiel Halbfinalspiel(int id) => new() { Id = id, TurnierRundeId = 1, Spielnummer = id };

    private static Spiel Endspiel(int id, int heimQuelle, int auswaertsQuelle) => new()
    {
        Id = id, TurnierRundeId = 2, Spielnummer = id,
        HeimQuellSpielId = heimQuelle, AuswaertsQuellSpielId = auswaertsQuelle
    };

    private static TurnierBaumPlan HalbfinaleUndFinale(params Spiel[] endspiele) =>
        TurnierBaumPlan.Erstellen([_halbfinale, _finale], [[Halbfinalspiel(1), Halbfinalspiel(2)], endspiele]);

    /// <summary>Der Plan so, wie ihn <see cref="TurnierBaumService"/> speichert: Knoten mit fortlaufenden IDs.</summary>
    private static TurnierBaum AlsBestand(TurnierBaumPlan plan)
    {
        var knotenJePlanKnoten = plan.AlleKnoten
            .Select((p, i) => (Plan: p, Knoten: new TurnierBaumKnoten { Id = i + 1, Runde = p.Runde, SpielId = p.SpielId }))
            .ToDictionary(x => x.Plan, x => x.Knoten);
        foreach (var (planKnoten, knoten) in knotenJePlanKnoten)
        {
            knoten.ElternKnotenId = planKnoten.Eltern is { } eltern ? knotenJePlanKnoten[eltern].Id : null;
            knoten.LinkerKindKnotenId = planKnoten.Links is { } links ? knotenJePlanKnoten[links].Id : null;
            knoten.RechterKindKnotenId = planKnoten.Rechts is { } rechts ? knotenJePlanKnoten[rechts].Id : null;
        }
        return new TurnierBaum
        {
            Knoten = knotenJePlanKnoten.Values.ToList(),
            WurzelKnotenId = plan.Wurzel is { } wurzel ? knotenJePlanKnoten[wurzel].Id : null
        };
    }

    // ── Aufbau ────────────────────────────────────────────────────────────

    [Fact]
    public void Erstellen_VerknuepftDasFinaleMitSeinenQuellspielen()
    {
        var plan = HalbfinaleUndFinale(Endspiel(3, heimQuelle: 2, auswaertsQuelle: 1));

        var finale = plan.Ebenen[1].Single();
        plan.Wurzel.Should().BeSameAs(finale);
        finale.Links!.SpielId.Should().Be(2);
        finale.Rechts!.SpielId.Should().Be(1);
        plan.Ebenen[0].Should().OnlyContain(k => k.Eltern == finale);
    }

    [Fact]
    public void Erstellen_LegtPlatzhalterKnotenAn_FuerEineRundeOhneSpiele()
    {
        var plan = HalbfinaleUndFinale();

        plan.Ebenen[1].Should().ContainSingle().Which.SpielId.Should().BeNull();
        plan.HatSpiele.Should().BeTrue("das Halbfinale hat Spiele");
    }

    // ── Vergleich mit dem gespeicherten Baum (Issue #390) ─────────────────

    [Fact]
    public void Entspricht_IstWahr_FuerDenGespeichertenSelbenAufbau()
    {
        var plan = HalbfinaleUndFinale(Endspiel(3, 1, 2));

        plan.Entspricht(TurnierBaumPlan.AusBestand(AlsBestand(plan))).Should().BeTrue();
    }

    [Fact]
    public void Entspricht_IstWahr_AuchMitPlatzhalterKnoten()
    {
        var plan = HalbfinaleUndFinale();

        plan.Entspricht(TurnierBaumPlan.AusBestand(AlsBestand(plan))).Should().BeTrue();
    }

    [Fact]
    public void Entspricht_IstFalsch_WennEineRundeNeueSpieleBekommenHat()
    {
        var bestand = AlsBestand(HalbfinaleUndFinale(Endspiel(3, 1, 2)));

        HalbfinaleUndFinale(Endspiel(4, 1, 2)).Entspricht(TurnierBaumPlan.AusBestand(bestand)).Should().BeFalse();
    }

    [Fact]
    public void Entspricht_IstFalsch_WennAusEinemPlatzhalterEinSpielWird()
    {
        var bestand = AlsBestand(HalbfinaleUndFinale());

        HalbfinaleUndFinale(Endspiel(3, 1, 2)).Entspricht(TurnierBaumPlan.AusBestand(bestand)).Should().BeFalse();
    }

    [Fact]
    public void Entspricht_IstFalsch_WennDieKinderVertauschtSind()
    {
        var bestand = AlsBestand(HalbfinaleUndFinale(Endspiel(3, 1, 2)));

        HalbfinaleUndFinale(Endspiel(3, 2, 1)).Entspricht(TurnierBaumPlan.AusBestand(bestand)).Should().BeFalse();
    }

    [Fact]
    public void Entspricht_IstFalsch_WennDieWurzelFehlt()
    {
        var plan = HalbfinaleUndFinale(Endspiel(3, 1, 2));
        var bestand = AlsBestand(plan);
        bestand.WurzelKnotenId = null;

        plan.Entspricht(TurnierBaumPlan.AusBestand(bestand)).Should().BeFalse();
    }

    [Fact]
    public void Entspricht_IstFalsch_WennEinKindSeinenElternknotenNichtKennt()
    {
        var plan = HalbfinaleUndFinale(Endspiel(3, 1, 2));
        var bestand = AlsBestand(plan);
        bestand.Knoten.First(k => k.Runde == 1).ElternKnotenId = null;

        plan.Entspricht(TurnierBaumPlan.AusBestand(bestand)).Should().BeFalse();
    }

    [Fact]
    public void Entspricht_IstFalsch_BeiEinemKreisImGespeichertenBaum()
    {
        var plan = HalbfinaleUndFinale(Endspiel(3, 1, 2));
        var bestand = AlsBestand(plan);
        bestand.Knoten.First(k => k.Runde == 1).LinkerKindKnotenId = bestand.WurzelKnotenId;

        plan.Entspricht(TurnierBaumPlan.AusBestand(bestand)).Should().BeFalse();
    }
}
