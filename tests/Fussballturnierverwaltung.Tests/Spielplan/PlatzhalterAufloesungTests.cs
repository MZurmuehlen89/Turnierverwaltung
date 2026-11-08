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
using static Fussballturnierverwaltung.Tests.Spielplan.SpielplanTestdaten;

namespace Fussballturnierverwaltung.Tests.Spielplan;

public class PlatzhalterAufloesungTests
{
    // ── Tests: Freitext-Platzhalter-Auflösung (Regressionstests für Issue #130) ─

    [Fact]
    public async Task RundeAufloesen_LoestGruppensiegerUndGruppenrangAuf()
    {
        await using var ctx = NeuerContext();
        GruppenphaseUndKoSetupAnlegen(ctx, out _, out int koRundeId);
        ctx.Spiele.Add(new Spiel
        {
            Id = 10, TurnierInstanzId = 1, TurnierRundeId = koRundeId, Spielnummer = 10,
            HeimPlatzhalterName = "Sieger Gruppe A", AuswaertsPlatzhalterName = "2. Gruppe B",
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        var anzahl = await dienste.Aufloesung.RundeAufloesenAsync(koRundeId);

        anzahl.Should().Be(1);
        var spiel = ctx.Spiele.First(s => s.Id == 10);
        spiel.HeimMannschaftId.Should().Be(1);      // Sieger Gruppe A = Team A
        spiel.AuswaertsMannschaftId.Should().Be(4); // 2. Gruppe B = Team D
    }

    [Fact]
    public async Task RundeAufloesen_LoestNurAufloesbareSeiteAufWennAndereSeiteNochOffenIst()
    {
        // Issue #206 (TV-Präsentation-Stil): Gruppe A ist entschieden (einziges Spiel der 2er-Gruppe
        // gespielt), Gruppe B ist noch offen (Spiel C-D noch nicht gespielt). Das Halbfinale soll
        // trotzdem die bereits bekannte Heim-Seite ("Sieger Gruppe A") schreiben, während die
        // Auswärts-Seite ("2. Gruppe B") als Platzhaltertext stehen bleibt, statt beide Seiten erst
        // gemeinsam zu schreiben, wenn auch Gruppe B entschieden ist.
        await using var ctx = NeuerContext();
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

        // Gruppe A (Team A/B): entschieden, Team A gewinnt.
        ctx.Spiele.Add(new Spiel
        {
            Id = 1, TurnierInstanzId = 1, TurnierRundeId = gruppenphase.Id, Spielnummer = 1,
            HeimMannschaftId = 1, AuswaertsMannschaftId = 2, Status = SpielStatus.Abgeschlossen
        });
        ctx.SpielErgebnisse.Add(new SpielErgebnis
            { Id = 1, SpielId = 1, HeimToreRegulaer = 1, AuswaertsToreRegulaer = 0, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit });

        // Gruppe B (Team C/D): noch offen, Spiel noch nicht gespielt.
        ctx.Spiele.Add(new Spiel
        {
            Id = 2, TurnierInstanzId = 1, TurnierRundeId = gruppenphase.Id, Spielnummer = 2,
            HeimMannschaftId = 3, AuswaertsMannschaftId = 4, Status = SpielStatus.Geplant
        });

        ctx.Spiele.Add(new Spiel
        {
            Id = 10, TurnierInstanzId = 1, TurnierRundeId = ko.Id, Spielnummer = 10,
            HeimPlatzhalterName = "Sieger Gruppe A", AuswaertsPlatzhalterName = "2. Gruppe B",
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        var anzahl = await dienste.Aufloesung.RundeAufloesenAsync(ko.Id);

        anzahl.Should().Be(1, "das Spiel wurde teilweise aufgelöst und zählt als Fortschritt");
        var spiel = ctx.Spiele.AsNoTracking().First(s => s.Id == 10);
        spiel.HeimMannschaftId.Should().Be(1, "Sieger Gruppe A = Team A, bereits entschieden");
        spiel.HeimPlatzhalterName.Should().BeNull();
        spiel.AuswaertsMannschaftId.Should().BeNull("Gruppe B ist noch offen");
        spiel.AuswaertsPlatzhalterName.Should().Be("2. Gruppe B", "Platzhaltertext bleibt stehen, bis Gruppe B entschieden ist");

        // Gruppe B jetzt abschließen (Team C gewinnt) — ein zweiter Aufruf muss die restliche Seite
        // nachziehen, ohne die bereits aufgelöste Heim-Seite anzufassen.
        ctx.Spiele.First(s => s.Id == 2).Status = SpielStatus.Abgeschlossen;
        ctx.SpielErgebnisse.Add(new SpielErgebnis
            { Id = 2, SpielId = 2, HeimToreRegulaer = 1, AuswaertsToreRegulaer = 0, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit });
        await ctx.SaveChangesAsync();

        var anzahlNachher = await dienste.Aufloesung.RundeAufloesenAsync(ko.Id);

        anzahlNachher.Should().Be(1, "die zweite, nachziehende Auflösung zählt erneut als Fortschritt");
        var spielNachher = ctx.Spiele.AsNoTracking().First(s => s.Id == 10);
        spielNachher.HeimMannschaftId.Should().Be(1, "bereits vorher aufgelöste Heim-Seite bleibt unverändert stehen");
        spielNachher.AuswaertsMannschaftId.Should().Be(4, "2. Gruppe B = Team D, jetzt auch entschieden");
        spielNachher.AuswaertsPlatzhalterName.Should().BeNull();
    }

    [Fact]
    public async Task Vorschau_LiefertOptimistischenNamenOhneZuPersistieren()
    {
        // Issue #208: Team A führt Gruppe A aktuell auf Rang 2 (3 Punkte, alle Spiele gespielt),
        // Team C ist uneinholbar Erster (7 Punkte). Rang 2 ist NICHT SicherPositioniert (ein
        // Unentschieden im offenen Spiel B-D hält Team A auf Rang 2, ein Sieg verdrängt es auf
        // Rang 3) — die reguläre Auflösung darf daher nur die Auswaerts-Seite (sicherer Rang 1)
        // schreiben, nicht die Heim-Seite (unsicherer Rang 2). Die Vorschau soll für beide Seiten
        // den aktuell führenden Namen liefern, ohne zusätzlich etwas zu persistieren.
        await using var ctx = NeuerContext();
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.Entscheidungsverfahren.Add(new EntscheidungsVerfahren
            { Id = 1, RegelwerkId = 1, Reihenfolge = 1, VerfahrenArt = VerfahrenArt.Elfmeterschiessen });

        var gruppenphase = new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Gruppenphase", Reihenfolge = 1,
            Rundenart = Rundenart.Gruppenphase, EingangsMannschaftsanzahl = 4, AusgangsMannschaftsanzahl = 4,
            RegelwerkId = 1, GruppenAnzahl = 1
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

        void Spiel(int id, int heim, int auswaerts, int? heimTore, int? auswaertsTore, int? sieger = null)
        {
            ctx.Spiele.Add(new Spiel
            {
                Id = id, TurnierInstanzId = 1, TurnierRundeId = gruppenphase.Id, Spielnummer = id,
                HeimMannschaftId = heim, AuswaertsMannschaftId = auswaerts,
                Status = heimTore.HasValue ? SpielStatus.Abgeschlossen : SpielStatus.Geplant
            });
            if (heimTore.HasValue && auswaertsTore.HasValue)
                ctx.SpielErgebnisse.Add(new SpielErgebnis
                {
                    Id = id, SpielId = id, HeimToreRegulaer = heimTore.Value, AuswaertsToreRegulaer = auswaertsTore.Value,
                    EntscheidungArt = EntscheidungArt.RegulaereSpielzeit, SiegerMannschaftId = sieger
                });
        }
        Spiel(1, 1, 2, 1, 1);                    // A - B 1:1
        Spiel(2, 3, 4, 2, 1, sieger: 3);          // C schlägt D
        Spiel(3, 4, 1, 0, 0);                     // D - A 0:0
        Spiel(4, 2, 3, 0, 1, sieger: 3);          // C schlägt B
        Spiel(5, 1, 3, 1, 1);                     // A - C 1:1
        Spiel(6, 4, 2, null, null);                // D - B noch offen
        await ctx.SaveChangesAsync();

        ctx.Spiele.Add(new Spiel
        {
            Id = 10, TurnierInstanzId = 1, TurnierRundeId = ko.Id, Spielnummer = 10,
            HeimPlatzhalterName = "2. Gruppe A", AuswaertsPlatzhalterName = "Sieger Gruppe A",
            Status = SpielStatus.Geplant
        });
        await ctx.SaveChangesAsync();
        var dienste = new SpielplanDienste(ctx);

        var regulaer = await dienste.Aufloesung.RundeAufloesenAsync(ko.Id);
        regulaer.Should().Be(1, "nur die Auswaerts-Seite (Sieger Gruppe A, sicher positioniert) darf regulär aufgelöst werden");
        var spielNachRegulaer = ctx.Spiele.AsNoTracking().First(s => s.Id == 10);
        spielNachRegulaer.HeimMannschaftId.Should().BeNull(
            "Rang 2 ist noch nicht SicherPositioniert, die reguläre Auflösung darf diese Seite nicht schreiben");
        spielNachRegulaer.AuswaertsMannschaftId.Should().Be(3, "Team C ist uneinholbar Erster und darf regulär aufgelöst werden");

        var vorschau = await dienste.Aufloesung.VorschauAsync(ko.Id);

        vorschau.Should().ContainKey(10);
        vorschau[10].Heim.Should().Be("Team A", "Team A führt Rang 2 aktuell, auch wenn das noch nicht sicher ist");
        vorschau[10].Auswaerts.Should().BeNull(
            "Auswaerts ist bereits regulär (persistent) aufgelöst, dafür liefert die Vorschau keinen Zusatzwert mehr");
        ctx.Spiele.AsNoTracking().First(s => s.Id == 10).HeimMannschaftId.Should().BeNull(
            "die Vorschau darf weiterhin nichts in die Datenbank geschrieben haben");
    }

    [Fact]
    public async Task RundeAufloesen_LoestGruppensiegerErstAufWennRangSicherPositioniertIst()
    {
        // 3-Team-Gruppe (Top 2 qualifizieren): Team C ist bereits abgehaengt, aber das direkte Duell A vs. B
        // ist noch offen und entscheidet erst, wer Rang 1 ("Sieger Gruppe A") wird (Issue #202 - der aktuelle
        // Tabellenrang allein reicht nicht, der Rang muss SicherPositioniert sein).
        await using var ctx = NeuerContext();
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.Entscheidungsverfahren.Add(new EntscheidungsVerfahren
            { Id = 1, RegelwerkId = 1, Reihenfolge = 1, VerfahrenArt = VerfahrenArt.Elfmeterschiessen });

        var gruppenphase = new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Gruppenphase", Reihenfolge = 1,
            Rundenart = Rundenart.Gruppenphase, EingangsMannschaftsanzahl = 3, AusgangsMannschaftsanzahl = 2,
            RegelwerkId = 1, GruppenAnzahl = 1
        };
        var ko = new TurnierRunde
        {
            Id = 2, TurnierInstanzId = 1, Name = "Halbfinale", Reihenfolge = 2,
            Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 2, AusgangsMannschaftsanzahl = 1,
            RegelwerkId = 1
        };
        ctx.TurnierRunden.AddRange(gruppenphase, ko);

        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Team A", Kurzname = "TA", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Team B", Kurzname = "TB", LandId = 1 },
            new Mannschaft { Id = 3, Name = "Team C", Kurzname = "TC", LandId = 1 });

        // A schlägt C 5:0, B schlägt C 5:0 (C rechnerisch raus) - A gegen B (Sieger = Gruppenerster) noch offen.
        ctx.Spiele.Add(new Spiel
        {
            Id = 1, TurnierInstanzId = 1, TurnierRundeId = gruppenphase.Id, Spielnummer = 1,
            HeimMannschaftId = 1, AuswaertsMannschaftId = 3, Status = SpielStatus.Abgeschlossen
        });
        ctx.SpielErgebnisse.Add(new SpielErgebnis
            { Id = 1, SpielId = 1, HeimToreRegulaer = 5, AuswaertsToreRegulaer = 0, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit });
        ctx.Spiele.Add(new Spiel
        {
            Id = 2, TurnierInstanzId = 1, TurnierRundeId = gruppenphase.Id, Spielnummer = 2,
            HeimMannschaftId = 2, AuswaertsMannschaftId = 3, Status = SpielStatus.Abgeschlossen
        });
        ctx.SpielErgebnisse.Add(new SpielErgebnis
            { Id = 2, SpielId = 2, HeimToreRegulaer = 5, AuswaertsToreRegulaer = 0, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit });
        var abSpiel = new Spiel
        {
            Id = 3, TurnierInstanzId = 1, TurnierRundeId = gruppenphase.Id, Spielnummer = 3,
            HeimMannschaftId = 1, AuswaertsMannschaftId = 2, Status = SpielStatus.Geplant
        };
        ctx.Spiele.Add(abSpiel);

        ctx.Spiele.Add(new Spiel
        {
            Id = 10, TurnierInstanzId = 1, TurnierRundeId = ko.Id, Spielnummer = 10,
            HeimPlatzhalterName = "Sieger Gruppe A", AuswaertsMannschaftId = 3,
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        var anzahlVorher = await dienste.Aufloesung.RundeAufloesenAsync(ko.Id);

        anzahlVorher.Should().Be(0, "wer Gruppenerster wird, entscheidet erst das noch offene Duell A vs. B");
        ctx.Spiele.First(s => s.Id == 10).HeimMannschaftId.Should().BeNull();

        // Jetzt A vs B abschließen (A gewinnt) - Rang 1 ist jetzt final.
        abSpiel.Status = SpielStatus.Abgeschlossen;
        ctx.SpielErgebnisse.Add(new SpielErgebnis
            { Id = 3, SpielId = 3, HeimToreRegulaer = 1, AuswaertsToreRegulaer = 0, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit });
        await ctx.SaveChangesAsync();

        var anzahlNachher = await dienste.Aufloesung.RundeAufloesenAsync(ko.Id);

        anzahlNachher.Should().Be(1);
        ctx.Spiele.First(s => s.Id == 10).HeimMannschaftId.Should().Be(1, "Sieger Gruppe A = Team A");
    }

    [Fact]
    public async Task RundeAufloesen_ReproduziertRealeGruppeAusManuellemTest()
    {
        // Reproduziert 1:1 die reale Gruppe "Gruppenphase 2" / "Gruppe A" aus dem manuellen #202-Test
        // (Arsenal/Dortmund/Polen/PSG, echtes FIFA-Regelwerk mit DirekterVergleich-Kaskade). Polen hat alle
        // 3 Spiele gespielt (9 Punkte), PSG (3 Punkte, 2 Spiele) kann selbst mit einem Sieg gegen Dortmund
        // maximal 6 Punkte erreichen - Polen ist damit rein rechnerisch bereits als Gruppenerster fixiert.
        await using var ctx = NeuerContext();
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });

        var regelSet = new RankingRegelSet { Id = 1, Name = "FIFA" };
        var regeln = new[]
        {
            new RankingRegel { Id = 1, Regelart = RankingRegelArt.Punkte,                       Sortierrichtung = Sortierrichtung.Absteigend },
            new RankingRegel { Id = 2, Regelart = RankingRegelArt.Tordifferenz,                  Sortierrichtung = Sortierrichtung.Absteigend },
            new RankingRegel { Id = 3, Regelart = RankingRegelArt.ErzielteTore,                  Sortierrichtung = Sortierrichtung.Absteigend },
            new RankingRegel { Id = 4, Regelart = RankingRegelArt.DirekterVergleichPunkte,       Sortierrichtung = Sortierrichtung.Absteigend },
            new RankingRegel { Id = 5, Regelart = RankingRegelArt.DirekterVergleichTordifferenz, Sortierrichtung = Sortierrichtung.Absteigend },
            new RankingRegel { Id = 6, Regelart = RankingRegelArt.DirekterVergleichErzielteTore, Sortierrichtung = Sortierrichtung.Absteigend },
            new RankingRegel { Id = 7, Regelart = RankingRegelArt.DirekterVergleichAuswaertstore,Sortierrichtung = Sortierrichtung.Absteigend },
            new RankingRegel { Id = 8, Regelart = RankingRegelArt.FairplayWertung,               Sortierrichtung = Sortierrichtung.Absteigend },
            new RankingRegel { Id = 9, Regelart = RankingRegelArt.Losentscheid,                  Sortierrichtung = Sortierrichtung.Aufsteigend },
        };
        var eintraege = regeln.Select((r, i) => new RankingRegelSetEintrag
            { Id = i + 1, RankingRegelSetId = 1, RankingRegelId = r.Id, Reihenfolge = i + 1 }).ToArray();
        ctx.RankingRegelSets.Add(regelSet);
        ctx.RankingRegeln.AddRange(regeln);
        ctx.RankingRegelSetEintraege.AddRange(eintraege);

        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "FIFA-Regelwerk", RankingRegelSetId = 1 });
        ctx.PunkteRegeln.Add(new PunkteRegel
            { Id = 1, RegelwerkId = 1, PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 });
        ctx.Entscheidungsverfahren.Add(new EntscheidungsVerfahren
            { Id = 1, RegelwerkId = 1, Reihenfolge = 1, VerfahrenArt = VerfahrenArt.Elfmeterschiessen });

