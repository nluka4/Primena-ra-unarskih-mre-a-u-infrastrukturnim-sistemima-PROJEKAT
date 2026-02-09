public record PlayerState(int Id, string Name, int X, int Y, int Lives, int Score);

public record Obstacle(int X, int Y);

public record Bullet(int X, int Y, int FromPlayerId);

public record InputPacket(int PlayerId, int Move, bool Fire);

public record StatePacket(PlayerState[] Players, Obstacle[] Obstacles, Bullet[] Bullets);

public record GameOverPayload(PlayerState[] FinalScores, string Reason);

public enum PacketType { Input = 1, State = 2, GameOver = 3, Init = 4, Lobby = 5 }

public record PacketEnvelope(PacketType Type, string Json);

public record InitInfo(int MapW, int MapH, int StartX, int StartY, int UdpPort, int TargetScore, int PlayerId);

