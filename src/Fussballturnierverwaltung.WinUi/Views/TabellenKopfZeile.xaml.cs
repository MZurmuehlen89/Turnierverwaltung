using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Fussballturnierverwaltung.WinUi.Views;

public sealed partial class TabellenKopfZeile : UserControl
{
    public static readonly DependencyProperty ZeigtBuchholzProperty = DependencyProperty.Register(
        nameof(ZeigtBuchholz), typeof(bool), typeof(TabellenKopfZeile), new PropertyMetadata(false));

    /// <summary>Buchholz-Spalte nur beim Schweizer System (wie die Zeilen, <c>ZeigtBuchholzSpalte</c>).</summary>
    public bool ZeigtBuchholz
    {
        get => (bool)GetValue(ZeigtBuchholzProperty);
        set => SetValue(ZeigtBuchholzProperty, value);
    }

    public static readonly DependencyProperty ZeigtVortragProperty = DependencyProperty.Register(
        nameof(ZeigtVortrag), typeof(bool), typeof(TabellenKopfZeile), new PropertyMetadata(false));

    /// <summary>Spalte „Vortrag“ nur bei einer Runde mit Punkteübernahme (#355, wie die Zeilen, <c>ZeigtVortragSpalte</c>).</summary>
    public bool ZeigtVortrag
    {
        get => (bool)GetValue(ZeigtVortragProperty);
        set => SetValue(ZeigtVortragProperty, value);
    }
    public TabellenKopfZeile()
    {
        InitializeComponent();
    }
}
