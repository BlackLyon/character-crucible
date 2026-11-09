using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CharacterCrucible.CoreApi.Tests.Persistence;

/// <summary>Rules the whole schema must follow, read from the model, so no database is needed.</summary>
public class SchemaTests
{
    // Postgres truncates longer identifiers without an error, so two long names can collide.
    private const int MaxIdentifierBytes = 63;

    [Fact]
    public void The_model_matches_the_latest_migration()
    {
        using var db = PostgresFixture.ContextFor(connectionString: null);

        Assert.False(db.Database.HasPendingModelChanges(),
            "The model has changed since the last migration. Add one, or undo the configuration change.");
    }

    [Fact]
    public void Every_identifier_fits_within_the_Postgres_limit()
    {
        using var db = PostgresFixture.ContextFor(connectionString: null);
        var model = db.GetService<IDesignTimeModel>().Model.GetRelationalModel();

        HashSet<(string Kind, string Name)> identifiers =
        [
            .. model.Tables.SelectMany(Identifiers),
            .. model.Tables.Select(t => t.Schema).OfType<string>().Select(s => ("schema", s)),
            .. model.Sequences.Select(s => ("sequence", s.Name)),
        ];

        // One known name of each kind, so a kind that stops being collected fails here.
        Assert.Subset(identifiers, new HashSet<(string, string)>
        {
            ("schema", "characters"),
            ("table", "character"),
            ("column", "target_key"),
            ("key", "pk_character"),
            ("index", "ux_advancement_request_one_in_flight_per_item"),
            ("foreign key", "fk_advancement_request_evaluated_ruleset_version"),
            ("check", "ck_ruleset_version_publication_all_or_nothing"),
        });

        var tooLong = identifiers
            .Select(i => (i.Kind, i.Name, Bytes: Encoding.UTF8.GetByteCount(i.Name)))
            .Where(i => i.Bytes > MaxIdentifierBytes)
            .Select(i => $"{i.Kind} {i.Name} ({i.Bytes} bytes)")
            .ToList();
        Assert.True(tooLong.Count == 0, "Postgres would truncate: " + string.Join(", ", tooLong));
    }

    private static IEnumerable<(string Kind, string Name)> Identifiers(ITable table) =>
    [
        ("table", table.Name),
        .. table.Columns.Select(c => ("column", c.Name)),
        .. table.UniqueConstraints.Select(u => ("key", u.Name)),
        .. table.Indexes.Select(i => ("index", i.Name)),
        .. table.ForeignKeyConstraints.Select(f => ("foreign key", f.Name)),
        .. table.CheckConstraints.Select(c => c.Name).OfType<string>().Select(n => ("check", n)),
    ];
}
