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
            Func<TState, Exception?, string> formatter)
        {
            lock (Zeilen) Zeilen.Add(formatter(state, exception));
        }
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

    // ── DB-Anteil (#49) ─────────────────────────────────────────────────────

    private static TimeSpan Ms(double ms) => TimeSpan.FromMilliseconds(ms);

    [Fact]
    public void DbAnteil_OhneDatenbankzugriff_BleibtDieEndezeileUnveraendert()
    {
        var logger = new SammelLogger();

        using (logger.Vorgang("Test-Vorgang E1")) { }

        logger.Zeilen[1].Should().MatchRegex(@"^Ende:   Test-Vorgang E1 \(\d+ ms\)$");
    }

    [Fact]
    public void DbAnteil_NenntSpanneBefehleVerbindungUndSaveChanges()
    {
        var logger = new SammelLogger();

        using (logger.Vorgang("Test-Vorgang F1"))
        {
            AblaufVorgang.DbVerbindungErfassen(Ms(3));
            AblaufVorgang.DbBefehlErfassen(Ms(10));
            AblaufVorgang.DbBelegungErfassen(Ms(25));
            AblaufVorgang.DbBefehlErfassen(Ms(5));
            AblaufVorgang.DbBelegungErfassen(Ms(5));
            AblaufVorgang.DbSpeichernErfassen(Ms(7));
            AblaufVorgang.DbSpeichernErfassen(Ms(8));
        }

        // Spanne: Ausführung 15 bzw. Belegung 30, jeweils + 3 ms Verbindungsaufbau.
        logger.Zeilen[1].Should().MatchRegex(
            @"^Ende:   Test-Vorgang F1 \(\d+ ms; DB 18–33 ms in 2 Befehlen, Verbindungsaufbau 3 ms, SaveChanges 2× 15 ms\)$");
    }

    [Fact]
    public void DbAnteil_EinzelnerBefehl_InEinzahl_VerbindungAusDemPoolOhneAngabe()
    {
        var logger = new SammelLogger();

        using (logger.Vorgang("Test-Vorgang G1"))
        {
            AblaufVorgang.DbVerbindungErfassen(Ms(0.2));
            AblaufVorgang.DbBefehlErfassen(Ms(2));
            AblaufVorgang.DbBelegungErfassen(Ms(4));
        }

        logger.Zeilen[1].Should().EndWith("; DB 2–4 ms in 1 Befehl)");
    }

    [Fact]
    public void DbAnteil_VerschachtelteVorgaenge_AeussererEnthaeltDieBefehleDesInneren()
    {
        var logger = new SammelLogger();

        using (logger.Vorgang("Test-Vorgang H1"))
        {
            using (logger.Vorgang("Test-Vorgang H2"))
            {
                AblaufVorgang.DbBefehlErfassen(Ms(10));
                AblaufVorgang.DbBelegungErfassen(Ms(10));
            }

            // nach dem Ende des inneren zählt ein Befehl nur noch beim äußeren
            AblaufVorgang.DbBefehlErfassen(Ms(1));
            AblaufVorgang.DbBelegungErfassen(Ms(1));
        }

        logger.Zeilen.Should().Contain(z => z.StartsWith("Ende:   Test-Vorgang H2") && z.EndsWith("; DB 10–10 ms in 1 Befehl)"));
        logger.Zeilen.Should().Contain(z => z.StartsWith("Ende:   Test-Vorgang H1") && z.EndsWith("; DB 11–11 ms in 2 Befehlen)"));
    }

    [Fact]
    public void DbAnteil_NurBelegungOhneGezaehltenBefehl_WirdTrotzdemGenannt()
    {
        var logger = new SammelLogger();

        // ein gescheiterter Befehl gibt nur die Belegung frei
        using (logger.Vorgang("Test-Vorgang K1"))
            AblaufVorgang.DbBelegungErfassen(Ms(5));

        logger.Zeilen[1].Should().EndWith("; DB 0–5 ms in 0 Befehlen)");
    }

    [Fact]
    public void DbAnteil_AusserhalbEinesVorgangs_OhneWirkung()
    {
        var logger = new SammelLogger();

        AblaufVorgang.DbBefehlErfassen(Ms(10));
        AblaufVorgang.DbBelegungErfassen(Ms(10));
        using (logger.Vorgang("Test-Vorgang I1")) { }

        logger.Zeilen[1].Should().EndWith(" ms)");
    }

    [Fact]
    public async Task DbAnteil_FolgtDemAblaufUeberAwait_UndTrenntParalleleAblaeufe()
    {
        var logger = new SammelLogger();
        var beideOffen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int offen = 0;

        async Task Ablauf(string name, double ms)
        {
            using (logger.Vorgang(name))
            {
                if (Interlocked.Increment(ref offen) == 2) beideOffen.SetResult();
                await beideOffen.Task;  // beide Vorgänge sind gleichzeitig offen
                await Task.Yield();     // Fortsetzung auf einem anderen Thread
                AblaufVorgang.DbBefehlErfassen(Ms(ms));
                AblaufVorgang.DbBelegungErfassen(Ms(ms));
            }
        }

        await Task.WhenAll(Ablauf("Test-Vorgang J1", 4), Ablauf("Test-Vorgang J2", 9));

        lock (logger.Zeilen)
        {
            logger.Zeilen.Should().Contain(z => z.StartsWith("Ende:   Test-Vorgang J1") && z.EndsWith("; DB 4–4 ms in 1 Befehl)"));
            logger.Zeilen.Should().Contain(z => z.StartsWith("Ende:   Test-Vorgang J2") && z.EndsWith("; DB 9–9 ms in 1 Befehl)"));
        }
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
