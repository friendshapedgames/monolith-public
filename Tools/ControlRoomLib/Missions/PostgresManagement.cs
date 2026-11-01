using ControlRoomLib.Core;
using Npgsql;

namespace ControlRoomLib.Missions;

public class PostgresManagement : Mission
{
    public PostgresManagement(List<string> rawArgs, MissionVariables missionVariables) : base(rawArgs, missionVariables)
    {
    }

    public override async Task Run()
    {
        await using var connection = CreateConnection("test");

        await RunOneOff(connection, "DROP TABLE IF EXISTS users");
        await RunOneOff(connection,
            "CREATE TABLE IF NOT EXISTS users(id TEXT, fudge_count INTEGER, name TEXT, PRIMARY KEY (id))");
        await RunOneOff(connection, """INSERT INTO users VALUES ('abc', 10, 'mark')""");
        await RunOneOff(connection, """INSERT INTO users VALUES ('some random id', 12, 'helly')""");
        await RunOneOff(connection, """INSERT INTO users VALUES ('qrf', 13, 'irving')""");

        await using var command = connection.CreateCommand();
        command.CommandText = """
                                  SELECT name
                                  FROM users
                                  WHERE id = (@id)
                              """;

        command.Parameters.AddWithValue("id", "some random id");

        await using var reader = await command.ExecuteReaderAsync();

        while (reader.Read())
        {
            var name = reader.GetString(0);

            await OutPipe.LogMessage($"Hello, {name}!");
        }
    }

    public static NpgsqlDataSource CreateConnection(string databaseName)
    {
        var connectionString = $"Host=localhost;Database={databaseName}";
        return NpgsqlDataSource.Create(connectionString);
    }

    public static async Task RunOneOff(NpgsqlDataSource source, string exactText)
    {
        await using var createTableCommand = source.CreateCommand();
        createTableCommand.CommandText = exactText;
        await OutPipe.LogMessage($"SQL: {exactText}");
        createTableCommand.ExecuteNonQuery();
    }

    public static string GetTypeNameFromType(Type type)
    {
        if (type == typeof(int) || type == typeof(uint))
        {
            // instead of "Int32"
            return "int4";
        }

        if (type == typeof(long) || type == typeof(ulong))
        {
            // Instead of "Int64"
            return "int8";
        }

        if (type == typeof(byte[]))
        {
            return "bytea";
        }

        if (type == typeof(float))
        {
            return "float4";
        }

        if (type == typeof(double))
        {
            return "float8";
        }

        if (type == typeof(DateTime))
        {
            // always assume we want timestamp with timezone
            return "timestamptz";
        }

        if (type == typeof(string))
        {
            return "text";
        }

        if (type.IsEnum)
        {
            // We're going to read the enum as a name so it may as well be treated as text
            return "text";
        }

        // This might be right, at the very least it'll be an obvious sign that something is wrong
        return type.Name;
    }

    public static object? GetDefaultValueFor(Type type)
    {
        return Activator.CreateInstance(type);
    }

    public static bool ShouldWrapInQuotes(Type type)
    {
        // DateTime's do their own string wrapping so they don't need to be included here
        return type == typeof(string) || type.IsEnum;
    }

    public static object? StringifyValue(object? cell, Type type)
    {
        if (cell == null)
        {
            cell = GetDefaultValueFor(type);
        }

        if (cell is DateTime dateTime)
        {
            // ISO format
            return $"TIMESTAMP '{dateTime:O}'";
        }

        // We're gonna lean on this a lot
        var s = cell?.ToString();

        if (s == null)
        {
            return s;
        }

        // Escape single quote characters
        return s.Replace("\'", "\'\'");
    }
}