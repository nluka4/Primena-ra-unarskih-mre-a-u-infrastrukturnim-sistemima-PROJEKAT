






using System.Net;
using System.Net.Sockets;
using System.Text;

static void Main(string[] args)
{

    PocetneInformacije? pokreniIgru = null;
    Socket? klijent = null;
    IPEndPoint? server = null;
    int playerId = 0;
    void SendInput(int move, bool fire)
    {
        if (klijent == null || server == null)
        {
            return;
        }

        try
        {
            // Create input packet
            var inputPacket = new UlazniPodaci(playerId, move, fire);

            // Wrap in envelope
            var envelope = JsonBytes.Wrap(TipPaketa.Input, inputPacket);

            // Serialize to bytes
            byte[] data = JsonBytes.ToBytes(envelope);

            // Send via UDP
            klijent.SendTo(data, server);
        }
        catch (Exception ex)
        {
            // Silently handle input send errors
        }
    }
    void Render(StanjeIgrice statePacket)
    {
        if (pokreniIgru == null) return;

        // Create grid
        char[,] grid = new char[pokreniIgru.MapH, pokreniIgru.MapW];

        // Fill with spaces
        for (int y = 0; y < pokreniIgru.MapH; y++)
        {
            for (int x = 0; x < pokreniIgru.MapW; x++)
            {
                grid[y, x] = ' ';
            }
        }

        // Draw obstacles as '#'
        foreach (var obstacle in statePacket.Obstacles)
        {
            if (obstacle.X >= 0 && obstacle.X < pokreniIgru.MapW && obstacle.Y >= 0 && obstacle.Y < pokreniIgru.MapH)
            {
                grid[obstacle.Y, obstacle.X] = '#';
            }
        }

        // Draw bullets as '*'
        foreach (var bullet in statePacket.Bullets)
        {
            if (bullet.X >= 0 && bullet.X < pokreniIgru.MapW && bullet.Y >= 0 && bullet.Y < pokreniIgru.MapH)
            {
                grid[bullet.Y, bullet.X] = '*';
            }
        }

        // Draw players as '^' (drawn last so they appear on top)
        foreach (var player in statePacket.Players)
        {
            if (player.X >= 0 && player.X < pokreniIgru.MapW && player.Y >= 0 && player.Y < pokreniIgru.MapH)
            {
                grid[player.Y, player.X] = '^';
            }
        }

        // Use StringBuilder to minimize flicker
        var sb = new StringBuilder();

        // Add minimalistic game info header
        sb.AppendLine("SPACE INVADERS");

        // Add player info in a clean format
        foreach (var player in statePacket.Players)
        {
            sb.AppendLine($"{player.Name}: {player.Score}/{pokreniIgru.TargetScore} ❤️{player.Lives}");
        }

        sb.AppendLine("".PadRight(pokreniIgru.MapW + 2, '─'));

        // Draw top border
        sb.Append('┌');
        sb.Append("".PadRight(pokreniIgru.MapW, '─'));
        sb.AppendLine("┐");

        // Draw grid rows
        for (int y = 0; y < pokreniIgru .MapH; y++)
        {
            sb.Append('│');
            for (int x = 0; x < pokreniIgru.MapW; x++)
            {
                sb.Append(grid[y, x]);
            }
            sb.AppendLine("│");
        }

        // Draw bottom border
        sb.Append('└');
        sb.Append("".PadRight(pokreniIgru.MapW, '─'));
        sb.AppendLine("┘");

        sb.AppendLine("← → SPACE Q");

        // Set cursor to top-left and write entire frame at once
        Console.SetCursorPosition(0, 0);
        Console.Write(sb.ToString());
    }
}