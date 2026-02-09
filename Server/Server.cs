namespace Server
{
    internal class Server
    {
        static async Task Main(string[] args)
        {
            Socket? server = null;
            Socket? udp = null;
            var clients = new List<Socket>();
            var playerNames = new Dictionary<Socket, string>();

            // Ovo je za inpute igraca
            var playerInputs = new Dictionary<int, InputPacket>();
            // Cuva trenutno stanje igraca
            var playerStates = new Dictionary<int, PlayerState>(); 
            
            // Prati poslednje vreme kada je igrac poslao input
            var lastSeen = new Dictionary<int, DateTime>(); 
            
            // Cuva aktivne metke
            var bullets = new List<Bullet>(); 
            
            // Cuva aktivne prepreke
            var obstacles = new List<Obstacle>(); 
            
            // Cuva UDP endpoint-e klijenata
            var clientEndPoints = new HashSet<EndPoint>(); 
            
            // 0 = nije izabrano, 1 = single player, 2 = multiplayer
            var gameMode = 0; 
            
            // Bice postavljeno na osnovu moda igre
            var playersNeeded = 1; 
            
            // Prati da li je mod igre izabran
            var gameModeSet = false; 
            
            // Zajednicki ciljni skor za multiplayer
            var sharedTargetScore = 10; 
            
            // Tajmer za spawn prepreka
            var obstacleSpawnTimer = 0; 
            
            // Tajmer za pomeranje prepreka (da ih uspori)
            var obstacleMoveTimer = 0; 
            
            // Generator slucajnih brojeva za pozicije prepreka
            var random = new Random(); 
            
            // Flag koji oznacava da li je igra zavrsena
            var gameEnded = false; 
            
            // Cuva konfiguraciju igre
            var gameConfig = GameConfig.Default(); 

            // Tajmer igre
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

                Console.WriteLine("Space Invaders Server");
                Console.WriteLine($"Server radi {IPAddress.Loopback}:{NetConstants.TcpPort}");
                Console.WriteLine("Cekamo igrace...\n");

                // Pokreni game loop
                var gameLoopTask = Task.Run(async () =>
                {
                    while (await timer.WaitForNextTickAsync() && !gameEnded)
                    {
                        try
                        {
                            if (UpdateGame(playerInputs, playerStates, lastSeen, bullets, obstacles, ref obstacleSpawnTimer, ref obstacleMoveTimer, random, udp, clientEndPoints, server, ref gameEnded, ref gameConfig))
                            {
                                // Igra je zavrsena, izlazi iz petlje
                                break;
                            }

                            if (!gameEnded)
                            {
                                BroadcastState(udp, playerStates, bullets, obstacles, clientEndPoints);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Loguj samo kriticne greske koje bi oborile server
                            Console.WriteLine($"Eksplodirao ti server: {ex.Message}");
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

                    // Proveri greske
                    if (errorSockets.Count > 0)
                    {
                        // Tiho ukloni klijente sa greskom
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
                            Console.WriteLine("Eksplodirao ti server");
                            break;
                        }
                    }

                    // Proveri nove TCP konekcije
                    if (readSockets.Contains(server))
                    {
                        Socket client = server.Accept();
                        client.Blocking = false;
                        clients.Add(client);
                    }

                    // Proveri UDP podatke
                    if (readSockets.Contains(udp))
                    {
                        try
                        {
                            byte[] udpBuffer = new byte[2048];
                            EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                            int udpBytesReceived = udp.ReceiveFrom(udpBuffer, ref remoteEndPoint);

                            // Zapamti UDP endpoint ovog klijenta
                            clientEndPoints.Add(remoteEndPoint);

                            // Prvo probaj da parsiras kao tekst (za PING)
                            string udpMessage = Encoding.UTF8.GetString(udpBuffer, 0, udpBytesReceived);

                            // Obradi PING poruku
                            if (udpMessage == "PING")
                            {
                                byte[] pongResponse = Encoding.UTF8.GetBytes("PONG");
                                udp.SendTo(pongResponse, remoteEndPoint);
                            }
                            else
                            {
                                // Probaj da parsiras kao PacketEnvelope
                                try
                                {
                                    // Napravi bafer tacne velicine
                                    byte[] packetData = new byte[udpBytesReceived];
                                    Array.Copy(udpBuffer, packetData, udpBytesReceived);

                                    // Deserijalizuj PacketEnvelope
                                    var envelope = JsonBytes.FromBytes<PacketEnvelope>(packetData);

                                    // Obradi Input pakete
                                    if (envelope.Type == PacketType.Input)
                                    {
                                        var inputPacket = JsonBytes.Unwrap<InputPacket>(envelope);
                                        playerInputs[inputPacket.PlayerId] = inputPacket;

                                        // Azuriraj poslednje vreme kada je igrac "vidjen"
                                        lastSeen[inputPacket.PlayerId] = DateTime.UtcNow;

                                        // Obradi pucanje
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
                                    // Tiho ignorisi neispravne pakete
                                }
                            }
                        }
                        catch (SocketException ex)
                        {
                            // Tiho ignorisi greske pri UDP prijemu
                        }
                    }

                    // Proveri podatke od postojecih TCP klijenata
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
                                    // Uredan diskonekt
                                    playerNames.Remove(client);
                                    clientsToRemove.Add(client);
                                }
                                else
                                {
                                    string message = Encoding.UTF8.GetString(buffer, 0, bytesReceived).Trim();

                                    // Proveri da li je ovo NAME poruka
                                    if (message.StartsWith("NAME:"))
                                    {
                                        string playerName = message.Substring(5);
                                        playerNames[client] = playerName;
                                        Console.WriteLine($"🎮 {playerName} joined the game");

                                        // Ako mod nije izabran (prvi igrac), pitaj za mod igre
                                        if (!gameModeSet)
                                        {
                                            string modePrompt = "GAMEMODE:Sta igras \n1===>Single Player \n2===>Multiplayer): \n";
                                            byte[] modePromptBytes = Encoding.UTF8.GetBytes(modePrompt);
                                            client.Send(modePromptBytes);
                                            Thread.Sleep(10);
                                        }
                                        else if (gameMode == 1 || playerStates.Count == 0)
                                        {
                                            // Single player mod ILI prvi igrac u multiplayeru - pitaj za ciljni skor
                                            string scorePrompt = "PROMPT:Unesi cilj (1-50): \n";
                                            byte[] scorePromptBytes = Encoding.UTF8.GetBytes(scorePrompt);
                                            client.Send(scorePromptBytes);
                                            Thread.Sleep(10);
                                        }
                                        else
                                        {
                                            // Drugi igrac u multiplayeru - automatski koristi zajednicki cilj, ne treba prompt

                                            // Direktno inicijalizuj stanje igraca
                                            int playerId = playerStates.Count + 1; // Ispravna dodela ID-a (1, 2, 3...)
                                            int startX = 3 * NetConstants.MapW / 4; // Pozicija drugog igraca
                                            int startY = NetConstants.MapH - 2;

                                            playerStates[playerId] = new PlayerState(playerId, playerName, startX, startY, 3, 0);

                                            // Inicijalizuj "last seen" vreme
                                            lastSeen[playerId] = DateTime.UtcNow;

                                            // Azuriraj konfiguraciju igre
                                            gameConfig = new GameConfig(NetConstants.MapW, NetConstants.MapH, sharedTargetScore, gameMode);

                                            // Proveri da li sada imamo dovoljno igraca
                                            if (playerStates.Count >= playersNeeded)
                                            {
                                                Console.WriteLine($"Uslo {playerStates.Count} igraca (Cilj: {sharedTargetScore})\n");

                                                // Posalji INIT poruku SVIM igracima
                                                foreach (var playerClient in playerNames.Keys)
                                                {
                                                    try
                                                    {
                                                        if (playerClient.Connected)
                                                        {
                                                            // Nadji player ID za ovog klijenta
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
                                                        // Tiho obradi neuspeh slanja INIT-a
                                                    }
                                                }
                                            }
                                            else if (gameMode == 2)
                                            {
                                                // Multiplayer mod - cekamo jos igraca
                                                string waitMessage = $"WAIT:Cekamo .... ({playerStates.Count}/{playersNeeded})... Cilj: {sharedTargetScore}\n";
                                                byte[] waitBytes = Encoding.UTF8.GetBytes(waitMessage);
                                                client.Send(waitBytes);
                                            }
                                        }
                                    }
                                    else if (message.StartsWith("MODE:"))
                                    {
                                        // Parsiraj mod igre
                                        string modeStr = message.Substring(5);
                                        if (int.TryParse(modeStr, out int selectedMode) && (selectedMode == 1 || selectedMode == 2))
                                        {
                                            gameMode = selectedMode;
                                            playersNeeded = gameMode; // 1 za single, 2 za multi
                                            gameModeSet = true;

                                            Console.WriteLine($"🎮Mode: {(gameMode == 1 ? "Single Player" : "Multiplayer")}");

                                            // Sada pitaj za ciljni skor
                                            string scorePrompt = "PROMPT:Izaberi cilj (1-50): \n";
                                            byte[] scorePromptBytes = Encoding.UTF8.GetBytes(scorePrompt);
                                            client.Send(scorePromptBytes);
                                            Thread.Sleep(10);
                                        }
                                        else
                                        {
                                            // Neispravan mod, pitaj ponovo
                                            string errorPrompt = "MODEERROR:greskaaaaaaaaaaa! Izaberi \n1===>Single Player \n2===>Multiplayer): \n";
                                            byte[] errorBytes = Encoding.UTF8.GetBytes(errorPrompt);
                                            client.Send(errorBytes);
                                        }
                                    }
                                    else if (message.StartsWith("SCORE:"))
                                    {
                                        // Parsiraj ciljni skor
                                        string scoreStr = message.Substring(6);
                                        if (int.TryParse(scoreStr, out int targetScore) && targetScore >= 1 && targetScore <= 50)
                                        {
                                            // Za prvog igraca ili single player mod, postavi zajednicki ciljni skor
                                            if (playerStates.Count == 0 || gameMode == 1)
                                            {
                                                sharedTargetScore = targetScore;
                                            }

                                            // Azuriraj konfiguraciju igre sa zajednickim ciljem
                                            gameConfig = new GameConfig(NetConstants.MapW, NetConstants.MapH, sharedTargetScore, gameMode);

                                            // Inicijalizuj stanje igraca
                                            int playerId = playerStates.Count + 1; // Ispravna dodela ID-a (1, 2, 3...)
                                            int startX = NetConstants.MapW / 2;
                                            int startY = NetConstants.MapH - 2;

                                            // Za multiplayer, pomeri start pozicije
                                            if (gameMode == 2 && playerStates.Count > 0)
                                            {
                                                startX = playerStates.Count == 1 ? NetConstants.MapW / 4 : 3 * NetConstants.MapW / 4;
                                            }

                                            playerStates[playerId] = new PlayerState(playerId, playerNames[client], startX, startY, 3, 0);

                                            // Inicijalizuj "last seen" vreme
                                            lastSeen[playerId] = DateTime.UtcNow;

                                            // Proveri da li imamo dovoljno igraca za trenutni mod
                                            if (playerStates.Count >= playersNeeded)
                                            {
                                                Console.WriteLine($"Pocinjemo sa {playerStates.Count} players (Cilj: {sharedTargetScore})\n");

                                                // Posalji INIT poruku SVIM igracima
                                                foreach (var playerClient in playerNames.Keys)
                                                {
                                                    try
                                                    {
                                                        if (playerClient.Connected)
                                                        {
                                                            // Nadji player ID za ovog klijenta
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
                                                        // Tiho obradi neuspeh slanja INIT-a
                                                    }
                                                }
                                            }
                                            else if (gameMode == 2)
                                            {
                                                // Multiplayer mod - cekamo ostale igrace
                                                string waitMessage = $"WAIT:Cekamo ostale igrace({playerStates.Count}/{playersNeeded})... Target score: {sharedTargetScore}\n";
                                                byte[] waitBytes = Encoding.UTF8.GetBytes(waitMessage);
                                                client.Send(waitBytes);
                                            }
                                        }
                                        else
                                        {
                                            // Neispravan skor, pitaj ponovo
                                            string errorPrompt = "ERROR:Unesti poene u rasponu (1-50): \n";
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

                    // Ukloni diskonektovane klijente
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
                // Ocisti sve klijente
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
            // Ne azuriraj igru ako nema povezanih igraca
            if (playerStates.Count == 0)
            {
                return false;
            }

            // Proveri timeout igraca (20 sekundi bez inputa)
            var currentTime = DateTime.UtcNow;
            var timeoutThreshold = TimeSpan.FromSeconds(20);

            foreach (var playerKv in playerStates.ToList()) // ToList da se izbegne izmena kolekcije tokom iteracije
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
                        return true; // Izlaz iz update petlje jer je igra zavrsena
                    }
                }
            }

            // Obradi najnoviji input svakog igraca
            foreach (var input in playerInputs)
            {
                int playerId = input.Key;
                var inputPacket = input.Value;

                // Proveri da li stanje igraca postoji
                if (!playerStates.ContainsKey(playerId))
                {
                    continue; // Preskoci ako igrac jos nema state
                }

                var currentState = playerStates[playerId];

                // Primeni pomeranje: Move ∈ {-1, 0, +1}
                int newX = currentState.X + inputPacket.Move;

                // Ogranicavanje X pozicije na granice mape [0, MapW-1]
                newX = Math.Max(0, Math.Min(newX, NetConstants.MapW - 1));

                // Azuriraj stanje igraca sa novom pozicijom
                playerStates[playerId] = new PlayerState(
                    currentState.Id,
                    currentState.Name,
                    newX,
                    currentState.Y,
                    currentState.Lives,
                    currentState.Score
                );
            }

            // Obradi sudare metak-prepreka NAKON pomeranja metaka i prepreka
            var bulletsToRemove = new HashSet<int>();
            var obstaclesToRemove = new HashSet<int>();

            // Prvo pomeri metke
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                var bullet = bullets[i];

                // Pomeri metak na gore
                var newBullet = new Bullet(bullet.X, bullet.Y - 1, bullet.FromPlayerId);

                // Ukloni metak ako izadje sa ekrana
                if (newBullet.Y < 0)
                {
                    bullets.RemoveAt(i);
                }
                else
                {
                    bullets[i] = newBullet;
                }
            }

            // Zatim pomeri prepreke (svaka 2 tick-a da se uspori)
            obstacleMoveTimer++;
            bool shouldMoveObstacles = obstacleMoveTimer >= 2;
            if (shouldMoveObstacles)
            {
                obstacleMoveTimer = 0;
            }

            for (int i = obstacles.Count - 1; i >= 0; i--)
            {
                var obstacle = obstacles[i];

                // Pomeri prepreku na dole samo svaka 2 tick-a
                var newObstacle = shouldMoveObstacles ? new Obstacle(obstacle.X, obstacle.Y + 1) : obstacle;

                // Proveri sudar igrac-prepreka
                bool hitPlayer = false;
                foreach (var playerKv in playerStates.ToList())
                {
                    var player = playerKv.Value;

                    // Proveri da li su igrac i prepreka na istoj poziciji
                    if (player.X == newObstacle.X && player.Y == newObstacle.Y)
                    {
                        // Igrac je pogodjen preprek
                        if (player.Lives > 0)
                        {
                            var updatedPlayer = new PlayerState(
                                player.Id,
                                player.Name,
                                player.X,
                                player.Y,
                                player.Lives - 1,
                                player.Score
                            );
                            playerStates[playerKv.Key] = updatedPlayer;

                            // Proveri da li igrac nema vise zivota
                            if (updatedPlayer.Lives <= 0)
                            {
                                gameEnded = true;
                                EndGame("noLives", playerStates, udpSocket, clientEndPoints, tcpServer);
                                return true; // Izlaz iz update petlje jer je igra zavrsena
                            }
                        }

                        // Ukloni prepreku koja je udarila igraca
                        obstacles.RemoveAt(i);
                        hitPlayer = true;
                        break; // Izlaz iz player petlje jer je prepreka uklonjena
                    }
                }

                if (!hitPlayer)
                {
                    // Proveri da li je prepreka stigla do dna mape (samo ako se pomerila)
                    if (shouldMoveObstacles && newObstacle.Y >= NetConstants.MapH)
                    {
                        // Samo ukloni prepreku, nema stete po igraca
                        obstacles.RemoveAt(i);
                    }
                    else if (shouldMoveObstacles)
                    {
                        obstacles[i] = newObstacle;
                    }
                }
            }

            // Na kraju proveri sudare metak-prepreka (poboljsana detekcija)
            for (int b = 0; b < bullets.Count; b++)
            {
                if (bulletsToRemove.Contains(b)) continue;

                var bullet = bullets[b];

                for (int o = 0; o < obstacles.Count; o++)
                {
                    if (obstaclesToRemove.Contains(o)) continue;

                    var obstacle = obstacles[o];

                    // Poboljsana detekcija sudara:
                    // 1. Tacno poklapanje pozicije (bullet.X == obstacle.X && bullet.Y == obstacle.Y)
                    // 2. Metak "prodje kroz" prepreku (isti X, bullet Y < obstacle Y)
                    bool exactHit = (bullet.X == obstacle.X && bullet.Y == obstacle.Y);
                    bool passThroughHit = (bullet.X == obstacle.X && bullet.Y < obstacle.Y && bullet.Y >= obstacle.Y - 1);

                    if (exactHit || passThroughHit)
                    {
                        // Oznaci za uklanjanje
                        bulletsToRemove.Add(b);
                        obstaclesToRemove.Add(o);

                        // Dodeli poen igracu
                        if (playerStates.ContainsKey(bullet.FromPlayerId))
                        {
                            var player = playerStates[bullet.FromPlayerId];
                            var updatedPlayer = new PlayerState(
                                player.Id,
                                player.Name,
                                player.X,
                                player.Y,
                                player.Lives,
                                player.Score + 1
                            );
                            playerStates[bullet.FromPlayerId] = updatedPlayer;

                            // Proveri da li je cilj dostignut
                            if (updatedPlayer.Score >= gameConfig.TargetScore)
                            {
                                gameEnded = true;
                                EndGame("targetScore", playerStates, udpSocket, clientEndPoints, tcpServer);
                                return true; // Izlaz iz update petlje jer je igra zavrsena
                            }
                        }

                        break; // Jedan sudar po metku
                    }
                }
            }

            // Ukloni metke i prepreke koje su se sudarile (unazad da se sacuvaju indeksi)
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

            // Spawn prepreka (svakih 6 tick-ova umesto 3) - samo ako ima igraca
            if (playerStates.Count > 0)
            {
                obstacleSpawnTimer++;
                if (obstacleSpawnTimer >= 6)
                {
                    obstacleSpawnTimer = 0;

                    // Spawn prepreku na slucajnom X, Y=0 (vrh mape)
                    int randomX = random.Next(0, NetConstants.MapW);
                    var obstacle = new Obstacle(randomX, 0);
                    obstacles.Add(obstacle);
                }
            }

            // Ocisti obradjene inpute
            playerInputs.Clear();
            return false; // Igra se nastavlja
        }

        static void BroadcastState(Socket udpSocket, Dictionary<int, PlayerState> playerStates, List<Bullet> bullets, List<Obstacle> obstacles, HashSet<EndPoint> clientEndPoints)
        {
            if (clientEndPoints.Count == 0 || playerStates.Count == 0)
            {
                return; // Nema klijenata ili igraca za broadcast
            }

            try
            {
                // Kreiraj nizove za StatePacket
                var players = playerStates.Values.ToArray();
                var obstaclesArray = obstacles.ToArray();
                var bulletsArray = bullets.ToArray();

                // Kreiraj StatePacket
                var statePacket = new StatePacket(players, obstaclesArray, bulletsArray);

                // Umotaj u PacketEnvelope
                var envelope = JsonBytes.Wrap(PacketType.State, statePacket);

                // Serijalizuj u bajtove
                byte[] data = JsonBytes.ToBytes(envelope);

                // Posalji svim registrovanim UDP klijentima
                foreach (var clientEndPoint in clientEndPoints)
                {
                    try
                    {
                        udpSocket.SendTo(data, clientEndPoint);
                    }
                    catch (SocketException ex)
                    {
                        // Tiho ignorisi neuspesna slanja
                    }
                }
            }
            catch (Exception ex)
            {
                // Tiho ignorisi greske pri broadcast-u
            }
        }

        static void EndGame(string reason, Dictionary<int, PlayerState> playerStates, Socket udpSocket, HashSet<EndPoint> clientEndPoints, Socket? tcpServer = null)
        {
            Console.WriteLine("\n🏁 GAME OVER");

            string gameEndMessage = reason switch
            {
                "targetScore" => "POBEDAAAAAAA!",
                "noLives" => "GUBITNICEEEE",
                "disconnect" => "NEKO ISP'O",
                _ => "Game ended"
            };
            Console.WriteLine($"   {gameEndMessage}");

            Console.WriteLine("\nFinal Scores:");
            foreach (var playerKv in playerStates.OrderByDescending(p => p.Value.Score))
            {
                var player = playerKv.Value;
                string medal = playerStates.Count > 1 && playerKv.Equals(playerStates.OrderByDescending(p => p.Value.Score).First()) ? "🥇 " : "   ";
                Console.WriteLine($"{medal}{player.Name}: {player.Score} points, {player.Lives} lives");
            }

            try
            {
                // Posalji GameOver paket svim klijentima
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
                        // Tiho ignorisi neuspeh slanja GameOver-a
                    }
                }

                // Daj klijentima malo vremena da prime poruku
                Thread.Sleep(100);

                // Zatvori sokete
                try
                {
                    udpSocket?.Close();
                    tcpServer?.Close();
                }
                catch (Exception ex)
                {
                    // Tiho ignorisi greske pri zatvaranju soketa
                }
            }
            catch (Exception ex)
            {
                // Tiho ignorisi greske u EndGame
            }

            Console.WriteLine("\ngAsenje servera");
        }
    }
}