        var gruppenphase = new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Gruppenphase 2", Reihenfolge = 1,
            Rundenart = Rundenart.Gruppenphase, EingangsMannschaftsanzahl = 4, AusgangsMannschaftsanzahl = 2,
            RegelwerkId = 1, GruppenAnzahl = 1
        };
        var halbfinale = new TurnierRunde
        {
            Id = 2, TurnierInstanzId = 1, Name = "Halbfinale", Reihenfolge = 2,
            Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 4, AusgangsMannschaftsanzahl = 2,
            RegelwerkId = 1
        };
        ctx.TurnierRunden.AddRange(gruppenphase, halbfinale);

        const int arsenal = 67, dortmund = 61, polen = 15, psg = 69;
        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = arsenal,  Name = "Arsenal FC",           Kurzname = "ARS", LandId = 1 },
            new Mannschaft { Id = dortmund, Name = "Borussia Dortmund",    Kurzname = "BVB", LandId = 1 },
            new Mannschaft { Id = polen,    Name = "Polen",                Kurzname = "POL", LandId = 1 },
            new Mannschaft { Id = psg,      Name = "Paris Saint-Germain",  Kurzname = "PSG", LandId = 1 });
        ctx.SaveChanges();

        void Ergebnis(int id, int spielnr, int heim, int auswaerts, int heimTore, int auswaertsTore)
        {
            ctx.Spiele.Add(new Spiel
            {
                Id = id, TurnierInstanzId = 1, TurnierRundeId = gruppenphase.Id, Spielnummer = spielnr,
                HeimMannschaftId = heim, AuswaertsMannschaftId = auswaerts, Status = SpielStatus.Abgeschlossen
            });
            ctx.SpielErgebnisse.Add(new SpielErgebnis
            {
                Id = id, SpielId = id, HeimToreRegulaer = heimTore, AuswaertsToreRegulaer = auswaertsTore,
                EntscheidungArt = EntscheidungArt.RegulaereSpielzeit
            });
        }

        Ergebnis(1, 49, arsenal, dortmund, 1, 1); // Arsenal - Dortmund 1:1
        Ergebnis(2, 50, polen, psg, 2, 1);        // Polen schlägt PSG 2:1
        Ergebnis(3, 53, psg, arsenal, 3, 1);      // PSG schlägt Arsenal 3:1
        Ergebnis(4, 54, dortmund, polen, 1, 2);   // Polen schlägt Dortmund 1:2
        Ergebnis(5, 57, arsenal, polen, 1, 3);    // Polen schlägt Arsenal 1:3
        ctx.Spiele.Add(new Spiel                  // PSG - Dortmund noch offen
        {
            Id = 6, TurnierInstanzId = 1, TurnierRundeId = gruppenphase.Id, Spielnummer = 58,
            HeimMannschaftId = psg, AuswaertsMannschaftId = dortmund, Status = SpielStatus.Geplant
        });

        ctx.Spiele.Add(new Spiel
        {
            Id = 10, TurnierInstanzId = 1, TurnierRundeId = halbfinale.Id, Spielnummer = 61,
            HeimPlatzhalterName = "1. Gruppe A", AuswaertsMannschaftId = arsenal, Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);
        var tabellenService = new TabellenService(new TurnierRundeRepository(ctx), new SpielRepository(ctx), new RegelwerkRepository(ctx), new RanglisteRepository(ctx), new AuslosungRepository(ctx));

        var gruppen = await tabellenService.GruppenTabellenBerechnenAsync(gruppenphase.Id);
        var polenEintrag = gruppen[0].Tabelle.First(e => e.Mannschaft.Id == polen);
        polenEintrag.Punkte.Should().Be(9);
        polenEintrag.Rang.Should().Be(1);
        polenEintrag.Qualifikationsstatus.Should().Be(Qualifikationsstatus.SicherQualifiziert);
        polenEintrag.SicherPositioniert.Should().BeTrue(
            "PSG kann mit maximal 6 Punkten Polens 9 Punkte nicht mehr einholen - unabhängig von der Tordifferenz");

        var anzahl = await dienste.Aufloesung.RundeAufloesenAsync(halbfinale.Id);

        anzahl.Should().Be(1, "Polen ist bereits als Gruppenerster fixiert und sollte übernommen werden");
        ctx.Spiele.First(s => s.Id == 10).HeimMannschaftId.Should().Be(polen, "1. Gruppe A = Polen");
    }

    [Fact]
    public async Task RundeAufloesen_LoestKoSiegerUndVerliererAuf()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: 3);
        ctx.Spiele.Add(new Spiel
        {
            Id = 20, TurnierInstanzId = 1, TurnierRundeId = finaleRundeId, Spielnummer = 20,
            HeimPlatzhalterName = "Sieger HF 1", AuswaertsPlatzhalterName = "Verlierer HF 2",
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        var anzahl = await dienste.Aufloesung.RundeAufloesenAsync(finaleRundeId);

        anzahl.Should().Be(1);
        var spiel = ctx.Spiele.First(s => s.Id == 20);
        spiel.HeimMannschaftId.Should().Be(1);      // Sieger HF 1 = Team A
        spiel.AuswaertsMannschaftId.Should().Be(4); // Verlierer HF 2 = Team D
    }

    // Issue #619: "Sieger Halbfinale 1" meint die erste Paarung im Tableau, auch solange sie noch Platzhalter
    // ist — nicht die erste bereits besetzte.
    [Fact]
    public async Task RundeAufloesen_KoQuellrundeTeilweiseBesetzt_ZaehltPaarungenNachTableauPosition()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        OffenesHalbfinalSpielAnlegen(ctx, 1, hfRundeId);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: 3);
        ctx.Spiele.Add(new Spiel
        {
            Id = 20, TurnierInstanzId = 1, TurnierRundeId = finaleRundeId, Spielnummer = 20,
            HeimPlatzhalterName = "Sieger Halbfinale 1", AuswaertsPlatzhalterName = "Sieger Halbfinale 2",
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        await dienste.Aufloesung.RundeAufloesenAsync(finaleRundeId);

        var spiel = ctx.Spiele.First(s => s.Id == 20);
        spiel.HeimMannschaftId.Should().BeNull("Halbfinale 1 ist noch nicht besetzt");
        spiel.AuswaertsMannschaftId.Should().Be(3, "Sieger Halbfinale 2 = Team C");
    }

    // Issue #466 (WM 1982): Gruppenphase → fest besetzte Zwischenrunde → K.-o. „1. Gruppe A“ meint je Runde die
    // nächstliegende Gruppenphase davor — in der Zwischenrunde die Vorrunde, in der K.-o.-Runde die Zwischenrunde.
    [Fact]
    public async Task RundeAufloesen_ZweiGruppenphasenHintereinander_GruppenrangBeziehtSichAufDieNaechsteDavor()
    {
        await using var ctx = NeuerContext();
        GruppenphaseUndKoSetupAnlegen(ctx, out _, out int zwischenrundeId);
        var zwischenrunde = ctx.TurnierRunden.Single(r => r.Id == zwischenrundeId);
        zwischenrunde.Name = "Zwischenrunde";
        zwischenrunde.Rundenart = Rundenart.Gruppenphase;
        zwischenrunde.GruppenAnzahl = 1;
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 3, TurnierInstanzId = 1, Name = "Finale", Reihenfolge = 3, Rundenart = Rundenart.Finale,
            EingangsMannschaftsanzahl = 2, AusgangsMannschaftsanzahl = 1, RegelwerkId = 1
        });
        ctx.Spiele.Add(new Spiel
        {
            Id = 10, TurnierInstanzId = 1, TurnierRundeId = zwischenrundeId, Spielnummer = 10, GruppenNummer = 1,
            HeimPlatzhalterName = "1. Gruppe A", AuswaertsPlatzhalterName = "1. Gruppe B", Status = SpielStatus.Geplant
        });
        ctx.Spiele.Add(new Spiel
        {
            Id = 20, TurnierInstanzId = 1, TurnierRundeId = 3, Spielnummer = 20,
            HeimPlatzhalterName = "1. Gruppe A", AuswaertsPlatzhalterName = "2. Gruppe A", Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        await dienste.Aufloesung.RundeAufloesenAsync(zwischenrundeId);
        var zwischenrundenSpiel = ctx.Spiele.First(s => s.Id == 10);
        zwischenrundenSpiel.HeimMannschaftId.Should().Be(1);      // Sieger Vorrundengruppe A = Team A
        zwischenrundenSpiel.AuswaertsMannschaftId.Should().Be(3); // Sieger Vorrundengruppe B = Team C

        zwischenrundenSpiel.Status = SpielStatus.Abgeschlossen;
        ctx.SpielErgebnisse.Add(new SpielErgebnis
            { Id = 10, SpielId = 10, HeimToreRegulaer = 0, AuswaertsToreRegulaer = 2, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit });
        ctx.SaveChanges();

        await new SpielplanDienste(ctx).Aufloesung.RundeAufloesenAsync(3);
        var finale = ctx.Spiele.First(s => s.Id == 20);
        finale.HeimMannschaftId.Should().Be(3);      // Sieger der Zwischenrunde, nicht der Vorrunde
        finale.AuswaertsMannschaftId.Should().Be(1);
    }

    /// <summary>Regressionstest für einen Nutzerfund: keine mitgelieferte Turniervorlage benennt ihre K.o.-
    /// Runden mit einer Kurzform ("VF"/"HF"/"AF") — sie heißen immer ausgeschrieben ("Viertelfinale",
    /// "Halbfinale", ...). <see cref="TeilnehmerPoolHilfsmittel"/> (WinUi) bzw. dessen WPF-Pendant erzeugen
    /// Platzhaltertext ausschließlich aus dem ECHTEN Rundennamen ("Sieger Halbfinale 1"), nie aus einer
    /// Kurzform — der obige Test <see cref="PlatzhalterAufloesenAsync_LoestKoSiegerUndVerliererAuf"/> verdeckte
    /// das, weil er den Platzhaltertext von Hand mit der Kurzform "HF" schreibt statt mit dem echten
    /// Rundennamen "Halbfinale" der Testrunde. In der Praxis lösten sich K.o.-Platzhalter dadurch nie auf,
    /// weder einzeln noch simuliert, weder produktiv noch in der Vorschau.</summary>
    [Fact]
    public async Task RundeAufloesen_LoestKoSiegerAufMitAusgeschriebenemRundennamen()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: 3);
        ctx.Spiele.Add(new Spiel
        {
            Id = 20, TurnierInstanzId = 1, TurnierRundeId = finaleRundeId, Spielnummer = 20,
            HeimPlatzhalterName = "Sieger Halbfinale 1", AuswaertsPlatzhalterName = "Verlierer Halbfinale 2",
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        var anzahl = await dienste.Aufloesung.RundeAufloesenAsync(finaleRundeId);

        anzahl.Should().Be(1);
        var spiel = ctx.Spiele.First(s => s.Id == 20);
        spiel.HeimMannschaftId.Should().Be(1);      // Sieger Halbfinale 1 = Team A
        spiel.AuswaertsMannschaftId.Should().Be(4); // Verlierer Halbfinale 2 = Team D
    }

    /// <summary>Regressionstest Issue #368: Rundennamen mit Leerzeichen/Bindestrich ("Playoff-Runde" der
    /// UEFA-Wettbewerbe, "1. Runde" im DFB-Pokal) scheiterten an der K.o.-Platzhalter-Regex — die Sieger
    /// wurden nie in die Folgerunde übernommen.</summary>
    [Theory]
    [InlineData("Playoff-Runde")]
    [InlineData("1. Runde")]
    public async Task RundeAufloesen_LoestKoSiegerAufBeiRundennamenMitLeerzeichenOderBindestrich(string rundenName)
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId, hfName: rundenName);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: 3);
        ctx.Spiele.Add(new Spiel
        {
            Id = 20, TurnierInstanzId = 1, TurnierRundeId = finaleRundeId, Spielnummer = 20,
            HeimPlatzhalterName = $"Sieger {rundenName} 1", AuswaertsPlatzhalterName = $"Verlierer {rundenName} 2",
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        var anzahl = await dienste.Aufloesung.RundeAufloesenAsync(finaleRundeId);

        anzahl.Should().Be(1);
        var spiel = ctx.Spiele.First(s => s.Id == 20);
        spiel.HeimMannschaftId.Should().Be(1);
        spiel.AuswaertsMannschaftId.Should().Be(4);
    }

    // ── Tests: K.o.-Quellverknüpfung (Issue #241) ──────────────────────────

    [Fact]
    public async Task RundeAufloesen_NutztIdFastpathOhneRegex_WennQuellIdBereitsGesetzt()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: 3);
        // Kein (bzw. ein zur Regex nicht passender) Platzhaltertext — die ID-Felder sind die einzige
        // Quelle, aus der aufgelöst werden kann.
        ctx.Spiele.Add(new Spiel
        {
            Id = 20, TurnierInstanzId = 1, TurnierRundeId = finaleRundeId, Spielnummer = 20,
            HeimQuellSpielId = 1, HeimQuellErgebnis = SpielQuellErgebnisTyp.Sieger,
            AuswaertsQuellSpielId = 2, AuswaertsQuellErgebnis = SpielQuellErgebnisTyp.Verlierer,
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        var anzahl = await dienste.Aufloesung.RundeAufloesenAsync(finaleRundeId);

        anzahl.Should().Be(1);
        var spiel = ctx.Spiele.First(s => s.Id == 20);
        spiel.HeimMannschaftId.Should().Be(1);      // Sieger HF 1 = Team A
        spiel.AuswaertsMannschaftId.Should().Be(4); // Verlierer HF 2 = Team D
    }

    // ── Tests: regelbasierte Auflösung über Uebergangsregel (Issue #130) ─

    [Fact]
    public async Task UebergangsregelQualifikanten_GruppenphaseQuelle_LoestTabellenraengeAuf()
    {
        await using var ctx = NeuerContext();
        GruppenphaseUndKoSetupAnlegen(ctx, out int gruppenphaseRundeId, out int koRundeId);
        var gruppenphase = ctx.TurnierRunden.First(r => r.Id == gruppenphaseRundeId);
        var regel = new Uebergangsregel
        {
            QuellRundeId = gruppenphaseRundeId, QuellRunde = gruppenphase, ZielRundeId = koRundeId,
            GruppeIndex = 1, PlatzierungVon = 1, PlatzierungBis = 2
        };
        var dienste = new SpielplanDienste(ctx);

        var qualifikanten = await dienste.Aufloesung.UebergangsregelQualifikantenAsync(regel);

        qualifikanten[1].Should().Be(1); // Platz 1 Gruppe A = Team A
        qualifikanten[2].Should().Be(2); // Platz 2 Gruppe A = Team B
    }

    [Fact]
    public async Task UebergangsregelPlatzAufloesen_LigaQuelle_LoestTabellenrangAuf()
    {
        await using var ctx = NeuerContext();
        LigaGrundSetupAnlegen(ctx, out int ligaRundeId);
        ctx.Spiele.Add(new Spiel
        {
            Id = 1, TurnierInstanzId = 1, TurnierRundeId = ligaRundeId, Spielnummer = 1,
            HeimMannschaftId = 1, AuswaertsMannschaftId = 2, Status = SpielStatus.Abgeschlossen
        });
        ctx.SpielErgebnisse.Add(new SpielErgebnis
            { Id = 1, SpielId = 1, HeimToreRegulaer = 3, AuswaertsToreRegulaer = 0, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit });
        ctx.Spiele.Add(new Spiel
        {
            Id = 2, TurnierInstanzId = 1, TurnierRundeId = ligaRundeId, Spielnummer = 2,
            HeimMannschaftId = 3, AuswaertsMannschaftId = 4, Status = SpielStatus.Abgeschlossen
        });
        ctx.SpielErgebnisse.Add(new SpielErgebnis
            { Id = 2, SpielId = 2, HeimToreRegulaer = 1, AuswaertsToreRegulaer = 0, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit });
        ctx.SaveChanges();

        var ligaRunde = ctx.TurnierRunden.First(r => r.Id == ligaRundeId);
        var regel = new Uebergangsregel
        {
            QuellRundeId = ligaRundeId, QuellRunde = ligaRunde, ZielRundeId = 999,
            GruppeIndex = null, PlatzierungVon = 1, PlatzierungBis = 1
        };
        var dienste = new SpielplanDienste(ctx);

        var platz1 = await dienste.Aufloesung.UebergangsregelPlatzAufloesenAsync(regel, 1);

        platz1.Should().Be(1); // Team A: 3 Punkte, Tordifferenz +3 → Rang 1
    }

    [Fact]
    public async Task UebergangsregelPlatzAufloesen_SchweizerSystemQuelle_LoestTabellenrangAuf()
    {
        // Issue #25-Nachlieferung: Ligaphase im CL-2024+-Format nutzt SchweizerSystem statt Liga
        // (Teilrunde, nicht volle Punktrunde) — Rangauflösung muss denselben Tabellenweg gehen wie Liga.
        await using var ctx = NeuerContext();
        SchweizerSystemGrundSetupAnlegen(ctx, out int ligaphaseRundeId);
        ctx.Spiele.Add(new Spiel
        {
            Id = 1, TurnierInstanzId = 1, TurnierRundeId = ligaphaseRundeId, Spielnummer = 1, Spieltag = 1,
            HeimMannschaftId = 1, AuswaertsMannschaftId = 2, Status = SpielStatus.Abgeschlossen
        });
        ctx.SpielErgebnisse.Add(new SpielErgebnis
            { Id = 1, SpielId = 1, HeimToreRegulaer = 3, AuswaertsToreRegulaer = 0, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit });
        ctx.Spiele.Add(new Spiel
        {
            Id = 2, TurnierInstanzId = 1, TurnierRundeId = ligaphaseRundeId, Spielnummer = 2, Spieltag = 1,
            HeimMannschaftId = 3, AuswaertsMannschaftId = 4, Status = SpielStatus.Abgeschlossen
        });
        ctx.SpielErgebnisse.Add(new SpielErgebnis
            { Id = 2, SpielId = 2, HeimToreRegulaer = 1, AuswaertsToreRegulaer = 0, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit });
        ctx.SaveChanges();

        var ligaphaseRunde = ctx.TurnierRunden.First(r => r.Id == ligaphaseRundeId);
        // Issue #343: fest aufgelöst wird nur ein gesicherter Rang — mit nur einem Spieltag ist die Runde komplett.
        ligaphaseRunde.KonfigurationJson = "{\"anzahlSpieltage\":1}";
        ctx.SaveChanges();
        var regel = new Uebergangsregel
        {
            QuellRundeId = ligaphaseRundeId, QuellRunde = ligaphaseRunde, ZielRundeId = 999,
            GruppeIndex = null, PlatzierungVon = 1, PlatzierungBis = 1
        };
        var dienste = new SpielplanDienste(ctx);

        var platz1 = await dienste.Aufloesung.UebergangsregelPlatzAufloesenAsync(regel, 1);

        platz1.Should().Be(1); // Team A: 3 Punkte, Tordifferenz +3 → Rang 1
    }

    // ── Tests: Schweizer-Rundenfolge über mehrere Spieltage (Issue #329 Teil E) ─

    [Fact]
    public async Task UebergangsregelPlatzAufloesen_KoRundeQuelle_LoestSiegerUndVerliererAuf()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        var hfRunde = ctx.TurnierRunden.First(r => r.Id == hfRundeId);
        var regelSieger = new Uebergangsregel
        {
            QuellRundeId = hfRundeId, QuellRunde = hfRunde, ZielRundeId = finaleRundeId,
            GruppeIndex = 1, PlatzierungVon = 1, PlatzierungBis = 1
        };
        var regelVerlierer = new Uebergangsregel
        {
            QuellRundeId = hfRundeId, QuellRunde = hfRunde, ZielRundeId = finaleRundeId,
            GruppeIndex = 1, PlatzierungVon = 2, PlatzierungBis = 2
        };
        var dienste = new SpielplanDienste(ctx);

        var sieger = await dienste.Aufloesung.UebergangsregelPlatzAufloesenAsync(regelSieger, 1);
        var verlierer = await dienste.Aufloesung.UebergangsregelPlatzAufloesenAsync(regelVerlierer, 2);

        sieger.Should().Be(1);
        verlierer.Should().Be(2);
    }

    [Fact]
    public async Task UebergangsregelPlatzAufloesen_NochNichtEntschieden_LiefertNull()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: null);
        var hfRunde = ctx.TurnierRunden.First(r => r.Id == hfRundeId);
        var regel = new Uebergangsregel
        {
            QuellRundeId = hfRundeId, QuellRunde = hfRunde, ZielRundeId = finaleRundeId,
            GruppeIndex = 1, PlatzierungVon = 1, PlatzierungBis = 1
        };
        var dienste = new SpielplanDienste(ctx);

        var sieger = await dienste.Aufloesung.UebergangsregelPlatzAufloesenAsync(regel, 1);

        sieger.Should().BeNull();
    }

    // ── Tests: gruppenübergreifende Platzhalter-Auflösung in der Auslosung (Issue #213) ─

    [Fact]
    public async Task RundeAufloesen_LoestBestenDrittenUeberMehrereGruppenAuf()
    {
        await using var ctx = NeuerContext();
        DreiGruppenUndZielrundeSetupAnlegen(ctx, out int gruppenphaseId, out int zielRundeId);
        AnzahlBesteSpieleHinzufuegen(ctx, gruppenphaseId);
        ctx.Spiele.Add(new Spiel
        {
            Id = 10, TurnierInstanzId = 1, TurnierRundeId = zielRundeId, Spielnummer = 10,
            HeimPlatzhalterName = "1. bester 3. Platz", AuswaertsPlatzhalterName = "2. bester 3. Platz",
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        var anzahl = await dienste.Aufloesung.RundeAufloesenAsync(zielRundeId);

        anzahl.Should().Be(1);
        var spiel = ctx.Spiele.First(s => s.Id == 10);
        spiel.HeimMannschaftId.Should().Be(8, "Team 8 (GD -1) ist der beste Gruppendritte");
        spiel.AuswaertsMannschaftId.Should().Be(5, "Team 5 (GD -2) ist der zweitbeste Gruppendritte");
    }

    [Fact]
    public async Task RundeAufloesen_BestePlatzierungBleibtOffenSolangeGruppenphaseNichtVollstaendigIst()
    {
        await using var ctx = NeuerContext();
        DreiGruppenUndZielrundeSetupAnlegen(ctx, out int gruppenphaseId, out int zielRundeId);
        AnzahlBesteSpieleHinzufuegen(ctx, gruppenphaseId);
        ctx.SaveChanges();
        // Spiel 6 (Gruppe 3) wieder auf offen setzen - alle Gruppen müssen fertig sein (konservatives Gate).
        var spiel6 = ctx.Spiele.First(s => s.Id == 6);
        spiel6.Status = SpielStatus.Geplant;
        ctx.SpielErgebnisse.Remove(ctx.SpielErgebnisse.First(e => e.SpielId == 6));
        ctx.Spiele.Add(new Spiel
        {
            Id = 10, TurnierInstanzId = 1, TurnierRundeId = zielRundeId, Spielnummer = 10,
            HeimPlatzhalterName = "1. bester 3. Platz", AuswaertsPlatzhalterName = "2. bester 3. Platz",
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        var anzahl = await dienste.Aufloesung.RundeAufloesenAsync(zielRundeId);

        anzahl.Should().Be(0);
        var spiel = ctx.Spiele.First(s => s.Id == 10);
        spiel.HeimMannschaftId.Should().BeNull();
        spiel.AuswaertsMannschaftId.Should().BeNull();
    }

    // Issue #619: Gruppe 2 ist fertig gespielt, Gruppe 1 wartet noch auf zwei Quali-Sieger (#395) — ihre
    // Spiele sind unbesetzt, nicht abgeschlossen. Der beste Gruppensieger steht damit noch nicht fest.
    [Fact]
    public async Task RundeAufloesen_BestePlatzierungBleibtOffenSolangeGruppeNochPlatzhalterHat()
    {
        await using var ctx = NeuerContext();
        await GruppeZweiFertigGruppeEinsMitPlatzhalternAnlegen(ctx);
        ctx.Spiele.Add(new Spiel
        {
            Id = 100, TurnierInstanzId = 1, TurnierRundeId = 3, Spielnummer = 100,
            HeimPlatzhalterName = "1. bester 1. Platz", AuswaertsPlatzhalterName = "2. bester 1. Platz",
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();

        await new SpielplanDienste(ctx).Aufloesung.RundeAufloesenAsync(3);

        var spiel = ctx.Spiele.First(s => s.Id == 100);
        spiel.HeimMannschaftId.Should().BeNull();
        spiel.AuswaertsMannschaftId.Should().BeNull();
    }

    [Fact]
    public async Task UebergangsregelQualifikanten_AnzahlBeste_BleibtOffenSolangeGruppeNochPlatzhalterHat()
    {
        await using var ctx = NeuerContext();
        int gruppenphaseId = await GruppeZweiFertigGruppeEinsMitPlatzhalternAnlegen(ctx);
        var regel = new Uebergangsregel
        {
            QuellRundeId = gruppenphaseId, QuellRunde = ctx.TurnierRunden.First(r => r.Id == gruppenphaseId),
            ZielRundeId = 3, PlatzierungVon = 1, PlatzierungBis = 1, AnzahlBeste = 1
        };

        var qualifikanten = await new SpielplanDienste(ctx).Aufloesung.UebergangsregelQualifikantenAsync(regel);

        qualifikanten.Should().Equal(new Dictionary<int, int?> { [1] = null });
    }

    /// <summary><see cref="TeilaufgeloesteGruppenAnlegen"/> mit fertig gespielter Gruppe 2 (Heimteam gewinnt
    /// 1:0) und einer Zielrunde 3 dahinter.</summary>
    private static async Task<int> GruppeZweiFertigGruppeEinsMitPlatzhalternAnlegen(TurnierverwaltungDbContext ctx)
    {
        int gruppenphaseId = await TeilaufgeloesteGruppenAnlegen(ctx);
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 3, TurnierInstanzId = 1, Name = "Finale", Reihenfolge = 3, Rundenart = Rundenart.Finale,
            EingangsMannschaftsanzahl = 2, AusgangsMannschaftsanzahl = 1, RegelwerkId = 1
        });
        var besetzte = ctx.Spiele.Where(s => s.TurnierRundeId == gruppenphaseId
            && s.HeimMannschaftId != null && s.AuswaertsMannschaftId != null).ToList();
        besetzte.Should().HaveCount(3, "Gruppe 1 hat kein besetztes Spiel, Gruppe 2 drei");
        foreach (var spiel in besetzte)
        {
            spiel.Status = SpielStatus.Abgeschlossen;
            ctx.SpielErgebnisse.Add(new SpielErgebnis
            {
                SpielId = spiel.Id, HeimToreRegulaer = 1, AuswaertsToreRegulaer = 0,
                EntscheidungArt = EntscheidungArt.RegulaereSpielzeit, SiegerMannschaftId = spiel.HeimMannschaftId
            });
        }
        await ctx.SaveChangesAsync();
        return gruppenphaseId;
    }

    [Fact]
    public async Task RundeAufloesen_BestePlatzierungLoestNurAufloesbareSeiteAufWennAngeforderterRangNichtExistiert()
    {
        await using var ctx = NeuerContext();
        DreiGruppenUndZielrundeSetupAnlegen(ctx, out int gruppenphaseId, out int zielRundeId);
        AnzahlBesteSpieleHinzufuegen(ctx, gruppenphaseId);
        ctx.Spiele.Add(new Spiel
        {
            // Nur 3 Gruppendritte existieren (3 Gruppen) - Rang 5 ist nicht auflösbar.
            Id = 10, TurnierInstanzId = 1, TurnierRundeId = zielRundeId, Spielnummer = 10,
            HeimPlatzhalterName = "1. bester 3. Platz", AuswaertsPlatzhalterName = "5. bester 3. Platz",
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        var anzahl = await dienste.Aufloesung.RundeAufloesenAsync(zielRundeId);

        anzahl.Should().Be(1, "die Heim-Seite ist auflösbar, unabhängig von der nicht auflösbaren Auswärts-Seite (Issue #206)");
        var spiel = ctx.Spiele.First(s => s.Id == 10);
        spiel.HeimMannschaftId.Should().Be(8, "Team 8 (GD -1) ist der beste Gruppendritte");
        spiel.AuswaertsMannschaftId.Should().BeNull("Rang 5 existiert bei nur 3 Gruppen nicht");
    }

    [Fact]
    public async Task RundeAufloesen_BestePlatzierungUndGruppensiegerAufDenBeidenSpielseitenUnabhaengigVoneinanderAufloesbar()
    {
        await using var ctx = NeuerContext();
        DreiGruppenUndZielrundeSetupAnlegen(ctx, out int gruppenphaseId, out int zielRundeId);
        AnzahlBesteSpieleHinzufuegen(ctx, gruppenphaseId);
        ctx.Spiele.Add(new Spiel
        {
            Id = 10, TurnierInstanzId = 1, TurnierRundeId = zielRundeId, Spielnummer = 10,
            HeimPlatzhalterName = "Sieger Gruppe 1", AuswaertsPlatzhalterName = "1. bester 3. Platz",
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        var anzahl = await dienste.Aufloesung.RundeAufloesenAsync(zielRundeId);

        anzahl.Should().Be(1);
        var spiel = ctx.Spiele.First(s => s.Id == 10);
        spiel.HeimMannschaftId.Should().Be(1, "Team 1 gewinnt Gruppe 1");
        spiel.AuswaertsMannschaftId.Should().Be(8, "Team 8 (GD -1) ist der beste Gruppendritte");
    }

    // ── Tests: Gruppendritte per Kombinationstabelle (Issue #372) ──────────────

    [Fact]
    public async Task RundeAufloesen_LoestGruppendrittenPerKombinationstabelleAuf()
    {
        await using var ctx = NeuerContext();
        DreiGruppenUndZielrundeSetupAnlegen(ctx, out int gruppenphaseId, out int zielRundeId);
        AnzahlBesteSpieleHinzufuegen(ctx, gruppenphaseId);
        KombinationstabelleSetzen(ctx, zielRundeId);
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        var anzahl = await dienste.Aufloesung.RundeAufloesenAsync(zielRundeId);

        anzahl.Should().Be(1);
        var spiel = ctx.Spiele.First(s => s.Id == 10);
        spiel.HeimMannschaftId.Should().Be(5, "Zeile BC ordnet '3. Gruppe A/B' dem Dritten aus Gruppe B zu");
        spiel.AuswaertsMannschaftId.Should().Be(8, "Zeile BC ordnet '3. Gruppe B/C' dem Dritten aus Gruppe C zu");
    }

    [Fact]
    public async Task RundeAufloesen_KombinationstabelleBleibtOffenSolangeGruppenphaseNichtVollstaendigIst()
    {
        await using var ctx = NeuerContext();
        DreiGruppenUndZielrundeSetupAnlegen(ctx, out int gruppenphaseId, out int zielRundeId);
        AnzahlBesteSpieleHinzufuegen(ctx, gruppenphaseId);
        KombinationstabelleSetzen(ctx, zielRundeId);
        ctx.SaveChanges();
        var spiel6 = ctx.Spiele.First(s => s.Id == 6);
        spiel6.Status = SpielStatus.Geplant;
        ctx.SpielErgebnisse.Remove(ctx.SpielErgebnisse.First(e => e.SpielId == 6));
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        var anzahl = await dienste.Aufloesung.RundeAufloesenAsync(zielRundeId);

        anzahl.Should().Be(0);
        var spiel = ctx.Spiele.First(s => s.Id == 10);
        spiel.HeimMannschaftId.Should().BeNull();
        spiel.AuswaertsMannschaftId.Should().BeNull();
    }

    [Fact]
    public async Task RundeAufloesen_KombinationsplatzOhneTabelleBleibtOffen()
    {
        await using var ctx = NeuerContext();
        DreiGruppenUndZielrundeSetupAnlegen(ctx, out int gruppenphaseId, out int zielRundeId);
        AnzahlBesteSpieleHinzufuegen(ctx, gruppenphaseId);
        KombinationstabelleSetzen(ctx, zielRundeId);
        ctx.TurnierRunden.First(r => r.Id == zielRundeId).KombinationstabelleJson = null;
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        var anzahl = await dienste.Aufloesung.RundeAufloesenAsync(zielRundeId);

        anzahl.Should().Be(0);
    }

    // ── Tests: Liga-Platzhalter "Platz N (Runde)" mit Sicherheits-Gate (Issue #343) ─

    [Theory]
    [InlineData(Rundenart.Liga)]
    [InlineData(Rundenart.TopfLiga)]
    public async Task RundeAufloesen_LigaKomplettGespielt_LoestPlatzNAufTabellenrangAuf(Rundenart ligaRundenart)
    {
        await using var ctx = NeuerContext();
        LigaMitFolgeKoRundeAnlegen(ctx, out _, out int koRundeId, ligaRundenart);
        ZweiSpieltageMitUneinholbaremTabellenfuehrer(ctx);
        LetztenSpieltagSpielen(ctx);
        var dienste = new SpielplanDienste(ctx);

        await dienste.Aufloesung.RundeAufloesenAsync(koRundeId);

        var spiele = ctx.Spiele.Where(s => s.TurnierRundeId == koRundeId).OrderBy(s => s.Id).ToList();
        (spiele[0].HeimMannschaftId, spiele[0].AuswaertsMannschaftId).Should().Be((1, 3), "Platz 1 = A, Platz 4 = C");
        (spiele[1].HeimMannschaftId, spiele[1].AuswaertsMannschaftId).Should().Be((2, 4), "Platz 2 = B, Platz 3 = D");
    }

    [Fact]
    public async Task RundeAufloesen_LigaVorLetztemSpieltag_LoestNurUneinholbarenRangAuf()
    {
        await using var ctx = NeuerContext();
        LigaMitFolgeKoRundeAnlegen(ctx, out _, out int koRundeId);
        ZweiSpieltageMitUneinholbaremTabellenfuehrer(ctx);
        var dienste = new SpielplanDienste(ctx);

        await dienste.Aufloesung.RundeAufloesenAsync(koRundeId);

        var spiele = ctx.Spiele.Where(s => s.TurnierRundeId == koRundeId).OrderBy(s => s.Id).ToList();
        spiele[0].HeimMannschaftId.Should().Be(1, "A kann mit 6 Punkten von niemandem mehr eingeholt werden");
        spiele[0].AuswaertsMannschaftId.Should().BeNull("Platz 4 hängt noch vom letzten Spieltag ab");
        spiele[1].HeimMannschaftId.Should().BeNull();
        spiele[1].AuswaertsMannschaftId.Should().BeNull();
    }

    [Fact]
    public async Task RundeAufloesen_LigaNachErstemSpieltag_LoestNichtsAufAberVorschauZeigtStand()
    {
        await using var ctx = NeuerContext();
        LigaMitFolgeKoRundeAnlegen(ctx, out _, out int koRundeId);
        LigaErgebnisSetzen(ctx, 1, 2, 0); // A–B
        LigaErgebnisSetzen(ctx, 2, 1, 0); // C–D
        var dienste = new SpielplanDienste(ctx);

        var aufgeloest = await dienste.Aufloesung.RundeAufloesenAsync(koRundeId);
        var vorschau = await dienste.Aufloesung.VorschauAsync(koRundeId);

        aufgeloest.Should().Be(0, "nach einem Spieltag ist noch kein Tabellenplatz gesichert");
        ctx.Spiele.Where(s => s.TurnierRundeId == koRundeId)
            .Should().OnlyContain(s => s.HeimMannschaftId == null && s.AuswaertsMannschaftId == null);
        vorschau.Should().HaveCount(2, "die optimistische Vorschau zeigt den aktuellen Tabellenstand");
        vorschau[101].Heim.Should().NotBeNull();
    }

    [Fact]
    public async Task RundeAufloesen_SchweizerSystemOhneSpieltagAnzahl_LoestNichtFestAuf()
    {
        await using var ctx = NeuerContext();
        LigaMitFolgeKoRundeAnlegen(ctx, out _, out int koRundeId, Rundenart.SchweizerSystem);
        ZweiSpieltageMitUneinholbaremTabellenfuehrer(ctx);
        LetztenSpieltagSpielen(ctx);
        var dienste = new SpielplanDienste(ctx);

        var aufgeloest = await dienste.Aufloesung.RundeAufloesenAsync(koRundeId);

        aufgeloest.Should().Be(0,
            "ohne AnzahlSpieltage ist unbekannt, ob noch Schweizer Spieltage folgen — kein Rang gilt als gesichert");
    }

    [Fact]
    public async Task RundeAufloesen_SchweizerSystemAlleSpieltageGespielt_LoestAllesAuf()
    {
        await using var ctx = NeuerContext();
        LigaMitFolgeKoRundeAnlegen(ctx, out _, out int koRundeId, Rundenart.SchweizerSystem);
        ctx.TurnierRunden.Single(r => r.Id == 1).KonfigurationJson = "{\"anzahlSpieltage\":3}";
        ctx.SaveChanges();
        ZweiSpieltageMitUneinholbaremTabellenfuehrer(ctx);
        LetztenSpieltagSpielen(ctx);
        var dienste = new SpielplanDienste(ctx);

        await dienste.Aufloesung.RundeAufloesenAsync(koRundeId);

        ctx.Spiele.Where(s => s.TurnierRundeId == koRundeId)
            .Should().OnlyContain(s => s.HeimMannschaftId != null && s.AuswaertsMannschaftId != null);
    }

    [Fact]
    public async Task RundeAufloesen_SchweizerSystemNochNichtGenerierterSpieltag_ZaehltAlsOffen()
    {
        await using var ctx = NeuerContext();
        LigaMitFolgeKoRundeAnlegen(ctx, out _, out int koRundeId, Rundenart.SchweizerSystem);
        ctx.TurnierRunden.Single(r => r.Id == 1).KonfigurationJson = "{\"anzahlSpieltage\":4}";
        ctx.SaveChanges();
        ZweiSpieltageMitUneinholbaremTabellenfuehrer(ctx);
        LetztenSpieltagSpielen(ctx);
        var dienste = new SpielplanDienste(ctx);

        await dienste.Aufloesung.RundeAufloesenAsync(koRundeId);

        // Spieltag 4 ist noch nicht generiert: A (9, max. 12) bleibt uneinholbar vor B (max. 7), alle anderen
        // Plätze sind noch offen.
        var spiele = ctx.Spiele.Where(s => s.TurnierRundeId == koRundeId).OrderBy(s => s.Id).ToList();
        spiele[0].HeimMannschaftId.Should().Be(1);
        spiele[0].AuswaertsMannschaftId.Should().BeNull();
        spiele[1].HeimMannschaftId.Should().BeNull();
        spiele[1].AuswaertsMannschaftId.Should().BeNull();
    }

    // ── Tests: K.o.-Runden mit Hin-/Rückspiel und Platzhaltern (Issue #346) ─

    [Fact]
    public async Task RundeAufloesen_KoSiegerAusHinUndRueckspiel_NimmtGesamtsiegerJePaarung()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId, hfMitRueckspiel: true);
        HalbfinaleMitRueckspielenAnlegen(ctx, hfRundeId);
        ctx.Spiele.Add(new Spiel
        {
            Id = 20, TurnierInstanzId = 1, TurnierRundeId = finaleRundeId, Spielnummer = 20,
            HeimPlatzhalterName = "Sieger Halbfinale 1", AuswaertsPlatzhalterName = "Sieger Halbfinale 2",
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        await dienste.Aufloesung.RundeAufloesenAsync(finaleRundeId);

        var finale = ctx.Spiele.First(s => s.Id == 20);
        // Früher: "Sieger Halbfinale 1" = Hinspiel-Sieger B, "Sieger Halbfinale 2" = Rückspiel der 1. Paarung.
        finale.HeimMannschaftId.Should().Be(1, "Team A gewinnt Paarung 1 über beide Spiele");
        finale.AuswaertsMannschaftId.Should().Be(4, "Team D gewinnt Paarung 2");
    }
}
