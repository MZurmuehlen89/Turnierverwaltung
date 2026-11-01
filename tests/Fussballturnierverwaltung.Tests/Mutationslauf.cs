namespace Fussballturnierverwaltung.Tests;

/// <summary>
/// Erkennt einen Lauf von build/Mutation-Ausfuehren.ps1 (#541). Tests, die unter Strykers Instrumentierung so langsam
/// werden, dass sie die Abdeckungsmessung sprengen, setzen dort mit
/// <c>[Fact(Skip = …, SkipWhen = nameof(Mutationslauf.Aktiv), SkipType = typeof(Mutationslauf))]</c> aus.
/// </summary>
public static class Mutationslauf
{
    public const string UMGEBUNGSVARIABLE = "TURNIERVERWALTUNG_MUTATIONSLAUF";

    public static bool Aktiv => Environment.GetEnvironmentVariable(UMGEBUNGSVARIABLE) == "1";
}
