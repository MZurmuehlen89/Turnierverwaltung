using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Speichert generierte Spiele; Rückspiele erhalten die HinspielId ihres zuvor gespeicherten Hinspiels.</summary>
internal static class SpielplanSpeicherung
{
    /// <summary>Speichert das Ergebnis eines Generators — mit Hin- und Rückspielen erst die Hinspiele, dann die
    /// Rückspiele, jeweils in Spiel-Reihenfolge.</summary>
    public static Task ErgebnisSpeichernAsync(ISpielRepository spielRepository, SpielplanErgebnis ergebnis, CancellationToken ct)
    {
        if (!ergebnis.HatHinUndRueckspiele)
            return HinUndRueckspieleSpeichernAsync(spielRepository, ergebnis.Spiele, [], ct);

        var hinspielIndizes      = ergebnis.HinRueckspielPaare.Select(p => p.HinspielIndex).ToHashSet();
        var hinspielJeRueckspiel = ergebnis.HinRueckspielPaare.ToDictionary(p => p.RueckspielIndex, p => p.HinspielIndex);

        var hinspiele   = ergebnis.Spiele.Where((_, i) => hinspielIndizes.Contains(i)).ToList();
        var rueckspiele = ergebnis.Spiele
            .Select((spiel, i) => (Spiel: spiel, Index: i))
            .Where(x => hinspielJeRueckspiel.ContainsKey(x.Index))
            .Select(x => (Hinspiel: ergebnis.Spiele[hinspielJeRueckspiel[x.Index]], Rueckspiel: x.Spiel))
            .ToList();
        return HinUndRueckspieleSpeichernAsync(spielRepository, hinspiele, rueckspiele, ct);
    }

    /// <summary>Speichert erst <paramref name="hinspiele"/> (ohne Rückspiele: alle Spiele) — dabei vergibt die
    /// Datenbank die Ids —, dann die Rückspiele mit der HinspielId ihres Hinspiels. Ohne Rückspiele bleibt es bei
    /// einem Speichervorgang.</summary>
    public static async Task HinUndRueckspieleSpeichernAsync(
        ISpielRepository spielRepository, IReadOnlyList<Spiel> hinspiele,
        IReadOnlyList<(Spiel Hinspiel, Spiel Rueckspiel)> rueckspiele, CancellationToken ct)
    {
        await spielRepository.HinzufuegenMehrfachAsync(hinspiele, ct);
        if (rueckspiele.Count == 0) return;

        foreach (var (hinspiel, rueckspiel) in rueckspiele)
            rueckspiel.HinspielId = hinspiel.Id;
        await spielRepository.HinzufuegenMehrfachAsync([.. rueckspiele.Select(r => r.Rueckspiel)], ct);
    }
}
