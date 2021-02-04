using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JsonEnvelopes
{
    public class EnvelopeJsonConverter : JsonConverter<Envelope>
    {
        public override Envelope Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var contentTypePropertyName = options.GetContentTypePropertyName();
            var contentPropertyName = options.GetContentPropertyName();
            var propertyNameComparison = options.PropertyNameCaseInsensitive ? StringComparison.OrdinalIgnoreCase : default;
            string? propertyName = null;
            var contentType = default(Type);
            var content = default(object);

            do
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        propertyName = reader.GetString();
                        break;
                    case JsonTokenType.String:
                        if (contentTypePropertyName.Equals(propertyName, propertyNameComparison)) { contentType = Type.GetType(JsonSerializer.Deserialize<string>(ref reader, options), true); }
                        break;
                    case JsonTokenType.StartObject:
                        if (contentPropertyName.Equals(propertyName, propertyNameComparison)) { content = JsonSerializer.Deserialize(ref reader, contentType, options); }
                        break;
                }
            } while (reader.Read());

            var envelopeType = typeof(Envelope<>).MakeGenericType(contentType);

            return Activator.CreateInstance(envelopeType, content) as Envelope ?? 
                throw new InvalidOperationException($"Read did not produce an {nameof(Envelope)} object.");
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
}
