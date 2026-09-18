using System.Reflection;

[assembly: AssemblyKeyFile("JsonEnvelopes.snk")]

namespace JsonEnvelopes;

internal static class Extensions
{
    internal static string GetContentPropertyName(this JsonSerializerOptions jsonOptions) =>
        ConvertName(jsonOptions, nameof(Envelope<object>.Content));

    internal static string GetContentTypePropertyName(this JsonSerializerOptions jsonOptions) =>
        ConvertName(jsonOptions, nameof(Envelope<object>.ContentType));

    private static string ConvertName(JsonSerializerOptions jsonOptions, string propertyName) =>
        jsonOptions.PropertyNamingPolicy?.ConvertName(propertyName) ?? propertyName;
}
