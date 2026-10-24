using FluentAssertions;

namespace GuiTest.Framework.Tests;

public sealed class LogTailTests : IDisposable
{
    private readonly string _pfad = Path.Combine(Path.GetTempPath(), $"logtail-{Guid.NewGuid():N}.log");

    public void Dispose() => File.Delete(_pfad);

    [Fact]
    public void Read_LiefertNurDieLetztenZeilen()
    {
        File.WriteAllLines(_pfad, ["eins", "zwei", "drei", "vier"]);

        LogTail.Read(_pfad, 2).Should().Be($"drei{Environment.NewLine}vier");
    }

    [Fact]
    public void Read_KuerzeresLog_LiefertAlles()
    {
        File.WriteAllLines(_pfad, ["eins", "zwei"]);

        LogTail.Read(_pfad, 10).Should().Be($"eins{Environment.NewLine}zwei");
    }

    [Fact]
    public void Read_FehlendeDatei_SagtDasStattZuWerfen()
    {
        LogTail.Read(_pfad, 10).Should().Contain("log not found");
    }

    [Fact]
    public void Read_DateiNochZumSchreibenGeoeffnet_WirdTrotzdemGelesen()
    {
        using (var schreiber = new StreamWriter(new FileStream(_pfad, FileMode.Create, FileAccess.Write, FileShare.Read)))
        {
            schreiber.WriteLine("laeuft noch");
            schreiber.Flush();

            LogTail.Read(_pfad, 5).Should().Be("laeuft noch");
        }
    }
}
