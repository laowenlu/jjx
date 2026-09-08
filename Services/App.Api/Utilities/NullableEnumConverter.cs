using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Custom JSON converter that allows nullable enum properties to be correctly
/// serialized and deserialized as strings when using System.Text.Json.
/// </summary>
/// <typeparam name="T">Enum type being (de)serialized (must be a value type enum).</typeparam>
public class NullableEnumConverter<T> : JsonConverter<T?> where T : struct, Enum
{
    /// <summary>
    /// Reads a string from the JSON payload and converts it to the specified nullable enum value.
    /// Returns null if the string is empty or cannot be parsed.
    /// </summary>
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var enumString = reader.GetString();
            if (string.IsNullOrEmpty(enumString))
            {
                return null;
            }

            if (Enum.TryParse(enumString, true, out T parsedEnum))
            {
                return parsedEnum;
            }
        }

        return null;
    }

    /// <summary>
    /// Writes the nullable enum value to JSON as a string, or a null if the value is null.
    /// </summary>
    public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteStringValue(value.ToString());
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
