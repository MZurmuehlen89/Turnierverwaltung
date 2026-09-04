using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Repositories;

public class TurnierVorlageRepository(TurnierverwaltungDbContext context)
    : GenericRepository<TurnierVorlage>(context), ITurnierVorlageRepository
{
    public override async Task<IReadOnlyList<TurnierVorlage>> AlleFindenAsync(CancellationToken ct = default)
        => await Context.TurnierVorlagen
            .OrderBy(v => v.VorlagenKategorie)
            .ThenBy(v => v.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TurnierVorlage>> AlleNachKategorieFindenAsync(VorlagenKategorie kategorie, CancellationToken ct = default)
        => await Context.TurnierVorlagen
            .Where(v => v.VorlagenKategorie == kategorie)
            .OrderBy(v => v.Name)
            .ToListAsync(ct);

    public async Task<TurnierVorlage?> FindenNachIdMitRundenAsync(int id, CancellationToken ct = default)
        => await Context.TurnierVorlagen
            .Include(v => v.VorlagenRunden.OrderBy(r => r.Reihenfolge))
                .ThenInclude(r => r.Regelwerk)
            .Include(v => v.VorlagenRunden)
                .ThenInclude(r => r.AusgehendeUebergangsregeln)
                    .ThenInclude(regel => regel.ZielVorlagenRunde)
            .Include(v => v.VorlagenRunden)
                .ThenInclude(r => r.Paarungen)
            .FirstOrDefaultAsync(v => v.Id == id, ct);

    public async Task RundenErsetzenAsync(
        int vorlageId,
        IReadOnlyList<VorlagenRunde> neueRunden,
        IReadOnlyList<VorlagenUebergangsRegelEingabe> neueUebergangsregeln,
        IReadOnlyList<VorlagenPaarungEingabe>? neuePaarungen = null,
        CancellationToken ct = default)
    {
        // Runden positionsbasiert aktualisieren statt komplett zu löschen und neu anzulegen (Nutzer-Fund:
        // ein blindes Ersetzen vergibt für JEDE Runde eine neue Id, auch für unveränderte — das ließ die
        // Rundenauswahl in der WinUI3-Oberfläche nach jedem Speichern verschwinden). Analog zum bereits
        // etablierten, korrekten Muster in TurnierInstanzRepository.RundenErsetzenAsync: bestehende Zeilen
        // in-place aktualisieren (Id bleibt erhalten), nur überzählige alte Runden werden tatsächlich
        // gelöscht — dieselbe Diff-Technik (statt Löschen+Neuanlegen) gilt jetzt einheitlich auch für
        // Übergangsregeln und Paarungen weiter unten (Nutzer-Vorgabe: grundsätzlich Insert/Update statt
        // Löschen+Neuanlegen, Delete nur für tatsächlich weggefallene Zeilen).
        var alteRunden = await Context.VorlagenRunden
            .Where(r => r.TurnierVorlageId == vorlageId)
            .OrderBy(r => r.Reihenfolge)
            .ToListAsync(ct);

        int gemeinsam = Math.Min(alteRunden.Count, neueRunden.Count);
        for (int i = 0; i < gemeinsam; i++)
        {
            var alt = alteRunden[i];
            var neu = neueRunden[i];
            alt.Name                      = neu.Name;
            alt.Reihenfolge                = neu.Reihenfolge;
            alt.Rundenart                  = neu.Rundenart;
            alt.EingangsMannschaftsanzahl  = neu.EingangsMannschaftsanzahl;
            alt.AusgangsMannschaftsanzahl  = neu.AusgangsMannschaftsanzahl;
            alt.GruppenAnzahl              = neu.GruppenAnzahl;
            alt.MitRueckspiel              = neu.MitRueckspiel;
            alt.AuswaertstorRegelAktiv     = neu.AuswaertstorRegelAktiv;
            alt.RegelwerkId                = neu.RegelwerkId;
            alt.PaarungFestVorgegeben      = neu.PaarungFestVorgegeben;
        }

        for (int i = alteRunden.Count; i < neueRunden.Count; i++)
            Context.VorlagenRunden.Add(neueRunden[i]);

        if (alteRunden.Count > neueRunden.Count)
        {
            var entfernte = alteRunden.Skip(neueRunden.Count).ToList();
            var entfernteIds = entfernte.Select(r => r.Id).ToHashSet();

            // Übergangsregeln, die eine entfernte Runde als Zielrunde referenzieren, müssen vorab entfernt
            // werden: die Quellrunden-Beziehung cascadet bereits über die DB-Konfiguration, die Zielrunden-
            // Beziehung ist bewusst Restrict (analog TurnierInstanzRepository.RundenErsetzenAsync).
            var betroffeneZielregeln = await Context.VorlagenUebergangsregeln
                .Where(r => entfernteIds.Contains(r.ZielVorlagenRundeId))
                .ToListAsync(ct);
            Context.VorlagenUebergangsregeln.RemoveRange(betroffeneZielregeln);

            // Kein separates Entfernen der zugehörigen VorlagenPaarungen nötig: ON DELETE CASCADE der
            // Runden-Löschung reicht (Paarungen referenzieren ausschließlich ihre eigene VorlagenRunde).
            Context.VorlagenRunden.RemoveRange(entfernte);
        }

        await Context.SaveChangesAsync(ct);

        // Positionsgleich zu neueRunden: bestehende Runden über alteRunden[i] (Id unverändert), neu
        // hinzugefügte über neueRunden[i] (EF befüllt deren Id erst nach SaveChangesAsync) — analog
        // TurnierInstanzRepository.RundenErsetzenAsync.
        var idNachReihenfolge = new Dictionary<int, int>(neueRunden.Count);
        for (int i = 0; i < neueRunden.Count; i++)
        {
            var runde = i < gemeinsam ? alteRunden[i] : neueRunden[i];
            idNachReihenfolge[runde.Reihenfolge] = runde.Id;
        }
        var aktuelleRundenIds = idNachReihenfolge.Values.ToHashSet();

        // Übergangsregeln per Schlüssel-Diff synchronisieren: Quell-/Zielrunde + Gruppe/Platzierung bilden
        // zusammen die vollständige Identität einer Regel (kein separates veränderliches Feld) — nur
        // tatsächlich neue Kombinationen werden angelegt, nur weggefallene entfernt, unveränderte bleiben
        // unangetastet (bleiben dadurch auch ihre eigene Id/Existenz über einen Speichervorgang hinweg behalten).
        var alteRegeln = await Context.VorlagenUebergangsregeln
            .Where(r => aktuelleRundenIds.Contains(r.QuellVorlagenRundeId))
            .ToListAsync(ct);
        var alteRegelSchluessel = alteRegeln
            .ToDictionary(r => (r.QuellVorlagenRundeId, r.ZielVorlagenRundeId, r.GruppeIndex, r.PlatzierungVon, r.PlatzierungBis, r.AnzahlBeste));

        var neueRegelSchluessel = new HashSet<(int Quell, int Ziel, int? GruppeIndex, int Von, int Bis, int? AnzahlBeste)>();
        foreach (var e in neueUebergangsregeln)
        {
            (int Quell, int Ziel, int? GruppeIndex, int Von, int Bis, int? AnzahlBeste) schluessel =
                (idNachReihenfolge[e.QuellReihenfolge], idNachReihenfolge[e.ZielReihenfolge], e.GruppeIndex, e.PlatzierungVon, e.PlatzierungBis, e.AnzahlBeste);
            neueRegelSchluessel.Add(schluessel);
            if (!alteRegelSchluessel.ContainsKey(schluessel))
                Context.VorlagenUebergangsregeln.Add(new VorlagenUebergangsRegel
                {
                    QuellVorlagenRundeId = schluessel.Quell,
                    ZielVorlagenRundeId  = schluessel.Ziel,
                    GruppeIndex          = schluessel.GruppeIndex,
                    PlatzierungVon       = schluessel.Von,
                    PlatzierungBis       = schluessel.Bis,
                    AnzahlBeste          = schluessel.AnzahlBeste
                });
        }
        var entfernteRegeln = alteRegeln
            .Where(r => !neueRegelSchluessel.Contains((r.QuellVorlagenRundeId, r.ZielVorlagenRundeId, r.GruppeIndex, r.PlatzierungVon, r.PlatzierungBis, r.AnzahlBeste)))
            .ToList();
        Context.VorlagenUebergangsregeln.RemoveRange(entfernteRegeln);

        // Paarungen per Schlüssel-Diff synchronisieren: Runde+PaarungsNummer+SlotNr identifizieren einen
        // Slot eindeutig, PlatzhalterBezeichnung ist das einzige veränderliche Feld — bestehende Slots
        // werden aktualisiert statt gelöscht+neu angelegt, nur wirklich neue/weggefallene Slots werden
        // hinzugefügt/entfernt.
        var altePaarungen = await Context.VorlagenPaarungen
            .Where(p => aktuelleRundenIds.Contains(p.VorlagenRundeId))
            .ToListAsync(ct);
        var altePaarungenNachSchluessel = altePaarungen.ToDictionary(p => (p.VorlagenRundeId, p.PaarungsNummer, p.SlotNr));

        var neueSlotSchluessel = new HashSet<(int RundeId, int PaarungsNummer, int SlotNr)>();
        foreach (var e in neuePaarungen ?? [])
        {
            var rundeId = idNachReihenfolge[e.RundenReihenfolge];
            var schluessel = (rundeId, e.PaarungsNummer, e.SlotNr);
            neueSlotSchluessel.Add(schluessel);

            if (altePaarungenNachSchluessel.TryGetValue(schluessel, out var bestehend))
                bestehend.PlatzhalterBezeichnung = e.PlatzhalterBezeichnung;
            else
                Context.VorlagenPaarungen.Add(new VorlagenPaarung
                {
                    VorlagenRundeId        = rundeId,
                    PaarungsNummer         = e.PaarungsNummer,
                    SlotNr                 = e.SlotNr,
                    PlatzhalterBezeichnung = e.PlatzhalterBezeichnung
                });
        }
        var entfernteSlots = altePaarungen
            .Where(p => !neueSlotSchluessel.Contains((p.VorlagenRundeId, p.PaarungsNummer, p.SlotNr)))
            .ToList();
        Context.VorlagenPaarungen.RemoveRange(entfernteSlots);

        await Context.SaveChangesAsync(ct);
    }
}
