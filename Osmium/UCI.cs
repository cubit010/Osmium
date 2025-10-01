
using System.IO;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Numerics;
using static System.Formats.Asn1.AsnWriter;
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
                    Console.WriteLine("id name Osmium");
                    Console.WriteLine("id author Yuanxi Chen");

                    Console.WriteLine($"option name windowDefault type spin default {Search.windowDefaultProp} min 0 max 1000");
                    Console.WriteLine($"option name LmrSafetyMargin type spin default {Search.LmrSafetyMarginProp} min 0 max 5000");
                    Console.WriteLine($"option name NMPmargin type spin default {Search.NMPmarginProp} min 0 max 5000");
                    Console.WriteLine($"option name reduxBase type spin default {Search.reduxBaseProp} min 1 max 4");
                    Console.WriteLine($"option name reduxDiv type spin default {Search.reduxDivProp} min 1 max 10");
                    Console.WriteLine($"option name LMPSafeMoves type spin default {Search.LMPSafeMovesProp} min 0 max 20");
                    Console.WriteLine($"option name LMPMvDepthMult type spin default {Search.LMPMvDepthMultProp} min 0 max 20");
                    Console.WriteLine($"option name futilityMargin type spin default {Search.futilityMarginProp} min 0 max 5000");
                    Console.WriteLine($"option name LMPDepth type spin default {Search.LMPDepthProp} min 0 max 50");
                    Console.WriteLine($"option name reduxSafeMoves type spin default {Search.reduxSafeMovesProp} min 0 max 50");
                    Console.WriteLine($"option name LMRReduxFactor type spin default {Search.LMRReduxFactorProp} min 1 max 4");

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
                        searchThread.Join();
                        Console.WriteLine($"bestmove {bestMove}");
                            // Wait until it actually stops
                        searchThread = null;
                    }
                    break;
                case "setoption":
                    if (parts.Length >= 5 && parts[1].ToLowerInvariant() == "name")
                    {
                        string optionName = parts[2];
                        int valueIndex = Array.IndexOf(parts, "value");
                        if (valueIndex != -1 && valueIndex + 1 < parts.Length && int.TryParse(parts[valueIndex + 1], out int val))
                        {
                            switch (optionName)
                            {
                                case "windowDefault": Search.windowDefaultProp = val; break;
                                case "LmrSafetyMargin": Search.LmrSafetyMarginProp = val; break;
                                case "NMPmargin": Search.NMPmarginProp = val; break;
                                case "reduxBase": Search.reduxBaseProp = val; break;
                                case "reduxDiv": Search.reduxDivProp = val; break;
                                case "LMPSafeMoves": Search.LMPSafeMovesProp = val; break;
                                case "LMPMvDepthMult": Search.LMPMvDepthMultProp = val; break;
                                case "futilityMargin": Search.futilityMarginProp = val; break;
                                case "LMPDepth": Search.LMPDepthProp = val; break;
                                case "reduxSafeMoves": Search.reduxSafeMovesProp = val; break;
                                case "LMRReduxFactor": Search.LMRReduxFactorProp = val; break;
                                default: Console.WriteLine($"Unknown option name: {optionName}"); break;
                            }
                        }
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
                    //Console.Write(board.sideToMove);
                    MoveGen.FilteredLegalMoves(board, span, ref count, isWhite); // Reset the move generation cache
                    //Console.Write(board.sideToMove);
                    //foreach (Move mv in span)
                    //{
                    //    Console.WriteLine(mv.ToString().ToLowerInvariant());
                    //}
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
                        Console.WriteLine(parts[i] + " not found");
                        foreach (var m in span.Slice(0, count))
                        {
                            Console.WriteLine($"Legal move: {MoveNotation.ToAlgebraicNotation(m)}");
                            Console.WriteLine($"{m.ToString()}");
                        }
                    }
                    //Console.WriteLine(board.sideToMove);
                    //else
                    //{
                    //    Console.WriteLine($"Error: Move '{moveStr}' not found in legal moves.");
                    //    Span<Move> allMove = stackalloc Move[256];
                    //    int allCount = 0;
                    //    MoveGen.GenAllDebug(board, allMove, ref allCount, isWhite);
                    //    foreach (var m in span.Slice(0, count))
                    //    {
                    //        Console.WriteLine($"Legal move: {m.ToString()}");
                    //    }
                    //    Console.WriteLine();
                    //    foreach (var m in allMove.Slice(0, allCount))
                    //    {
                    //        if(m.ToString().Equals(move.ToString()))
                    //            Console.WriteLine($"Matched move in all moves: {m}");
                    //        Console.WriteLine($"All move: {m}");
                    //    }
                    //    Utils.PrintBoard(board);
                    //    break;
                    //}
                }
                //Console.WriteLine($"After moves, side to move: {board.sideToMove}");
            }
            else
            {
                Console.WriteLine($"Error: Expected 'moves' keyword, found '{parts[offsetIdx]}'.");
            }
        }

        private static Move bestMove = default;
        private static int timeLimitMs = 10000;
        //0     1   2     3     4   5   6       7   8 
        //go wtime <ms> btime <ms> winc <ms> binc <ms>
        //go movetime <ms>
        public static int DetermineTimeControl(Board board, string[] parts)
        {

            int timeLeftIdx;
            int incrementIdx;
            bool inCheck;
            //search indef if there's only the "go" part and stop only when stopped
            if (parts.Length == 1) return int.MaxValue;

            if (parts.Length == 3 && parts[1].Equals("movetime"))
            {
                //keep 200ms spare for ping / overhead
                return int.Parse(parts[2])-200;
            }

            if (board.sideToMove == Color.White)
            {
                timeLeftIdx = 2;
                incrementIdx = 6;
                inCheck = MoveGen.IsInCheck(board, true);
            }
            else
            {
                timeLeftIdx = 4;
                incrementIdx = 8;
                inCheck = MoveGen.IsInCheck(board, false);
            }
            int timeLeft = 0;
            int inc = 0;
            if (parts.Length >= 5)
            {
                timeLeft = int.Parse(parts[timeLeftIdx]);

                if (parts.Length == 9)
                {
                    inc = int.Parse(parts[incrementIdx]);
                }
            }
            else
            {
                Console.Write("invalid number of parts in go time command");
                return 0;
            }

            // --- Estimate moves left using log curve ---
            int pieceCount = BitOperations.PopCount(board.occupancies[2]); // all non-empty squares
                                                 // Scale into [20, 50] roughly
                                                 // log makes it fall off quickly when few pieces remain
            double logFactor = Math.Log(pieceCount + 1) / Math.Log(33); // normalize 0..1 for [0..32 pieces]
            int movesToGo = (int)(20 + 30 * logFactor); // 20 at bare board, 50 at full board

            // --- Dynamic time allocation ---
            double ideal = (double)timeLeft / movesToGo;

            // Add increment (use 90% to be safe)
            ideal += inc * 0.9;

            // Boost in critical positions
            if (inCheck)
                ideal *= 1.3;

            // Clamp
            int minThink = 30;                      // always at least 30 ms
            int maxThink = (int)(timeLeft * 0.85);  // max 85% of remaining
            int allocated = (int)Math.Clamp(ideal, minThink, maxThink);

            return allocated;
        }

        public static void RunGoCommand(Board board, string[] parts, TranspositionTable tt)
        {
            timeLimitMs = DetermineTimeControl(board, parts);
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
                bestMove = Search.UCISearch(board, tt, timeLimitMs); // Replace with your actual search function

                if (!Search.stopSearch)
                {
                    Console.WriteLine($"bestmove {bestMove}");
                    //Console.WriteLine(board.sideToMove);
                    //Console.WriteLine($"pieceMoved {bestMove.PieceMoved}");
                    //Console.WriteLine($"pieceCaptured {bestMove.PieceCaptured}");
                    //Console.WriteLine($"moveFlags {bestMove.Flags}");

                }
            });

            searchThread.IsBackground = true;
            searchThread.Start();
        }
    }
}
