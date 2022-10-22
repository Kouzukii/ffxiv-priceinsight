using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PriceInsight;

public class UnixMilliDateTimeConverter : JsonConverter<DateTime> {
    public override bool HandleNull => false;

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TryGetInt64(out var time)) {
            return DateTimeOffset.FromUnixTimeMilliseconds(time).LocalDateTime;
        }

        throw new JsonException("Expected date object value.");
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) => throw new NotSupportedException();
}

public class UnixSecondsDateTimeConverter : JsonConverter<DateTime> {
    public override bool HandleNull => false;

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TryGetInt64(out var time)) {
            return DateTimeOffset.FromUnixTimeSeconds(time).LocalDateTime;
        }

        throw new JsonException("Expected date object value.");
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) => throw new NotSupportedException();
}
