using Microsoft.Data.Sqlite;

namespace CompeteDesk.Data;

public static class SqliteConnectionStringHelper
{
    public static string NormalizeForAppData(IWebHostEnvironment environment, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string is required.");
        }

        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource))
        {
            throw new InvalidOperationException("The SQLite connection string must include a Data Source.");
        }

        if (Path.IsPathRooted(builder.DataSource))
        {
            EnsureDirectoryExists(builder.DataSource);
            return builder.ToString();
        }

        var normalized = builder.DataSource.Replace('\\', Path.DirectorySeparatorChar)
                                           .Replace('/', Path.DirectorySeparatorChar)
                                           .TrimStart(Path.DirectorySeparatorChar);

        var absolutePath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, normalized));
        builder.DataSource = absolutePath;
        EnsureDirectoryExists(absolutePath);
        return builder.ToString();
    }

    private static void EnsureDirectoryExists(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
