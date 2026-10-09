namespace ModularMonolith.Shared.Infrastructure.Security;

/// <summary>
/// A named permission that an endpoint requires, e.g. <c>catalog.products.write</c>. It is granted when
/// <b>both</b> hold:
/// <list type="bullet">
/// <item>the user has one of <see cref="Roles"/> (what the <i>user</i> may do), and</item>
/// <item>the access token carries <see cref="Scope"/> (what the user allowed the <i>client application</i> to do).</item>
/// </list>
/// Each module declares its own permissions next to its endpoints and registers them with
/// <c>services.AddPermissions(...)</c>; endpoints reference them by name with <c>Policies(...)</c>.
/// Rules that depend on the resource itself (e.g. "only the seller may reprice a product") belong in the
/// handler, after the resource is loaded.
/// </summary>
/// <param name="Name">The permission (and policy) name: lowercase, dot-separated, e.g. <c>catalog.products.read</c>.</param>
/// <param name="Scope">The OAuth scope the access token must contain, e.g. <c>catalog.read</c>.</param>
/// <param name="Roles">The realm roles that grant the permission (any of them).</param>
public sealed record Permission(string Name, string Scope, IReadOnlyList<string> Roles);
