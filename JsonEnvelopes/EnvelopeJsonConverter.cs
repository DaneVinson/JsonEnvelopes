namespace JsonEnvelopes;

public class EnvelopeJsonConverter : JsonConverter<Envelope>
{
    public override Envelope Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (!JsonDocument.TryParseValue(ref reader, out var document))
        {
            throw new JsonException("Failed to parse JSON");
        }
        if (!document.RootElement.TryGetProperty(options.GetContentTypePropertyName(), out var contentTypeElement))
        {
            document.Dispose();
            throw new JsonException("Failed to parse JSON");
        }
        if (!document.RootElement.TryGetProperty(options.GetContentPropertyName(), out var contentElement))
        {
            document.Dispose();
            throw new JsonException("Failed to parse JSON");
        }

        var contentTypeString = contentTypeElement.GetString();
        if (string.IsNullOrEmpty(contentTypeString))
        {
            document.Dispose();
            throw new JsonException($"{options.GetContentTypePropertyName()} property is missing");
        }

        var contentType = Type.GetType(contentTypeString);
        if (contentType is null)
        {
            document.Dispose();
            throw new TypeLoadException($"{contentTypeString} is not a valid type");
        }

        var content = contentElement.Deserialize(contentType, options);

        var envelopeType = typeof(Envelope<>).MakeGenericType(contentType);

        return Activator.CreateInstance(envelopeType, content) as Envelope ??
               throw new InvalidOperationException($"Read did not produce an {nameof(Envelope)} object");
    }

    public override void Write(Utf8JsonWriter writer, Envelope message, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString(options.GetContentTypePropertyName(), message.ContentType);
        writer.WritePropertyName(options.GetContentPropertyName());
        JsonSerializer.Serialize(writer, message.GetContent(), options);
        writer.WriteEndObject();
    }
}
