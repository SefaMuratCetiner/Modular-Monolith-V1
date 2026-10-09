using System.Text.Json.Nodes;
using ModularMonolith.Shared.Infrastructure.Messaging;

namespace ModularMonolith.Identity.Features.RegisterUser;

/// <summary>
/// <c>identity.user-registered</c> v1 to v2: v2 no longer carries the e-mail address. Messages stored
/// before the change (outbox rows, dead letters) are delivered in the v2 shape.
/// </summary>
internal sealed class UserRegisteredV1ToV2Upcaster : IIntegrationEventUpcaster
{
    public string EventName => "identity.user-registered";

    public int FromVersion => 1;

    public JsonObject Upcast(JsonObject payload)
    {
        payload.Remove("email");
        return payload;
    }
}
