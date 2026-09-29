using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Vereinheitlichte Spielbericht-Karte (Scoreboard + Zeitleiste), 1:1-Port aus
/// Wpf/Views/SpielberichtControl.xaml.cs (#302). <see cref="LeerZustandText"/> ist der einzige
/// View-spezifische Teil — der DataContext liefert lose über Property-Namen (ProtokollKopf/
/// ProtokollEreignisse/HatProtokoll/HatProtokollEreignisse/ZeigeLeerZustand), die
/// <see cref="ViewModels.SimulationsViewModel"/> über <see cref="ViewModels.SimulationsProtokollHelfer"/>
/// bereitstellt.</summary>
public sealed partial class SpielberichtControl : UserControl
{
    public static readonly DependencyProperty LeerZustandTextProperty = DependencyProperty.Register(
        nameof(LeerZustandText), typeof(string), typeof(SpielberichtControl), new PropertyMetadata(null));

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
