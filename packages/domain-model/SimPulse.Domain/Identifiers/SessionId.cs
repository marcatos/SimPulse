namespace SimPulse.Domain;

public readonly record struct SessionId(Guid Value)
{
    public static SessionId New() => new(Guid.NewGuid());

    public static SessionId Parse(string value) => new(Guid.Parse(value));

    public override string ToString() => Value.ToString("D");
}
