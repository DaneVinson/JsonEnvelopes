namespace JsonEnvelopes;

/// <summary>
/// An immutable allow-list of the content types an <see cref="Envelope"/> may carry, keyed by the
/// logical name each type is given on the wire. Build one with <see cref="Create"/>.
/// </summary>
public sealed class EnvelopeTypeRegistry
{
    private readonly Dictionary<Type, string> _namesByType;
    private readonly Dictionary<string, EnvelopeRegistration> _registrationsByName;

    /// <summary>
    /// Create a registry that owns the given lookup dictionaries. Callers must not retain or
    /// mutate <paramref name="registrationsByName"/> or <paramref name="namesByType"/> after
    /// passing them in.
    /// </summary>
    internal EnvelopeTypeRegistry(
        Dictionary<string, EnvelopeRegistration> registrationsByName,
        Dictionary<Type, string> namesByType) =>
        (_registrationsByName, _namesByType) = (registrationsByName, namesByType);

    /// <summary>
    /// Build a registry. The builder is only valid inside <paramref name="configure"/>; the
    /// resulting registry is immutable and safe for concurrent use.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is null.</exception>
    public static EnvelopeTypeRegistry Create(Action<EnvelopeTypeRegistryBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new EnvelopeTypeRegistryBuilder();
        configure(builder);
        return builder.Build();
    }

    /// <summary>
    /// Return the logical wire name registered for <paramref name="contentType"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="contentType"/> is null.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="contentType"/> is not registered.</exception>
    public string GetName(Type contentType)
    {
        ArgumentNullException.ThrowIfNull(contentType);

        if (!_namesByType.TryGetValue(contentType, out var name))
        {
            throw new InvalidOperationException(
                $"The content type {contentType} is not registered. Add it when building the registry.");
        }

        return name;
    }

    /// <summary>
    /// Return the logical wire name registered for <paramref name="contentType"/> without throwing
    /// when it is absent. Use this on a path that must not throw, such as dead-lettering.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="contentType"/> is null.</exception>
    public bool TryGetName(Type contentType, out string? name)
    {
        ArgumentNullException.ThrowIfNull(contentType);

        return _namesByType.TryGetValue(contentType, out name);
    }

    /// <summary>
    /// Return the registration stored under <paramref name="name"/> without throwing when it is
    /// absent. Used internally when resolving content types from their wire name.
    /// </summary>
    internal bool TryGetRegistration(string name, out EnvelopeRegistration? registration) =>
        _registrationsByName.TryGetValue(name, out registration);

    /// <summary>
    /// Wrap <paramref name="content"/> in an <see cref="Envelope{TContent}"/> for its runtime type.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The content's type is not registered.</exception>
    public Envelope Wrap(object content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var name = GetName(content.GetType());
        return _registrationsByName[name].Factory(content);
    }
}

/// <summary>
/// One entry in an <see cref="EnvelopeTypeRegistry"/>: the registered content type and a factory
/// that wraps an instance of it without reflection.
/// </summary>
internal sealed class EnvelopeRegistration
{
    /// <summary>
    /// Create a registration pairing <paramref name="contentType"/> with the <paramref name="factory"/>
    /// that wraps an instance of it.
    /// </summary>
    internal EnvelopeRegistration(Type contentType, Func<object, Envelope> factory) =>
        (ContentType, Factory) = (contentType, factory);

    /// <summary>
    /// The registered content type.
    /// </summary>
    internal Type ContentType { get; }

    /// <summary>
    /// Wraps an instance of <see cref="ContentType"/> in its <see cref="Envelope{TContent}"/>
    /// without reflection.
    /// </summary>
    internal Func<object, Envelope> Factory { get; }
}
