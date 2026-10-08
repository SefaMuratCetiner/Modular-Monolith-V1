using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ModularMonolith.Shared.Infrastructure.Configuration;

/// <summary>
/// Helpers for strongly typed, fail-fast configuration: bind a section, register an
/// <see cref="IValidateOptions{TOptions}"/> and validate when the host starts.
/// </summary>
public static class OptionsValidation
{
    /// <summary>Binds a configuration section, registers its validator and validates on start.</summary>
    /// <typeparam name="TOptions">The options type.</typeparam>
    /// <typeparam name="TValidator">The validator type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="sectionName">The section to bind.</param>
    /// <returns>The options builder for further configuration.</returns>
    public static OptionsBuilder<TOptions> AddValidatedOptions<TOptions, TValidator>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName)
        where TOptions : class
        where TValidator : class, IValidateOptions<TOptions>
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<TOptions>, TValidator>());
        return services.AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateOnStart();
    }

    /// <summary>Turns collected failures into a validation result.</summary>
    /// <param name="section">The section name used to prefix messages.</param>
    /// <param name="failures">The failure messages.</param>
    /// <returns>The validation result.</returns>
    public static ValidateOptionsResult ToResult(string section, IReadOnlyCollection<string> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures.Select(failure => $"{section}: {failure}"));
    }

    /// <summary>Checks whether a value is an absolute http(s) URL.</summary>
    /// <param name="value">The value.</param>
    /// <param name="requireHttps">Whether only https is accepted.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    public static bool IsHttpUrl(string? value, bool requireHttps = false)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
           && (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
               || (!requireHttps && string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)));
}
