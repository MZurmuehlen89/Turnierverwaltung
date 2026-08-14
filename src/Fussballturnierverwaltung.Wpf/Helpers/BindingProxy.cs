using System.Windows;

namespace Fussballturnierverwaltung.Wpf.Helpers;

/// <summary>
/// DataGridColumn ist kein FrameworkElement und steht nicht im Visual-/Logical-Tree,
/// daher greifen RelativeSource-Bindings (z.B. AncestorType=DataGrid) auf Spalten-Properties
/// wie ItemsSource nicht. Dieses Freezable dient als Bindungs-Brücke: als Resource mit
/// Binding auf den DataContext eingebunden, kann eine Spalte dann {Binding Data.Xyz,
/// Source={StaticResource ...}} verwenden.
/// </summary>
public class BindingProxy : Freezable
{
    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy));

    public object? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    protected override Freezable CreateInstanceCore() => new BindingProxy();
}
