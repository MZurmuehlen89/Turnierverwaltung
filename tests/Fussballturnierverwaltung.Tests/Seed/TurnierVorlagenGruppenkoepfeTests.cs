using FluentAssertions;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Seed.Daten;

namespace Fussballturnierverwaltung.Tests.Seed;

/// <summary>Feste Gruppenköpfe der Turniervorlagen (Issue #487).</summary>
public class TurnierVorlagenGruppenkoepfeTests
{
    private static readonly List<TurnierInstanz> _vorlagen = TurnierVorlagenSeedDaten.Erstellen();

    private static TurnierRunde ErsteRunde(string name, string variante) =>
        _vorlagen.Single(v => v.Name == name && v.Variante == variante).Runden.Single(r => r.Reihenfolge == 1);

    // Ein Tippfehler in Name oder Variante liefe sonst still ins Leere — ebenso in der Migration Gruppenkoepfe.
    [Fact]
    public void JedeZuordnung_TrifftEineGruppenphase_MitPassenderGruppenanzahl()
    {
        foreach (var ((name, variante), koepfe) in TurnierVorlagenSeedDaten.GruppenkoepfeJeVorlage)
        {
            var runde = ErsteRunde(name, variante);
            runde.Rundenart.Should().Be(Rundenart.Gruppenphase, $"{name} · {variante}");
            koepfe.Gastgeber.Concat(koepfe.Titelverteidiger is { } t ? [t] : Array.Empty<int>())
                .Should().OnlyContain(g => g >= 1 && g <= runde.GruppenAnzahl, $"{name} · {variante}");
        }
    }

    [Theory]
    [InlineData("FIFA WM", "48 Teams · ab 2026", new[] { 1, 2, 4 }, null)]
    [InlineData("UEFA EM", "24 Teams · 2016+", new[] { 1 }, null)]
    [InlineData("Copa América", "16 Teams · 2016/2024", new[] { 3 }, 1)]
    public void Gruppenphase_NachRealemVorbild(string name, string variante, int[] gastgeber, int? titelverteidiger)
    {
        var runde = ErsteRunde(name, variante);
        runde.GastgeberGruppen.Should().Equal(gastgeber);
        runde.TitelverteidigerGruppe.Should().Be(titelverteidiger);
    }

    [Fact]
    public void ChampionsLeague_OhneGruppenkoepfe() =>
        ErsteRunde("UEFA Champions League", "32 Teams · ohne Auswärtstorregel · 2021–2024")
            .GastgeberGruppen.Should().BeEmpty();
}
