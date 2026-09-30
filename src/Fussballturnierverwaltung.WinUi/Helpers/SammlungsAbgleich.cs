using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.Helpers;

/// <summary>Gleicht eine gebundene <see cref="ObservableCollection{T}"/> per Schlüssel mit einer Zielliste ab,
/// statt sie neu anzulegen (Issue #388): Eine neue ItemsSource-Instanz lässt eine ListView an den Anfang
/// springen und verwirft Auswahl und Scrollposition. Vorhandene Elemente mit passendem Schlüssel bleiben als
/// Instanz erhalten (bzw. werden verschoben), fehlende werden eingefügt, überzählige entfernt.</summary>
internal static class SammlungsAbgleich
{
    /// <param name="istGleich">Optional: prüft, ob ein vorhandenes Element trotz gleichen Schlüssels inhaltlich
    /// veraltet ist — dann wird es durch das Zielelement ersetzt.</param>
    public static void Abgleichen<T, TSchluessel>(ObservableCollection<T> ist, IReadOnlyList<T> soll,
        Func<T, TSchluessel> schluessel, Func<T, T, bool>? istGleich = null)
    {
        var vergleicher = EqualityComparer<TSchluessel>.Default;

        // Erst Überzählige entfernen (Schlüssel kommen im Ziel nicht mehr vor) — hält die folgenden Suchen kurz.
        var sollSchluessel = soll.Select(schluessel).ToHashSet();
        for (var i = ist.Count - 1; i >= 0; i--)
            if (!sollSchluessel.Contains(schluessel(ist[i]))) ist.RemoveAt(i);

        for (var i = 0; i < soll.Count; i++)
        {
            var ziel = soll[i];
            var zielSchluessel = schluessel(ziel);

            var j = i;
            while (j < ist.Count && !vergleicher.Equals(schluessel(ist[j]), zielSchluessel)) j++;

            if (j == ist.Count)
            {
                ist.Insert(i, ziel);
                continue;
            }
            if (j != i) ist.Move(j, i);
            if (istGleich is not null && !istGleich(ist[i], ziel)) ist[i] = ziel;
        }

        // Rest (z. B. doppelte Schlüssel, die im Ziel seltener vorkommen)
        while (ist.Count > soll.Count) ist.RemoveAt(ist.Count - 1);
    }
}
