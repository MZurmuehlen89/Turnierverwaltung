using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Fussballturnierverwaltung.AvaloniaSpike.ViewModels;
using Fussballturnierverwaltung.AvaloniaSpike.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Fussballturnierverwaltung.AvaloniaSpike;

public partial class App : global::Avalonia.Application
{
    public static IServiceProvider Services { get; set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
