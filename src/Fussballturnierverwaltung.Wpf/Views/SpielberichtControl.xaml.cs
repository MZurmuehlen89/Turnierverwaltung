using System.Windows;
using System.Windows.Controls;

namespace Fussballturnierverwaltung.Wpf.Views;

public partial class SpielberichtControl : UserControl
{
    public static readonly DependencyProperty LeerZustandTextProperty =
        DependencyProperty.Register(nameof(LeerZustandText), typeof(string), typeof(SpielberichtControl));

    public string? LeerZustandText
    {
        get => (string?)GetValue(LeerZustandTextProperty);
        set => SetValue(LeerZustandTextProperty, value);
    }

    public SpielberichtControl()
    {
        InitializeComponent();
    }
}
