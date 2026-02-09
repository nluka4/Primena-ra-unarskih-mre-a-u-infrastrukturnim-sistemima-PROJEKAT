public record GameConfig(int MapW, int MapH, int TargetScore, int Mode)
{
    public static GameConfig Default() => new GameConfig(NetConstants.MapW, NetConstants.MapH, 10, 1);
}