using System.Text.Json.Nodes;

namespace ModularMonolith.Shared.Infrastructure.Messaging;

/// <summary>
/// Converts the stored JSON of one integration event version into the next version
/// (<see cref="FromVersion"/> to <see cref="FromVersion"/> + 1). Implemented by the publishing module
/// (internal classes are discovered by assembly scanning) whenever it bumps a contract version.
/// Chains are applied in order, so a v1 message is upcast v1 to v2 to v3 before deserialization.
/// </summary>
/// <example>
/// <code>
/// internal sealed class ProductCreatedV1ToV2 : IIntegrationEventUpcaster
/// {
///     public string EventName => "catalog.product-created";
///     public int FromVersion => 1;
///     public JsonObject Upcast(JsonObject payload)
///     {
///         // v2 renamed "price" to "amount".
///         payload["amount"] = payload["price"]?.DeepClone();
///         payload.Remove("price");
///         return payload;
///     }
/// }
/// </code>
/// </example>
public interface IIntegrationEventUpcaster
{
    /// <summary>Gets the contract name (see <c>IntegrationEventAttribute.Name</c>).</summary>
    string EventName { get; }

    /// <summary>Gets the version this upcaster reads.</summary>
    int FromVersion { get; }

    /// <summary>Transforms a payload of <see cref="FromVersion"/> into <see cref="FromVersion"/> + 1.</summary>
    /// <param name="payload">The stored payload.</param>
    /// <returns>The payload in the next version's shape.</returns>
    JsonObject Upcast(JsonObject payload);
}
