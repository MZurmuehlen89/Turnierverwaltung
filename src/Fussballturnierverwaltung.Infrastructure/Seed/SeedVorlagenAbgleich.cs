using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Infrastructure.Seed;

/// <summary>Vergleicht den Aufbau einer Bestands-Vorlage mit ihrem Seed-Stand und ergänzt, was fehlt — ohne
/// Datenbank: Werte an vorhandenen Runden setzt der Abgleich selbst, neue Zeilen (Übergangsregeln, Vorbelegungen)
/// gibt er zurück, anlegen muss sie der Aufrufer (<see cref="TurnierVorlagenNachruestung"/>). Ergänzt wird nur,
/// solange die betroffenen Runden noch dem Seed entsprechen; eine umgebaute Vorlage bleibt unangetastet.</summary>
public static class SeedVorlagenAbgleich
{
    /// <summary>Die Runde der anderen Seite (Bestand ↔ Seed) mit derselben Reihenfolge und demselben Namen.</summary>
    public static TurnierRunde? Gegenstueck(TurnierInstanz vorlage, TurnierRunde runde) =>
        vorlage.Runden.FirstOrDefault(r => r.Reihenfolge == runde.Reihenfolge && r.Name == runde.Name);

    // ── Quoten und Kontingente (Issue #574) ───────────────────────────────────

    /// <summary>Die Quoten des Seeds, die der Vorlage fehlen — je Kontingent, damit spätere Ergänzungen des Seeds
    /// nachkommen: eine Verbandsquote je Verband, ein Landes-Kontingent je Land, Spielklasse und Startplatz. Vorhandene
    /// Quoten bleiben, auch mit anderer Anzahl. Die neuen Zeilen verweisen auf die Vorlage, anlegen muss sie der
    /// Aufrufer.</summary>
    public static (List<VerbandsQuote> Verbaende, List<LandesQuote> Laender) FehlendeQuoten(
        TurnierInstanz vorlage, TurnierInstanz soll)
    {
        var verbaende = soll.VerbandsQuoten
            .Where(q => vorlage.VerbandsQuoten.All(v => v.VerbandId != q.VerbandId))
            .Select(q => new VerbandsQuote { TurnierInstanzId = vorlage.Id, VerbandId = q.VerbandId, Anzahl = q.Anzahl })
            .ToList();
        var laender = soll.LandesQuoten
            .Where(q => vorlage.LandesQuoten.All(v => (v.LandId, v.Spielklasse, v.AbPlatz) != (q.LandId, q.Spielklasse, q.AbPlatz)))
            .Select(q => new LandesQuote
            {
                TurnierInstanzId = vorlage.Id, LandId = q.LandId, Anzahl = q.Anzahl, Spielklasse = q.Spielklasse, AbPlatz = q.AbPlatz,
            })
            .ToList();
        return (verbaende, laender);
    }

    // ── Standard-Rangliste und Heimrecht für Unterklassige (Issue #393) ────────

    /// <summary>Trägt die Standard-Rangliste des Seeds nach, wo die Runde noch keine hat. Liefert die Zahl der
    /// ergänzten Runden.</summary>
    public static int StandardRanglisteErgaenzen(TurnierInstanz vorlage, TurnierInstanz soll)
    {
        int ergaenzt = 0;
        foreach (var sollRunde in soll.Runden.Where(r => r.StandardRanglisteId is not null))
        {
            var runde = Gegenstueck(vorlage, sollRunde);
            if (runde is null || runde.StandardRanglisteId is not null || runde.Rundenart != sollRunde.Rundenart) continue;

            runde.StandardRanglisteId = sollRunde.StandardRanglisteId;
            ergaenzt++;
        }
        return ergaenzt;
    }

    /// <summary>Stellt die Vorgabe „Heimrecht nach Los“ auf „Heimrecht für Unterklassige“ um, wo der Seed die neue
    /// Regel vorgibt — nur, solange die Runde genau die alte Vorgabe trägt. Liefert die Zahl der umgestellten Runden.</summary>
    public static int UnterklassigenHeimrechtUebernehmen(TurnierInstanz vorlage, TurnierInstanz soll)
    {
        int umgestellt = 0;
        foreach (var sollRunde in soll.Runden.Where(r =>
                     r.VorgegebeneAuslosungsregeln.Contains(AuslosungsRegelArt.HeimrechtFuerUnterklassige)))
        {
            var runde = Gegenstueck(vorlage, sollRunde);
            if (runde is null || !runde.VorgegebeneAuslosungsregeln.SequenceEqual([AuslosungsRegelArt.HeimrechtNachLos]))
                continue;

            runde.VorgegebeneAuslosungsregeln = [.. sollRunde.VorgegebeneAuslosungsregeln];
            umgestellt++;
        }
        return umgestellt;
    }

