using FluentAssertions;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Seed;
using Fussballturnierverwaltung.Infrastructure.Seed.Daten;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Seed;

/// <summary>Issue #372: reale Kombinationstabellen und feste K.-o.-Bäume der Seed-Vorlagen.</summary>
public class FesteKoBaeumeSeedTests
{
    private const string EM24 = "UEFA EM";
    private const string EM24_VARIANTE = "24 Teams · 2016+";

    public static TheoryData<string, int, int> Tabellen => new()
    {
        { nameof(KombinationstabellenSeedDaten.SechsGruppenSchema2016), 6, 4 },
        { nameof(KombinationstabellenSeedDaten.SechsGruppenSchema2020), 6, 4 },
        { nameof(KombinationstabellenSeedDaten.CopaAmerica12), 3, 2 },
        { nameof(KombinationstabellenSeedDaten.FifaWm2026), 12, 8 },
    };

    private static Kombinationstabelle Tabelle(string name) =>
        (Kombinationstabelle)typeof(KombinationstabellenSeedDaten).GetProperty(name)!.GetValue(null)!;

    private static long Binomial(int n, int k) =>
        Enumerable.Range(1, k).Aggregate(1L, (acc, i) => acc * (n - k + i) / i);

    [Theory]
    [MemberData(nameof(Tabellen))]
    public void Kombinationstabelle_EnthaeltJedeKombinationGenauEinmalMitGueltigerZuordnung(string name, int gruppen, int beste)
    {
        var tabelle = Tabelle(name);

        tabelle.Kombinationen.Should().HaveCount((int)Binomial(gruppen, beste));
        tabelle.AnzahlBeste.Should().Be(beste);
        var kandidaten = tabelle.Plaetze.Select(p => p.Split(' ')[^1].Split('/')).ToList();
        foreach (var (schluessel, zuordnung) in tabelle.Kombinationen)
        {
            zuordnung.Order().Should().Equal(schluessel.Select(c => c.ToString()), schluessel);
            for (int i = 0; i < zuordnung.Count; i++)
                kandidaten[i].Should().Contain(zuordnung[i], $"{name} {schluessel}: Platz '{tabelle.Plaetze[i]}'");
        }
    }

    // Stichproben gegen tatsächlich gespielte Turniere.
    [Theory]
    [InlineData(nameof(KombinationstabellenSeedDaten.SechsGruppenSchema2020), "CDEF", "3. Gruppe A/D/E/F", "F")] // EURO 2024: Spanien – Georgien
    [InlineData(nameof(KombinationstabellenSeedDaten.SechsGruppenSchema2020), "ACDF", "3. Gruppe A/B/C", "A")]   // EURO 2020: Frankreich – Schweiz
    [InlineData(nameof(KombinationstabellenSeedDaten.SechsGruppenSchema2016), "ACDE", "3. Gruppe C/D/E", "C")]   // AFCON 2023: Äquatorialguinea – Guinea
    [InlineData(nameof(KombinationstabellenSeedDaten.SechsGruppenSchema2016), "ABCF", "3. Gruppe B/E/F", "F")]   // AFCON 2021: Nigeria – Tunesien
    [InlineData(nameof(KombinationstabellenSeedDaten.SechsGruppenSchema2016), "ADEF", "3. Gruppe A/C/D", "A")]   // WM 1994: Brasilien – USA
    [InlineData(nameof(KombinationstabellenSeedDaten.CopaAmerica12), "AB", "3. Gruppe B/C", "B")]                // Copa 2019: Brasilien – Paraguay
    [InlineData(nameof(KombinationstabellenSeedDaten.FifaWm2026), "BDEFIJKL", "3. Gruppe C/E/F/H/I", "E")]      // WM 2026: Mexiko – Ecuador
    [InlineData(nameof(KombinationstabellenSeedDaten.FifaWm2026), "BDEFIJKL", "3. Gruppe E/H/I/J/K", "K")]      // WM 2026: England – DR Kongo
    public void Kombinationstabelle_ReproduziertRealeAnsetzung(string name, string qualifiziert, string platz, string gruppe)
    {
        Tabelle(name).GruppeFuer(platz, qualifiziert.Select(c => c.ToString())).Should().Be(gruppe);
    }

