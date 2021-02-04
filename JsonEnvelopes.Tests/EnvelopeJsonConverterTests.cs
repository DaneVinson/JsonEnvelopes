using System;
using System.Text.Json;
using Xunit;

namespace JsonEnvelopes.Tests
{
    public class EnvelopeJsonConverterTests
    {
        [Fact]
        public void Deserialization_Throws_TypeLoadException_When_UnknownType()
        {
            var foo = new FooCommand<BarEntity>();
            var envelope = new Envelope<FooCommand<BarEntity>>(foo);

            var resultJson = JsonSerializer.Serialize<Envelope>(envelope);
            resultJson = resultJson.Replace("JsonEnvelopes.Tests.FooCommand", "NewNamespace.FooCommand");

            Assert.Throws<TypeLoadException>(() =>
            {
                var resultFoo = JsonSerializer.Deserialize<Envelope>(resultJson)
                    .GetContent() as FooCommand<BarEntity>;
            });
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, false)]
        [InlineData(true, true)]
        [InlineData(false, true)]
        public void Serialization_Then_Deserialization_Produces_Equal_Object(bool useCamelCase, bool caseInsensitive)
        {
            var jsonOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = caseInsensitive };
            if (useCamelCase) { jsonOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase; }
            var foo = new FooCommand<BarEntity>(new BarEntity("pdq"), "abc");
            var envelope = new Envelope<FooCommand<BarEntity>>(foo);

            var resultJson = JsonSerializer.Serialize<Envelope>(envelope, jsonOptions);
            var resultFoo = JsonSerializer.Deserialize<Envelope>(resultJson, jsonOptions)
                                            .GetContent() as FooCommand<BarEntity>;

            Assert.NotNull(resultFoo);
            Assert.NotNull(foo.Bar!.Id);
            Assert.Equal(foo.Id, resultFoo!.Id);
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
