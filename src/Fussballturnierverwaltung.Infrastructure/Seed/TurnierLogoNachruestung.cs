using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Seed;

/// <summary>Issue #481: setzt das Wettbewerbslogo bei Turniervorlagen ohne Logo — bei neu angelegten Seed-Vorlagen
/// genauso wie bei älteren Datenbanken (Nachrüsten-Pattern wie bei den Wappen, #233). Bekommt eine Vorlage hier ihr
/// Logo, übernehmen es im selben Lauf auch die Turniere desselben Wettbewerbs ohne Logo; das geschieht damit nur
/// einmal, ein später am Turnier entferntes Logo kommt nicht zurück.</summary>
internal static class TurnierLogoNachruestung
{
    // Ein Logo je Wettbewerb (Vorlagenname), gleich für alle Varianten — Logos einzelner Ausgaben (WM 2006, EM 2000)
    // passen nicht zu Varianten über mehrere Turniere. Wert = Dateiname unter Seed/Bildmaterial/Privat/Turnierlogos/, Quelle
    // siehe tools/seed-bildmaterial/fetch_turnierlogos.py. Mini-Turnier und Freies Turnier bleiben ohne Logo.
    private static readonly IReadOnlyDictionary<string, string> _logoDateien = new Dictionary<string, string>
    {
        ["FIFA WM"]                    = "FIFA_WM",
        ["FIFA Klub-WM"]               = "FIFA_KLUB_WM",
        ["UEFA EM"]                    = "UEFA_EM",
        ["UEFA Nations League Liga A"] = "UEFA_NATIONS_LEAGUE",
        ["Copa América"]               = "COPA_AMERICA",
        ["Afrika-Cup"]                 = "AFRIKA_CUP",
        ["AFC Asian Cup"]              = "AFC_ASIAN_CUP",
        ["CONCACAF Gold Cup"]          = "CONCACAF_GOLD_CUP",
        ["UEFA Champions League"]      = "UEFA_CHAMPIONS_LEAGUE",
        ["UEFA Europa League"]         = "UEFA_EUROPA_LEAGUE",
        ["UEFA Conference League"]     = "UEFA_CONFERENCE_LEAGUE",
        ["DFB-Pokal"]                  = "DFB_POKAL",
        ["FA Cup"]                     = "FA_CUP",
        ["Bundesliga"]                 = "BUNDESLIGA",
        ["2. Bundesliga"]              = "ZWEITE_BUNDESLIGA",
        ["Premier League"]             = "PREMIER_LEAGUE",
        ["La Liga"]                    = "LA_LIGA",
        ["Serie A"]                    = "SERIE_A",
        ["Ligue 1"]                    = "LIGUE_1",
    };

    public static async Task AusfuehrenAsync(
        TurnierverwaltungDbContext context, SeedBildQuelle bildQuelle, Action<int, string> protokollieren, CancellationToken ct)
    {
        var vorlagen = await OhneLogoAsync(context, vorlagen: true, ct);
        var wettbewerbe = VorlagenErgaenzen(vorlagen, bildQuelle);
        if (wettbewerbe.Count == 0) return;

        var turniere = await OhneLogoAsync(context, vorlagen: false, ct);
        int turniereErgaenzt = TurniereErgaenzen(turniere, wettbewerbe);

        await context.SaveChangesAsync(ct);
        protokollieren(wettbewerbe.Values.Sum(w => w.Anzahl), "Turniervorlagen um ihr Turnierlogo ergänzt.");
        if (turniereErgaenzt > 0) protokollieren(turniereErgaenzt, "Turniere um das Logo ihres Wettbewerbs ergänzt.");
    }

    private static Task<List<TurnierInstanz>> OhneLogoAsync(
        TurnierverwaltungDbContext context, bool vorlagen, CancellationToken ct) =>
        context.TurnierInstanzen
            .Where(i => i.Logo == null && (i.Status == TurnierStatus.Vorlage) == vorlagen)
            .ToListAsync(ct);

    /// <summary>Liefert je Wettbewerb, der in diesem Lauf mindestens eine Vorlage ergänzt hat, das Logo und die Anzahl
    /// ergänzter Vorlagen.</summary>
    private static Dictionary<string, (byte[] Logo, int Anzahl)> VorlagenErgaenzen(
        List<TurnierInstanz> vorlagen, SeedBildQuelle bildQuelle)
    {
        var wettbewerbe = new Dictionary<string, (byte[] Logo, int Anzahl)>();
        foreach (var vorlage in vorlagen)
        {
            if (!_logoDateien.TryGetValue(vorlage.Name, out var datei)) continue;
            if (!wettbewerbe.TryGetValue(vorlage.Name, out var eintrag))
            {
                var logo = bildQuelle.Laden("Turnierlogos", datei);
                if (logo is null) continue;
                eintrag = (logo, 0);
            }
            vorlage.Logo = eintrag.Logo;
            wettbewerbe[vorlage.Name] = (eintrag.Logo, eintrag.Anzahl + 1);
        }
        return wettbewerbe;
    }

    private static int TurniereErgaenzen(
        List<TurnierInstanz> turniere, Dictionary<string, (byte[] Logo, int Anzahl)> wettbewerbe)
    {
        int ergaenzt = 0;
        foreach (var turnier in turniere)
        {
            var wettbewerb = Wettbewerb(turnier.Name);
            if (wettbewerb is null || !wettbewerbe.TryGetValue(wettbewerb, out var eintrag)) continue;
            turnier.Logo = eintrag.Logo;
            ergaenzt++;
        }
        return ergaenzt;
    }

    // Turniere haben keinen Verweis auf ihre Vorlage, tragen aber den Wettbewerbsnamen mit Zusatz („Bundesliga 2026“,
    // „UEFA EM (16 Teams, 1996-2012)“). Das Leerzeichen danach verhindert Treffer mitten im Wort.
    private static string? Wettbewerb(string turnierName) =>
        _logoDateien.Keys
            .Where(w => turnierName == w || turnierName.StartsWith(w + " ", StringComparison.Ordinal))
            .MaxBy(w => w.Length);
}