    /// <summary>Issue #469: übernimmt „ein Gegner je Topf“ aus dem Seed, wo die Runde noch auf dem Standard (2)
    /// steht. Liefert die Zahl der umgestellten Runden.</summary>
    public static int GegnerJeTopfUebernehmen(TurnierInstanz vorlage, TurnierInstanz soll)
    {
        int umgestellt = 0;
        foreach (var sollRunde in soll.Runden.Where(r => r.GegnerJeTopf == 1))
        {
            var runde = Gegenstueck(vorlage, sollRunde);
            if (runde is null || runde.GegnerJeTopf != 2 || runde.Rundenart != sollRunde.Rundenart) continue;

            runde.GegnerJeTopf = sollRunde.GegnerJeTopf;
            umgestellt++;
        }
        return umgestellt;
    }

    // ── Struktur (Issue #350) ──────────────────────────────────────────────────

    /// <summary>Trägt die Gruppenanzahl nach, wo sie leer ist. Liefert die Zahl der ergänzten Runden.</summary>
    public static int GruppenAnzahlErgaenzen(TurnierInstanz vorlage, TurnierInstanz soll)
    {
        int ergaenzt = 0;
        foreach (var sollRunde in soll.Runden.Where(r => r.GruppenAnzahl is not null))
        {
            var runde = Gegenstueck(vorlage, sollRunde);
            if (runde is null || runde.GruppenAnzahl is not null || runde.Rundenart != sollRunde.Rundenart) continue;

            runde.GruppenAnzahl = sollRunde.GruppenAnzahl;
            ergaenzt++;
        }
        return ergaenzt;
    }

    /// <summary>Die Übergangsregeln des Seeds für eine Vorlage ganz ohne Regeln, deren beteiligte Runden noch dem
    /// Seed entsprechen — sonst leer.</summary>
    public static List<Uebergangsregel> FehlendeUebergangsregeln(
        TurnierInstanz vorlage, TurnierInstanz soll, IReadOnlyCollection<Uebergangsregel> vorhandeneRegeln)
    {
        var eigeneRundenIds = vorlage.Runden.Select(r => r.Id).ToHashSet();
        if (vorhandeneRegeln.Any(u => eigeneRundenIds.Contains(u.QuellRundeId))) return [];

        var sollRegeln = soll.Runden.SelectMany(r => r.Uebergangsregeln).ToList();
        var zuordnung = sollRegeln
            .SelectMany(u => new[] { u.QuellRunde, u.ZielRunde })
            .Distinct()
            .ToDictionary(sollRunde => sollRunde, sollRunde => Gegenstueck(vorlage, sollRunde));
        if (!zuordnung.All(z => HatAufbauWie(z.Value, z.Key))) return [];

        return
        [
            .. sollRegeln.Select(sollRegel => new Uebergangsregel
            {
                QuellRundeId   = zuordnung[sollRegel.QuellRunde]!.Id,
                ZielRundeId    = zuordnung[sollRegel.ZielRunde]!.Id,
                GruppeIndex    = sollRegel.GruppeIndex,
                PlatzierungVon = sollRegel.PlatzierungVon,
                PlatzierungBis = sollRegel.PlatzierungBis,
                AnzahlBeste    = sollRegel.AnzahlBeste,
            })
        ];
    }

    private static bool HatAufbauWie(TurnierRunde? runde, TurnierRunde sollRunde) =>
        runde is not null
        && runde.Rundenart == sollRunde.Rundenart
        && runde.EingangsMannschaftsanzahl == sollRunde.EingangsMannschaftsanzahl
        && runde.AusgangsMannschaftsanzahl == sollRunde.AusgangsMannschaftsanzahl
        && runde.GruppenAnzahl == sollRunde.GruppenAnzahl;

    /// <summary>Setzt <c>EingangUeberUebergangsregeln</c> nur bei Runden mit tatsächlich eingehenden Regeln (fehlte
    /// auch beim Achtelfinale von CL/EL/ECL ab 2024/25). Liefert die Zahl der geänderten Runden.</summary>
    public static int EingangUeberUebergangsregelnSetzen(
        TurnierInstanz vorlage, TurnierInstanz soll, IReadOnlyCollection<Uebergangsregel> uebergangsregeln)
    {
        int gesetzt = 0;
        foreach (var sollRunde in soll.Runden.Where(r => r.EingangUeberUebergangsregeln))
        {
            var runde = Gegenstueck(vorlage, sollRunde);
            if (runde is null || runde.EingangUeberUebergangsregeln) continue;
            if (!uebergangsregeln.Any(u => u.ZielRundeId == runde.Id)) continue;

            runde.EingangUeberUebergangsregeln = true;
            gesetzt++;
        }
        return gesetzt;
    }

