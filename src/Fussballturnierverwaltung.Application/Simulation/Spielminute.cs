namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>Anzeige und Reihenfolge von Spielminuten mit Nachspielzeit (#361).</summary>
public static class Spielminute
{
    /// <summary>„45'“ bzw. „90+4'“ in der Nachspielzeit.</summary>
    public static string Text(int minute, int? nachspielminute)
        => nachspielminute is int nachspiel ? $"{minute}+{nachspiel}'" : $"{minute}'";

    /// <summary>Ereignisse in Spielreihenfolge: nach Minute, dann Nachspielminute; innerhalb derselben Minute bleibt
    /// die Reihenfolge der Eingabe erhalten (Tore derselben Minute fallen nacheinander).</summary>
    public static IEnumerable<T> Chronologisch<T>(
        this IEnumerable<T> ereignisse, Func<T, int> minute, Func<T, int?> nachspielminute)
        => ereignisse.OrderBy(minute).ThenBy(e => nachspielminute(e) ?? 0);
}
