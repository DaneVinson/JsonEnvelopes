using System.Reflection;

[assembly: AssemblyKeyFile("JsonEnvelopes.snk")]

namespace JsonEnvelopes;

public static class Extensions
{
    /// <summary>
    /// Use the <see cref="JsonSerializerOptions"/> object to determine the expected name of the 
    /// <see cref="Envelope{TContent}"/>'s Content property.
    /// </summary>
    public static string GetContentPropertyName(this JsonSerializerOptions jsonOptions)
    {
        var propertyName = nameof(Envelope<object>.Content);

        if (jsonOptions.PropertyNamingPolicy == JsonNamingPolicy.CamelCase)
        {
            propertyName = "content";
        }

        return propertyName;
    }

    /// <summary>
    /// Use the <see cref="JsonSerializerOptions"/> object to determine the expected name of the 
    /// <see cref="Envelope{TContent}"/>'s ContentType property.
    /// </summary>
    public static string GetContentTypePropertyName(this JsonSerializerOptions jsonOptions)
    {
        var propertyName = nameof(Envelope<object>.ContentType);

        if (jsonOptions.PropertyNamingPolicy == JsonNamingPolicy.CamelCase) 
        { 
            propertyName = "contentType";
        }

        return propertyName;
    }
}
