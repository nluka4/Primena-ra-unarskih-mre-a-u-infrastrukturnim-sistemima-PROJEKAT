
using System.Net.Sockets;
using System.Text.Json;

public class StanjeIgraca
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int Lives { get; set; }
    public int Score { get; set; }

    public
    StanjeIgraca(int id, string name, int x, int y, int lives, int score)
    {
        Id = id;
        Name = name;
        X = x;
        Y = y;
        Lives = lives;
        Score = score;
    }
}

public class Prepreka
{
    public int X { get; set; }
    public int Y { get; set; }


    public Prepreka(int x, int y)
    {
        X = x;
        Y = y;
    }
}

public class Metak
{
    public int X { get; set; }
    public int Y { get; set; }
    public int FromPlayerId { get; set; }


    public Metak(int x, int y, int fromPlayerId)
    {
        X = x;
        Y = y;
        FromPlayerId = fromPlayerId;
    }
}

public class StanjeIgrice
{
    public StanjeIgraca[] Players { get; set; } = Array.Empty<StanjeIgraca>();
    public Prepreka[] Obstacles { get; set; } = Array.Empty<Prepreka>();
    public Metak[] Bullets { get; set; } = Array.Empty<Metak>();


    public StanjeIgrice(StanjeIgraca[] players, Prepreka[] obstacles, Metak[] bullets)
    {
        Players = players;
        Obstacles = obstacles;
        Bullets = bullets;
    }
}

public record PocetneInformacije
{
    public int MapW { get; init; }
    public int MapH { get; init; }
    public int StartX { get; init; }
    public int StartY { get; init; }
    public int UdpPort { get; init; }
    public int TargetScore { get; init; }
    public int PlayerId { get; init; }

    public PocetneInformacije(int MapW, int MapH, int StartX, int StartY, int UdpPort, int TargetScore, int PlayerId)
    {
        this.MapW = MapW;
        this.MapH = MapH;
        this.StartX = StartX;
        this.StartY = StartY;
        this.UdpPort = UdpPort;
        this.TargetScore = TargetScore;
        this.PlayerId = PlayerId;
    }
}

public class UlazniPodaci
{
    public int PlayerId { get; set; }
    public int Move { get; set; }
    public bool Fire { get; set; }

    public UlazniPodaci(int playerId, int move, bool fire)
    {
        PlayerId = playerId;
        Move = move;
        Fire = fire;
    }
}

public enum TipPaketa { Input = 1, State = 2, GameOver = 3, Init = 4, Lobby = 5 }

public class Preimenuj
{
    public TipPaketa Type { get; set; }
    public string Json { get; set; }

    public Preimenuj(TipPaketa type, string json)
    {
        Type = type;
        Json = json;
    }
}


public static class JsonBytes
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerOptions.Default)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static byte[] ToBytes<T>(T obj) => JsonSerializer.SerializeToUtf8Bytes(obj, Options);

    public static T FromBytes<T>(byte[] data) => JsonSerializer.Deserialize<T>(data, Options)!;

    public static Preimenuj Wrap<T>(TipPaketa type, T payload) => new(type, JsonSerializer.Serialize(payload, Options));

    public static T Unwrap<T>(Preimenuj env) => JsonSerializer.Deserialize<T>(env.Json, Options)!;
}