using GestureClip.Core.Clipboard;
using GestureClip.Features.Clipboard;
using GestureClip.Infrastructure.Database;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestureClip.Tests.Clipboard;

public sealed class ClipboardRepositorySearchTests
{
    private const int SearchLimit = 20;

    [Theory]
    [InlineData("%", "discount 50% today", "discount 500 today")]
    [InlineData("_", "file_name", "filename")]
    [InlineData("[", "array[index]", "array index")]
    [InlineData("\\", "C:\\Users\\Lucas", "C:/Users/Lucas")]
    [InlineData("\\%", "literal \\% token", "literal \\ token")]
    [InlineData("\\_", "literal \\_ token", "literal \\x token")]
    public async Task SearchAsync_matches_special_characters_literally(
        string keyword, string expected, string unrelated)
    {
        await using var database = await SearchDatabase.CreateAsync();
        var repository = new ClipboardRepository(database);
        var matchingItem = CreateItem(expected);
        await repository.InsertAsync(matchingItem, CancellationToken.None);
        await repository.InsertAsync(CreateItem(unrelated), CancellationToken.None);

        var results = await repository.SearchAsync(keyword, SearchLimit, CancellationToken.None);

        Assert.Equal(matchingItem.Id, Assert.Single(results).Id);
    }

    [Theory]
    [InlineData("text", "preview")]
    [InlineData("text", "ocr")]
    [InlineData("image/png", "preview")]
    [InlineData("image/png", "ocr")]
    public async Task SearchAsync_escapes_each_searchable_field(string contentType, string field)
    {
        await using var database = await SearchDatabase.CreateAsync();
        var repository = new ClipboardRepository(database);
        var matchingItem = CreateItem("unrelated") with
        {
            ContentType = contentType,
            PreviewText = field == "preview" ? "literal %_[ token" : "unrelated",
            OcrText = field == "ocr" ? "literal %_[ token" : null
        };
        await repository.InsertAsync(matchingItem, CancellationToken.None);
        await repository.InsertAsync(CreateItem("unrelated"), CancellationToken.None);

        var results = await repository.SearchAsync("%_[", SearchLimit, CancellationToken.None);

        Assert.Equal(matchingItem.Id, Assert.Single(results).Id);
    }

    private static ClipboardItem CreateItem(string text)
    {
        var timestamp = DateTimeOffset.UtcNow;
        return new ClipboardItem(
            Guid.NewGuid(), "text", text, text, Guid.NewGuid().ToString(), null,
            "Test", "test.exe", false, false, false, 0, timestamp, timestamp, null);
    }

    private sealed class SearchDatabase(SqliteConnection anchor) : ISqliteConnectionFactory, IAsyncDisposable
    {
        public static async Task<SearchDatabase> CreateAsync()
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = $"clipboard-search-{Guid.NewGuid():N}",
                Mode = SqliteOpenMode.Memory,
                Cache = SqliteCacheMode.Shared
            }.ToString();
            var anchor = new SqliteConnection(connectionString);
            await anchor.OpenAsync();
            var runner = new SqlMigrationRunner(NullLogger<SqlMigrationRunner>.Instance);
            await runner.RunAsync(anchor,
                [
                    new SqlMigration(1, "initial", InitialMigration.Sql),
                    new SqlMigration(5, "clipboard_image_thumbnails", ClipboardThumbnailMigration.Sql),
                    new SqlMigration(7, "clipboard_ocr_text", ClipboardOcrMigration.Sql)
                ], CancellationToken.None);
            return new SearchDatabase(anchor);
        }

        public async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        {
            var connection = new SqliteConnection(anchor.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }

        public ValueTask DisposeAsync() => anchor.DisposeAsync();
    }
}
