using Fussballturnierverwaltung.Application.Spielplan;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>No-op-Stub für IPlatzhalterKaskadeService — hält reine Auslosungs-Algorithmus-Tests frei von
/// einem vollen, DB-gestützten SpielplanService (siehe Issue #306).</summary>
public class FakePlatzhalterKaskadeService : IPlatzhalterKaskadeService
{
    public int AufrufeAnzahl { get; private set; }

    public Task KaskadeAusloesenAsync(int turnierInstanzId, CancellationToken ct = default)
    {
        AufrufeAnzahl++;
        return Task.CompletedTask;
    }
}
