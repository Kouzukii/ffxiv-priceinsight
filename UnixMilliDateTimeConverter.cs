using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PriceInsight;

[JsonConverter(typeof(UnixMilliDateTimeConverter))]
public record UnixMilliDateTime(DateTime Value) {
    public static implicit operator DateTime?(UnixMilliDateTime? self) => self?.Value;
}

public class UnixMilliDateTimeConverter : JsonConverter<UnixMilliDateTime> {
    public override bool HandleNull => false;

    public override UnixMilliDateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TryGetInt64(out var time)) {
            return new UnixMilliDateTime(DateTimeOffset.FromUnixTimeMilliseconds(time).LocalDateTime);
        }

        throw new JsonException("Expected date object value.");
    }

    public override void Write(Utf8JsonWriter writer, UnixMilliDateTime value, JsonSerializerOptions options) => throw new NotSupportedException();
}

[JsonConverter(typeof(UnixSecondDateTimeConverter))]
public record UnixSecondDateTime(DateTime Value) {
    public static implicit operator DateTime?(UnixSecondDateTime? self) => self?.Value;
}

public class UnixSecondDateTimeConverter : JsonConverter<UnixSecondDateTime> {
    public override bool HandleNull => false;

    public override UnixSecondDateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TryGetInt64(out var time)) {
            return new UnixSecondDateTime(DateTimeOffset.FromUnixTimeSeconds(time).LocalDateTime);
        }

        throw new JsonException("Expected date object value.");
    }

    public override void Write(Utf8JsonWriter writer, UnixSecondDateTime value, JsonSerializerOptions options) => throw new NotSupportedException();
}
