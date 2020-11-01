using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace JsonEnvelopes
{
    /// <summary>
    /// Abstract base class for <see cref="Envelope{TContent}"/> utilizing a custom JsonConverter.
    /// </summary>
    [JsonConverter(typeof(EnvelopeJsonConverter))]
    public abstract class Envelope
    {
        public abstract object GetContent();

        public abstract string ContentType { get; }
    }
}
