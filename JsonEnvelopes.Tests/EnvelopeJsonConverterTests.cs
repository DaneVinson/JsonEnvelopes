using System;
using System.Text.Json;
using Xunit;

namespace JsonEnvelopes.Tests
{
    public class EnvelopeJsonConverterTests
    {
        [Fact]
        public void Serialization_Then_Deserialization_Produces_Equal_Object()
        {
            var foo = new FooCommand<BarEntity>(new BarEntity("pdq"), "abc");
            var envelope = new Envelope<FooCommand<BarEntity>>(foo);
            var jsonOptions = new JsonSerializerOptions();

            var resultJson = JsonSerializer.Serialize<Envelope>(envelope, jsonOptions);
            var resultEnvelope = JsonSerializer.Deserialize<Envelope>(resultJson, jsonOptions);
            var resultFoo = resultEnvelope.GetContent() as FooCommand<BarEntity>;

            Assert.NotNull(resultFoo);
            Assert.NotNull(foo.Bar.Id);
            Assert.Equal(foo.Id, resultFoo.Id);
            Assert.Equal(foo.Bar.Id, resultFoo.Bar?.Id);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Serialization_Honors_JsonSerializationOptions_PropertyNamingPolicy(bool useCamelCase)
        {
            var contentPropertyName = nameof(Envelope<object>.Content);
            var contentTypePropertyName = nameof(Envelope<object>.ContentType);
            var jsonOptions = new JsonSerializerOptions();
            if (useCamelCase) 
            { 
                jsonOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                contentPropertyName = contentPropertyName.ToLower();
                contentTypePropertyName = "contentType";
            }

            var envelope = new Envelope<object>(new object());
            var json = JsonSerializer.Serialize<Envelope>(envelope, jsonOptions);

            Assert.Contains($"\"{contentTypePropertyName}\":", json, StringComparison.Ordinal);
            Assert.Contains($"\"{contentPropertyName}\":", json, StringComparison.Ordinal);
        }
    }
}
