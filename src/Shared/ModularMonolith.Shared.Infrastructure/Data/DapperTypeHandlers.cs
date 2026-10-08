using System.Data;
using Dapper;

namespace ModularMonolith.Shared.Infrastructure.Data;

/// <summary>
/// Dapper conventions. Npgsql reads <c>timestamptz</c> as a UTC <see cref="DateTime"/>; this handler
/// lets read models use <see cref="DateTimeOffset"/> (and always writes UTC offsets, which Npgsql requires).
/// </summary>
public static class DapperTypeHandlers
{
    private static int _registered;

    /// <summary>Registers the handlers once per process.</summary>
    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
        {
            return;
        }

        // Dapper only consults custom handlers for types without a built-in mapping.
        SqlMapper.RemoveTypeMap(typeof(DateTimeOffset));
        SqlMapper.RemoveTypeMap(typeof(DateTimeOffset?));
        SqlMapper.AddTypeHandler(new DateTimeOffsetHandler());
    }

    private sealed class DateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>
    {
        public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
        {
            parameter.DbType = DbType.DateTimeOffset;
            parameter.Value = value.ToUniversalTime();
        }

        public override DateTimeOffset Parse(object value) => value switch
        {
            DateTimeOffset offset => offset,
            DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
            _ => throw new DataException($"Cannot convert {value.GetType().Name} to DateTimeOffset."),
        };
    }
}
