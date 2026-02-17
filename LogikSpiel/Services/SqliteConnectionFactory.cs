using SQLite;

namespace LogikSpiel.Services;

public static class SqliteConnectionFactory
{
    private const string DbName = "LogikSpiel_v1.db3";
    private static readonly SemaphoreSlim ConnectionLock = new(1, 1);
    private static SQLiteAsyncConnection? _connection;

    public static async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (_connection is not null)
            return _connection;

        await ConnectionLock.WaitAsync();
        try
        {
            if (_connection is null)
            {
                var dbPath = Path.Combine(FileSystem.AppDataDirectory, DbName);
                _connection = new SQLiteAsyncConnection(dbPath);
            }

            return _connection;
        }
        finally
        {
            ConnectionLock.Release();
        }
    }
}
