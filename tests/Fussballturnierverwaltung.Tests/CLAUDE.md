# CLAUDE.md — Tests

Gilt zusätzlich zur Root-`CLAUDE.md` bei Arbeit an diesem Projekt.

## Testing

No mocking framework is used — generators and validators are instantiated directly. Integration tests use the EF Core InMemory provider.

Die Tests der Spielplan-Dienste bauen ihre Dienste über `Spielplan/SpielplanDienste` (ein Objektgraph wie in der App) und ihre Turniere über `Spielplan/SpielplanTestdaten` auf.

