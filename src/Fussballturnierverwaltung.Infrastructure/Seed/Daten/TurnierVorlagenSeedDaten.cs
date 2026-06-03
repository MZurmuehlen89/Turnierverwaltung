using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Infrastructure.Seed.Daten;

public static class TurnierVorlagenSeedDaten
{
    public static List<TurnierVorlage> Erstellen() =>
    [
        // Weltmeisterschaften
        new()
        {
            Name = "FIFA WM (32 Teams, 1998–2022)",
            Beschreibung = "Gruppenphase (8×4) → Achtelfinale → Viertelfinale → Halbfinale → Spiel um Platz 3 + Finale",
            ErforderlicheMannschaftsanzahl = 32,
            VorlagenKategorie = VorlagenKategorie.Weltmeisterschaft,
            IstHistorisch = false,
            GueltigVon = new DateOnly(1998, 1, 1),
        },
        new()
        {
            Name = "FIFA WM (24 Teams, 1982–1994)",
            Beschreibung = "6 Dreiergruppen + Achtelfinale-Runde → K.-o.",
            ErforderlicheMannschaftsanzahl = 24,
            VorlagenKategorie = VorlagenKategorie.Weltmeisterschaft,
            IstHistorisch = true,
            GueltigVon = new DateOnly(1982, 1, 1),
            GueltigBis = new DateOnly(1994, 12, 31),
        },
        new()
        {
            Name = "FIFA WM (16 Teams, 1934–1978)",
            Beschreibung = "Direktes K.-o. ab 16 Teams",
            ErforderlicheMannschaftsanzahl = 16,
            VorlagenKategorie = VorlagenKategorie.Weltmeisterschaft,
            IstHistorisch = true,
            GueltigVon = new DateOnly(1934, 1, 1),
            GueltigBis = new DateOnly(1978, 12, 31),
        },

        // Europameisterschaften
        new()
        {
            Name = "UEFA EM (24 Teams, 2016+)",
            Beschreibung = "Gruppenphase (6×4) → Achtelfinale → Viertelfinale → Halbfinale → Finale",
            ErforderlicheMannschaftsanzahl = 24,
            VorlagenKategorie = VorlagenKategorie.Kontinentalmeisterschaft,
            IstHistorisch = false,
            GueltigVon = new DateOnly(2016, 1, 1),
        },
        new()
        {
            Name = "UEFA EM (16 Teams, 1996–2012)",
            Beschreibung = "Gruppenphase (4×4) → Viertelfinale → Halbfinale → Finale",
            ErforderlicheMannschaftsanzahl = 16,
            VorlagenKategorie = VorlagenKategorie.Kontinentalmeisterschaft,
            IstHistorisch = true,
            GueltigVon = new DateOnly(1996, 1, 1),
            GueltigBis = new DateOnly(2012, 12, 31),
        },
        new()
        {
            Name = "UEFA EM (8 Teams, 1960–1992)",
            Beschreibung = "Halbfinale → Finale + Spiel um Platz 3",
            ErforderlicheMannschaftsanzahl = 8,
            VorlagenKategorie = VorlagenKategorie.Kontinentalmeisterschaft,
            IstHistorisch = true,
            GueltigVon = new DateOnly(1960, 1, 1),
            GueltigBis = new DateOnly(1992, 12, 31),
        },

        // Kleine Turniere
        new()
        {
            Name = "Mini-Turnier (8 Teams)",
            Beschreibung = "2 Vierergruppen → Halbfinale → Finale + Spiel um Platz 3",
            ErforderlicheMannschaftsanzahl = 8,
            VorlagenKategorie = VorlagenKategorie.KleinesTurnier,
            IstHistorisch = false,
        },
        new()
        {
            Name = "Mini-Turnier (4 Teams)",
            Beschreibung = "Liga-Runde oder direktes Halbfinale + Finale",
            ErforderlicheMannschaftsanzahl = 4,
            VorlagenKategorie = VorlagenKategorie.KleinesTurnier,
            IstHistorisch = false,
        },
        new()
        {
            Name = "Freies Turnier (benutzerdefiniert)",
            Beschreibung = "Frei konfigurierbare Rundenstruktur",
            ErforderlicheMannschaftsanzahl = 0,
            VorlagenKategorie = VorlagenKategorie.Benutzerdefiniert,
            IstHistorisch = false,
        },
    ];
}
