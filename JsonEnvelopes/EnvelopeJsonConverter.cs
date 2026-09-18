namespace JsonEnvelopes;

/// <summary>
/// Reads and writes <see cref="Envelope"/> instances, resolving content types through an
/// <see cref="EnvelopeTypeRegistry"/> allow-list. Add a configured instance to
/// <see cref="JsonSerializerOptions.Converters"/>; a converter built without a registry throws.
/// </summary>
public class EnvelopeJsonConverter : JsonConverter<Envelope>
{
    private readonly EnvelopeTypeRegistry? _registry;

    /// <summary>
    /// Create a converter with no registry. This constructor exists only because the
    /// <see cref="JsonConverterAttribute"/> on <see cref="Envelope"/> requires one; every read and
    /// write on the resulting instance throws <see cref="InvalidOperationException"/>.
    /// </summary>
    public EnvelopeJsonConverter()
    { }

    /// <summary>
    /// Create a converter that resolves content types through <paramref name="registry"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is null.</exception>
    public EnvelopeJsonConverter(EnvelopeTypeRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        _registry = registry;
    }

    /// <inheritdoc />
    /// <exception cref="UnknownContentTypeException">
    /// The JSON names a content type that is not registered.
    /// </exception>
    public override Envelope Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var registry = RequireRegistry();
        var contentTypePropertyName = options.GetContentTypePropertyName();
        var contentPropertyName = options.GetContentPropertyName();

        if (!JsonDocument.TryParseValue(ref reader, out var document))
        {
            throw new JsonException("The envelope JSON could not be parsed.");
        }

        using (document)
        {
            if (!TryGetProperty(document.RootElement, contentTypePropertyName, options, out var contentTypeElement))
            {
                throw new JsonException($"The envelope JSON has no '{contentTypePropertyName}' property.");
            }
            if (!TryGetProperty(document.RootElement, contentPropertyName, options, out var contentElement))
            {
                throw new JsonException($"The envelope JSON has no '{contentPropertyName}' property.");
            }

            if (contentTypeElement.ValueKind != JsonValueKind.String &&
                contentTypeElement.ValueKind != JsonValueKind.Null)
            {
                throw new JsonException($"The envelope JSON's '{contentTypePropertyName}' property is not a string.");
            }

            var contentTypeName = contentTypeElement.GetString();
            if (string.IsNullOrEmpty(contentTypeName))
            {
                throw new JsonException($"The envelope JSON has an empty '{contentTypePropertyName}' property.");
            }
            if (!registry.TryGetRegistration(contentTypeName, out var registration))
            {
                throw new UnknownContentTypeException(contentTypeName);
            }

            var content = contentElement.Deserialize(registration!.ContentType, options) ??
                          throw new JsonException($"The '{contentPropertyName}' property deserialized to null.");

            return registration.Factory(content);
        }
    }

    private EnvelopeTypeRegistry RequireRegistry() =>
        _registry ??
        throw new InvalidOperationException(
            $"This {nameof(EnvelopeJsonConverter)} has no {nameof(EnvelopeTypeRegistry)}. Build a " +
            $"registry and add new {nameof(EnvelopeJsonConverter)}(registry) to " +
            $"{nameof(JsonSerializerOptions)}.{nameof(JsonSerializerOptions.Converters)}.");

    private static bool TryGetProperty(
        JsonElement element,
        string propertyName,
        JsonSerializerOptions options,
        out JsonElement value)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }
        if (element.TryGetProperty(propertyName, out value))
        {
            return true;
        }
        if (!options.PropertyNameCaseInsensitive)
        {
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The content's type is not registered.</exception>
    public override void Write(Utf8JsonWriter writer, Envelope value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        var registry = RequireRegistry();
        var content = value.GetContent();

        writer.WriteStartObject();
        writer.WriteString(options.GetContentTypePropertyName(), registry.GetName(value.ContentType));
        writer.WritePropertyName(options.GetContentPropertyName());
        JsonSerializer.Serialize(writer, content, options);
        writer.WriteEndObject();
    }
}
