using System.Reflection;

[assembly: AssemblyKeyFile("JsonEnvelopes.snk")]

namespace JsonEnvelopes;

/// <summary>
/// Helpers for resolving the wire names of <see cref="Envelope{TContent}"/>'s properties under a
/// <see cref="JsonSerializerOptions.PropertyNamingPolicy"/>.
/// </summary>
internal static class Extensions
{
    private static string ConvertName(JsonSerializerOptions jsonOptions, string propertyName) =>
        jsonOptions.PropertyNamingPolicy?.ConvertName(propertyName) ?? propertyName;

    /// <summary>
    /// Use the <see cref="JsonSerializerOptions"/> object to determine the expected name of the
    /// <see cref="Envelope{TContent}"/>'s Content property.
    /// </summary>
    internal static string GetContentPropertyName(this JsonSerializerOptions jsonOptions) =>
        ConvertName(jsonOptions, nameof(Envelope<object>.Content));

    /// <summary>
    /// Use the <see cref="JsonSerializerOptions"/> object to determine the expected name of the
    /// <see cref="Envelope{TContent}"/>'s ContentType property.
    /// </summary>
    internal static string GetContentTypePropertyName(this JsonSerializerOptions jsonOptions) =>
        ConvertName(jsonOptions, nameof(Envelope<object>.ContentType));
}
