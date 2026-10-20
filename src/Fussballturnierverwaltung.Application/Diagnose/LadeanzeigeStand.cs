namespace Fussballturnierverwaltung.Application.Diagnose;

/// <summary>Was die zentrale Ladeanzeige gerade zeigt (#323): Balken ja/nein und ggf. ein Text.</summary>
public sealed record LadeanzeigeStand(bool BalkenSichtbar, string? Text)
{
    /// <summary>Ab dieser Dauer erscheint der Balken — schnellere Vorgänge ließen ihn nur aufblitzen.</summary>
    public static readonly TimeSpan BALKEN_AB = TimeSpan.FromMilliseconds(300);

    /// <summary>Ab dieser Dauer steht dabei, was läuft.</summary>
    public static readonly TimeSpan TEXT_AB = TimeSpan.FromSeconds(2);

    public const string STANDARD_TEXT = "Bitte warten …";

    public static readonly LadeanzeigeStand Keine = new(false, null);

    /// <summary>Maßgeblich ist der am längsten laufende Vorgang; der Text kommt vom ältesten Vorgang mit
    /// Anzeigetext — das ist der äußere, den der Nutzer ausgelöst hat, nicht ein Teilschritt darin.</summary>
    public static LadeanzeigeStand Ermitteln(IReadOnlyList<OffenerVorgang> offene)
    {
        if (offene.Count == 0) return Keine;

        var laengste = offene.Max(v => v.Dauer);
        if (laengste < BALKEN_AB) return Keine;
        if (laengste < TEXT_AB) return new(true, null);

        string text = offene.FirstOrDefault(v => v.Anzeige is not null)?.Anzeige ?? STANDARD_TEXT;
        return new(true, text);
    }
}
