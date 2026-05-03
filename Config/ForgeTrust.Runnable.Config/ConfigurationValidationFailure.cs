namespace ForgeTrust.Runnable.Config;

/// <summary>
/// Describes one validation failure found while initializing a strongly typed configuration value.
/// </summary>
/// <param name="Key">The configuration key being initialized.</param>
/// <param name="ConfigType">The concrete configuration wrapper type being initialized.</param>
/// <param name="ValueType">The resolved value type that was validated.</param>
/// <param name="MemberNames">The member names or paths associated with the failure.</param>
/// <param name="Message">The validation message.</param>
public sealed record ConfigurationValidationFailure(
    string Key,
    Type ConfigType,
    Type ValueType,
    IReadOnlyList<string> MemberNames,
    string Message);
