using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace EveConv.Cache.Redis;

/// <summary>
/// Serialization functionality for RedisCache.
/// </summary>
public partial class RedisCache
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private const string NullMarker = "__NULL__";

    /// <summary>
    /// Serializes an object to a string representation for Redis storage.
    /// </summary>
    /// <param name="value">The object to serialize.</param>
    /// <returns>The serialized string representation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when serialization fails.</exception>
    protected string SerializeValue(object? value)
    {
        try
        {
            if (value is null)
            {
                return NullMarker;
            }

            // Handle string values directly to avoid double serialization
            if (value is string stringValue)
            {
                return stringValue;
            }

            // Handle primitive types that can be converted to string directly
            if (IsPrimitiveType(value.GetType()))
            {
                return value.ToString() ?? NullMarker;
            }

            // Serialize complex objects to JSON
            return _configuration.SerializationType switch
            {
                SerializationType.Json => JsonSerializer.Serialize(value, JsonOptions),
                _ => throw new NotSupportedException($"Serialization type {_configuration.SerializationType} is not supported")
            };
        }
        catch (Exception ex) when (ex is not RedisCacheSerializationException)
        {
            _logger.LogError(ex, "Failed to serialize value of type {ValueType}", value?.GetType().Name ?? "null");
            throw new RedisCacheSerializationException($"Failed to serialize value of type {value?.GetType().Name ?? "null"}", ex);
        }
    }

    /// <summary>
    /// Deserializes a string representation back to an object.
    /// </summary>
    /// <param name="serializedValue">The serialized string value.</param>
    /// <returns>The deserialized object, or null if the value represents null.</returns>
    /// <exception cref="InvalidOperationException">Thrown when deserialization fails.</exception>
    protected object? DeserializeValue(string? serializedValue)
    {
        try
        {
            if (string.IsNullOrEmpty(serializedValue) || serializedValue == NullMarker)
            {
                return null;
            }

            // Try to deserialize as JSON first for complex objects
            if (serializedValue.StartsWith('{') || serializedValue.StartsWith('['))
            {
                return _configuration.SerializationType switch
                {
                    SerializationType.Json => JsonSerializer.Deserialize<object>(serializedValue, JsonOptions),
                    _ => throw new NotSupportedException($"Serialization type {_configuration.SerializationType} is not supported")
                };
            }

            // Return as string for simple values
            return serializedValue;
        }
        catch (Exception ex) when (ex is not RedisCacheSerializationException)
        {
            _logger.LogError(ex, "Failed to deserialize value: {SerializedValue}", serializedValue);
            throw new RedisCacheSerializationException($"Failed to deserialize value: {serializedValue}", ex);
        }
    }

    /// <summary>
    /// Deserializes a string representation to a specific type.
    /// </summary>
    /// <typeparam name="T">The target type for deserialization.</typeparam>
    /// <param name="serializedValue">The serialized string value.</param>
    /// <returns>The deserialized object of type T, or default(T) if the value represents null.</returns>
    /// <exception cref="InvalidOperationException">Thrown when deserialization fails.</exception>
    protected T? DeserializeValue<T>(string? serializedValue)
    {
        try
        {
            if (string.IsNullOrEmpty(serializedValue) || serializedValue == NullMarker)
            {
                return default;
            }

            var targetType = typeof(T);

            // Handle string type directly
            if (targetType == typeof(string))
            {
                return (T)(object)serializedValue;
            }

            // Handle primitive types
            if (IsPrimitiveType(targetType))
            {
                return (T)Convert.ChangeType(serializedValue, targetType);
            }

            // Handle nullable types
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var underlyingType = Nullable.GetUnderlyingType(targetType);
                if (underlyingType != null && IsPrimitiveType(underlyingType))
                {
                    return (T)Convert.ChangeType(serializedValue, underlyingType);
                }
            }

            // Deserialize complex objects from JSON
            return _configuration.SerializationType switch
            {
                SerializationType.Json => JsonSerializer.Deserialize<T>(serializedValue, JsonOptions),
                _ => throw new NotSupportedException($"Serialization type {_configuration.SerializationType} is not supported")
            };
        }
        catch (Exception ex) when (ex is not RedisCacheSerializationException)
        {
            _logger.LogError(ex, "Failed to deserialize value to type {TargetType}: {SerializedValue}",
                typeof(T).Name, serializedValue);
            throw new RedisCacheSerializationException($"Failed to deserialize value to type {typeof(T).Name}: {serializedValue}", ex);
        }
    }

    /// <summary>
    /// Determines if a type is a primitive type that can be converted directly.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is primitive; otherwise, false.</returns>
    private static bool IsPrimitiveType(Type type)
    {
        // Boolean, Byte, SByte, Int16, Int32, UInt16, UInt32, Int64, UInt64, IntPtr, UIntPtr, Char, Double, Single
        return type.IsPrimitive ||
               type == typeof(string) ||
               type == typeof(decimal) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(TimeSpan) ||
               type == typeof(Guid) ||
               (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>) &&
                IsPrimitiveType(Nullable.GetUnderlyingType(type)!));
    }
}