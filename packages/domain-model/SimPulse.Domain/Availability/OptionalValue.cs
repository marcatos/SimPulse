namespace SimPulse.Domain;

public enum DataPresence
{
    Available = 0,
    Unavailable = 1,
    Unknown = 2
}

/// <summary>
/// Explicit availability. Callers must not treat default(T) as a real measurement.
/// </summary>
public sealed class OptionalValue<T>
{
    public DataPresence Presence { get; }

    public T? Value { get; }

    private OptionalValue(DataPresence presence, T? value)
    {
        Presence = presence;
        Value = value;
    }

    public static OptionalValue<T> Available(T value) => new(DataPresence.Available, value);

    public static OptionalValue<T> Unavailable() => new(DataPresence.Unavailable, default);

    public static OptionalValue<T> Unknown() => new(DataPresence.Unknown, default);

    public bool TryGet(out T value)
    {
        if (Presence != DataPresence.Available)
        {
            value = default!;
            return false;
        }

        value = Value!;
        return true;
    }
}
