namespace GestureClip.Infrastructure.Database;

public sealed record SqlMigration(int Version, string Name, string Sql);
