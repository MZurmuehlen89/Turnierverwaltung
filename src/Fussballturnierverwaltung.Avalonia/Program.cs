using Avalonia;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.AvaloniaSpike.ViewModels;
using Fussballturnierverwaltung.Infrastructure.Erweiterungen;
using Fussballturnierverwaltung.Infrastructure.Seed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.IO;

namespace Fussballturnierverwaltung.AvaloniaSpike;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var logPfad = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Turnierverwaltung", "logs", "avalonia-spike-.log");

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

        var host = builder.Build();

        using (var scope = host.Services.CreateScope())
        {
            var seed = scope.ServiceProvider.GetRequiredService<SeedDatenService>();
            seed.InitialisierenAsync().GetAwaiter().GetResult();
        }

        App.Services = host.Services;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        host.StopAsync().GetAwaiter().GetResult();
        host.Dispose();
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
}
