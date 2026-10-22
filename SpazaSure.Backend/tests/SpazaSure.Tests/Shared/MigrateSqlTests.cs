using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace SpazaSure.Tests.Shared;

/// <summary>
/// Production gets its schema from migrate.sql, not from EF migrations. A column added to the model
/// (and the EF snapshot) but not to migrate.sql makes every query on that table fail in production
/// ("column does not exist"), so the two must always match.
/// </summary>
public class MigrateSqlTests
{
    private static string BackendFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "migrate.sql"))) dir = dir.Parent;
        dir.Should().NotBeNull("the tests run inside SpazaSure.Backend, next to migrate.sql");
        return Path.Combine([dir!.FullName, .. parts]);
    }

    /// <summary>(table, column) pairs from the EF model snapshot.</summary>
    private static List<(string Table, string Column)> ModelColumns()
    {
        var snapshot = File.ReadAllText(BackendFile(
            "src", "Shared", "SpazaSure.Infrastructure", "Migrations", "SpazaSureDbContextModelSnapshot.cs"));
        var columns = new List<(string, string)>();
        // Each entity block: modelBuilder.Entity("...", b => { ...HasColumnName("x")... b.ToTable("t", ...) });
        foreach (Match entity in Regex.Matches(snapshot, @"modelBuilder\.Entity\(""[^""]+"", b =>\s*\{(?<body>.*?)\n\s{16}\}\);", RegexOptions.Singleline))
        {
            var body = entity.Groups["body"].Value;
            var table = Regex.Match(body, @"b\.ToTable\(""(?<t>[a-z0-9_]+)""").Groups["t"].Value;
            if (table.Length == 0) continue; // relationship-only blocks
            foreach (Match column in Regex.Matches(body, @"\.HasColumnName\(""(?<c>[a-z0-9_]+)""\)"))
                columns.Add((table, column.Groups["c"].Value));
        }
        return columns;
    }

    [Fact]
    public void EveryColumnInTheModel_IsCreatedByMigrateSql()
    {
        var sql = File.ReadAllText(BackendFile("migrate.sql")).ToLowerInvariant();
        var columns = ModelColumns();
        columns.Should().HaveCountGreaterThan(100, "the snapshot should have been parsed");

        bool Exists(string table, string column)
        {
            var created = Regex.Matches(sql, $@"create table if not exists ""?{table}""?\s*\((?<cols>.*?)\n\);", RegexOptions.Singleline)
                .Any(m => Regex.IsMatch(m.Groups["cols"].Value, $@"(^|[\s,(]){column}\s", RegexOptions.Multiline));
            var added = Regex.IsMatch(sql, $@"alter table ""?{table}""?\s+add column if not exists ""?{column}""?\s");
            return created || added;
        }

        var missing = columns.Where(c => !Exists(c.Table, c.Column)).Select(c => $"{c.Table}.{c.Column}").ToList();
        missing.Should().BeEmpty("migrate.sql must create every column the code uses, or production queries fail");
    }

    /// <summary>
    /// Tables used only through raw SQL (ExecuteSqlRaw / SqlQueryRaw) are invisible to the EF snapshot,
    /// so the test above can't catch them. delivery_ratings and delivery_disputes were once missing.
    /// </summary>
    [Fact]
    public void EveryTableInRawSql_IsCreatedByMigrateSql()
    {
        var sql = File.ReadAllText(BackendFile("migrate.sql")).ToLowerInvariant();
        var tables = Directory.EnumerateFiles(BackendFile("src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}"))
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"\b(?:INSERT INTO|UPDATE|FROM|JOIN)\s+""?(?<t>[a-z][a-z0-9_]+)""?\b")
                .Select(m => m.Groups["t"].Value))
            .Where(t => t.Contains('_') || t.EndsWith('s'))
            .Distinct()
            .ToList();
        tables.Should().Contain("delivery_ratings", "the raw SQL scan should have found the rating queries");

        var missing = tables.Where(t => !Regex.IsMatch(sql, $@"create table if not exists ""?{t}""?\s*\(")).ToList();
        missing.Should().BeEmpty("migrate.sql must create every table raw SQL queries use, or production queries fail");
    }
}
