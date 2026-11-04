namespace Fussballturnierverwaltung.Application.Abstraktionen;

public record ValidationErgebnis(bool IstGueltig, IReadOnlyList<Feldfehler> Meldungen)
{
    public bool IstUngueltig => !IstGueltig;

    /// <summary>Nur die Texte, ohne Feldbezug.</summary>
    public IReadOnlyList<string> Fehler => [.. Meldungen.Select(m => m.Text)];

    public static ValidationErgebnis Ok() => new(true, []);
    public static ValidationErgebnis MitFehlern(params string[] fehler) => new(false, [.. fehler.Select(f => new Feldfehler(f))]);

    /// <summary>Ok ohne Meldungen, sonst ungültig.</summary>
    public static ValidationErgebnis Aus(IReadOnlyCollection<Feldfehler> meldungen) =>
        meldungen.Count == 0 ? Ok() : new(false, [.. meldungen]);

    public string FehlerZusammenfassung => string.Join(Environment.NewLine, Fehler);
}
