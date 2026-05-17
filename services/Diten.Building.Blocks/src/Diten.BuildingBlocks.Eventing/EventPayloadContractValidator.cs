using System.Collections;
using System.Reflection;

namespace Diten.BuildingBlocks.Eventing;

public sealed class EventPayloadContractValidator
{
    private static readonly string[] SensitiveTerms =
    [
        "password",
        "token",
        "secret",
        "credential",
        "connectionstring",
        "keymaterial",
        "apikey",
        "privatekey"
    ];

    public void Validate<TEvent>(TEvent @event)
        where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(@event);
        ValidateType(typeof(TEvent), typeof(TEvent).Name);
        EventName.EnsureMatchesVersion(@event.EventName, @event.EventVersion);
    }

    private static void ValidateType(Type type, string path)
    {
        if (IsEntityType(type))
        {
            throw new EventValidationException($"Event payload '{path}' must not inherit from or include persistence entity types.");
        }

        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            var normalizedName = property.Name.Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
            if (SensitiveTerms.Any(term => normalizedName.Contains(term, StringComparison.Ordinal)))
            {
                throw new EventValidationException($"Event payload property '{path}.{property.Name}' contains sensitive data.");
            }

            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (IsEntityType(propertyType))
            {
                throw new EventValidationException($"Event payload property '{path}.{property.Name}' must not include entity types.");
            }

            if (propertyType == typeof(byte[]) || propertyType == typeof(Stream))
            {
                throw new EventValidationException($"Event payload property '{path}.{property.Name}' must not include binary/blob data.");
            }

            if (propertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(propertyType))
            {
                throw new EventValidationException($"Event payload property '{path}.{property.Name}' must not include collections.");
            }

            if (!IsAllowedValueType(propertyType))
            {
                throw new EventValidationException($"Event payload property '{path}.{property.Name}' must be primitive or a value-object record without navigation properties.");
            }
        }
    }

    private static bool IsAllowedValueType(Type type)
    {
        if (type.IsPrimitive || type.IsEnum)
        {
            return true;
        }

        return type == typeof(string)
               || type == typeof(Guid)
               || type == typeof(DateTime)
               || type == typeof(DateTimeOffset)
               || type == typeof(TimeSpan)
               || type == typeof(decimal);
    }

    private static bool IsEntityType(Type type)
    {
        return type.Name is "BaseEntity" or "EntityBase" or "GlobalEntity"
               || type.BaseType is not null && IsEntityType(type.BaseType)
               || type.Namespace?.Contains(".Entities", StringComparison.OrdinalIgnoreCase) == true;
    }
}
