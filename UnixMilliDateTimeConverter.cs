using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PriceInsight;

public class UnixMilliDateTimeConverter : DateTimeConverterBase {
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) {
        long milliseconds;

        if (value is DateTime dateTime) {
            milliseconds = (long)(dateTime.ToUniversalTime() - DateTime.UnixEpoch).TotalMilliseconds;
        }
        else {
            throw new JsonSerializationException("Expected date object value.");
        }

        if (milliseconds < 0) {
            throw new JsonSerializationException("Cannot convert date value that is before Unix epoch of 00:00:00 UTC on 1 January 1970.");
        }

        writer.WriteValue(milliseconds);
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer) {
        if (reader.TokenType == JsonToken.Null) {
            return null;
        }

        long millis;

        switch (reader.TokenType) {
            case JsonToken.Integer: 
                millis = (long)reader.Value!;
                break;
            case JsonToken.String: {
                if (!long.TryParse((string)reader.Value!, out millis)) {
                    throw new JsonSerializationException($"Cannot convert invalid value to {objectType}.");
                }

                break;
            }
            default: 
                throw new JsonSerializationException($"Unexpected token parsing date. Expected Integer or String, got {reader.TokenType}.");
        }

        if (millis < 0)
            throw new JsonSerializationException($"Cannot convert value that is before Unix epoch of 00:00:00 UTC on 1 January 1970 to {objectType}.");
        
        var d = DateTime.UnixEpoch.AddMilliseconds(millis);
        return d;

    }
}
