using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Client
{
    internal class Client
    {
        static void Main(string[] args)
        {
            Socket? client = null;

            Socket? udpClient = null;
            InitInfo? gameInit = null;
            // Inicijalno postavi playerId na 0, posle cemo ga postaviti, kada dobijemo od server info 
            int playerId = 0; 
            IPEndPoint? serverUdpEndPoint = null;

           

            // moramo da napravimo while(true) kako bismo konstanto azurirali izlaz datoteke 
            void Render(StatePacket statePacket)
            {
                if (gameInit == null) return;
                
                // kreiraj grid, mapu
                char[,] grid = new char[gameInit.MapH, gameInit.MapW];
                




                // popuni je poljima za kretanje
                for (int y = 0; y < gameInit.MapH; y++)
                {
                    for (int x = 0; x < gameInit.MapW; x++)
                    {
                        grid[y, x] = ' ';
                    }
                }
                
                
                // crtaj prepreke
                foreach (var obstacle in statePacket.Obstacles)
                {
                    if (obstacle.X >= 0 && obstacle.X < gameInit.MapW && obstacle.Y >= 0 && obstacle.Y < gameInit.MapH)
                    {
                        grid[obstacle.Y, obstacle.X] = '0';
                    }
                }
                
                // crtaj metke 
                foreach (var bullet in statePacket.Bullets)
                {
                    if (bullet.X >= 0 && bullet.X < gameInit.MapW && bullet.Y >= 0 && bullet.Y < gameInit.MapH)
                    {
                        grid[bullet.Y, bullet.X] = '^';
                    }
                }
                
                // crtaj igraca
                foreach (var player in statePacket.Players)
                {
                    if (player.X >= 0 && player.X < gameInit.MapW && player.Y >= 0 && player.Y < gameInit.MapH)
                    {
                        grid[player.Y, player.X] = 'P';
                    }
                }
                
                // uzeli smo stringbuilder jer je brzi i ne izaziva velike resete console, jer cuva text odmah u memoriji
                var sb = new StringBuilder();
                
                
                sb.AppendLine("===================== SPACE INVADERS =====================");
                
                // printaj info o igracu
                foreach (var player in statePacket.Players)
                {
                    sb.AppendLine($"Igrac: {player.Name}: \nScore: {player.Score} \nPostavljen cilj: {gameInit.TargetScore} \nZivota: {player.Lives}");
                }
                
                sb.AppendLine("".PadRight(gameInit.MapW + 2, '.'));
                
                // Gornja granica
                sb.Append('.');
                sb.Append("".PadRight(gameInit.MapW, '.'));
                
                
                
                
                sb.AppendLine(".");
                
                // leva i desna granica
                for (int y = 0; y < gameInit.MapH; y++)
                {
                    sb.Append('.');
                    for (int x = 0; x < gameInit.MapW; x++)
                    {
                        sb.Append(grid[y, x]);
                    }
                    sb.AppendLine(".");
                }
                
                // Donja granica
                sb.Append('.');
                sb.Append("".PadRight(gameInit.MapW, '.'));
                sb.AppendLine(".");
                
                sb.AppendLine("← → SPACE Q");




                // postavi kursor na gornju levu ivicu
                Console.SetCursorPosition(0, 0);
                Console.Write(sb.ToString());
            }

            try
            {
                client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                client.Connect(IPAddress.Loopback, NetConstants.TcpPort);
                
                
                Console.Write("Ime Igraca: ");
                string? playerName = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(playerName))
                {
                    playerName = "Steve";
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
                        Console.WriteLine("Veza je prekinuta");
                        break;
                    }

                    // Dodaj primljene podatke u bafer
                    string newData = Encoding.UTF8.GetString(buffer, 0, bytesReceived);
                    receivedData.Append(newData);

                    // Obradi kompletne linije (koje se zavrsavaju sa \n)
                    string allData = receivedData.ToString();
                    string[] lines = allData.Split('\n');

                    // Zadrzi poslednju nepotpunu liniju u baferu
                    receivedData.Clear();
                    if (!allData.EndsWith('\n') && lines.Length > 0)



                    {
                        receivedData.Append(lines[lines.Length - 1]);
                        // Ukloni nepotpunu liniju iz obrade
                        Array.Resize(ref lines, lines.Length - 1);
                    }


                    // ucitaj svaku liniju
                    foreach (string line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        if (line.StartsWith("PROMPT:"))
                        {
                            // Server trazi unos ciljnog skora
                            string prompt = line.Substring(7);
                            Console.Write(prompt);
                            string? userInput = Console.ReadLine();

                            // Posalji skor nazad serveru
                            
                            byte[] scoreMessage = Encoding.UTF8.GetBytes($"SCORE:{userInput}");
                            client.Send(scoreMessage);



                        }
                        else if (line.StartsWith("ERROR:"))
                        {
                            // Server je poslao poruku o gresci
                            string errorMsg = line.Substring(6);
                            Console.Write(errorMsg);
                            string? userInput = Console.ReadLine();

                            // Posalji skor nazad serveru
                            byte[] scoreMessage = Encoding.UTF8.GetBytes($"SCORE:{userInput}");
                            client.Send(scoreMessage);
                        }

                        else if (line.StartsWith("INIT:"))
                        {
                            if (InitParser.TryParse(line, out InitInfo initInfo))
                            {
                                gameInit = initInfo;
                                playerId = initInfo.PlayerId; // Koristi stvarni ID igraca koji je poslao server

                                // Kreiraj UDP socket i testiraj komunikaciju
                                try
                                {
                                    udpClient = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                                    udpClient.Blocking = false; // Postavi na neblokirajuci mod
                                    serverUdpEndPoint = new IPEndPoint(IPAddress.Loopback, initInfo.UdpPort);

                                    // Posalji PING serveru
                                    byte[] pingMessage = Encoding.UTF8.GetBytes("PING");
                                    udpClient.SendTo(pingMessage, serverUdpEndPoint);

                                    // Sacekaj PONG odgovor
                                    byte[] udpBuffer = new byte[256];
                                    EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                                    int udpBytesReceived = udpClient.ReceiveFrom(udpBuffer, ref remoteEndPoint);



                                    string udpResponse = Encoding.UTF8.GetString(udpBuffer, 0, udpBytesReceived);

                                    if (udpResponse == "PONG")
                                    {
                                        // Veza uspesna, nastavi bez poruke
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
                            // Server trazi izbor moda igre
                            string prompt = line.Substring(9);
                            Console.Write(prompt);
                            string? userInput = Console.ReadLine();

                            // Posalji izabrani mod nazad serveru
                            byte[] modeMessage = Encoding.UTF8.GetBytes($"MODE:{userInput}");
                            client.Send(modeMessage);
                        }
                        else if (line.StartsWith("MODEERROR:"))
                        {
                            // Server je poslao poruku o gresci za mod
                            string errorMsg = line.Substring(10);
                            Console.Write(errorMsg);
                            string? userInput = Console.ReadLine();







                            // Posalji mod nazad serveru
                            byte[] modeMessage = Encoding.UTF8.GetBytes($"MODE:{userInput}");
                            client.Send(modeMessage);
                        }
                        else if (line.StartsWith("WAIT:"))
                        {
                            // Server nam govori da sacekamo jos igraca
                            string waitMsg = line.Substring(5);
                            Console.WriteLine(waitMsg);
                        }
                    }

                    // Ako imamo inicijalizaciju igre, pokreni petlju igre
                    if (gameInit != null && udpClient != null)
                    {
                        Console.Clear();



                        // Pokreni task za unos sa tastature
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

                                    // Prevedi tastere u ulaze za igru
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

                                    // Posalji input ako postoji pomeranje ili pucanje
                                    if (move != 0 || fire)
                                    {
                        
                                        SendInput(move, fire);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                // napravi se lud ako je grska
                            }
                        });

                        
                        
                        // Petlja za prijem stanja igre
                        while (true)
                        {
                            try
                            {
                                // Proveri da li ima UDP podataka pomocu Select
                                var readSockets = new List<Socket> { udpClient };
                                var errorSockets = new List<Socket> { udpClient };

                                // Neblokirajuci select sa kratkim timeout-om
                                Socket.Select(readSockets, null, errorSockets, 10_000); // 10ms timeout

                                if (errorSockets.Count > 0)
                                {
                                    break;
                                }

                                if (readSockets.Count > 0)
                                {
                                    // Primi UDP podatke
                                    byte[] udpBuffer = new byte[2048];
                                    
                                    
                                    EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                                    int udpBytesReceived = udpClient.ReceiveFrom(udpBuffer, ref remoteEndPoint);

                                    try
                                    {
                                        // Kreiraj bafer tacne velicine
                                        byte[] packetData = new byte[udpBytesReceived];
                                        Array.Copy(udpBuffer, packetData, udpBytesReceived);

                                        // Deserijalizuj PacketEnvelope
                                        var envelope = JsonBytes.FromBytes<PacketEnvelope>(packetData);

                                        // Obradi State pakete
                                        if (envelope.Type == PacketType.State)
                                        {
                                            var statePacket = JsonBytes.Unwrap<StatePacket>(envelope);
                                            Render(statePacket);
                                        }
                                        // Obradi GameOver pakete
                                        else if (envelope.Type == PacketType.GameOver)
                                        {
                                            var gameOverPayload = JsonBytes.Unwrap<GameOverPayload>(envelope);



                                            // Ocisti ekran i prikazi informacije o kraju igre
                                            Console.Clear();
                                            Console.WriteLine("═ ═ ═ ═ ═ ═ ═ ═ ═ ═ ═ ");
                                            Console.WriteLine("      GAME OVER");
                                            Console.WriteLine(" ═ ═ ═ ═ ═ ═ ═ ═ ═ ═ ═");
                                            Console.WriteLine();

                                            // Sortiraj igrace po poenima (opadajuce) i prikazi rang listu
                                            var sortedPlayers = gameOverPayload.FinalScores
                                                .OrderByDescending(p => p.Score)
                                                .ThenByDescending(p => p.Lives)
                                                .ToArray();

                                            for (int i = 0; i < sortedPlayers.Length; i++)
                                            {
                                                var player = sortedPlayers[i];
                                                string medal = i switch
                                                {
                                                    0 => "prvo",
                                                    1 => "drugo",
                                                    2 => "trece",
                                                    _ => "  "
                                                };

                                                Console.WriteLine($"{medal} {player.Name}");
                                                Console.WriteLine($"   Poeni: {player.Score} | Zivoti: {player.Lives}");
                                                if (i < sortedPlayers.Length - 1) Console.WriteLine();
                                            }

                                            Console.WriteLine();

                                            // Prikazi poruku u zavisnosti od razloga
                                            string reasonMessage = gameOverPayload.Reason switch
                                            {
                                                "targetScore" => "Pobedio SIIIIIIII!",
                                                "noLives" => "LUZERUUU HaHa",
                                                "disconnect" => "Connection lost-Ugasi/Upali ruter :)",
                                                _ => " "
                                            };

                                            Console.WriteLine(reasonMessage);
                                            Console.WriteLine("\nStisni bilo sta da izadjes...");

                                            // Signaliziraj input task-u da stane i sacekaj pritisak tastera
                                            Console.ReadKey();
                                            return; // Izlaz iz petlje igre
                                        }
                                    }
                                    catch (Exception parseEx)
                                    {
                                        // Tiho ignorisi greske pri parsiranju
                                    }
                                }

                                // Mala pauza da se izbegne busy waiting
                                Thread.Sleep(1);




                                // Proveri da li je input task zavrsen (korisnik je izasao)
                                if (inputTask.IsCompleted)
                                {
                                    break;
                                }
                            }
                            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock)
                            {
                                // Nema podataka, nastavi petlju
                                Thread.Sleep(1);
                            }
                            catch (Exception ex)
                            {
                                break;
                            }
                        }


                        // Sacekaj da se input task zavrsi pre ciscenja resursa
                        try
                        {
                            if (!inputTask.Wait(1000)) // Sacekaj najvise 1 sekundu
                            {
                                // Isteklo vreme cekanja za input task, nastavi sa ciscenjem
                            }
                        }
                        catch (Exception ex)
                        {
                            // Tiho obradi greske pri cekanju input task-a
                        }

                        break;

                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Greska sa konekcijom");
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
            // Metoda za slanje inputa servera preko UDPa
            void SendInput(int move, bool fire)
            {
                if (udpClient == null || serverUdpEndPoint == null)
                {
                    return;
                }

                try
                {
                    // napravi paket od tvog unosa 
                    var inputPacket = new InputPacket(playerId, move, fire);

                    // sklopi ga u format pogodan za slanje
                    var envelope = JsonBytes.Wrap(PacketType.Input, inputPacket);

                    // serujalizuj ga u bajtove
                    byte[] data = JsonBytes.ToBytes(envelope);

                    // salji udpu
                    udpClient.SendTo(data, serverUdpEndPoint);
                }
                catch (Exception ex)
                {
                    // precuti ako zaglupi negde 
                }
            }
        }
    }
}
