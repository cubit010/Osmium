
namespace Osmium
{
    internal class UCI
    {
        public static void parseCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return;
            string[] parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;
            switch (parts[0].ToLowerInvariant())
            {
                case "uci":
                    Console.WriteLine("id name ChessC#");
                    Console.WriteLine("id Yuanxi Chen");
                    Console.WriteLine("uciok");

                    break;
                case "isready":
                    // setup, then respond with readyok
                    break;

                case "quit":
                    Environment.Exit(0);
                    break;

                case "ucinewgame":

                // Add more commands as needed
                default:
                    Console.WriteLine($"Unknown command: {parts[0]}");
                    break;
            }
        }
    }
}
