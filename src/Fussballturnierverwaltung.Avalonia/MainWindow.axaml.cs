using Avalonia.Controls;
using Avalonia.Styling;

namespace Fussballturnierverwaltung.AvaloniaSpike;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private void OnDarkModeToggleClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Avalonia.Application.Current is null) return;
        Avalonia.Application.Current.RequestedThemeVariant =
            DarkModeToggle.IsChecked == true ? ThemeVariant.Dark : ThemeVariant.Light;
    }
}
