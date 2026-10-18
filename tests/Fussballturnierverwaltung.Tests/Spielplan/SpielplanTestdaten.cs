using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Spielplan;

/// <summary>Turnieraufbauten für die Tests der Spielplan-Dienste (Generierung, Auflösung, Kaskade,
/// Finale-Automatik, Quellverknüpfung) — von mehreren Testklassen genutzt.</summary>
internal static class SpielplanTestdaten
{
    internal static TurnierverwaltungDbContext NeuerContext() =>
        new(new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    /// <summary>Legt Regelwerk (mit Entscheidungsverfahren, für KO/Finale Pflicht), Halbfinale- und Finale-Runde
    /// sowie 4 Mannschaften an. Halbfinale-Spiele werden NICHT angelegt (Aufgabe der einzelnen Tests).</summary>
    internal static void GrundSetupAnlegen(
        TurnierverwaltungDbContext ctx, out int hfRundeId, out int finaleRundeId, bool hfMitRueckspiel = false,
        string hfName = "Halbfinale")
    {
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.Entscheidungsverfahren.Add(new EntscheidungsVerfahren
            { Id = 1, RegelwerkId = 1, Reihenfolge = 1, VerfahrenArt = VerfahrenArt.Elfmeterschiessen });

        var hf = new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = hfName, Reihenfolge = 1,
            Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 4, AusgangsMannschaftsanzahl = 2,
            RegelwerkId = 1, MitRueckspiel = hfMitRueckspiel
        };
        var finale = new TurnierRunde
        {
            Id = 2, TurnierInstanzId = 1, Name = "Finale", Reihenfolge = 2,
            Rundenart = Rundenart.Finale, EingangsMannschaftsanzahl = 2, AusgangsMannschaftsanzahl = 1,
            RegelwerkId = 1
        };
        ctx.TurnierRunden.AddRange(hf, finale);

        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Team A", Kurzname = "TA", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Team B", Kurzname = "TB", LandId = 1 },
            new Mannschaft { Id = 3, Name = "Team C", Kurzname = "TC", LandId = 1 },
            new Mannschaft { Id = 4, Name = "Team D", Kurzname = "TD", LandId = 1 });

        ctx.SaveChanges();
        hfRundeId = hf.Id;
        finaleRundeId = finale.Id;
    }

    internal static void HalbfinalSpielAnlegen(
        TurnierverwaltungDbContext ctx, int id, int rundeId,
        int heimId, int auswaertsId, int? siegerMannschaftId)
    {
        ctx.Spiele.Add(new Spiel
        {
            Id = id, TurnierInstanzId = 1, TurnierRundeId = rundeId,
            Spielnummer = id, HeimMannschaftId = heimId, AuswaertsMannschaftId = auswaertsId,
            Status = siegerMannschaftId.HasValue ? SpielStatus.Abgeschlossen : SpielStatus.Geplant,
            IstRueckspiel = false
        });
        if (siegerMannschaftId.HasValue)
        {
            ctx.SpielErgebnisse.Add(new SpielErgebnis
            {
                Id = id, SpielId = id,
                HeimToreRegulaer = siegerMannschaftId == heimId ? 1 : 0,
                AuswaertsToreRegulaer = siegerMannschaftId == auswaertsId ? 1 : 0,
                EntscheidungArt = EntscheidungArt.RegulaereSpielzeit,
                SiegerMannschaftId = siegerMannschaftId
            });
        }
        ctx.SaveChanges();
    }

    internal static void LigaGrundSetupAnlegen(TurnierverwaltungDbContext ctx, out int rundeId)
    {
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        var runde = new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Liga", Reihenfolge = 1,
            Rundenart = Rundenart.Liga, EingangsMannschaftsanzahl = 4, AusgangsMannschaftsanzahl = 4,
            RegelwerkId = 1
        };
        ctx.TurnierRunden.Add(runde);
        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Team A", Kurzname = "TA", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Team B", Kurzname = "TB", LandId = 1 },
            new Mannschaft { Id = 3, Name = "Team C", Kurzname = "TC", LandId = 1 },
            new Mannschaft { Id = 4, Name = "Team D", Kurzname = "TD", LandId = 1 });
        ctx.SaveChanges();
        rundeId = runde.Id;
    }

    internal static void GruppenphaseUndKoSetupAnlegen(
        TurnierverwaltungDbContext ctx, out int gruppenphaseRundeId, out int koRundeId)
    {
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.Entscheidungsverfahren.Add(new EntscheidungsVerfahren
            { Id = 1, RegelwerkId = 1, Reihenfolge = 1, VerfahrenArt = VerfahrenArt.Elfmeterschiessen });

        var gruppenphase = new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Gruppenphase", Reihenfolge = 1,
            Rundenart = Rundenart.Gruppenphase, EingangsMannschaftsanzahl = 4, AusgangsMannschaftsanzahl = 4,
            RegelwerkId = 1, GruppenAnzahl = 2
        };
        var ko = new TurnierRunde
        {
            Id = 2, TurnierInstanzId = 1, Name = "Halbfinale", Reihenfolge = 2,
            Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 4, AusgangsMannschaftsanzahl = 2,
            RegelwerkId = 1
        };
        ctx.TurnierRunden.AddRange(gruppenphase, ko);

        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Team A", Kurzname = "TA", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Team B", Kurzname = "TB", LandId = 1 },
            new Mannschaft { Id = 3, Name = "Team C", Kurzname = "TC", LandId = 1 },
            new Mannschaft { Id = 4, Name = "Team D", Kurzname = "TD", LandId = 1 });

        // Gruppe A (Team A/B): Team A gewinnt → Rang 1. Gruppe B (Team C/D): Team C gewinnt → Rang 1.
        ctx.Spiele.Add(new Spiel
        {
            Id = 1, TurnierInstanzId = 1, TurnierRundeId = gruppenphase.Id, Spielnummer = 1,
            HeimMannschaftId = 1, AuswaertsMannschaftId = 2, Status = SpielStatus.Abgeschlossen
        });
        ctx.SpielErgebnisse.Add(new SpielErgebnis
            { Id = 1, SpielId = 1, HeimToreRegulaer = 1, AuswaertsToreRegulaer = 0, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit });
        ctx.Spiele.Add(new Spiel
        {
            Id = 2, TurnierInstanzId = 1, TurnierRundeId = gruppenphase.Id, Spielnummer = 2,
            HeimMannschaftId = 3, AuswaertsMannschaftId = 4, Status = SpielStatus.Abgeschlossen
        });
        ctx.SpielErgebnisse.Add(new SpielErgebnis
            { Id = 2, SpielId = 2, HeimToreRegulaer = 1, AuswaertsToreRegulaer = 0, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit });

        ctx.SaveChanges();
        gruppenphaseRundeId = gruppenphase.Id;
        koRundeId = ko.Id;
    }

    internal static void SchweizerSystemGrundSetupAnlegen(TurnierverwaltungDbContext ctx, out int rundeId)
    {
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        var runde = new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Ligaphase", Reihenfolge = 1,
            Rundenart = Rundenart.SchweizerSystem, EingangsMannschaftsanzahl = 4, AusgangsMannschaftsanzahl = 4,
            RegelwerkId = 1
        };
        ctx.TurnierRunden.Add(runde);
        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Team A", Kurzname = "TA", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Team B", Kurzname = "TB", LandId = 1 },
            new Mannschaft { Id = 3, Name = "Team C", Kurzname = "TC", LandId = 1 },
            new Mannschaft { Id = 4, Name = "Team D", Kurzname = "TD", LandId = 1 });
        ctx.SaveChanges();
        rundeId = runde.Id;
    }

    internal static async Task<Auslosung> SchweizerAuslosungAnlegen(
        TurnierverwaltungDbContext ctx, int rundeId, params (int? Heim, int? Auswaerts)[] paarungen)
    {
        var auslosung = new Auslosung
        {
            TurnierInstanzId = 1, TurnierRundeId = rundeId, Name = "Auslosung",
            Auslosungsart = Auslosungsart.Manuell, Status = AuslosungsStatus.Abgeschlossen
        };
        ctx.Auslosungen.Add(auslosung);
        await ctx.SaveChangesAsync();

        int nr = 1;
        foreach (var (heim, auswaerts) in paarungen)
            ctx.AuslosungsErgebnisse.Add(new AuslosungsErgebnis
            {
                AuslosungId = auslosung.Id, PaarungsNummer = nr++,
                HeimMannschaftId = heim, AuswaertsMannschaftId = auswaerts, IstFreilos = auswaerts is null
            });
        await ctx.SaveChangesAsync();
        return auslosung;
    }

    internal static void ZweiKoZweigeSetupAnlegen(
        TurnierverwaltungDbContext ctx, out int zweig1Id, out int zweig2Id, out int zielRundeId)
    {
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.Entscheidungsverfahren.Add(new EntscheidungsVerfahren
            { Id = 1, RegelwerkId = 1, Reihenfolge = 1, VerfahrenArt = VerfahrenArt.Elfmeterschiessen });

        var zweig1 = new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Zweig 1", Reihenfolge = 1,
            Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 2, AusgangsMannschaftsanzahl = 1, RegelwerkId = 1
        };
        var zweig2 = new TurnierRunde
        {
            Id = 2, TurnierInstanzId = 1, Name = "Zweig 2", Reihenfolge = 1,
            Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 2, AusgangsMannschaftsanzahl = 1, RegelwerkId = 1
        };
        // Zielrunde ist bewusst eine KoRunde (nicht Rundenart.Finale) - zeigt dass der Sync-Hook
        // jetzt jede Zielrunde mit zwei Eingangsteams bedient, nicht nur klassische Finalrunden.
        var ziel = new TurnierRunde
        {
            Id = 3, TurnierInstanzId = 1, Name = "Halbfinale", Reihenfolge = 2,
            Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 2, AusgangsMannschaftsanzahl = 1, RegelwerkId = 1
        };
        ctx.TurnierRunden.AddRange(zweig1, zweig2, ziel);

        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Team A", Kurzname = "TA", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Team B", Kurzname = "TB", LandId = 1 },
            new Mannschaft { Id = 3, Name = "Team C", Kurzname = "TC", LandId = 1 },
            new Mannschaft { Id = 4, Name = "Team D", Kurzname = "TD", LandId = 1 });

        ctx.Uebergangsregeln.AddRange(
            new Uebergangsregel { QuellRundeId = zweig1.Id, ZielRundeId = ziel.Id, GruppeIndex = 1, PlatzierungVon = 1, PlatzierungBis = 1 },
            new Uebergangsregel { QuellRundeId = zweig2.Id, ZielRundeId = ziel.Id, GruppeIndex = 1, PlatzierungVon = 1, PlatzierungBis = 1 });

        ctx.SaveChanges();
        zweig1Id = zweig1.Id;
        zweig2Id = zweig2.Id;
        zielRundeId = ziel.Id;
    }

    internal static void AnzahlBesteSetupAnlegen(
        TurnierverwaltungDbContext ctx, out int gruppenphaseId, out int zielRundeId)
    {
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.PunkteRegeln.Add(new PunkteRegel
            { Id = 1, RegelwerkId = 1, PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 });
        ctx.Entscheidungsverfahren.Add(new EntscheidungsVerfahren
            { Id = 1, RegelwerkId = 1, Reihenfolge = 1, VerfahrenArt = VerfahrenArt.Elfmeterschiessen });

        var gruppenphase = new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Gruppenphase", Reihenfolge = 1,
            Rundenart = Rundenart.Gruppenphase, EingangsMannschaftsanzahl = 9, AusgangsMannschaftsanzahl = 2,
            RegelwerkId = 1, GruppenAnzahl = 3
        };
        var ziel = new TurnierRunde
        {
            Id = 2, TurnierInstanzId = 1, Name = "Finale", Reihenfolge = 2,
            Rundenart = Rundenart.Finale, EingangsMannschaftsanzahl = 2, AusgangsMannschaftsanzahl = 1, RegelwerkId = 1
        };
        ctx.TurnierRunden.AddRange(gruppenphase, ziel);

        for (int i = 1; i <= 9; i++)
            ctx.Mannschaften.Add(new Mannschaft { Id = i, Name = $"Team {i}", Kurzname = $"T{i}", LandId = 1 });

        ctx.Uebergangsregeln.Add(new Uebergangsregel
        {
            QuellRundeId = gruppenphase.Id, ZielRundeId = ziel.Id,
            GruppeIndex = null, PlatzierungVon = 3, PlatzierungBis = 3, AnzahlBeste = 2
        });

        ctx.SaveChanges();
        gruppenphaseId = gruppenphase.Id;
        zielRundeId = ziel.Id;
    }

    internal static void AnzahlBesteSpieleHinzufuegen(TurnierverwaltungDbContext ctx, int rundeId)
    {
        // Drei Gruppen zu je drei Teams, Gruppendritte mit unterschiedlicher Tordifferenz (-3/-2/-1) -
        // identisches Szenario wie TabellenServiceTests.UebergreifendeRangfolgeBerechnenAsync_*.
        void Spiel(int id, int heim, int aus, int heimTore, int ausTore, int? sieger)
        {
            ctx.Spiele.Add(new Spiel
            {
                Id = id, TurnierInstanzId = 1, TurnierRundeId = rundeId, Spielnummer = id,
                HeimMannschaftId = heim, AuswaertsMannschaftId = aus, Status = SpielStatus.Abgeschlossen
            });
            ctx.SpielErgebnisse.Add(new SpielErgebnis
            {
                Id = id, SpielId = id, HeimToreRegulaer = heimTore, AuswaertsToreRegulaer = ausTore,
                EntscheidungArt = EntscheidungArt.RegulaereSpielzeit, SiegerMannschaftId = sieger
            });
        }

        Spiel(1, heim: 1, aus: 2, heimTore: 3, ausTore: 0, sieger: 1);
        Spiel(2, heim: 2, aus: 3, heimTore: 1, ausTore: 1, sieger: null); // Gruppe 1, 3. = Team 2 (GD -3)
        Spiel(3, heim: 4, aus: 5, heimTore: 2, ausTore: 0, sieger: 4);
        Spiel(4, heim: 5, aus: 6, heimTore: 0, ausTore: 0, sieger: null); // Gruppe 2, 3. = Team 5 (GD -2)
        Spiel(5, heim: 7, aus: 8, heimTore: 1, ausTore: 0, sieger: 7);
        Spiel(6, heim: 8, aus: 9, heimTore: 2, ausTore: 2, sieger: null); // Gruppe 3, 3. = Team 8 (GD -1)
    }

    internal static void DreiGruppenUndZielrundeSetupAnlegen(
        TurnierverwaltungDbContext ctx, out int gruppenphaseRundeId, out int zielRundeId)
    {
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.PunkteRegeln.Add(new PunkteRegel
            { Id = 1, RegelwerkId = 1, PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 });
        ctx.Entscheidungsverfahren.Add(new EntscheidungsVerfahren
            { Id = 1, RegelwerkId = 1, Reihenfolge = 1, VerfahrenArt = VerfahrenArt.Elfmeterschiessen });

        var gruppenphase = new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Gruppenphase", Reihenfolge = 1,
            Rundenart = Rundenart.Gruppenphase, EingangsMannschaftsanzahl = 9, AusgangsMannschaftsanzahl = 9,
            RegelwerkId = 1, GruppenAnzahl = 3
        };
        var ziel = new TurnierRunde
        {
            Id = 2, TurnierInstanzId = 1, Name = "Achtelfinale", Reihenfolge = 2,
            Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 2, AusgangsMannschaftsanzahl = 1,
            RegelwerkId = 1
        };
        ctx.TurnierRunden.AddRange(gruppenphase, ziel);

        for (int i = 1; i <= 9; i++)
            ctx.Mannschaften.Add(new Mannschaft { Id = i, Name = $"Team {i}", Kurzname = $"T{i}", LandId = 1 });

        ctx.SaveChanges();
        gruppenphaseRundeId = gruppenphase.Id;
        zielRundeId = ziel.Id;
    }

    // Drei Gruppen, zwei beste Dritte (Copa-América-12-Format): qualifiziert sind Gruppe B (Team 5) und
    // Gruppe C (Team 8). Die Tabelle ordnet die Plätze bewusst NICHT in Ranglisten-Reihenfolge zu.
    internal static void KombinationstabelleSetzen(TurnierverwaltungDbContext ctx, int zielRundeId)
    {
        var tabelle = new Kombinationstabelle(
            ["3. Gruppe A/B", "3. Gruppe B/C"],
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["AB"] = ["A", "B"],
                ["AC"] = ["A", "C"],
                ["BC"] = ["B", "C"],
            });
        ctx.TurnierRunden.First(r => r.Id == zielRundeId).KombinationstabelleJson = tabelle.ZuJson();
        ctx.Spiele.Add(new Spiel
        {
            Id = 10, TurnierInstanzId = 1, TurnierRundeId = zielRundeId, Spielnummer = 10,
            HeimPlatzhalterName = "3. Gruppe A/B", AuswaertsPlatzhalterName = "3. Gruppe B/C",
            Status = SpielStatus.Geplant
        });
    }

    internal static async Task<Auslosung> AuslosungMitPlatzhalternAnlegen(
        TurnierverwaltungDbContext ctx, int rundeId,
        params (string? Heim, string? Auswaerts)[] paarungen)
    {
        var auslosung = new Auslosung
        {
            TurnierInstanzId = 1, TurnierRundeId = rundeId, Name = "Auslosung",
            Auslosungsart = Auslosungsart.Manuell, Status = AuslosungsStatus.Abgeschlossen
        };
        ctx.Auslosungen.Add(auslosung);
        await ctx.SaveChangesAsync();

        int nr = 1;
        foreach (var (heim, auswaerts) in paarungen)
            ctx.AuslosungsErgebnisse.Add(new AuslosungsErgebnis
            {
                AuslosungId = auslosung.Id, PaarungsNummer = nr++,
                HeimPlatzhalterName = heim, AuswaertsPlatzhalterName = auswaerts
            });
        await ctx.SaveChangesAsync();
        return auslosung;
    }

    /// <summary>Runde 1 „Quali“ (K.o., 4 entschiedene Spiele, Heimteams 1/3/5/7 gewinnen), Runde 2
    /// Gruppenphase mit einer Gruppe und abgeschlossener Gruppen-Auslosung (ein Eintrag pro Slot).</summary>
    internal static Task<int> QualiUndPlatzhalterGruppeAnlegen(
        TurnierverwaltungDbContext ctx, bool mitRueckspiel, params (int? MannschaftId, string? Platzhalter)[] slots)
        => QualiUndPlatzhalterGruppenAnlegen(ctx, mitRueckspiel, entschiedeneQualiSpiele: 4,
            [.. slots.Select(x => (1, x.MannschaftId, x.Platzhalter))]);

    /// <summary>Wie <see cref="QualiUndPlatzhalterGruppeAnlegen"/>, aber mit mehreren Gruppen (Gruppe = PaarungsNummer)
    /// und nur den ersten <paramref name="entschiedeneQualiSpiele"/> Quali-Spielen entschieden.</summary>
    internal static async Task<int> QualiUndPlatzhalterGruppenAnlegen(
        TurnierverwaltungDbContext ctx, bool mitRueckspiel, int entschiedeneQualiSpiele,
        (int Gruppe, int? MannschaftId, string? Platzhalter)[] slots)
    {
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.Entscheidungsverfahren.Add(new EntscheidungsVerfahren
            { Id = 1, RegelwerkId = 1, Reihenfolge = 1, VerfahrenArt = VerfahrenArt.Elfmeterschiessen });
        ctx.TurnierRunden.AddRange(
            new TurnierRunde
            {
                Id = 1, TurnierInstanzId = 1, Name = "Quali", Reihenfolge = 1, Rundenart = Rundenart.KoRunde,
                EingangsMannschaftsanzahl = 8, AusgangsMannschaftsanzahl = 4, RegelwerkId = 1
            },
            new TurnierRunde
            {
                Id = 2, TurnierInstanzId = 1, Name = "Gruppenphase", Reihenfolge = 2, Rundenart = Rundenart.Gruppenphase,
                EingangsMannschaftsanzahl = slots.Length, AusgangsMannschaftsanzahl = 2, RegelwerkId = 1,
                GruppenAnzahl = slots.Select(x => x.Gruppe).Distinct().Count(), MitRueckspiel = mitRueckspiel
            });
        ctx.Mannschaften.AddRange(Enumerable.Range(1, 10)
            .Select(i => new Mannschaft { Id = i, Name = $"Team {i}", Kurzname = $"T{i}", LandId = 1 }));
        for (int q = 1; q <= 4; q++)
        {
            int heim = q * 2 - 1;
            bool entschieden = q <= entschiedeneQualiSpiele;
            ctx.Spiele.Add(new Spiel
            {
                Id = q, TurnierInstanzId = 1, TurnierRundeId = 1, Spielnummer = q,
                HeimMannschaftId = heim, AuswaertsMannschaftId = heim + 1,
                Status = entschieden ? SpielStatus.Abgeschlossen : SpielStatus.Geplant
            });
            if (entschieden) ctx.SpielErgebnisse.Add(new SpielErgebnis
            {
                Id = q, SpielId = q, HeimToreRegulaer = 1, AuswaertsToreRegulaer = 0,
                EntscheidungArt = EntscheidungArt.RegulaereSpielzeit, SiegerMannschaftId = heim
            });
        }
        var auslosung = new Auslosung
        {
            TurnierInstanzId = 1, TurnierRundeId = 2, Name = "Auslosung",
            Auslosungsart = Auslosungsart.Manuell, Status = AuslosungsStatus.Abgeschlossen
        };
        ctx.Auslosungen.Add(auslosung);
        await ctx.SaveChangesAsync();

        foreach (var (gruppe, mannschaftId, platzhalter) in slots)
            ctx.AuslosungsErgebnisse.Add(new AuslosungsErgebnis
            {
                AuslosungId = auslosung.Id, PaarungsNummer = gruppe,
                HeimMannschaftId = mannschaftId, HeimPlatzhalterName = platzhalter
            });
        await ctx.SaveChangesAsync();

        var dienste = new SpielplanDienste(ctx);
        await dienste.Spielplan.GenerierenAusAuslosungAsync(2, [.. ctx.AuslosungsErgebnisse]);
        return 2;
    }

    /// <summary>Gruppe 1: „Sieger Quali 1“ (= Team 1), „Sieger Quali 3“, „Sieger Quali 4“ (beide offen) —
    /// Team 1 spielt nur gegen offene Plätze. Gruppe 2: „Sieger Quali 2“ (= Team 3), Team 9, Team 10.</summary>
    internal static Task<int> TeilaufgeloesteGruppenAnlegen(TurnierverwaltungDbContext ctx) =>
        QualiUndPlatzhalterGruppenAnlegen(ctx, mitRueckspiel: false, entschiedeneQualiSpiele: 2,
        [
            (1, null, "Sieger Quali 1"), (1, null, "Sieger Quali 3"), (1, null, "Sieger Quali 4"),
            (2, null, "Sieger Quali 2"), (2, 9, null), (2, 10, null)
        ]);

    internal static TabellenService TabellenServiceFuer(TurnierverwaltungDbContext ctx) =>
        new(new TurnierRundeRepository(ctx), new SpielRepository(ctx), new RegelwerkRepository(ctx), new RanglisteRepository(ctx));

    /// <summary>Ligaphase (4 Teams, Einfachrunde, 3 Spieltage) + Halbfinale mit Platzhaltern
    /// "Platz 1/4 (Ligaphase)" und "Platz 2/3 (Ligaphase)". Spieltag 1: A–B, C–D; 2: A–C, B–D; 3: A–D, B–C.</summary>
    internal static void LigaMitFolgeKoRundeAnlegen(
        TurnierverwaltungDbContext ctx, out int ligaRundeId, out int koRundeId,
        Rundenart ligaRundenart = Rundenart.Liga)
    {
        LigaGrundSetupAnlegen(ctx, out ligaRundeId);
        var liga = ctx.TurnierRunden.Single(r => r.Id == 1);
        liga.Name = "Ligaphase";
        liga.Rundenart = ligaRundenart;
        ctx.Entscheidungsverfahren.Add(new EntscheidungsVerfahren
            { Id = 1, RegelwerkId = 1, Reihenfolge = 1, VerfahrenArt = VerfahrenArt.Elfmeterschiessen });
        var ko = new TurnierRunde
        {
            Id = 2, TurnierInstanzId = 1, Name = "Halbfinale", Reihenfolge = 2,
            Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 4, AusgangsMannschaftsanzahl = 2,
            RegelwerkId = 1
        };
        ctx.TurnierRunden.Add(ko);
        ctx.Spiele.AddRange(
            new Spiel { Id = 101, TurnierInstanzId = 1, TurnierRundeId = 2, Spielnummer = 101,
                        HeimPlatzhalterName = "Platz 1 (Ligaphase)", AuswaertsPlatzhalterName = "Platz 4 (Ligaphase)" },
            new Spiel { Id = 102, TurnierInstanzId = 1, TurnierRundeId = 2, Spielnummer = 102,
                        HeimPlatzhalterName = "Platz 2 (Ligaphase)", AuswaertsPlatzhalterName = "Platz 3 (Ligaphase)" });

        (int Id, int Spieltag, int Heim, int Auswaerts)[] paarungen =
            [(1, 1, 1, 2), (2, 1, 3, 4), (3, 2, 1, 3), (4, 2, 2, 4), (5, 3, 1, 4), (6, 3, 2, 3)];
        foreach (var (id, spieltag, heim, auswaerts) in paarungen)
            ctx.Spiele.Add(new Spiel
            {
                Id = id, TurnierInstanzId = 1, TurnierRundeId = 1, Spielnummer = id, Spieltag = spieltag,
                HeimMannschaftId = heim, AuswaertsMannschaftId = auswaerts, Status = SpielStatus.Geplant
            });
        ctx.SaveChanges();
        koRundeId = ko.Id;
    }

    internal static void LigaErgebnisSetzen(TurnierverwaltungDbContext ctx, int spielId, int heimTore, int auswaertsTore)
    {
        var spiel = ctx.Spiele.Single(s => s.Id == spielId);
        spiel.Status = SpielStatus.Abgeschlossen;
        ctx.SpielErgebnisse.Add(new SpielErgebnis
        {
            Id = spielId, SpielId = spielId, HeimToreRegulaer = heimTore, AuswaertsToreRegulaer = auswaertsTore,
            EntscheidungArt = EntscheidungArt.RegulaereSpielzeit,
            SiegerMannschaftId = heimTore > auswaertsTore ? spiel.HeimMannschaftId
                               : auswaertsTore > heimTore ? spiel.AuswaertsMannschaftId : null
        });
        ctx.SaveChanges();
    }

    /// <summary>Spieltag 1+2: A gewinnt beide (6 Pkt.), alle übrigen Spiele Unentschieden
    /// (B 1, C 1, D 2) — A ist vor dem letzten Spieltag uneinholbar Erster (D max. 5).</summary>
    internal static void ZweiSpieltageMitUneinholbaremTabellenfuehrer(TurnierverwaltungDbContext ctx)
    {
        LigaErgebnisSetzen(ctx, 1, 2, 0); // A–B
        LigaErgebnisSetzen(ctx, 2, 1, 1); // C–D
        LigaErgebnisSetzen(ctx, 3, 3, 0); // A–C
        LigaErgebnisSetzen(ctx, 4, 0, 0); // B–D
    }

    /// <summary>Letzter Spieltag: A–D 1:0, B–C 2:1 → Endstand A 9, B 4, D 2, C 1.</summary>
    internal static void LetztenSpieltagSpielen(TurnierverwaltungDbContext ctx)
    {
        LigaErgebnisSetzen(ctx, 5, 1, 0);
        LigaErgebnisSetzen(ctx, 6, 2, 1);
    }

    internal static void KoSpielAnlegen(
        TurnierverwaltungDbContext ctx, int id, int rundeId, int heimId, int auswaertsId,
        int? siegerMannschaftId, bool istRueckspiel = false, int? hinspielId = null)
    {
        ctx.Spiele.Add(new Spiel
        {
            Id = id, TurnierInstanzId = 1, TurnierRundeId = rundeId,
            Spielnummer = id, HeimMannschaftId = heimId, AuswaertsMannschaftId = auswaertsId,
            Status = SpielStatus.Abgeschlossen, IstRueckspiel = istRueckspiel, HinspielId = hinspielId
        });
        ctx.SpielErgebnisse.Add(new SpielErgebnis
        {
            Id = id, SpielId = id,
            HeimToreRegulaer = siegerMannschaftId == heimId ? 1 : 0,
            AuswaertsToreRegulaer = siegerMannschaftId == auswaertsId ? 1 : 0,
            EntscheidungArt = EntscheidungArt.RegulaereSpielzeit,
            SiegerMannschaftId = siegerMannschaftId
        });
        ctx.SaveChanges();
    }

    /// <summary>Halbfinale mit Hin-/Rückspiel: Paarung 1 A–B (Hinspiel-Sieger B, Gesamtsieger A),
    /// Paarung 2 C–D (Gesamtsieger D). Spielnummern 1–4, Rückspiele direkt nach ihrem Hinspiel.</summary>
    internal static void HalbfinaleMitRueckspielenAnlegen(TurnierverwaltungDbContext ctx, int hfRundeId)
    {
        KoSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 2);
        KoSpielAnlegen(ctx, 2, hfRundeId, heimId: 2, auswaertsId: 1, siegerMannschaftId: 1, istRueckspiel: true, hinspielId: 1);
        KoSpielAnlegen(ctx, 3, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: 4);
        KoSpielAnlegen(ctx, 4, hfRundeId, heimId: 4, auswaertsId: 3, siegerMannschaftId: 4, istRueckspiel: true, hinspielId: 3);
    }
}
