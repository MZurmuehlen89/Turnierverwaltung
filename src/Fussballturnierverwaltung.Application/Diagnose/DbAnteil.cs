using System.Globalization;

namespace Fussballturnierverwaltung.Application.Diagnose;

/// <summary>Datenbankanteil eines Vorgangs im Ablaufprotokoll (#49, Stufe 0): Anzahl der Befehle und ihre Dauer als
/// Spanne. Die Untergrenze zählt nur die Ausführung der Befehle, die Obergrenze die Zeit, in der die Verbindung
/// belegt ist (ein Befehl läuft oder sein Reader ist noch offen, also mit dem Einlesen der Zeilen durch EF). Der
/// Verbindungsaufbau steckt in beiden Grenzen und wird zusätzlich ausgewiesen. SaveChanges läuft getrennt mit;
/// seine Befehle sind in der Spanne bereits enthalten. Wird von mehreren Threads befüllt, daher nur über
/// <see cref="Interlocked"/>.</summary>
internal sealed class DbAnteil
{
    private long _befehle;
    private long _ausfuehrungTicks;
    private long _belegungTicks;
    private long _verbindungTicks;
    private long _speichernAnzahl;
    private long _speichernTicks;

    public void BefehlErfassen(TimeSpan ausfuehrung)
    {
        Interlocked.Increment(ref _befehle);
        Interlocked.Add(ref _ausfuehrungTicks, ausfuehrung.Ticks);
    }

    public void BelegungErfassen(TimeSpan dauer) => Interlocked.Add(ref _belegungTicks, dauer.Ticks);

    public void VerbindungErfassen(TimeSpan dauer) => Interlocked.Add(ref _verbindungTicks, dauer.Ticks);

    public void SpeichernErfassen(TimeSpan dauer)
    {
        Interlocked.Increment(ref _speichernAnzahl);
        Interlocked.Add(ref _speichernTicks, dauer.Ticks);
    }

    /// <summary>Text für die Endezeile, z. B. „DB 140–260 ms in 63 Befehlen, Verbindungsaufbau 3 ms, SaveChanges 2× 80 ms“;
    /// <c>null</c>, wenn der Vorgang die Datenbank nicht berührt hat.</summary>
    public string? Beschreiben()
    {
        long befehle = Interlocked.Read(ref _befehle);
        long verbindung = Interlocked.Read(ref _verbindungTicks);
        long speichern = Interlocked.Read(ref _speichernAnzahl);
        long belegung = Interlocked.Read(ref _belegungTicks);
        // Auch eine Belegung allein zählt: gescheiterte Befehle geben sie frei, ohne als Befehl gezählt zu werden.
        if (befehle == 0 && belegung == 0 && verbindung == 0 && speichern == 0) return null;

        long von = Interlocked.Read(ref _ausfuehrungTicks) + verbindung;
        long bis = belegung + verbindung;
        string text = $"DB {Ms(von)}–{Ms(bis)} ms in {befehle} {(befehle == 1 ? "Befehl" : "Befehlen")}";
        // Aus dem Pool kommt eine Verbindung in Mikrosekunden — „0 ms“ wäre nur Rauschen.
        if (Ms(verbindung) != "0") text += $", Verbindungsaufbau {Ms(verbindung)} ms";
        if (speichern > 0) text += $", SaveChanges {speichern}× {Ms(Interlocked.Read(ref _speichernTicks))} ms";
        return text;
    }

    private static string Ms(long ticks) =>
        Math.Round(TimeSpan.FromTicks(ticks).TotalMilliseconds).ToString(CultureInfo.InvariantCulture);
}
