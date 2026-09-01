using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI.Xaml;

namespace Fussballturnierverwaltung.WinUi;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    public MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    private void OnDarkModeToggleClick(object sender, RoutedEventArgs e)
        => RootGrid.RequestedTheme = DarkModeToggle.IsChecked == true ? ElementTheme.Dark : ElementTheme.Light;
}
