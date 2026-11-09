using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeliveryPilot.Projects.API.Contracts.Engagements.Serialization;

/// <summary>Rejects over-precise JSON numbers before decimal parsing can round them.</summary>
public abstract class ExactDecimalJsonConverter(int fractionalPlaces) : JsonConverter<decimal?>
{
    public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetDecimal(out decimal value))
        {
            throw new JsonException("A valid decimal number is required.");
        }

        string raw = reader.HasValueSequence ? Encoding.UTF8.GetString(reader.ValueSequence.ToArray()) : Encoding.UTF8.GetString(reader.ValueSpan);
        int exponentIndex = raw.IndexOfAny(['e', 'E']);
        ReadOnlySpan<char> mantissa = exponentIndex < 0 ? raw.AsSpan() : raw.AsSpan(0, exponentIndex);
        long exponent = 0;
        if (exponentIndex >= 0 && !long.TryParse(raw.AsSpan(exponentIndex + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent))
        {
            exponent = raw[exponentIndex + 1] == '-' ? long.MinValue : long.MaxValue;
        }

        int dot = mantissa.IndexOf('.');
        int decimals = dot < 0 ? 0 : mantissa.Length - dot - 1;
        int trailingZeros = 0;
        bool nonzero = false;
        for (int i = mantissa.Length - 1; i >= 0; i--)
        {
            char digit = mantissa[i];
            if (digit is '.' or '-')
            {
                continue;
            }

            if (digit != '0')
            {
                nonzero = true;
                break;
            }

            trailingZeros++;
        }

        if (nonzero && exponent < (long)decimals - trailingZeros - fractionalPlaces)
        {
            throw new JsonException($"At most {fractionalPlaces} decimal places are allowed.");
        }

        if (nonzero)
        {
            // Decimal parsing can also round a large coefficient even when its scale is valid.
            // Compare the normalized wire number with the exact parsed decimal representation.
            if (exponent is < -100000 or > 100000)
            {
                throw new JsonException("The number cannot be represented exactly.");
            }

            string coefficient = mantissa.ToString().Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace(".", string.Empty, StringComparison.Ordinal).TrimStart('0').TrimEnd('0');
            int[] bits = decimal.GetBits(value);
            BigInteger parsed = ((BigInteger)(uint)bits[2] << 64) | ((BigInteger)(uint)bits[1] << 32) | (uint)bits[0];
            int parsedExponent = -((bits[3] >> 16) & 0xff);
            while (parsed != 0 && parsed % 10 == 0)
            {
                parsed /= 10;
                parsedExponent++;
            }

            if (parsed.ToString(CultureInfo.InvariantCulture) != coefficient || parsedExponent != exponent - decimals + trailingZeros)
            {
                throw new JsonException("The number cannot be represented exactly.");
            }
        }

        return value;
    }

    public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteNumberValue(value.Value);
        }
    }
}
