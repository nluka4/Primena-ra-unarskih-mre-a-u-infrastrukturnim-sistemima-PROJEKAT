public class PlayerState
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Lives { get; set; }
    public int Score { get; set; }

    public PlayerState(int id, string name, int x, int y, int lives, int score)
    {
        Id = id;
        Name = name;
        X = x;
        Y = y;
        Lives = lives;
        Score = score;
    }
}

public class Obstacle
{
    public int X { get; set; }
    public int Y { get; set; }

    public Obstacle(int x, int y)
    {
        X = x;
        Y = y;
    }
}

public class Bullet
{
    public int X { get; set; }
    public int Y { get; set; }
    public int FromPlayerId { get; set; }

    public Bullet(int x, int y, int fromPlayerId)
    {
        X = x;
        Y = y;
        FromPlayerId = fromPlayerId;
    }
}

public class InputPacket
{
    public int PlayerId { get; set; }
    public int Move { get; set; }
    public bool Fire { get; set; }

    public InputPacket(int playerId, int move, bool fire)
    {
        PlayerId = playerId;
        Move = move;
        Fire = fire;
    }
}

public class StatePacket
{
    public PlayerState[] Players { get; set; }
    public Obstacle[] Obstacles { get; set; }
    public Bullet[] Bullets { get; set; }

    public StatePacket(PlayerState[] players, Obstacle[] obstacles, Bullet[] bullets)
    {
        Players = players;
        Obstacles = obstacles;
        Bullets = bullets;
    }
}

public class GameOverPayload
{
    public PlayerState[] FinalScores { get; set; }
    public string Reason { get; set; }

    public GameOverPayload(PlayerState[] finalScores, string reason)
    {
        FinalScores = finalScores;
        Reason = reason;
    }
}

public enum PacketType { Input = 1, State = 2, GameOver = 3, Init = 4, Lobby = 5 }

public class PacketEnvelope
{
    public PacketType Type { get; set; }
    public string Json { get; set; }

    public PacketEnvelope(PacketType type, string json)
    {
        Type = type;
        Json = json;
    }
}

public class InitInfo
{
    public int MapW { get; set; }
    public int MapH { get; set; }
    public int StartX { get; set; }
    public int StartY { get; set; }
    public int UdpPort { get; set; }
    public int TargetScore { get; set; }
    public int PlayerId { get; set; }

    public InitInfo(int mapW, int mapH, int startX, int startY, int udpPort, int targetScore, int playerId)
    {
        MapW = mapW;
        MapH = mapH;
        StartX = startX;
        StartY = startY;
        UdpPort = udpPort;
        TargetScore = targetScore;
        PlayerId = playerId;
    }
}