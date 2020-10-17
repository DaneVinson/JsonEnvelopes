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
            string? propertyName = null;
            var contentType = default(Type);
            var content = default(object);

            do
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        propertyName = reader.GetString().ToLower();
                        break;
                    case JsonTokenType.StartObject:
                    case JsonTokenType.String:
                        switch (propertyName)
                        {
                            case "contenttype":
                                var typeString = JsonSerializer.Deserialize<string>(ref reader, options);
                                contentType = Type.GetType(typeString);
                                break;
                            case "content":
                                content = JsonSerializer.Deserialize(ref reader, contentType, options);
                                break;
                        }
                        break;
                }
            } while (reader.Read());

            var envelopeType = typeof(Envelope<>).MakeGenericType(contentType);

            return Activator.CreateInstance(envelopeType, content) as Envelope ?? 
                throw new InvalidOperationException($"Read did not produce an {nameof(Envelope)} object.");
        }

        public override void Write(Utf8JsonWriter writer, Envelope message, JsonSerializerOptions options)
        {
            // Camel case if requested by options.
            string contentPropertyName;
            string contentTypePropertyName;
            if (options.PropertyNamingPolicy == JsonNamingPolicy.CamelCase)
            {
                contentPropertyName = nameof(Envelope<object>.Content).ToLower();
                contentTypePropertyName = "contentType";    // Ugh, magic string
            }
            else
            {
                contentPropertyName = nameof(Envelope<object>.Content);
                contentTypePropertyName = nameof(Envelope<object>.ContentType);
            }

            writer.WriteStartObject();
            writer.WriteString(contentTypePropertyName, message.ContentType);
            writer.WritePropertyName(contentPropertyName);
            JsonSerializer.Serialize(writer, message.GetContent(), options);
            writer.WriteEndObject();
        }
    }
}
