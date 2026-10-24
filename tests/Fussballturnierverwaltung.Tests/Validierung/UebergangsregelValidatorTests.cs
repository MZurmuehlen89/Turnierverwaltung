using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Validierung;

public class UebergangsregelValidatorTests
{
    private readonly UebergangsregelValidator _validator = new();

    private static readonly IReadOnlyList<Uebergangsregel> _keineRegeln = [];

    private static TurnierRunde Gruppenphase(int? gruppenAnzahl = 2, int eingangsMannschaftsanzahl = 8) => new()
    {
        Id = 1, Name = "Gruppenphase", Rundenart = Rundenart.Gruppenphase,
        EingangsMannschaftsanzahl = eingangsMannschaftsanzahl, GruppenAnzahl = gruppenAnzahl
    };

    private static TurnierRunde Liga(int eingangsMannschaftsanzahl = 36) => new()
    {
        Id = 1, Name = "Liga-Phase", Rundenart = Rundenart.Liga, EingangsMannschaftsanzahl = eingangsMannschaftsanzahl
    };

    private static TurnierRunde SchweizerSystem(int eingangsMannschaftsanzahl = 36) => new()
    {
        Id = 1, Name = "Ligaphase", Rundenart = Rundenart.SchweizerSystem, EingangsMannschaftsanzahl = eingangsMannschaftsanzahl
    };

    private static TurnierRunde KoRunde(int eingangsMannschaftsanzahl = 16) => new()
    {
        Id = 1, Name = "Sechzehntelfinale", Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = eingangsMannschaftsanzahl
    };

    private static TurnierRunde Ziel(int id = 2, int eingangsMannschaftsanzahl = 2) =>
        new() { Id = id, Name = $"Zielrunde {id}", Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = eingangsMannschaftsanzahl };

