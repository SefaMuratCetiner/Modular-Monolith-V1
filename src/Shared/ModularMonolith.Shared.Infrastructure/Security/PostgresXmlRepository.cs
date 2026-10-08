using System.Xml.Linq;
using Dapper;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Npgsql;

namespace ModularMonolith.Shared.Infrastructure.Security;

/// <summary>
/// Stores the ASP.NET Core Data Protection key ring in PostgreSQL (<c>platform.data_protection_keys</c>),
/// so every replica shares the same keys and they survive restarts and redeployments.
/// The interface is synchronous by design; the key ring is cached and reloaded rarely.
/// </summary>
public sealed class PostgresXmlRepository(NpgsqlDataSource dataSource) : IXmlRepository
{
    /// <inheritdoc />
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var connection = dataSource.OpenConnection();
        return [.. connection.Query<string>("select xml from platform.data_protection_keys order by id").Select(xml => XElement.Parse(xml))];
    }

    /// <inheritdoc />
    public void StoreElement(XElement element, string friendlyName)
    {
        ArgumentNullException.ThrowIfNull(element);
        using var connection = dataSource.OpenConnection();
        connection.Execute(
            "insert into platform.data_protection_keys (friendly_name, xml) values (@FriendlyName, @Xml)",
            new { FriendlyName = friendlyName, Xml = element.ToString(SaveOptions.DisableFormatting) });
    }
}
