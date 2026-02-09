public static class JsonBytes
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerOptions.Default)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static byte[] ToBytes<T>(T obj) => JsonSerializer.SerializeToUtf8Bytes(obj, Options);

    public static T FromBytes<T>(byte[] data) => JsonSerializer.Deserialize<T>(data, Options)!;

    public static PacketEnvelope Wrap<T>(PacketType type, T payload) => new(type, JsonSerializer.Serialize(payload, Options));

    public static T Unwrap<T>(PacketEnvelope env) => JsonSerializer.Deserialize<T>(env.Json, Options)!;
}