    [Fact]
    public void GueltigeGruppenphaseRegel_IstGueltig()
    {
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 2);
        _validator.Validieren(eingabe, Gruppenphase(gruppenAnzahl: 2, eingangsMannschaftsanzahl: 8), Ziel(), _keineRegeln).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void GueltigeLigaRegel_IstGueltig()
    {
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 8);
        _validator.Validieren(eingabe, Liga(eingangsMannschaftsanzahl: 36), Ziel(eingangsMannschaftsanzahl: 8), _keineRegeln).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void GueltigeSchweizerSystemRegel_IstGueltig()
    {
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 8);
        _validator.Validieren(eingabe, SchweizerSystem(eingangsMannschaftsanzahl: 36), Ziel(eingangsMannschaftsanzahl: 8), _keineRegeln).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void GueltigeTopfLigaRegel_IstGueltig()
    {
        // Issue #343: TopfLiga (UEFA-Swiss-Modell) war als Quellrunde fälschlich "nicht unterstützt".
        var topfLiga = new TurnierRunde { Id = 1, Name = "Ligaphase", Rundenart = Rundenart.TopfLiga, EingangsMannschaftsanzahl = 36 };
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: null, PlatzierungVon: 9, PlatzierungBis: 24);
        _validator.Validieren(eingabe, topfLiga, Ziel(eingangsMannschaftsanzahl: 16), _keineRegeln).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void GueltigeKoRundeRegel_IstGueltig()
    {
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1);
        _validator.Validieren(eingabe, KoRunde(eingangsMannschaftsanzahl: 16), Ziel(), _keineRegeln).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void QuellUndZielrundeIdentisch_IstUngueltig()
    {
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 1, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1);
        _validator.Validieren(eingabe, Gruppenphase(), Ziel(id: 1), _keineRegeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void PlatzierungVonKleinerEins_IstUngueltig()
    {
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: 1, PlatzierungVon: 0, PlatzierungBis: 1);
        _validator.Validieren(eingabe, Gruppenphase(), Ziel(), _keineRegeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void PlatzierungBisKleinerVon_IstUngueltig()
    {
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: 1, PlatzierungVon: 3, PlatzierungBis: 2);
        _validator.Validieren(eingabe, Gruppenphase(), Ziel(), _keineRegeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void GruppenphaseOhneGruppenIndex_IstUngueltig()
    {
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 1);
        _validator.Validieren(eingabe, Gruppenphase(), Ziel(), _keineRegeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void GruppenphaseGruppenIndexAusserhalbBereich_IstUngueltig()
    {
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: 5, PlatzierungVon: 1, PlatzierungBis: 1);
        _validator.Validieren(eingabe, Gruppenphase(gruppenAnzahl: 2), Ziel(), _keineRegeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void GruppenphasePlatzierungGroesserGruppengroesse_IstUngueltig()
    {
        // 8 Teams / 2 Gruppen = 4 pro Gruppe, Platzierung 5 gibt es nicht.
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 5);
        _validator.Validieren(eingabe, Gruppenphase(gruppenAnzahl: 2, eingangsMannschaftsanzahl: 8), Ziel(), _keineRegeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void LigaMitGruppenIndex_IstUngueltig()
    {
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1);
        _validator.Validieren(eingabe, Liga(), Ziel(), _keineRegeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void LigaPlatzierungGroesserTeilnehmerzahl_IstUngueltig()
    {
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 40);
        _validator.Validieren(eingabe, Liga(eingangsMannschaftsanzahl: 36), Ziel(), _keineRegeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void KoRundeOhneSpielnummer_IstUngueltig()
    {
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 1);
        _validator.Validieren(eingabe, KoRunde(), Ziel(), _keineRegeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void KoRundePlatzierungGroesserZwei_IstUngueltig()
    {
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 3);
        _validator.Validieren(eingabe, KoRunde(), Ziel(), _keineRegeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void SchweizerSystemAlsQuelleMitGruppenIndex_IstUngueltig()
    {
        // War früher pauschal ungültig (SchweizerSystem als Quellrunde unterstützt) - seit
        // Issue #25-Nachlieferung erlaubt, sofern kein Gruppenindex angegeben wird (analog Liga).
        var quelle = new TurnierRunde { Id = 1, Name = "Schweiz", Rundenart = Rundenart.SchweizerSystem, EingangsMannschaftsanzahl = 8 };
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1);
        _validator.Validieren(eingabe, quelle, Ziel(), _keineRegeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void NeueRegelWuerdeZyklusSchliessen_IstUngueltig()
    {
        // Bestehende Regel A(1) -> B(2). Neue Regel B(2) -> A(1) würde einen Zyklus schließen.
        var bestehende = new Uebergangsregel { Id = 100, QuellRundeId = 1, ZielRundeId = 2, PlatzierungVon = 1, PlatzierungBis = 1 };
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 2, ZielRundeId: 1, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1);

        var ergebnis = _validator.Validieren(eingabe, KoRunde(), Ziel(id: 1), [bestehende]);

        ergebnis.IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void BearbeitenDerselbenRegel_LoestKeinenSelbstZyklusAus()
    {
        // Bestehende Regel A(1) -> B(2) wird bearbeitet (gleiche Werte) - darf sich nicht selbst als Zyklus melden.
        var bestehende = new Uebergangsregel { Id = 100, QuellRundeId = 1, ZielRundeId = 2, PlatzierungVon = 1, PlatzierungBis = 1 };
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 2);

        var ergebnis = _validator.Validieren(eingabe, KoRunde(), Ziel(), [bestehende], bearbeiteteRegelId: 100);

        ergebnis.IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void SummeDerPlatzierungenUebersteigtEingangsMannschaftsanzahl_IstUngueltig()
    {
        // Zielrunde erwartet 3 Mannschaften. Bestehende Regel liefert bereits 2 (Platz 1-2), die neue
        // Regel würde weitere 2 (Platz 1-2) aus einer anderen Quellrunde hinzufügen -> Summe 4 > 3.
        var bestehende = new Uebergangsregel { Id = 100, QuellRundeId = 1, ZielRundeId = 2, PlatzierungVon = 1, PlatzierungBis = 2 };
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 3, ZielRundeId: 2, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 2);

        var ergebnis = _validator.Validieren(eingabe, Liga(eingangsMannschaftsanzahl: 36), Ziel(eingangsMannschaftsanzahl: 3), [bestehende]);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*4*3*");
    }

    [Fact]
    public void SummeDerPlatzierungenPasstExaktZusammenfuehrungAusZweiQuellrunden_IstGueltig()
    {
        // Zielrunde erwartet 4 Mannschaften, gespeist aus zwei unterschiedlichen Quellrunden (je 2 Plätze) —
        // der Zusammenführungs-Fall (z.B. Champions-League-Achtelfinale aus Ligaphase + Playoffs).
        var bestehende = new Uebergangsregel { Id = 100, QuellRundeId = 1, ZielRundeId = 2, PlatzierungVon = 1, PlatzierungBis = 2 };
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 3, ZielRundeId: 2, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 2);

        var ergebnis = _validator.Validieren(eingabe, Liga(eingangsMannschaftsanzahl: 36), Ziel(eingangsMannschaftsanzahl: 4), [bestehende]);

        ergebnis.IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void SummeBerechnungIgnoriertBearbeiteteRegelSelbst()
    {
        // Beim Bearbeiten der bestehenden Regel darf ihr eigener alter Wert nicht doppelt gezählt werden.
        var bestehende = new Uebergangsregel { Id = 100, QuellRundeId = 1, ZielRundeId = 2, PlatzierungVon = 1, PlatzierungBis = 2 };
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 2);

        var ergebnis = _validator.Validieren(eingabe, Liga(eingangsMannschaftsanzahl: 36), Ziel(eingangsMannschaftsanzahl: 2), [bestehende], bearbeiteteRegelId: 100);

        ergebnis.IstGueltig.Should().BeTrue();
    }

    // ── Batch-Validierung (Issue #180) ─────────────────────────────────────────

    [Fact]
    public void Block_SummeUeberAlleZeilenUebersteigtKapazitaet_IstUngueltig()
    {
        // Zielrunde erwartet 8 Mannschaften. Eine einzelne Zeile (Platz 1-2) allein wäre unauffällig,
        // aber die vollständige Batch-Summe aus 4 Zeilen (je 2) ergibt 8 -> passt exakt, daher hier bewusst
        // eine fünfte Zeile ergänzt, die die Summe auf 10 treibt -> muss über den gesamten Block erkannt werden.
        var eingaben = new[]
        {
            new UebergangsregelEingabe(1, 2, 1, 1, 2),
            new UebergangsregelEingabe(1, 2, 2, 1, 2),
            new UebergangsregelEingabe(1, 2, 3, 1, 2),
            new UebergangsregelEingabe(1, 2, 4, 1, 2),
            new UebergangsregelEingabe(1, 2, 5, 1, 2),
        };

        var ergebnis = _validator.Validieren(eingaben, Gruppenphase(gruppenAnzahl: 5, eingangsMannschaftsanzahl: 20), Ziel(eingangsMannschaftsanzahl: 8), _keineRegeln);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*10*8*");
    }

    [Fact]
    public void Block_SummeUeberAlleZeilenPasstExakt_IstGueltig()
    {
        var eingaben = new[]
        {
            new UebergangsregelEingabe(1, 2, 1, 1, 2),
            new UebergangsregelEingabe(1, 2, 2, 1, 2),
            new UebergangsregelEingabe(1, 2, 3, 1, 2),
            new UebergangsregelEingabe(1, 2, 4, 1, 2),
        };

        var ergebnis = _validator.Validieren(eingaben, Gruppenphase(gruppenAnzahl: 4, eingangsMannschaftsanzahl: 16), Ziel(eingangsMannschaftsanzahl: 8), _keineRegeln);

        ergebnis.IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void Block_DoppelterGruppenIndex_IstUngueltig()
    {
        var eingaben = new[]
        {
            new UebergangsregelEingabe(1, 2, 1, 1, 1),
            new UebergangsregelEingabe(1, 2, 1, 1, 1),
        };

        var ergebnis = _validator.Validieren(eingaben, KoRunde(eingangsMannschaftsanzahl: 4), Ziel(eingangsMannschaftsanzahl: 2), _keineRegeln);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*mehrfach*");
    }

    [Fact]
    public void Block_BeruecksichtigtRegelnAndererBloeckeDerselbenZielrunde_SchliesstEigenenBlockAusZuKapazitaet()
    {
        // Ein anderer Block (Quellrunde 3) liefert bereits 2 Plätze an dieselbe Zielrunde. Der hier validierte
        // Block (Quellrunde 1, wird komplett ersetzt) darf trotzdem exakt die restlichen 2 Plätze beanspruchen —
        // seine eigenen (alten) Zeilen dürfen dabei nicht mitgezählt werden.
        var andererBlock = new Uebergangsregel { Id = 100, QuellRundeId = 3, ZielRundeId = 2, PlatzierungVon = 1, PlatzierungBis = 2 };
        var eigeneAlteZeile = new Uebergangsregel { Id = 101, QuellRundeId = 1, ZielRundeId = 2, PlatzierungVon = 1, PlatzierungBis = 1 };
        var eingaben = new[] { new UebergangsregelEingabe(1, 2, null, 1, 2) };

        var ergebnis = _validator.Validieren(eingaben, Liga(eingangsMannschaftsanzahl: 36), Ziel(eingangsMannschaftsanzahl: 4), [andererBlock, eigeneAlteZeile]);

        ergebnis.IstGueltig.Should().BeTrue();
    }

    // ── Gruppenübergreifende Rangfolge-Regel (Issue #184) ──────────────────────

    [Fact]
    public void GueltigeAnzahlBesteRegel_IstGueltig()
    {
        // 24 Teams / 6 Gruppen = 4 pro Gruppe - Platz 3 existiert, "beste 2" ist <= 6 Gruppen.
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: null, PlatzierungVon: 3, PlatzierungBis: 3, AnzahlBeste: 2);

        var ergebnis = _validator.Validieren(eingabe, Gruppenphase(gruppenAnzahl: 6, eingangsMannschaftsanzahl: 24), Ziel(eingangsMannschaftsanzahl: 2), _keineRegeln);

        ergebnis.IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void AnzahlBesteBeiNichtGruppenphase_IstUngueltig()
    {
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: null, PlatzierungVon: 3, PlatzierungBis: 3, AnzahlBeste: 2);

        _validator.Validieren(eingabe, Liga(eingangsMannschaftsanzahl: 36), Ziel(), _keineRegeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void AnzahlBesteMitGruppenIndex_IstUngueltig()
    {
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: 1, PlatzierungVon: 3, PlatzierungBis: 3, AnzahlBeste: 2);

        _validator.Validieren(eingabe, Gruppenphase(gruppenAnzahl: 6, eingangsMannschaftsanzahl: 24), Ziel(), _keineRegeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void AnzahlBestePlatzierungVonUngleichBis_IstUngueltig()
    {
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: null, PlatzierungVon: 3, PlatzierungBis: 4, AnzahlBeste: 2);

        _validator.Validieren(eingabe, Gruppenphase(gruppenAnzahl: 6, eingangsMannschaftsanzahl: 24), Ziel(), _keineRegeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void AnzahlBesteGroesserGruppenAnzahl_IstUngueltig()
    {
        // Nur 6 Gruppen -> es gibt maximal 6 Kandidaten auf Platz 3, "beste 7" ist unmöglich.
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: null, PlatzierungVon: 3, PlatzierungBis: 3, AnzahlBeste: 7);

        _validator.Validieren(eingabe, Gruppenphase(gruppenAnzahl: 6, eingangsMannschaftsanzahl: 24), Ziel(eingangsMannschaftsanzahl: 7), _keineRegeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void AnzahlBesteKapazitaetsSummeWirdBerücksichtigt()
    {
        // Zielrunde erwartet 2 Mannschaften, AnzahlBeste liefert aber 3 -> Kapazität überschritten,
        // obwohl die Platzierungsspanne selbst nur 1 wäre (Von == Bis).
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: null, PlatzierungVon: 3, PlatzierungBis: 3, AnzahlBeste: 3);

        var ergebnis = _validator.Validieren(eingabe, Gruppenphase(gruppenAnzahl: 6, eingangsMannschaftsanzahl: 24), Ziel(eingangsMannschaftsanzahl: 2), _keineRegeln);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*3*2*");
    }

    [Fact]
    public void Block_DoppelteAnzahlBestePlatzierung_IstUngueltig()
    {
        var eingaben = new[]
        {
            new UebergangsregelEingabe(1, 2, null, 3, 3, AnzahlBeste: 2),
            new UebergangsregelEingabe(1, 2, null, 3, 3, AnzahlBeste: 1),
        };

        var ergebnis = _validator.Validieren(eingaben, Gruppenphase(gruppenAnzahl: 6, eingangsMannschaftsanzahl: 24), Ziel(eingangsMannschaftsanzahl: 3), _keineRegeln);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*mehrfach*");
    }

    // ── Ganzgraph-Validierung (Vollersatz, Issue #294 — Nachfolger von VorlagenUebergangsregelValidatorTests) ──

    private static TurnierRunde GanzgraphGruppenphase(int? gruppenAnzahl = 2, int eingangsMannschaftsanzahl = 8, int? ausgangsMannschaftsanzahl = null) => new()
    {
        Name = "Gruppenphase", Rundenart = Rundenart.Gruppenphase,
        EingangsMannschaftsanzahl = eingangsMannschaftsanzahl,
        AusgangsMannschaftsanzahl = ausgangsMannschaftsanzahl ?? eingangsMannschaftsanzahl / 2,
        GruppenAnzahl = gruppenAnzahl
    };

    private static TurnierRunde GanzgraphLiga(int eingangsMannschaftsanzahl = 8, int? ausgangsMannschaftsanzahl = null) => new()
    {
        Name = "Liga", Rundenart = Rundenart.Liga,
        EingangsMannschaftsanzahl = eingangsMannschaftsanzahl,
        AusgangsMannschaftsanzahl = ausgangsMannschaftsanzahl ?? eingangsMannschaftsanzahl
    };

    private static TurnierRunde GanzgraphSchweizerSystem(int eingangsMannschaftsanzahl = 8, int? ausgangsMannschaftsanzahl = null) => new()
    {
        Name = "Ligaphase", Rundenart = Rundenart.SchweizerSystem,
        EingangsMannschaftsanzahl = eingangsMannschaftsanzahl,
        AusgangsMannschaftsanzahl = ausgangsMannschaftsanzahl ?? eingangsMannschaftsanzahl
    };

    private static TurnierRunde GanzgraphKoRunde(int eingangsMannschaftsanzahl = 4) => new()
    {
        Name = "Halbfinale", Rundenart = Rundenart.KoRunde,
        EingangsMannschaftsanzahl = eingangsMannschaftsanzahl, AusgangsMannschaftsanzahl = eingangsMannschaftsanzahl / 2
    };

    private static TurnierRunde GanzgraphZiel(int eingangsMannschaftsanzahl = 4) => new()
    {
        Name = "Finale", Rundenart = Rundenart.KoRunde,
        EingangsMannschaftsanzahl = eingangsMannschaftsanzahl, AusgangsMannschaftsanzahl = eingangsMannschaftsanzahl / 2
    };

    // Deckt auch ab, dass die Zielrunde als terminale Runde keine ausgehenden Regeln braucht
    [Fact]
    public void Ganzgraph_GueltigeGruppenphaseRegel_IstGueltig()
    {
        var runden = new[] { GanzgraphGruppenphase(gruppenAnzahl: 2, eingangsMannschaftsanzahl: 8, ausgangsMannschaftsanzahl: 2), GanzgraphZiel(eingangsMannschaftsanzahl: 2) };
        var regeln = new[]
        {
            new UebergangsregelPositionsEingabe(1, 2, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1),
            new UebergangsregelPositionsEingabe(1, 2, GruppeIndex: 2, PlatzierungVon: 1, PlatzierungBis: 1)
        };

        _validator.Validieren(runden, regeln).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void Ganzgraph_GruppenphaseOhneGruppenIndex_IstUngueltig()
    {
        var runden = new[] { GanzgraphGruppenphase(), GanzgraphZiel() };
        var regeln = new[] { new UebergangsregelPositionsEingabe(1, 2, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 1) };

        _validator.Validieren(runden, regeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void Ganzgraph_LigaMitGruppenIndex_IstUngueltig()
    {
        var runden = new[] { GanzgraphLiga(), GanzgraphZiel() };
        var regeln = new[] { new UebergangsregelPositionsEingabe(1, 2, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1) };

        _validator.Validieren(runden, regeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void Ganzgraph_GueltigeSchweizerSystemRegel_IstGueltig()
    {
        var runden = new[] { GanzgraphSchweizerSystem(eingangsMannschaftsanzahl: 36, ausgangsMannschaftsanzahl: 8), GanzgraphZiel(eingangsMannschaftsanzahl: 8) };
        var regeln = new[] { new UebergangsregelPositionsEingabe(1, 2, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 8) };

        _validator.Validieren(runden, regeln).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void Ganzgraph_SchweizerSystemMitGruppenIndex_IstUngueltig()
    {
        var runden = new[] { GanzgraphSchweizerSystem(), GanzgraphZiel() };
        var regeln = new[] { new UebergangsregelPositionsEingabe(1, 2, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1) };

        _validator.Validieren(runden, regeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void Ganzgraph_KoRundeOhneSpielnummer_IstUngueltig()
    {
        var runden = new[] { GanzgraphKoRunde(), GanzgraphZiel() };
        var regeln = new[] { new UebergangsregelPositionsEingabe(1, 2, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 1) };

        _validator.Validieren(runden, regeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void Ganzgraph_QuellUndZielGleich_IstUngueltig()
    {
        var runden = new[] { GanzgraphLiga(), GanzgraphZiel() };
        var regeln = new[] { new UebergangsregelPositionsEingabe(1, 1, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 1) };

        _validator.Validieren(runden, regeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void Ganzgraph_ReihenfolgeAusserhalbDerRundenliste_IstUngueltig()
    {
        var runden = new[] { GanzgraphLiga(), GanzgraphZiel() };
        var regeln = new[] { new UebergangsregelPositionsEingabe(1, 5, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 1) };

        _validator.Validieren(runden, regeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void Ganzgraph_DoppelterGruppenindexFuerDieselbeQuellrunde_IstUngueltig()
    {
        var runden = new[] { GanzgraphGruppenphase(gruppenAnzahl: 2, eingangsMannschaftsanzahl: 8), GanzgraphZiel(eingangsMannschaftsanzahl: 4) };
        var regeln = new[]
        {
            new UebergangsregelPositionsEingabe(1, 2, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1),
            new UebergangsregelPositionsEingabe(1, 2, GruppeIndex: 1, PlatzierungVon: 2, PlatzierungBis: 2)
        };

        _validator.Validieren(runden, regeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void Ganzgraph_SummeUebersteigtEingangsanzahlDerZielrunde_IstUngueltig()
    {
        var runden = new[] { GanzgraphGruppenphase(gruppenAnzahl: 2, eingangsMannschaftsanzahl: 8), GanzgraphZiel(eingangsMannschaftsanzahl: 3) };
        var regeln = new[]
        {
            new UebergangsregelPositionsEingabe(1, 2, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 2),
            new UebergangsregelPositionsEingabe(1, 2, GruppeIndex: 2, PlatzierungVon: 1, PlatzierungBis: 2)
        };

        var ergebnis = _validator.Validieren(runden, regeln);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*4*3*");
    }

    [Fact]
    public void Ganzgraph_SummeUnterschreitetEingangsanzahlDerZielrunde_IstUngueltig()
    {
        var runden = new[] { GanzgraphGruppenphase(gruppenAnzahl: 2, eingangsMannschaftsanzahl: 8, ausgangsMannschaftsanzahl: 2), GanzgraphZiel(eingangsMannschaftsanzahl: 2) };
        var regeln = new[] { new UebergangsregelPositionsEingabe(1, 2, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1) };

        var ergebnis = _validator.Validieren(runden, regeln);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*1*2*");
    }

    [Fact]
    public void Ganzgraph_AusgehendeRegelnDeckenNichtDieGesamteAusgangsanzahlAb_IstUngueltig()
    {
        var runden = new[] { GanzgraphGruppenphase(gruppenAnzahl: 2, eingangsMannschaftsanzahl: 8, ausgangsMannschaftsanzahl: 4), GanzgraphZiel(eingangsMannschaftsanzahl: 2) };
        var regeln = new[]
        {
            new UebergangsregelPositionsEingabe(1, 2, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1),
            new UebergangsregelPositionsEingabe(1, 2, GruppeIndex: 2, PlatzierungVon: 1, PlatzierungBis: 1)
        };

        var ergebnis = _validator.Validieren(runden, regeln);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*Gruppenphase*");
    }

    [Fact]
    public void Ganzgraph_ZyklischerUebergang_IstUngueltig()
    {
        var runden = new[] { GanzgraphLiga(), GanzgraphZiel() };
        var regeln = new[]
        {
            new UebergangsregelPositionsEingabe(1, 2, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 1),
            new UebergangsregelPositionsEingabe(2, 1, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1)
        };

        _validator.Validieren(runden, regeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void Ganzgraph_ZusammenfuehrungAusZweiQuellrunden_IstGueltig()
    {
        var runden = new[]
        {
            GanzgraphLiga(eingangsMannschaftsanzahl: 8, ausgangsMannschaftsanzahl: 2),
            GanzgraphLiga(eingangsMannschaftsanzahl: 8, ausgangsMannschaftsanzahl: 2),
            GanzgraphZiel(eingangsMannschaftsanzahl: 4)
        };
        var regeln = new[]
        {
            new UebergangsregelPositionsEingabe(1, 3, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 2),
            new UebergangsregelPositionsEingabe(2, 3, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 2)
        };

        _validator.Validieren(runden, regeln).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void Ganzgraph_ZusammenfuehrungAusLigaUndKoRundeUnterschiedlicherArt_IstGueltig()
    {
        var runden = new[] { GanzgraphLiga(eingangsMannschaftsanzahl: 8), GanzgraphKoRunde(eingangsMannschaftsanzahl: 16), GanzgraphZiel(eingangsMannschaftsanzahl: 16) };
        var regeln = new List<UebergangsregelPositionsEingabe>
        {
            new(1, 3, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 8)
        };
        for (var spiel = 1; spiel <= 8; spiel++)
            regeln.Add(new UebergangsregelPositionsEingabe(2, 3, GruppeIndex: spiel, PlatzierungVon: 1, PlatzierungBis: 1));

        var ergebnis = _validator.Validieren(runden, regeln);

        ergebnis.IstGueltig.Should().BeTrue(because: string.Join(" | ", ergebnis.Fehler));
    }

    [Fact]
    public void Ganzgraph_KoRundeSplitAufSiegerUndVerliererZielrunde_IstGueltig()
    {
        // Issue #334: Halbfinale (4 Eingang) speist BEIDE Sieger (→ Finale) UND Verlierer (→ Spiel um Platz 3)
        // über explizite Übergangsregeln — AusgangsMannschaftsanzahl entspricht dann der vollen Eingangszahl.
        var runden = new[]
        {
            new TurnierRunde { Name = "Halbfinale", Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 4, AusgangsMannschaftsanzahl = 4 },
            new TurnierRunde { Name = "Finale", Rundenart = Rundenart.Finale, EingangsMannschaftsanzahl = 2, AusgangsMannschaftsanzahl = 1 },
            new TurnierRunde { Name = "Spiel um Platz 3", Rundenart = Rundenart.SpielUmPlatzDrei, EingangsMannschaftsanzahl = 2, AusgangsMannschaftsanzahl = 1 },
        };
        var regeln = new[]
        {
            new UebergangsregelPositionsEingabe(1, 2, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1), // Sieger Spiel 1 -> Finale
            new UebergangsregelPositionsEingabe(1, 2, GruppeIndex: 2, PlatzierungVon: 1, PlatzierungBis: 1), // Sieger Spiel 2 -> Finale
            new UebergangsregelPositionsEingabe(1, 3, GruppeIndex: 1, PlatzierungVon: 2, PlatzierungBis: 2), // Verlierer Spiel 1 -> Spiel um Platz 3
            new UebergangsregelPositionsEingabe(1, 3, GruppeIndex: 2, PlatzierungVon: 2, PlatzierungBis: 2), // Verlierer Spiel 2 -> Spiel um Platz 3
        };

        var ergebnis = _validator.Validieren(runden, regeln);

        ergebnis.IstGueltig.Should().BeTrue(because: string.Join(" | ", ergebnis.Fehler));
    }

    [Fact]
    public void Ganzgraph_KoRundeSplitOhneVerliererRegeln_IstUngueltig()
    {
        // Dieselbe Ausgangslage, aber nur die Sieger-Regeln gepflegt: die ausgehende Summe (2) deckt nicht
        // die deklarierte AusgangsMannschaftsanzahl (4) ab.
        var runden = new[]
        {
            new TurnierRunde { Name = "Halbfinale", Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 4, AusgangsMannschaftsanzahl = 4 },
            new TurnierRunde { Name = "Finale", Rundenart = Rundenart.Finale, EingangsMannschaftsanzahl = 2, AusgangsMannschaftsanzahl = 1 },
        };
        var regeln = new[]
        {
            new UebergangsregelPositionsEingabe(1, 2, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1),
            new UebergangsregelPositionsEingabe(1, 2, GruppeIndex: 2, PlatzierungVon: 1, PlatzierungBis: 1),
        };

        var ergebnis = _validator.Validieren(runden, regeln);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*Halbfinale*");
    }

    [Fact]
    public void Ganzgraph_GueltigeAnzahlBesteRegel_IstGueltig()
    {
        var runden = new[] { GanzgraphGruppenphase(gruppenAnzahl: 4, eingangsMannschaftsanzahl: 16, ausgangsMannschaftsanzahl: 2), GanzgraphZiel(eingangsMannschaftsanzahl: 2) };
        var regeln = new[] { new UebergangsregelPositionsEingabe(1, 2, GruppeIndex: null, PlatzierungVon: 3, PlatzierungBis: 3, AnzahlBeste: 2) };

        _validator.Validieren(runden, regeln).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void Ganzgraph_AnzahlBesteBeiNichtGruppenphase_IstUngueltig()
    {
        var runden = new[] { GanzgraphLiga(eingangsMannschaftsanzahl: 8), GanzgraphZiel(eingangsMannschaftsanzahl: 2) };
        var regeln = new[] { new UebergangsregelPositionsEingabe(1, 2, GruppeIndex: null, PlatzierungVon: 3, PlatzierungBis: 3, AnzahlBeste: 2) };

        _validator.Validieren(runden, regeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void Ganzgraph_AnzahlBesteMitGruppenIndex_IstUngueltig()
    {
        var runden = new[] { GanzgraphGruppenphase(gruppenAnzahl: 4, eingangsMannschaftsanzahl: 16), GanzgraphZiel(eingangsMannschaftsanzahl: 2) };
        var regeln = new[] { new UebergangsregelPositionsEingabe(1, 2, GruppeIndex: 1, PlatzierungVon: 3, PlatzierungBis: 3, AnzahlBeste: 2) };

        _validator.Validieren(runden, regeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void Ganzgraph_AnzahlBesteGroesserGruppenAnzahl_IstUngueltig()
    {
        var runden = new[] { GanzgraphGruppenphase(gruppenAnzahl: 4, eingangsMannschaftsanzahl: 16), GanzgraphZiel(eingangsMannschaftsanzahl: 8) };
        var regeln = new[] { new UebergangsregelPositionsEingabe(1, 2, GruppeIndex: null, PlatzierungVon: 3, PlatzierungBis: 3, AnzahlBeste: 7) };

        _validator.Validieren(runden, regeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void Ganzgraph_DoppelteAnzahlBestePlatzierungFuerDieselbeQuellrunde_IstUngueltig()
    {
        var runden = new[] { GanzgraphGruppenphase(gruppenAnzahl: 4, eingangsMannschaftsanzahl: 16), GanzgraphZiel(eingangsMannschaftsanzahl: 4) };
        var regeln = new[]
        {
            new UebergangsregelPositionsEingabe(1, 2, GruppeIndex: null, PlatzierungVon: 3, PlatzierungBis: 3, AnzahlBeste: 2),
            new UebergangsregelPositionsEingabe(1, 2, GruppeIndex: null, PlatzierungVon: 3, PlatzierungBis: 3, AnzahlBeste: 1)
        };

        _validator.Validieren(runden, regeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void Ganzgraph_AnzahlBesteKapazitaetsSummeWirdBeruecksichtigt()
    {
        var runden = new[] { GanzgraphGruppenphase(gruppenAnzahl: 4, eingangsMannschaftsanzahl: 16), GanzgraphZiel(eingangsMannschaftsanzahl: 2) };
        var regeln = new[] { new UebergangsregelPositionsEingabe(1, 2, GruppeIndex: null, PlatzierungVon: 3, PlatzierungBis: 3, AnzahlBeste: 3) };

        var ergebnis = _validator.Validieren(runden, regeln);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*3*2*");
    }
}
