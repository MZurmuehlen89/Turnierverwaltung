using Fussballturnierverwaltung.Application.Einstellungen;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Fussballturnierverwaltung.WinUi.Helpers;

/// <summary>Schalter für das Ablaufprotokoll (#413): hebt die Log-Schwelle der eigenen Namensräume von Warnung
/// auf Information, sodass Nutzeraktionen, Wechsel und Ladevorgänge samt Dauer im Log mitlaufen
/// (<see cref="Application.Diagnose.AblaufVorgang"/> und die Information-Meldungen der Services). Wirkt sofort,
/// ohne Neustart; die Wahl steht in den Benutzereinstellungen.</summary>
public sealed class Ablaufprotokoll
{
    /// <summary>Präfix aller eigenen Log-Kategorien — EF Core und Framework bleiben auf Warnung.</summary>
    public const string KATEGORIE = "Fussballturnierverwaltung";

    private readonly EinstellungenService _einstellungenService;

    /// <summary>Von Serilog bei jedem Ereignis ausgewertet (siehe App.xaml.cs).</summary>
    public LoggingLevelSwitch Schwelle { get; }

    public Ablaufprotokoll(EinstellungenService einstellungenService)
    {
        _einstellungenService = einstellungenService;
        Schwelle = new LoggingLevelSwitch(StufeFuer(einstellungenService.Laden().AblaufprotokollAktiv));
    }

    public bool IstAktiv
    {
        get => Schwelle.MinimumLevel <= LogEventLevel.Information;
        set
        {
            if (value == IstAktiv) return;
            Schwelle.MinimumLevel = StufeFuer(value);

            var einstellungen = _einstellungenService.Laden();
            einstellungen.AblaufprotokollAktiv = value;
            _einstellungenService.Speichern(einstellungen);
        }
    }

    private static LogEventLevel StufeFuer(bool aktiv) => aktiv ? LogEventLevel.Information : LogEventLevel.Warning;

    /// <summary>Eine Bedienhandlung (Ribbon-Klick, Menüwechsel) — ohne Dauer, nur der Zeitpunkt. Statisch, weil
    /// die Ribbon-Buttons in <c>MainWindow</c> ohne Instanzbezug erzeugt werden. Der Logger wird je Aufruf
    /// geholt: Serilogs statischer Logger steht erst nach dem Host-Aufbau.</summary>
    public static void Bedienung(string was) =>
        Log.ForContext(Constants.SourceContextPropertyName, KATEGORIE + ".WinUi.Bedienung")
           .Information("Bedienung: {Was}", was);
}
