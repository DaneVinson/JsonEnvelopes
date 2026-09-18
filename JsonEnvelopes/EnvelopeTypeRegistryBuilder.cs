namespace JsonEnvelopes;

/// <summary>
/// Collects registrations for an <see cref="EnvelopeTypeRegistry"/>. Obtained inside the callback
/// passed to <see cref="EnvelopeTypeRegistry.Create"/>.
/// </summary>
public sealed class EnvelopeTypeRegistryBuilder
{
    private readonly Dictionary<Type, string> _namesByType = new();
    private readonly Dictionary<string, EnvelopeRegistration> _registrationsByName = new(StringComparer.Ordinal);

    internal EnvelopeTypeRegistryBuilder()
    { }

    /// <summary>
    /// Allow <typeparamref name="TContent"/> as envelope content, carried on the wire as
    /// <paramref name="name"/>. Names are opaque strings; this library never parses them.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is null, empty, or whitespace; or <paramref name="name"/> or
    /// <typeparamref name="TContent"/> is already registered.
    /// </exception>
    public EnvelopeTypeRegistryBuilder Add<TContent>(string name) where TContent : class, new()
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A logical content type name is required.", nameof(name));
        }
        if (_registrationsByName.ContainsKey(name))
        {
            throw new ArgumentException($"The name '{name}' is already registered.", nameof(name));
        }
        if (_namesByType.TryGetValue(typeof(TContent), out var existingName))
        {
            throw new ArgumentException(
                $"The content type {typeof(TContent)} is already registered as '{existingName}'.",
                nameof(name));
        }

        _registrationsByName.Add(
            name,
            new EnvelopeRegistration(typeof(TContent), content => new Envelope<TContent>((TContent)content)));
        _namesByType.Add(typeof(TContent), name);
        return this;
    }

    internal EnvelopeTypeRegistry Build() =>
        new(_registrationsByName, _namesByType);
}
