using Serilog.Core;
using Serilog.Events;

namespace Diten.Platform.API.Observability;

public sealed class SensitiveDataLogEventEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var property in logEvent.Properties.ToArray())
        {
            var redactedValue = Redact(property.Value);
            if (ReferenceEquals(redactedValue, property.Value))
            {
                continue;
            }

            logEvent.RemovePropertyIfPresent(property.Key);
            logEvent.AddPropertyIfAbsent(new LogEventProperty(property.Key, redactedValue));
        }
    }

    private static LogEventPropertyValue Redact(LogEventPropertyValue value)
    {
        return value switch
        {
            ScalarValue { Value: string text } => new ScalarValue(SensitiveDataRedactor.Redact(text)),
            SequenceValue sequence => RedactSequence(sequence),
            StructureValue structure => RedactStructure(structure),
            DictionaryValue dictionary => RedactDictionary(dictionary),
            _ => value
        };
    }

    private static SequenceValue RedactSequence(SequenceValue sequence)
    {
        return new SequenceValue(sequence.Elements.Select(Redact));
    }

    private static StructureValue RedactStructure(StructureValue structure)
    {
        var properties = structure.Properties
            .Select(property => new LogEventProperty(property.Name, Redact(property.Value)));

        return new StructureValue(properties, structure.TypeTag);
    }

    private static DictionaryValue RedactDictionary(DictionaryValue dictionary)
    {
        var elements = dictionary.Elements.ToDictionary(
            pair => pair.Key,
            pair => Redact(pair.Value));

        return new DictionaryValue(elements);
    }
}
