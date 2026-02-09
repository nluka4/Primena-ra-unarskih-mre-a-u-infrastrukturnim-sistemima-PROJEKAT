namespace Server
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Socket? server = null;
            Socket? udp = null;
            var clients = new List<Socket>();
            var playerNames = new Dictionary<Socket, string>();
            var playerInputs = new Dictionary<int, InputPacket>(); // Store latest input per player ID
            var playerStates = new Dictionary<int, PlayerState>(); // Store current player states
            var lastSeen = new Dictionary<int, DateTime>(); // Track last seen time for each player
            var bullets = new List<Bullet>(); // Store active bullets
            var obstacles = new List<Obstacle>(); // Store active obstacles
            var clientEndPoints = new HashSet<EndPoint>(); // Store UDP endpoints of clients
            var gameMode = 0; // 0 = not set, 1 = single player, 2 = multiplayer
            var playersNeeded = 1; // Will be set based on game mode
            var gameModeSet = false; // Track if game mode has been chosen
            var sharedTargetScore = 10; // Shared target score for multiplayer
            var obstacleSpawnTimer = 0; // Timer for obstacle spawning
            var obstacleMoveTimer = 0; // Timer for obstacle movement (to slow them down)
            var random = new Random(); // Random number generator for obstacle positions
            var gameEnded = false; // Flag to indicate game has ended
            var gameConfig = GameConfig.Default(); // Store game configuration

            // Game timer
            using PeriodicTimer timer = new(NetConstants.Tick);

            try
            {
                server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                server.Bind(new IPEndPoint(IPAddress.Loopback, NetConstants.TcpPort));
                server.Listen(10);
                server.Blocking = false;
                
                udp = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                udp.Bind(new IPEndPoint(IPAddress.Loopback, NetConstants.UdpPort));
                udp.Blocking = false;
                
                Console.WriteLine("🚀 Space Invaders Server");
                Console.WriteLine($"🌐 Server started on {IPAddress.Loopback}:{NetConstants.TcpPort}");
                Console.WriteLine("⏳ Waiting for players...\n");

                // Start game loop
                var gameLoopTask = Task.Run(async () =>
                {
                    while (await timer.WaitForNextTickAsync() && !gameEnded)
                    {
                        try
                        {
                            if (UpdateGame(playerInputs, playerStates, lastSeen, bullets, obstacles, ref obstacleSpawnTimer, ref obstacleMoveTimer, random, udp, clientEndPoints, server, ref gameEnded, ref gameConfig))
                            {
                                // Game ended, break out of loop
                                break;
                            }
                            
                            if (!gameEnded)
                            {
                                BroadcastState(udp, playerStates, bullets, obstacles, clientEndPoints);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Only log critical errors that would crash the server
                            Console.WriteLine($"❌ Critical server error: {ex.Message}");
                            break;
                        }
                    }
                });

                while (!gameEnded)
                {
                    var readSockets = new List<Socket> { server, udp };
                    readSockets.AddRange(clients);
                    var errorSockets = new List<Socket> { server, udp };
                    errorSockets.AddRange(clients);
                    
                    Socket.Select(readSockets, null, errorSockets, 1_000_000);
                    
                    // Check for errors
                    if (errorSockets.Count > 0)
                    {
                        // Remove error clients silently
                        foreach (var errorSocket in errorSockets)
                        {
                            if (errorSocket != server && errorSocket != udp && clients.Contains(errorSocket))
                            {
                                clients.Remove(errorSocket);
                                playerNames.Remove(errorSocket);
                                errorSocket.Close();
                            }
                        }
                        if (errorSockets.Contains(server) || errorSockets.Contains(udp))
                        {
                            Console.WriteLine("❌ Server connection error");
                            break;
                        }
                    }
                    
                    // Check for incoming TCP connections
                    if (readSockets.Contains(server))
                    {
                        Socket client = server.Accept();
                        client.Blocking = false;
                        clients.Add(client);
                    }
                    
                    // Check for UDP data
                    if (readSockets.Contains(udp))
                    {
                        try
                        {
                            byte[] udpBuffer = new byte[2048];
                            EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                            int udpBytesReceived = udp.ReceiveFrom(udpBuffer, ref remoteEndPoint);
                            
                            // Remember this client's UDP endpoint
                            clientEndPoints.Add(remoteEndPoint);
                            
                            // Try to parse as text first (for PING)
                            string udpMessage = Encoding.UTF8.GetString(udpBuffer, 0, udpBytesReceived);
                            
                            // Handle PING message
                            if (udpMessage == "PING")
                            {
                                byte[] pongResponse = Encoding.UTF8.GetBytes("PONG");
                                udp.SendTo(pongResponse, remoteEndPoint);
                            }
                            else
                            {
                                // Try to parse as PacketEnvelope
                                try
                                {
                                    // Create buffer with exact size
                                    byte[] packetData = new byte[udpBytesReceived];
                                    Array.Copy(udpBuffer, packetData, udpBytesReceived);
                                    
                                    // Deserialize PacketEnvelope
                                    var envelope = JsonBytes.FromBytes<PacketEnvelope>(packetData);
                                    
                                    // Handle Input packets
                                    if (envelope.Type == PacketType.Input)
                                    {
                                        var inputPacket = JsonBytes.Unwrap<InputPacket>(envelope);
                                        playerInputs[inputPacket.PlayerId] = inputPacket;
                                        
                                        // Update last seen time for this player
                                        lastSeen[inputPacket.PlayerId] = DateTime.UtcNow;
                                        
                                        // Handle firing
                                        if (inputPacket.Fire && playerStates.ContainsKey(inputPacket.PlayerId))
                                        {
                                            var player = playerStates[inputPacket.PlayerId];
                                            var bullet = new Bullet(player.X, player.Y - 1, inputPacket.PlayerId);
                                            bullets.Add(bullet);
                                        }
                                    }
                                }
                                catch (Exception parseEx)
                                {
                                    // Silently ignore malformed packets
                                }
                            }
                        }
                        catch (SocketException ex)
                        {
                            // Silently ignore UDP receive errors
                        }
                    }
                    
                    // Check for data from existing TCP clients
                    var clientsToRemove = new List<Socket>();
                    foreach (var client in clients)
                    {
                        if (readSockets.Contains(client))
                        {
                            try
                            {
                                byte[] buffer = new byte[256];
                                int bytesReceived = client.Receive(buffer);
                                
                                if (bytesReceived == 0)
                                {
                                    // Graceful disconnect
                                    playerNames.Remove(client);
                                    clientsToRemove.Add(client);
                                }
                                else
                                {
                                    string message = Encoding.UTF8.GetString(buffer, 0, bytesReceived).Trim();
                                    
                                    // Check if this is a NAME message
                                    if (message.StartsWith("NAME:"))
                                    {
                                        string playerName = message.Substring(5);
                                        playerNames[client] = playerName;
                                        Console.WriteLine($"🎮 {playerName} joined the game");
                                        
                                        // Ask for game mode if not set (first player)
                                        if (!gameModeSet)
                                        {
                                            string modePrompt = "GAMEMODE:Choose game mode (1=Single Player, 2=Multiplayer): \n";
                                            byte[] modePromptBytes = Encoding.UTF8.GetBytes(modePrompt);
                                            client.Send(modePromptBytes);
                                            Thread.Sleep(10);
                                        }
                                        else if (gameMode == 1 || playerStates.Count == 0)
                                        {
                                            // Single player mode OR first player in multiplayer - ask for target score
                                            string scorePrompt = "PROMPT:Enter target score (1-50): \n";
                                            byte[] scorePromptBytes = Encoding.UTF8.GetBytes(scorePrompt);
                                            client.Send(scorePromptBytes);
                                            Thread.Sleep(10);
                                        }
                                        else
                                        {
                                            // Second player in multiplayer - automatically use shared score, no prompt needed
                                            
                                            // Initialize player state directly
                                            int playerId = playerStates.Count + 1; // Proper ID assignment (1, 2, 3...)
                                            int startX = 3 * NetConstants.MapW / 4; // Second player position
                                            int startY = NetConstants.MapH - 2;

                                            playerStates[playerId] = new PlayerState(playerId, playerName, startX, startY, 3, 0);
                                            
                                            // Initialize last seen time
                                            lastSeen[playerId] = DateTime.UtcNow;
                                            
                                            // Update game config
                                            gameConfig = new GameConfig(NetConstants.MapW, NetConstants.MapH, sharedTargetScore, gameMode);
                                            
                                            // Check if we have enough players now
                                            if (playerStates.Count >= playersNeeded)
                                            {
                                                Console.WriteLine($"🎯 Game starting with {playerStates.Count} players (Target: {sharedTargetScore})\n");
                                                
                                                // Send INIT message to ALL players
                                                foreach (var playerClient in playerNames.Keys)
                                                {
                                                    try
                                                    {
                                                        if (playerClient.Connected)
                                                        {
                                                            // Find the player ID for this client
                                                            var playerEntry = playerStates.FirstOrDefault(p => playerNames[playerClient] == p.Value.Name);
                                                            int clientPlayerId = playerEntry.Key;
                                                            var clientPlayer = playerEntry.Value;
                                                           
                                                            string initMessage = $"INIT:map={NetConstants.MapH}x{NetConstants.MapW};startX={clientPlayer.X};startY={clientPlayer.Y};udpPort={NetConstants.UdpPort};targetScore={sharedTargetScore};playerId={clientPlayerId}\n";
                                                            byte[] initBytes = Encoding.UTF8.GetBytes(initMessage);
                                                            
                                                            playerClient.Send(initBytes);
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        // Silently handle INIT send failures
                                                    }
                                                }
                                            }
                                            else if (gameMode == 2)
                                            {
                                                // Multiplayer mode - waiting for more players
                                                string waitMessage = $"WAIT:Waiting for more players ({playerStates.Count}/{playersNeeded})... Target score: {sharedTargetScore}\n";
                                                byte[] waitBytes = Encoding.UTF8.GetBytes(waitMessage);
                                                client.Send(waitBytes);
                                            }
                                        }
                                    }
                                    else if (message.StartsWith("MODE:"))
                                    {
                                        // Parse game mode
                                        string modeStr = message.Substring(5);
                                        if (int.TryParse(modeStr, out int selectedMode) && (selectedMode == 1 || selectedMode == 2))
                                        {
                                            gameMode = selectedMode;
                                            playersNeeded = gameMode; // 1 for single, 2 for multiplayer
                                            gameModeSet = true;
                                            
                                            Console.WriteLine($"🎮 Mode: {(gameMode == 1 ? "Single Player" : "Multiplayer")}");
                                            
                                            // Now ask for target score
                                            string scorePrompt = "PROMPT:Enter target score (1-50): \n";
                                            byte[] scorePromptBytes = Encoding.UTF8.GetBytes(scorePrompt);
                                            client.Send(scorePromptBytes);
                                            Thread.Sleep(10);
                                        }
                                        else
                                        {
                                            // Invalid mode, ask again
                                            string errorPrompt = "MODEERROR:Invalid mode! Choose game mode (1 = Single Player, 2 = Multiplayer): \n";
                                            byte[] errorBytes = Encoding.UTF8.GetBytes(errorPrompt);
                                            client.Send(errorBytes);
                                        }
                                    }
                                    else if (message.StartsWith("SCORE:"))
                                    {
                                        // Parse target score
                                        string scoreStr = message.Substring(6);
                                        if (int.TryParse(scoreStr, out int targetScore) && targetScore >= 1 && targetScore <= 50)
                                        {
                                            // For first player or single player mode, set the shared target score
                                            if (playerStates.Count == 0 || gameMode == 1)
                                            {
                                                sharedTargetScore = targetScore;
                                            }

                                            // Update game configuration with shared target score
                                            gameConfig = new GameConfig(NetConstants.MapW, NetConstants.MapH, sharedTargetScore, gameMode);
                                            
                                            // Initialize player state
                                            int playerId = playerStates.Count + 1; // Proper ID assignment (1, 2, 3...)
                                            int startX = NetConstants.MapW / 2;
                                            int startY = NetConstants.MapH - 2;
                                            
                                            // For multiplayer, offset start positions
                                            if (gameMode == 2 && playerStates.Count > 0)
                                            {
                                                startX = playerStates.Count == 1 ? NetConstants.MapW / 4 : 3 * NetConstants.MapW / 4;
                                            }

                                            playerStates[playerId] = new PlayerState(playerId, playerNames[client], startX, startY, 3, 0);
                                            
                                            // Initialize last seen time
                                            lastSeen[playerId] = DateTime.UtcNow;
                                            
                                            // Check if we have enough players for the current game mode
                                            if (playerStates.Count >= playersNeeded)
                                            {
                                                Console.WriteLine($"🎯 Game starting with {playerStates.Count} players (Target: {sharedTargetScore})\n");
                                                
                                                // Send INIT message to ALL players
                                                foreach (var playerClient in playerNames.Keys)
                                                {
                                                    try
                                                    {
                                                        if (playerClient.Connected)
                                                        {
                                                            // Find the player ID for this client
                                                            var playerEntry = playerStates.FirstOrDefault(p => playerNames[playerClient] == p.Value.Name);
                                                            int clientPlayerId = playerEntry.Key;
                                                            var clientPlayer = playerEntry.Value;
                                                           
                                                            string initMessage = $"INIT:map={NetConstants.MapH}x{NetConstants.MapW};startX={clientPlayer.X};startY={clientPlayer.Y};udpPort={NetConstants.UdpPort};targetScore={sharedTargetScore};playerId={clientPlayerId}\n";
                                                            byte[] initBytes = Encoding.UTF8.GetBytes(initMessage);
                                                            
                                                            playerClient.Send(initBytes);
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        // Silently handle INIT send failures
                                                    }
                                                }
                                            }
                                            else if (gameMode == 2)
                                            {
                                                // Multiplayer mode - waiting for more players
                                                string waitMessage = $"WAIT:Waiting for more players ({playerStates.Count}/{playersNeeded})... Target score: {sharedTargetScore}\n";
                                                byte[] waitBytes = Encoding.UTF8.GetBytes(waitMessage);
                                                client.Send(waitBytes);
                                            }
                                        }
                                        else
                                        {
                                            // Invalid score, ask again
                                            string errorPrompt = "ERROR:Invalid score! Enter target score (1-50): \n";
                                            byte[] errorBytes = Encoding.UTF8.GetBytes(errorPrompt);
                                            client.Send(errorBytes);
                                        }
                                    }
                                }
                            }
                            catch (SocketException ex)
                            {
                                playerNames.Remove(client);
                                clientsToRemove.Add(client);
                            }
                        }
                    }
                    
                    // Remove disconnected clients
                    foreach (var client in clientsToRemove)
                    {
                        clients.Remove(client);
                        if (client.Connected)
                        {
                            client.Shutdown(SocketShutdown.Both);
                        }
                        client.Close();
                    }
                }
            }
            finally
            {
                // Clean up all clients
                foreach (var client in clients)
                {
                    if (client.Connected)
                    {
                        client.Shutdown(SocketShutdown.Both);
                    }
                    client.Close();
                }
                server?.Close();
                udp?.Close();
            }
        }

        static bool UpdateGame(Dictionary<int, InputPacket> playerInputs, Dictionary<int, PlayerState> playerStates, Dictionary<int, DateTime> lastSeen, List<Bullet> bullets, List<Obstacle> obstacles, ref int obstacleSpawnTimer, ref int obstacleMoveTimer, Random random, Socket udpSocket, HashSet<EndPoint> clientEndPoints, Socket tcpServer, ref bool gameEnded, ref GameConfig gameConfig)
        {
            // Don't update game if no players are connected
            if (playerStates.Count == 0)
            {
                return false;
            }
            
            // Check for player timeouts (20 seconds without input)
            var currentTime = DateTime.UtcNow;
            var timeoutThreshold = TimeSpan.FromSeconds(20);
            
            foreach (var playerKv in playerStates.ToList()) // ToList to avoid modification during enumeration
            {
                int playerId = playerKv.Key;
                var player = playerKv.Value;
                
                if (lastSeen.ContainsKey(playerId))
                {
                    var timeSinceLastSeen = currentTime - lastSeen[playerId];
                    if (timeSinceLastSeen > timeoutThreshold)
                    {
                        gameEnded = true;
                        EndGame("disconnect", playerStates, udpSocket, clientEndPoints, tcpServer);
                        return true; // Exit update loop as game has ended
                    }
                }
            }
            
            // Process each player's latest input
            foreach (var input in playerInputs)
            {
                int playerId = input.Key;
                var inputPacket = input.Value;
                
                // Check if player state exists
                if (!playerStates.ContainsKey(playerId))
                {
                    continue; // Skip if player doesn't have a state yet
                }
                
                var currentState = playerStates[playerId];
                
                // Apply movement input: Move ∈ {-1, 0, +1}
                int newX = currentState.X + inputPacket.Move;
                
                // Clamp X position to map boundaries [0, MapW-1]
                newX = Math.Max(0, Math.Min(newX, NetConstants.MapW - 1));
                
                // Update player state with new position
                playerStates[playerId] = currentState with { X = newX };
            }
            
            // Handle bullet-obstacle collisions AFTER moving bullets and obstacles
            var bulletsToRemove = new HashSet<int>();
            var obstaclesToRemove = new HashSet<int>();

            // First move bullets
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                var bullet = bullets[i];
                
                // Move bullet up
                var newBullet = bullet with { Y = bullet.Y - 1 };
                
                // Remove bullet if it goes off screen
                if (newBullet.Y < 0)
                {
                    bullets.RemoveAt(i);
                }
                else
                {
                    bullets[i] = newBullet;
                }
            }

            // Then move obstacles (every 2 ticks to slow them down)
            obstacleMoveTimer++;
            bool shouldMoveObstacles = obstacleMoveTimer >= 2;
            if (shouldMoveObstacles)
            {
                obstacleMoveTimer = 0;
            }

            for (int i = obstacles.Count - 1; i >= 0; i--)
            {
                var obstacle = obstacles[i];
                
                // Move obstacle down only every 2 ticks
                var newObstacle = shouldMoveObstacles ? obstacle with { Y = obstacle.Y + 1 } : obstacle;
                
                // Check for player-obstacle collisions
                bool hitPlayer = false;
                foreach (var playerKv in playerStates.ToList())
                {
                    var player = playerKv.Value;
                    
                    // Check if player and obstacle are at the same position
                    if (player.X == newObstacle.X && player.Y == newObstacle.Y)
                    {
                        // Player hit by obstacle - lose a life
                        if (player.Lives > 0)
                        {
                            var updatedPlayer = player with { Lives = player.Lives - 1 };
                            playerStates[playerKv.Key] = updatedPlayer;
                            
                            // Check if player has no lives left
                            if (updatedPlayer.Lives <= 0)
                            {
                                gameEnded = true;
                                EndGame("noLives", playerStates, udpSocket, clientEndPoints, tcpServer);
                                return true; // Exit update loop as game has ended
                            }
                        }
                        
                        // Remove the obstacle that hit the player
                        obstacles.RemoveAt(i);
                        hitPlayer = true;
                        break; // Exit player loop since obstacle is removed
                    }
                }
                
                if (!hitPlayer)
                {
                    // Check if obstacle reached bottom of map (only if it moved)
                    if (shouldMoveObstacles && newObstacle.Y >= NetConstants.MapH)
                    {
                        // Just remove the obstacle, no damage to players
                        obstacles.RemoveAt(i);
                    }
                    else if (shouldMoveObstacles)
                    {
                        obstacles[i] = newObstacle;
                    }
                }
            }

            // Finally check bullet-obstacle collisions (improved detection)
            for (int b = 0; b < bullets.Count; b++)
            {
                if (bulletsToRemove.Contains(b)) continue;
                
                var bullet = bullets[b];
                
                for (int o = 0; o < obstacles.Count; o++)
                {
                    if (obstaclesToRemove.Contains(o)) continue;
                    
                    var obstacle = obstacles[o];
                    
                    // Enhanced collision detection:
                    // 1. Exact position match (bullet.X == obstacle.X && bullet.Y == obstacle.Y)
                    // 2. Bullet "passes through" obstacle (same X, bullet Y < obstacle Y)
                    bool exactHit = (bullet.X == obstacle.X && bullet.Y == obstacle.Y);
                    bool passThroughHit = (bullet.X == obstacle.X && bullet.Y < obstacle.Y && bullet.Y >= obstacle.Y - 1);
                    
                    if (exactHit || passThroughHit)
                    {
                        // Mark for removal
                        bulletsToRemove.Add(b);
                        obstaclesToRemove.Add(o);
                        
                        // Award score to player
                        if (playerStates.ContainsKey(bullet.FromPlayerId))
                        {
                            var player = playerStates[bullet.FromPlayerId];
                            var updatedPlayer = player with { Score = player.Score + 1 };
                            playerStates[bullet.FromPlayerId] = updatedPlayer;
                            
                            // Check for target score reached
                            if (updatedPlayer.Score >= gameConfig.TargetScore)
                            {
                                gameEnded = true;
                                EndGame("targetScore", playerStates, udpSocket, clientEndPoints, tcpServer);
                                return true; // Exit update loop as game has ended
                            }
                        }
                        
                        break; // One collision per bullet
                    }
                }
            }
            
            // Remove collided bullets and obstacles (in reverse order to maintain indices)
            var bulletIndices = bulletsToRemove.OrderByDescending(x => x).ToList();
            var obstacleIndices = obstaclesToRemove.OrderByDescending(x => x).ToList();
            
            foreach (var bulletIndex in bulletIndices)
            {
                bullets.RemoveAt(bulletIndex);
            }
            
            foreach (var obstacleIndex in obstacleIndices)
            {
                obstacles.RemoveAt(obstacleIndex);
            }
            
            // Handle obstacle spawning (every 6 ticks instead of 3) - only if players are connected
            if (playerStates.Count > 0)
            {
                obstacleSpawnTimer++;
                if (obstacleSpawnTimer >= 6)
                {
                    obstacleSpawnTimer = 0;
                    
                    // Spawn obstacle at random X position, Y=0 (top of map)
                    int randomX = random.Next(0, NetConstants.MapW);
                    var obstacle = new Obstacle(randomX, 0);
                    obstacles.Add(obstacle);
                }
            }
            
            // Clear processed inputs
            playerInputs.Clear();
            return false; // Game continues
        }

        static void BroadcastState(Socket udpSocket, Dictionary<int, PlayerState> playerStates, List<Bullet> bullets, List<Obstacle> obstacles, HashSet<EndPoint> clientEndPoints)
        {
            if (clientEndPoints.Count == 0 || playerStates.Count == 0)
            {
                return; // No clients or players to broadcast to
            }
            
            try
            {
                // Create arrays for StatePacket
                var players = playerStates.Values.ToArray();
                var obstaclesArray = obstacles.ToArray();
                var bulletsArray = bullets.ToArray();
                
                // Create StatePacket
                var statePacket = new StatePacket(players, obstaclesArray, bulletsArray);
                
                // Wrap in PacketEnvelope
                var envelope = JsonBytes.Wrap(PacketType.State, statePacket);
                
                // Serialize to bytes
                byte[] data = JsonBytes.ToBytes(envelope);
                
                // Send to all registered UDP clients
                foreach (var clientEndPoint in clientEndPoints)
                {
                    try
                    {
                        udpSocket.SendTo(data, clientEndPoint);
                    }
                    catch (SocketException ex)
                    {
                        // Silently ignore failed sends
                    }
                }
            }
            catch (Exception ex)
            {
                // Silently ignore broadcast errors
            }
        }

        static void EndGame(string reason, Dictionary<int, PlayerState> playerStates, Socket udpSocket, HashSet<EndPoint> clientEndPoints, Socket? tcpServer = null)
        {
            Console.WriteLine("\n🏁 GAME OVER");
            
            string gameEndMessage = reason switch
            {
                "targetScore" => "🎯 Victory! Target score reached!",
                "noLives" => "💀 All lives lost!",
                "disconnect" => "🔌 Player disconnected",
                _ => "Game ended"
            };
            Console.WriteLine($"   {gameEndMessage}");
            
            Console.WriteLine("\n📊 Final Scores:");
            foreach (var playerKv in playerStates.OrderByDescending(p => p.Value.Score))
            {
                var player = playerKv.Value;
                string medal = playerStates.Count > 1 && playerKv.Equals(playerStates.OrderByDescending(p => p.Value.Score).First()) ? "🥇 " : "   ";
                Console.WriteLine($"{medal}{player.Name}: {player.Score} points, {player.Lives} lives");
            }
            
            try
            {
                // Send GameOver packet to all clients
                var gameOverPayload = new GameOverPayload(playerStates.Values.ToArray(), reason);
                var envelope = JsonBytes.Wrap(PacketType.GameOver, gameOverPayload);
                byte[] data = JsonBytes.ToBytes(envelope);
                
                foreach (var clientEndPoint in clientEndPoints)
                {
                    try
                    {
                        udpSocket.SendTo(data, clientEndPoint);
                    }
                    catch (Exception ex)
                    {
                        // Silently ignore GameOver send failures
                    }
                }
                
                // Give clients time to receive the message
                Thread.Sleep(100);
                
                // Close sockets
                try
                {
                    udpSocket?.Close();
                    tcpServer?.Close();
                }
                catch (Exception ex)
                {
                    // Silently ignore socket close errors
                }
            }
            catch (Exception ex)
            {
                // Silently ignore EndGame errors
            }
            
            Console.WriteLine("\n👋 Server shutdown complete");
        }
    }
}
