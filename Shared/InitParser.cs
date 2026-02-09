public static class InitParser
{
    public static bool TryParse(string line, out InitInfo info)
    {
        info = default;
        
        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("INIT:"))
        {
            return false;
        }
        
        try
        {
            // Remove "INIT:" prefix
            string data = line.Substring(5);
            
            // Parse key-value pairs separated by semicolons
            var parts = data.Split(';');
            int mapW = 0, mapH = 0, startX = 0, startY = 0, udpPort = 0, targetScore = 10, playerId = 1;
            
            foreach (string part in parts)
            {
                var keyValue = part.Split('=');
                if (keyValue.Length != 2) continue;
                
                string key = keyValue[0].Trim();
                string value = keyValue[1].Trim();
                
                switch (key)
                {
                    case "map":
                        // Parse format like "20x40"
                        var dimensions = value.Split('x');
                        if (dimensions.Length == 2 && 
                            int.TryParse(dimensions[0], out mapH) && 
                            int.TryParse(dimensions[1], out mapW))
                        {
                            // Successfully parsed map dimensions
                        }
                        else
                        {
                            return false;
                        }
                        break;
                    case "startX":
                        if (!int.TryParse(value, out startX))
                            return false;
                        break;
                    case "startY":
                        if (!int.TryParse(value, out startY))
                            return false;
                        break;
                    case "udpPort":
                        if (!int.TryParse(value, out udpPort))
                            return false;
                        break;
                    case "targetScore":
                        if (!int.TryParse(value, out targetScore))
                            targetScore = 10; // Default value
                        break;
                    case "playerId":
                        if (!int.TryParse(value, out playerId))
                            playerId = 1; // Default value
                        break;
                }
            }
            
            // Validate that all required values were found
            if (mapW > 0 && mapH > 0 && startX >= 0 && startY >= 0 && udpPort > 0)
            {
                info = new InitInfo(mapW, mapH, startX, startY, udpPort, targetScore, playerId);
                return true;
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }
}