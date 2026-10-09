namespace ModularMonolith.Shared.Kernel.Privacy;

/// <summary>
/// Marks a property as personal data (GDPR Art. 4): e-mail addresses, names, phone numbers, addresses.
/// Marked values are redacted wherever data leaves its owning table for diagnostics:
/// <list type="bullet">
/// <item>audit rows (<c>audit_log.payload</c> and <c>changes</c>) store <c>[redacted]</c>;</item>
/// <item>Serilog replaces them with <c>[redacted]</c> when an object is destructured (<c>{@Command}</c>).</item>
/// </list>
/// On positional records use the <c>property:</c> target:
/// <c>record RegisterUserCommand([property: PersonalData] string Email, ...)</c>.
/// Subject ids are pseudonymous identifiers and are not marked.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class PersonalDataAttribute : Attribute;
