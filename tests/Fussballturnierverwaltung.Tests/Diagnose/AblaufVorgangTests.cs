using FluentAssertions;
using Fussballturnierverwaltung.Application.Diagnose;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Diagnose;

public class AblaufVorgangTests
{
    private sealed class SammelLogger : ILogger
    {
        public List<string> Zeilen { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Zeilen.Add(formatter(state, exception));
    }

    // Die Liste der offenen Vorgänge ist prozessweit — parallel laufende Tests anderer Klassen können eigene
    // Vorgänge offen haben, daher eindeutige Namen und Contain statt exakter Vergleiche.

    [Fact]
    public void Vorgang_ProtokolliertBeginnUndEndeMitDauer()
    {
        var logger = new SammelLogger();

        using (logger.Vorgang("Test-Vorgang A1"))
        {
            AblaufVorgang.OffeneBeschreiben().Should().Contain("Test-Vorgang A1");
        }

        logger.Zeilen.Should().HaveCount(2);
        logger.Zeilen[0].Should().StartWith("Beginn: Test-Vorgang A1");
        logger.Zeilen[1].Should().StartWith("Ende:   Test-Vorgang A1 (").And.EndWith(" ms)");
        AblaufVorgang.OffeneBeschreiben().Should().NotContain("Test-Vorgang A1");
    }

    [Fact]
    public void Vorgang_NenntBeimBeginnBereitsLaufendeVorgaenge()
    {
        var logger = new SammelLogger();

        using (logger.Vorgang("Test-Vorgang B1"))
        using (logger.Vorgang("Test-Vorgang B2"))
        {
            // nur Beginn und Ende der verschachtelten Vorgänge werden geprüft
        }

        logger.Zeilen[1].Should().StartWith("Beginn: Test-Vorgang B2 — läuft bereits:").And.Contain("Test-Vorgang B1");
    }

    [Fact]
    public void Vorgang_EndetNurEinmal_AuchBeiDoppeltemDispose()
    {
        var logger = new SammelLogger();

        var vorgang = logger.Vorgang("Test-Vorgang C1");
        vorgang.Dispose();
        vorgang.Dispose();

        logger.Zeilen.Should().HaveCount(2);
    }

    // Beginnen Vorgänge auf mehreren Threads gleichzeitig, darf das Beschreiben der offenen Vorgänge nicht scheitern:
    // OrderBy kopierte das Dictionary früher über ICollection.CopyTo (erst Anzahl lesen, dann kopieren) und brach
    // bei gleichzeitigem Einfügen mit ArgumentException ab — sporadisch in parallelen Testläufen, möglich auch in der App.
    [Fact]
    public void Vorgang_GleichzeitigAufVielenThreads_WirftNicht()
    {
        var parallel = () => Parallel.For(0, 200_000, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
        {
            using var _ = NullLogger.Instance.Vorgang("Test-Vorgang D");
        });

        parallel.Should().NotThrow();
    }
}
