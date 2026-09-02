using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Infrastructure.Erweiterungen;
using Fussballturnierverwaltung.Infrastructure.Seed;
using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Serilog;

namespace Fussballturnierverwaltung.WinUi;

/// <summary>Spike (#278): DI-/Seed-Aufbau analog zu Fussballturnierverwaltung.Wpf\App.xaml.cs, auf den
/// TurnierVorlage-Ausschnitt reduziert.</summary>
public partial class App : global::Microsoft.UI.Xaml.Application
{
    private Window? _window;
    private IHost _host = null!;

    public App() => InitializeComponent();

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var logPfad = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Turnierverwaltung", "logs", "winui-spike-.log");

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSerilog((_, lc) => lc
            .ReadFrom.Configuration(builder.Configuration)
            .WriteTo.File(
                path: logPfad,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}"));
        builder.Services.AddInfrastruktur(builder.Configuration);

        builder.Services.AddScoped<VorlagenUebergangsregelValidator>();
        builder.Services.AddScoped<TurnierVorlageValidator>();
        builder.Services.AddScoped<RegelwerkValidator>();
        builder.Services.AddScoped<TurnierVorlageService>();
        builder.Services.AddScoped<RegelwerkService>();

        builder.Services.AddTransient<TurnierVorlageViewModel>();
        builder.Services.AddSingleton<MainViewModel>();

        _host = builder.Build();

        using (var scope = _host.Services.CreateScope())
        {
            var seed = scope.ServiceProvider.GetRequiredService<SeedDatenService>();
            await seed.InitialisierenAsync();
        }

        _window = new MainWindow(_host.Services.GetRequiredService<MainViewModel>());
        _window.Activate();
    }
}
