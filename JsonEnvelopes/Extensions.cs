using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace JsonEnvelopes
{
    public static class Extensions
    {
        /// <summary>
        /// Use the <see cref="JsonSerializerOptions"/> object to determine the expected name of the 
        /// <see cref="Envelope{TContent}"/>'s Content property.
        /// </summary>
        public static string GetContentPropertyName(this JsonSerializerOptions jsonOptions)
        {
            var propertyName = nameof(Envelope<object>.Content);

            if (jsonOptions.PropertyNameCaseInsensitive ||
                jsonOptions.PropertyNamingPolicy == JsonNamingPolicy.CamelCase)
            { propertyName = propertyName.ToLower(); }

            return propertyName;
        }

        /// <summary>
        /// Use the <see cref="JsonSerializerOptions"/> object to determine the expected name of the 
        /// <see cref="Envelope{TContent}"/>'s ContentType property.
        /// </summary>
        public static string GetContentTypePropertyName(this JsonSerializerOptions jsonOptions)
        {
            var propertyName = nameof(Envelope<object>.ContentType);

            if (jsonOptions.PropertyNameCaseInsensitive) { propertyName = propertyName.ToLower(); }
            else if (jsonOptions.PropertyNamingPolicy == JsonNamingPolicy.CamelCase) { propertyName = "contentType"; }
            return propertyName;
        }
    }
}
