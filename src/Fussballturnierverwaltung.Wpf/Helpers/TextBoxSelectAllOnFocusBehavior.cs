using System.Windows;
using System.Windows.Controls;

namespace Fussballturnierverwaltung.Wpf.Helpers;

/// <summary>
/// Attached Behavior (Issue #101): markiert bei Tab-/Klick-Fokus den kompletten Inhalt einer
/// TextBox, damit ein vorbelegter Wert (z.B. "0" in Ergebnis-Eingabefeldern) direkt per
/// Tastendruck überschrieben werden kann, statt ihn erst manuell löschen zu müssen.
/// </summary>
public static class TextBoxSelectAllOnFocusBehavior
{
    public static readonly DependencyProperty SelectAllOnFocusProperty =
        DependencyProperty.RegisterAttached(
            "SelectAllOnFocus", typeof(bool), typeof(TextBoxSelectAllOnFocusBehavior),
            new PropertyMetadata(false, OnSelectAllOnFocusChanged));

    public static bool GetSelectAllOnFocus(TextBox textBox) => (bool)textBox.GetValue(SelectAllOnFocusProperty);
    public static void SetSelectAllOnFocus(TextBox textBox, bool value) => textBox.SetValue(SelectAllOnFocusProperty, value);

    private static void OnSelectAllOnFocusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox) return;

        if ((bool)e.NewValue)
            textBox.GotFocus += TextBox_GotFocus;
        else
            textBox.GotFocus -= TextBox_GotFocus;
    }

    private static void TextBox_GotFocus(object sender, RoutedEventArgs e) => ((TextBox)sender).SelectAll();
}