    // Jede fest vorbelegte Runde: richtige Paarungsanzahl, jeder Platzhalter genau einmal; die aus der Gruppenphase
    // gespeisten Runden decken genau die über Gruppenrang bzw. Kombinationstabelle weitergeleiteten Plätze ab — in
    // der Regel die erste K.-o.-Runde, bei der EM 1980 Finale und Spiel um Platz 3 (#375).
    [Fact]
    public void Erstellen_FesteKoBaeumeSindVollstaendigUndKonsistent()
    {
        var vorlagen = TurnierVorlagenSeedDaten.Erstellen().Where(v => v.Auslosungen.Count > 0).ToList();
        vorlagen.Select(v => (v.Name, v.Variante)).Should().Contain(
            [(EM24, EM24_VARIANTE), ("FIFA WM", "48 Teams · ab 2026"), ("FIFA WM", "32 Teams · 1998–2022")]);

        foreach (var vorlage in vorlagen)
        {
            var runden = vorlage.Runden.OrderBy(r => r.Reihenfolge).ToList();
            var gruppenphase = runden.Single(r => r.Rundenart == Rundenart.Gruppenphase);
            int gruppen = gruppenphase.GruppenAnzahl!.Value;
            var koRunden = runden.Where(r => r.Rundenart != Rundenart.Gruppenphase).ToList();
            koRunden.Should().OnlyContain(r => r.PaarungFestVorgegeben, vorlage.Name);

            foreach (var runde in koRunden)
            {
                var auslosung = vorlage.Auslosungen.Single(a => a.TurnierRunde == runde);
                auslosung.Auslosungsart.Should().Be(Auslosungsart.Manuell);
                auslosung.Status.Should().Be(AuslosungsStatus.Abgeschlossen);
                auslosung.Ergebnisse.Should().HaveCount(runde.EingangsMannschaftsanzahl / 2, $"{vorlage.Name} {runde.Name}");
                auslosung.Ergebnisse.SelectMany(e => new[] { e.HeimPlatzhalterName, e.AuswaertsPlatzhalterName })
                    .Should().OnlyHaveUniqueItems($"{vorlage.Name} {runde.Name}");
            }

            var erste = koRunden.First();
            var plaetze = vorlage.Auslosungen.SelectMany(a => a.Ergebnisse)
                .SelectMany(e => new[] { e.HeimPlatzhalterName!, e.AuswaertsPlatzhalterName! })
                .Where(p => p.Contains(". Gruppe ")).ToList();
            int direkt = (gruppenphase.AusgangsMannschaftsanzahl - (Kombinationstabelle.AusJson(erste.KombinationstabelleJson)?.AnzahlBeste ?? 0)) / gruppen;
            var erwartetDirekt = Enumerable.Range(1, direkt)
                .SelectMany(platz => Enumerable.Range(0, gruppen).Select(g => $"{platz}. Gruppe {(char)('A' + g)}"));
            var kombinationsPlaetze = plaetze.Where(p => Kombinationstabelle.IstPlatz(p, out _)).ToList();

            plaetze.Except(kombinationsPlaetze).Should().BeEquivalentTo(erwartetDirekt, vorlage.Name);
            if (kombinationsPlaetze.Count > 0)
                kombinationsPlaetze.Should().BeEquivalentTo(Kombinationstabelle.AusJson(erste.KombinationstabelleJson)!.Plaetze, vorlage.Name);
        }
    }

    [Fact]
    public async Task InitialisierenAsync_RuestetFestenKoBaumNachOhneManuelleVorbelegungZuUeberschreiben()
    {
        const string WM32 = "FIFA WM";
        const string WM32_VARIANTE = "32 Teams · 1998–2022";
        var optionen = new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            await new SeedDatenService(ctx, NullLogger<SeedDatenService>.Instance).InitialisierenAsync();
            // Stand vor #372: EM 24 ohne festen Baum; WM 32 mit nur einer (manuell angelegten) Vorbelegung.
            foreach (var (name, variante) in new[] { (EM24, EM24_VARIANTE), (WM32, WM32_VARIANTE) })
            {
                var vorlage = await ctx.TurnierInstanzen.Include(i => i.Runden).Include(i => i.Auslosungen)
                    .SingleAsync(i => i.Name == name && i.Variante == variante && i.Status == TurnierStatus.Vorlage);
                var entfernen = name == WM32
                    ? vorlage.Auslosungen.Where(a => a.TurnierRundeId != vorlage.Runden.Single(r => r.Name == "Finale").Id)
                    : vorlage.Auslosungen;
                ctx.Auslosungen.RemoveRange(entfernen.ToList());
                foreach (var runde in vorlage.Runden)
                {
                    runde.PaarungFestVorgegeben = false;
                    runde.KombinationstabelleJson = null;
                }
            }
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            await new SeedDatenService(ctx, NullLogger<SeedDatenService>.Instance).InitialisierenAsync();

            var em = await ctx.TurnierInstanzen.Include(i => i.Runden).Include(i => i.Auslosungen).ThenInclude(a => a.Ergebnisse)
                .SingleAsync(i => i.Name == EM24 && i.Variante == EM24_VARIANTE);
            var achtelfinale = em.Runden.Single(r => r.Name == "Achtelfinale");
            achtelfinale.PaarungFestVorgegeben.Should().BeTrue();
            Kombinationstabelle.AusJson(achtelfinale.KombinationstabelleJson)!.Kombinationen.Should().HaveCount(15);
            em.Auslosungen.Should().HaveCount(4, "Achtelfinale, Viertelfinale, Halbfinale, Finale");
            em.Auslosungen.Single(a => a.TurnierRundeId == achtelfinale.Id).Ergebnisse
                .Single(e => e.PaarungsNummer == 1).AuswaertsPlatzhalterName.Should().Be("3. Gruppe A/D/E/F");

            var wm = await ctx.TurnierInstanzen.Include(i => i.Auslosungen).SingleAsync(i => i.Name == WM32 && i.Variante == WM32_VARIANTE);
            wm.Auslosungen.Should().ContainSingle("eine bereits vorhandene Vorbelegung verhindert die Nachrüstung");
        }
    }
}
