using System;
using System.Collections.Generic;
using System.Text;

namespace JsonEnvelopes
{
    /// <summary>
    /// Concrete implementation of <see cref="Envelope"/> with generic Content.
    /// </summary>
    public class Envelope<TContent> : Envelope where TContent : class, new()
    {
        public Envelope() : base()
        { }

        public Envelope(TContent content) =>
            Content = content ?? new TContent();


        public override object GetContent() =>
            Content;


        public TContent Content { get; set; } = new TContent();

        public override string ContentType =>
            typeof(TContent).AssemblyQualifiedName;
    }
}
