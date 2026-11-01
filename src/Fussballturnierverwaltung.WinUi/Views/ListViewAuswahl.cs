using Microsoft.UI.Xaml.Controls;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Worauf ein Rechtsklick in einer ListView mit Mehrfachauswahl wirkt.</summary>
internal static class ListViewAuswahl
{
    /// <summary>Windows-Explorer-Konvention: ist das rechtsgeklickte Element Teil der aktuellen Mehrfachauswahl, wirkt
    /// die Aktion auf die gesamte Auswahl — sonst nur auf dieses eine Element.</summary>
    public static List<T> Ziel<T>(ListView liste, T rechtsgeklickt) where T : notnull =>
        liste.SelectedItems.Contains(rechtsgeklickt)
            ? [.. liste.SelectedItems.OfType<T>()]
            : [rechtsgeklickt];
}
