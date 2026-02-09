using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Client
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Socket? client = null;
            Socket? udpClient = null;
            InitInfo? gameInit = null;
            int playerId = 0; // Will be set when we get player ID from server
            IPEndPoint? serverUdpEndPoint = null;

            // Method to send input to server via UDP
            void SendInput(int move, bool fire)
            {
                if (udpClient == null || serverUdpEndPoint == null)
                {
                    return;
                }
                
                try
                {
                    // Create input packet
                    var inputPacket = new InputPacket(playerId, move, fire);
                    
                    // Wrap in envelope
                    var envelope = JsonBytes.Wrap(PacketType.Input, inputPacket);
                    
                    // Serialize to bytes
                    byte[] data = JsonBytes.ToBytes(envelope);
                    
                    // Send via UDP
                    udpClient.SendTo(data, serverUdpEndPoint);
                }
                catch (Exception ex)
                {
                    // Silently handle input send errors
                }
            }

            // Method to render game state
            void Render(StatePacket statePacket)
            {
                if (gameInit == null) return;
                
                // Create grid
                char[,] grid = new char[gameInit.MapH, gameInit.MapW];
                
                // Fill with spaces
                for (int y = 0; y < gameInit.MapH; y++)
                {
                    for (int x = 0; x < gameInit.MapW; x++)
                    {
                        grid[y, x] = ' ';
                    }
                }
                
                // Draw obstacles as '#'
                foreach (var obstacle in statePacket.Obstacles)
                {
                    if (obstacle.X >= 0 && obstacle.X < gameInit.MapW && obstacle.Y >= 0 && obstacle.Y < gameInit.MapH)
                    {
                        grid[obstacle.Y, obstacle.X] = '#';
                    }
                }
                
                // Draw bullets as '*'
                foreach (var bullet in statePacket.Bullets)
                {
                    if (bullet.X >= 0 && bullet.X < gameInit.MapW && bullet.Y >= 0 && bullet.Y < gameInit.MapH)
                    {
                        grid[bullet.Y, bullet.X] = '*';
                    }
                }
                
                // Draw players as '^' (drawn last so they appear on top)
                foreach (var player in statePacket.Players)
                {
                    if (player.X >= 0 && player.X < gameInit.MapW && player.Y >= 0 && player.Y < gameInit.MapH)
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
                    sb.AppendLine($"{player.Name}: {player.Score}/{gameInit.TargetScore} ❤️{player.Lives}");
                }
                
                sb.AppendLine("".PadRight(gameInit.MapW + 2, '─'));
                
                // Draw top border
                sb.Append('┌');
                sb.Append("".PadRight(gameInit.MapW, '─'));
                sb.AppendLine("┐");
                
                // Draw grid rows
                for (int y = 0; y < gameInit.MapH; y++)
                {
                    sb.Append('│');
                    for (int x = 0; x < gameInit.MapW; x++)
                    {
                        sb.Append(grid[y, x]);
                    }
                    sb.AppendLine("│");
                }
                
                // Draw bottom border
                sb.Append('└');
                sb.Append("".PadRight(gameInit.MapW, '─'));
                sb.AppendLine("┘");
                
                sb.AppendLine("← → SPACE Q");
                
                // Set cursor to top-left and write entire frame at once
                Console.SetCursorPosition(0, 0);
                Console.Write(sb.ToString());
            }

            try
            {
                client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                client.Connect(IPAddress.Loopback, NetConstants.TcpPort);
                
                // Send NAME message
                Console.Write("Name: ");
                string? playerName = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(playerName))
                {
                    playerName = "Player1";
                }
                
                byte[] nameMessage = Encoding.UTF8.GetBytes($"NAME:{playerName}");
                client.Send(nameMessage);
                
                // Set up line-based receiving
                var receivedData = new StringBuilder();
                byte[] buffer = new byte[256];
                
                while (true)
                {
                    int bytesReceived = client.Receive(buffer);
                    
                    if (bytesReceived == 0)
                    {
                        Console.WriteLine("Connection lost");
                        break;
                    }
                    
                    // Add received data to buffer
                    string newData = Encoding.UTF8.GetString(buffer, 0, bytesReceived);
                    receivedData.Append(newData);
                    
                    // Process complete lines (ending with \n)
                    string allData = receivedData.ToString();
                    string[] lines = allData.Split('\n');
                    
                    // Keep the last incomplete line in the buffer
                    receivedData.Clear();
                    if (!allData.EndsWith('\n') && lines.Length > 0)
                    {
                        receivedData.Append(lines[lines.Length - 1]);
                        // Remove the incomplete line from processing
                        Array.Resize(ref lines, lines.Length - 1);
                    }
                    
                    // Process each complete line
                    foreach (string line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        
                        if (line.StartsWith("PROMPT:"))
                        {
                            // Server is asking for target score input
                            string prompt = line.Substring(7);
                            Console.Write(prompt);
                            string? userInput = Console.ReadLine();
                            
                            // Send score back to server
                            byte[] scoreMessage = Encoding.UTF8.GetBytes($"SCORE:{userInput}");
                            client.Send(scoreMessage);
                        }
                        else if (line.StartsWith("ERROR:"))
                        {
                            // Server sent an error message
                            string errorMsg = line.Substring(6);
                            Console.Write(errorMsg);
                            string? userInput = Console.ReadLine();
                            
                            // Send score back to server
                            byte[] scoreMessage = Encoding.UTF8.GetBytes($"SCORE:{userInput}");
                            client.Send(scoreMessage);
                        }
                        else if (line.StartsWith("INIT:"))
                        {
                            if (InitParser.TryParse(line, out InitInfo initInfo))
                            {
                                gameInit = initInfo;
                                playerId = initInfo.PlayerId; // Use actual player ID from server
                                
                                // Create UDP socket and test communication
                                try
                                {
                                    udpClient = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                                    udpClient.Blocking = false; // Set to non-blocking
                                    serverUdpEndPoint = new IPEndPoint(IPAddress.Loopback, initInfo.UdpPort);
                                    
                                    // Send PING to server
                                    byte[] pingMessage = Encoding.UTF8.GetBytes("PING");
                                    udpClient.SendTo(pingMessage, serverUdpEndPoint);
                                    
                                    // Wait for PONG response
                                    byte[] udpBuffer = new byte[256];
                                    EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                                    int udpBytesReceived = udpClient.ReceiveFrom(udpBuffer, ref remoteEndPoint);
                                    
                                    string udpResponse = Encoding.UTF8.GetString(udpBuffer, 0, udpBytesReceived);
                                    
                                    if (udpResponse == "PONG")
                                    {
                                        // Connection successful, proceed silently
                                    }
                                }
                                catch (Exception udpEx)
                                {
                                    Console.WriteLine("Connection error");
                                }
                            }
                        }
                        else if (line.StartsWith("GAMEMODE:"))
                        {
                            // Server is asking for game mode selection
                            string prompt = line.Substring(9);
                            Console.Write(prompt);
                            string? userInput = Console.ReadLine();
                            
                            // Send mode back to server
                            byte[] modeMessage = Encoding.UTF8.GetBytes($"MODE:{userInput}");
                            client.Send(modeMessage);
                        }
                        else if (line.StartsWith("MODEERROR:"))
                        {
                            // Server sent a mode error message
                            string errorMsg = line.Substring(10);
                            Console.Write(errorMsg);
                            string? userInput = Console.ReadLine();
                            
                            // Send mode back to server
                            byte[] modeMessage = Encoding.UTF8.GetBytes($"MODE:{userInput}");
                            client.Send(modeMessage);
                        }
                        else if (line.StartsWith("WAIT:"))
                        {
                            // Server is telling us to wait for more players
                            string waitMsg = line.Substring(5);
                            Console.WriteLine(waitMsg);
                        }
                    }
                    
                    // If we have game initialization, start the game loop
                    if (gameInit != null && udpClient != null)
                    {
                        Console.Clear();
                        
                        // Start keyboard input task
                        var inputTask = Task.Run(() =>
                        {
                            try
                            {
                                while (true)
                                {
                                    var key = Console.ReadKey(true);
                                    
                                    if (key.KeyChar == 'q' || key.KeyChar == 'Q')
                                    {
                                        break;
                                    }
                                    							
                                    int move = 0;
                                    bool fire = false;
                                    							
                                    // Translate keys to game inputs
                                    if (key.Key == ConsoleKey.LeftArrow)
                                    {
                                        move = -1;
                                    }
                                    else if (key.Key == ConsoleKey.RightArrow)
                                    {
                                        move = 1;
                                    }
                                    else if (key.Key == ConsoleKey.Spacebar)
                                    {
                                        fire = true;
                                    }
                                    							
                                    // Send input if there's movement or fire
                                    if (move != 0 || fire)
                                    {
                                        SendInput(move, fire);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                // Silently handle input errors
                            }
                        });

                        // Game state receive loop
                        while (true)
                        {
                            try
                            {
                                // Check for UDP data using Select
                                var readSockets = new List<Socket> { udpClient };
                                var errorSockets = new List<Socket> { udpClient };
                                
                                // Non-blocking select with short timeout
                                Socket.Select(readSockets, null, errorSockets, 10_000); // 10ms timeout
                                
                                if (errorSockets.Count > 0)
                                {
                                    break;
                                }
                                
                                if (readSockets.Count > 0)
                                {
                                    // Receive UDP data
                                    byte[] udpBuffer = new byte[2048];
                                    EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                                    int udpBytesReceived = udpClient.ReceiveFrom(udpBuffer, ref remoteEndPoint);
                        
                                    try
                                    {
                                        // Create buffer with exact size
                                        byte[] packetData = new byte[udpBytesReceived];
                                        Array.Copy(udpBuffer, packetData, udpBytesReceived);
                                        
                                        // Deserialize PacketEnvelope
                                        var envelope = JsonBytes.FromBytes<PacketEnvelope>(packetData);
                                        
                                        // Handle State packets
                                        if (envelope.Type == PacketType.State)
                                        {
                                            var statePacket = JsonBytes.Unwrap<StatePacket>(envelope);
                                            Render(statePacket);
                                        }
                                        // Handle GameOver packets
                                        else if (envelope.Type == PacketType.GameOver)
                                        {
                                            var gameOverPayload = JsonBytes.Unwrap<GameOverPayload>(envelope);
                                            
                                            // Clear screen and display game over information
                                            Console.Clear();
                                            Console.WriteLine("══════════════════════");
                                            Console.WriteLine("      GAME OVER");
                                            Console.WriteLine("══════════════════════");
                                            Console.WriteLine();
                                            
                                            // Sort players by score (descending) and display rankings
                                            var sortedPlayers = gameOverPayload.FinalScores
                                                .OrderByDescending(p => p.Score)
                                                .ThenByDescending(p => p.Lives)
                                                .ToArray();
                                            
                                            for (int i = 0; i < sortedPlayers.Length; i++)
                                            {
                                                var player = sortedPlayers[i];
                                                string medal = i switch
                                                {
                                                    0 => "🥇",
                                                    1 => "🥈", 
                                                    2 => "🥉",
                                                    _ => "  "
                                                };
                                                
                                                Console.WriteLine($"{medal} {player.Name}");
                                                Console.WriteLine($"   Score: {player.Score} | Lives: {player.Lives}");
                                                if (i < sortedPlayers.Length - 1) Console.WriteLine();
                                            }
                                            
                                            Console.WriteLine();
                                            
                                            // Display reason-specific message
                                            string reasonMessage = gameOverPayload.Reason switch
                                            {
                                                "targetScore" => "🎯 Victory! Target reached!",
                                                "noLives" => "💀 No lives remaining!",
                                                "disconnect" => "🔌 Connection lost!",
                                                _ => "Game ended"
                                            };
                                            
                                            Console.WriteLine(reasonMessage);
                                            Console.WriteLine("\nPress any key to exit...");
                                            
                                            // Signal input task to stop and wait for key press
                                            Console.ReadKey();
                                            return; // Exit game loop
                                        }
                                    }
                                    catch (Exception parseEx)
                                    {
                                        // Silently ignore parse errors
                                    }
                                }
                                
                                // Small delay to prevent busy waiting
                                Thread.Sleep(1);
                                
                                // Check if input task is completed (user quit)
                                if (inputTask.IsCompleted)
                                {
                                    break;
                                }
                            }
                            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock)
                            {
                                // No data available, continue loop
                                Thread.Sleep(1);
                            }
                            catch (Exception ex)
                            {
                                break;
                            }
                        }

                        // Wait for input task to complete before cleanup
                        try
                        {
                            if (!inputTask.Wait(1000)) // Wait up to 1 second
                            {
                                // Input task timeout, continue with cleanup
                            }
                        }
                        catch (Exception ex)
                        {
                            // Silently handle input task wait errors
                        }

                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Connection error");
            }
            finally
            {
                try
                {
                    client?.Close();
                    udpClient?.Close();
                }
                catch (Exception ex)
                {
                    // Silently handle cleanup errors
                }
            }
        }
    }
}
