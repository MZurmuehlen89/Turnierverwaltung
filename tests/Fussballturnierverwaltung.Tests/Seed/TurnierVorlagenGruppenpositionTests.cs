using FluentAssertions;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Seed.Daten;

namespace Fussballturnierverwaltung.Tests.Seed;

/// <summary>Vorgegebene Position in der Gruppe der Turniervorlagen (Issue #475).</summary>
public class TurnierVorlagenGruppenpositionTests
{
    private static readonly List<TurnierInstanz> _vorlagen = TurnierVorlagenSeedDaten.Erstellen();

    private static Gruppenposition Vorgabe(string name, string variante) =>
        _vorlagen.Single(v => v.Name == name && v.Variante == variante)
            .Runden.Single(r => r.Reihenfolge == 1).VorgegebeneGruppenposition;

    // Ein Tippfehler in Name oder Variante liefe sonst still ins Leere — ebenso in der Migration Gruppenposition.
    [Fact]
    public void JedeZuordnung_TrifftEineVorlageMitGruppenphase()
    {
        foreach (var (name, variante) in TurnierVorlagenSeedDaten.GruppenpositionJeVorlage.Keys)
            _vorlagen.Should().ContainSingle(v => v.Name == name && v.Variante == variante
                && v.Runden.Any(r => r.Reihenfolge == 1 && r.Rundenart == Rundenart.Gruppenphase),
                $"„{name} · {variante}“ braucht eine Gruppenphase als erste Runde");
    }

    [Theory]
    [InlineData("FIFA WM", "48 Teams · ab 2026", Gruppenposition.Topf1Fest)]
    [InlineData("FIFA WM", "32 Teams · 1998–2022", Gruppenposition.Topf1Fest)]
    [InlineData("UEFA EM", "24 Teams · 2016+", Gruppenposition.AlleGelost)]
    [InlineData("UEFA Champions League", "32 Teams · ohne Auswärtstorregel · 2021–2024", Gruppenposition.AlleGelost)]
    [InlineData("Copa América", "16 Teams · 2016/2024", Gruppenposition.NachTopf)]
    [InlineData("Afrika-Cup", "24 Teams · ab 2019", Gruppenposition.NachTopf)]
    public void Gruppenphase_NachRealemVorbild(string name, string variante, Gruppenposition erwartet) =>
        Vorgabe(name, variante).Should().Be(erwartet);

    [Fact]
    public void RundenOhneGruppenphase_BleibenNachTopf() =>
        _vorlagen.SelectMany(v => v.Runden).Where(r => r.Rundenart != Rundenart.Gruppenphase)
            .Should().OnlyContain(r => r.VorgegebeneGruppenposition == Gruppenposition.NachTopf);
}