    // ── Tabellenbereiche (Issue #420) ──────────────────────────────────────────

    /// <summary>Die Tabellenbereiche des Seeds für Runden ganz ohne Bereiche, die noch dem Seed entsprechen
    /// (<c>TabellenBereiche</c> der Bestands-Runden muss geladen sein).</summary>
    public static List<TabellenBereich> FehlendeTabellenBereiche(TurnierInstanz vorlage, TurnierInstanz soll)
    {
        List<TabellenBereich> fehlende = [];
        foreach (var sollRunde in soll.Runden.Where(r => r.TabellenBereiche.Count > 0))
        {
            var runde = Gegenstueck(vorlage, sollRunde);
            if (!HatAufbauWie(runde, sollRunde) || runde!.TabellenBereiche.Count > 0) continue;

            fehlende.AddRange(sollRunde.TabellenBereiche.Select(b => new TabellenBereich
            {
                TurnierRundeId = runde.Id,
                PlatzVon       = b.PlatzVon,
                PlatzBis       = b.PlatzBis,
                Art            = b.Art,
                Bezeichnung    = b.Bezeichnung,
            }));
        }
        return fehlende;
    }

    // ── Feste Vorbelegungen (Issue #372, Gruppen #466) ─────────────────────────

    /// <summary>Überträgt die festen Vorbelegungen des Seeds (K.-o.-Baum, fest besetzte Gruppen): markiert die Runden als fest vorgegeben (samt
    /// Kombinationstabelle, wo noch keine steht) und liefert ihre Vorbelegungen. Nur, wenn ALLE betroffenen Runden
    /// noch dem Seed entsprechen und keine davon schon eine Auslosung hat, sonst leer und ohne Änderung — eine
    /// manuell angelegte Vorbelegung wird nie überschrieben, ein halb nachgerüsteter Baum entsteht nie.</summary>
    public static List<Auslosung> FesteVorbelegungenUebertragen(TurnierInstanz vorlage, TurnierInstanz soll)
    {
        var zuordnung = soll.Auslosungen.ToDictionary(a => a, a => RundeFuerFestenBaum(vorlage, a.TurnierRunde));
        if (zuordnung.Values.Any(r => r is null)) return [];
        if (zuordnung.Values.Any(r => vorlage.Auslosungen.Any(a => a.TurnierRundeId == r!.Id))) return [];

        List<Auslosung> vorbelegungen = [];
        foreach (var (sollAuslosung, runde) in zuordnung)
        {
            runde!.PaarungFestVorgegeben = true;
            runde.KombinationstabelleJson ??= sollAuslosung.TurnierRunde.KombinationstabelleJson;
            vorbelegungen.Add(VorbelegungAusSeed(sollAuslosung, vorlage, runde));
        }
        return vorbelegungen;
    }

    private static TurnierRunde? RundeFuerFestenBaum(TurnierInstanz vorlage, TurnierRunde sollRunde)
    {
        var runde = Gegenstueck(vorlage, sollRunde);
        bool entsprichtSeed = runde is not null
            && runde.Rundenart == sollRunde.Rundenart
            && runde.EingangsMannschaftsanzahl == sollRunde.EingangsMannschaftsanzahl;
        return entsprichtSeed ? runde : null;
    }

    private static Auslosung VorbelegungAusSeed(Auslosung sollAuslosung, TurnierInstanz vorlage, TurnierRunde runde)
    {
        var auslosung = new Auslosung
        {
            TurnierInstanzId = vorlage.Id,
            TurnierRundeId   = runde.Id,
            Name             = sollAuslosung.Name,
            Auslosungsart    = sollAuslosung.Auslosungsart,
            Status           = sollAuslosung.Status,
        };
        foreach (var ergebnis in sollAuslosung.Ergebnisse)
        {
            auslosung.Ergebnisse.Add(new AuslosungsErgebnis
            {
                PaarungsNummer           = ergebnis.PaarungsNummer,
                HeimPlatzhalterName      = ergebnis.HeimPlatzhalterName,
                AuswaertsPlatzhalterName = ergebnis.AuswaertsPlatzhalterName,
            });
        }
        return auslosung;
    }
}
