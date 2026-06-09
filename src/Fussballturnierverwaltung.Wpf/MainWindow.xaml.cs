using Fussballturnierverwaltung.Wpf.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace Fussballturnierverwaltung.Wpf;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.HatRibbonBereich) && _viewModel.HatRibbonBereich)
            SeitenTab.IsSelected = true;
    }

    private void BeendenButton_Click(object sender, RoutedEventArgs e)
        => Close();
}
