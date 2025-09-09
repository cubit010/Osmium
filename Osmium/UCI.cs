
using System.IO;

namespace Osmium
{
    internal class UCI
    {

        private static Thread? searchThread;

        public static Move[] moveBuffer = new Move[256]; // Preallocate a buffer for move generation
        public static void Run(Board board, TranspositionTable tt)
        {
            while (true)
            {
                string? command = Console.ReadLine();
                if (command == null) continue; // Handle null input
                Thread.Sleep(1); 
                ParseRunCommand(command, board, tt);
            }
        }
        public static void ParseRunCommand(string command, Board board, TranspositionTable tt)
        {
            if (string.IsNullOrWhiteSpace(command)) return;
            string[] parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;
            switch (parts[0].ToLowerInvariant()) 
            {
                case "uci":
                    Console.WriteLine("id name ChessC#");
                    Console.WriteLine("id author Yuanxi Chen");
                    Console.WriteLine("uciok");

                    break;
                case "isready":
                    Console.WriteLine("readyok");
                    // setup, then respond with readyok
                    break;
                    
                case "quit":
                    Environment.Exit(0);
                    break;

                case "ucinewgame":
                    Fen.LoadStartPos(board);
                    break;

                case "position":
                    ParseRunPositionCommand(board, parts);
                    break;

                case "go":
                    RunGoCommand(board, parts, tt);
                    break;

                case "stop":
                    if (searchThread != null && searchThread.IsAlive)
                    {
                        Search.stopSearch = true; // Signal the search to stop
                        searchThread.Join();       // Wait until it actually stops
                        searchThread = null;
                    }
                    break;

                default:
                    Console.WriteLine($"Unknown command: {parts[0]}");
                    break;
            }
        }
        public static void ParseRunPositionCommand(Board board, string[] parts)
        {
            /*
            case 1: position command with FEN and moves
            position fen rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1 moves e2e4 e7e5 g1f3

            Tokens:
            [0]  position
            [1]  fen
            [2]  rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR   // FEN field 1: piece placement
            [3]  w                                            // FEN field 2: side to move
            [4]  KQkq                                         // FEN field 3: castling rights
            [5]  -                                            // FEN field 4: en passant target square
            [6]  0                                            // FEN field 5: halfmove clock
            [7]  1                                            // FEN field 6: fullmove number
            [8]  moves                                        // keyword indicating moves follow
            [9]  e2e4                                         // move 1
            [10] e7e5                                         // move 2
            [11] g1f3                                         // move 3

            ------------------------------------------------------------

            case 2: position command with startpos and moves
            position startpos moves e2e4 e7e5 g1f3

            Tokens:
            [0]  position
            [1]  startpos                                     // keyword indicating standard start position
            [2]  moves                                        // keyword indicating moves follow
            [3]  e2e4                                         // move 1
            [4]  e7e5                                         // move 2
            [5]  g1f3                                         // move 3
            */
            if (parts.Length < 2)
            {
                Console.WriteLine("Error: 'position' command requires additional arguments, less than 2 arguments detected.");
                return;
            }
            sbyte offsetIdx;
            if (parts[1] == "fen")
            {
                string fen = string.Join(' ', parts, 2, 6); // goes from [2] to [7] inclusive
                Fen.LoadFEN(board, fen);
                offsetIdx = 8;

            }
            else if (parts[1] == "startpos")
            {
                Fen.LoadStartPos(board);
                offsetIdx = 2;
            }
            else
            {
                Console.WriteLine($"Error: Unknown position type '{parts[1]}'. Expected 'fen' or 'startpos'.");
                return;
            }
            if (parts.Length <= offsetIdx)
            {
                return;
            }
            if (parts[offsetIdx] == "moves")
            {
                for (int i = offsetIdx + 1; i < parts.Length; i++)
                {
                    string moveStr = parts[i];
                    int count = 0;
                    var span = new Span<Move>(moveBuffer);
                    bool isWhite = board.sideToMove == Color.White;
                    MoveGen.FilteredLegalMoves(board, span, ref count, isWhite); // Reset the move generation cache

                    var toMove = new Dictionary<string, Move>(StringComparer.Ordinal);
                    foreach (var m in span)
                    {
                        string mStr = m.ToString();
                        if (!toMove.ContainsKey(mStr))
                            toMove[mStr] = m;
                    }
                    if (toMove.TryGetValue(moveStr, out Move move))
                    {
                        board.MakeRealMove(move);
                    }
                    else
                    {
                        Console.WriteLine($"Error: Move '{moveStr}' not found in legal moves.");
                        break;
                    }
                }
            }
            else
            {
                Console.WriteLine($"Error: Expected 'moves' keyword, found '{parts[offsetIdx]}'.");
            }
        }

        private static int timeLimitMs = 10000;
        public static void RunGoCommand(Board board, string[] parts, TranspositionTable tt)
        {
            // Stop previous search if running
            if (searchThread != null && searchThread.IsAlive)
            {
                Search.stopSearch = true;
                searchThread.Join();
            }

            // Reset stop flag
            Search.stopSearch = false;

            // Start search in a new thread
            searchThread = new Thread(() =>
            {
                Move bestMove = Search.UCISearch(board, tt, timeLimitMs); // Replace with your actual search function

                if (!Search.stopSearch)
                {
                    Console.WriteLine($"bestmove {bestMove}");
                }
            });

            searchThread.IsBackground = true;
            searchThread.Start();
        }
    }
}
