using Microsoft.EntityFrameworkCore;

namespace CharacterCrucible.CoreApi.Persistence;

/// <summary>The provider and naming options, shared by the app and the tests so both build one model.</summary>
public static class DatabaseOptions
{
    // snake_case rather than EF's PascalCase: Postgres folds unquoted identifiers, so PascalCase
    // means every hand-written query needs quoting. pgAdmin is in the compose file, so raw queries
    // will happen.
    public static DbContextOptionsBuilder UseCharacterCrucibleDatabase(
        this DbContextOptionsBuilder options, string? connectionString) =>
        options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();
}
