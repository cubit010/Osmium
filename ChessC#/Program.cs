using System;
using System.Collections.Generic;

namespace ChessC_
{
    class Program
    {
        static void Main(string[] args)
        {
            // 1) Initialize board from FEN or default start
            var board = new Board();

            //custom debug fen
            //Fen.LoadFEN(board, "8/2K5/8/5p2/8/4r3/8/k7 w - - 8 134");

            // En Passant White Test
            //Fen.LoadFEN(board, "rnbqkbnr/pppp1ppp/8/3Pp3/8/8/PPP2PPP/RNBQKBNR w KQkq e6 0 3");

            // En Passant Black Test (try e2e4, then Black plays d4xe3 e.p.)
            //Fen.LoadFEN(board, "rnbqkbnr/pppppppp/8/8/3p4/8/PPP1P1PP/RNBQKBNR w KQkq - 0 2");

            // K B Q vs k
            //Fen.LoadFEN(board, "4k3/8/8/8/8/8/8/2BQK3 w - - 0 1");

            // Castling Not Allowed (through check or out of check) — test by placing piece attacking f1/f8 or e1/e8
            // Fen.LoadFEN(board, "r3k2r/pppq1ppp/2n2n2/3pp3/3PP3/2N2N2/PPPQ1PPP/R3K2R w KQkq - 0 1"); // try O-O and O-O-O

            // Discovered Check Test — play e4 to open bishop's diagonal to give check
            // Fen.LoadFEN(board, "r1b2rk1/pp1n1ppp/2p1pn2/3p4/3P4/2N1PN2/PP1N1PPP/R1B2RK1 w - - 0 1");

            // Queen & Rook Threats — verify sliding checks and pins
            // Fen.LoadFEN(board, "r4rk1/ppp2ppp/2n5/3q4/3P4/4Q3/PPP2PPP/R3R1K1 w - - 0 1");

            // Promotion Test — try d8=Q+, d8=R+, d8=N+, or d8=B+
            // Fen.LoadFEN(board, "8/3P4/3k4/8/8/3K4/8/8 w - - 0 1");

            //M1 white Qa8#
            //Fen.LoadFEN(board, "6k1/5ppp/8/8/8/8/5PPP/Q6K w - - 0 1");
            //M1 black Re1#
            //Fen.LoadFEN(board, "r5k1/8/8/8/8/6PP/5PPP/7K w - - 0 1");



            // Regular Opening Position //
            Fen.LoadFEN(board, "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");

            var stopwatch = new System.Diagnostics.Stopwatch();

            // 2) Create transposition table (e.g. 16 MB)
            var tt = new TranspositionTable(16);
            tt.NewSearch();
            Utils.PrintBoard(board);

            Console.WriteLine("Enter moves in SAN (e.g. e4, Nf3, O-O). Type 'quit' to exit.\n");

            while (true)
            {
                // [A] Generate all legal moves at the start of this turn
                bool isWhiteToMove = (board.sideToMove == Color.White);
                List<Move> legalMoves = MoveGen.FilteredLegalMoves(board, isWhiteToMove);
                /**
                foreach (Move move in legalMoves) {
                    Console.WriteLine(MoveNotation.ToAlgebraicNotation(move, legalMoves));
                }/**/
                MoveGen.FlagCheckAndMate(board, legalMoves, isWhiteToMove);

                var sanToMove = new Dictionary<string, Move>(StringComparer.Ordinal);
                foreach (var m in legalMoves)
                {
                    string san = MoveNotation.ToAlgebraicNotation(m, legalMoves);
                    if (!sanToMove.ContainsKey(san))
                        sanToMove[san] = m;
                }

                // [B] Input move from player
                Move userMove;
                while (true)
                {
                    Console.Write(board.sideToMove == Color.White ? "White> " : "Black> ");
                    string input = Console.ReadLine()?.Trim();
                    if (string.IsNullOrEmpty(input) || input == "quit")
                        return;

                    if (sanToMove.TryGetValue(input, out userMove))
                        break;

                    Console.WriteLine("Invalid SAN or ambiguous—try again.\n");
                }

                board.MakeMove(userMove);
                if ((userMove.Flags & MoveFlags.Checkmate) != 0)
                {
                    Console.WriteLine("Checkmate! " + (isWhiteToMove ? "White" : "Black") + " wins.");
                    break;
                }

                // [C] Engine responds
                stopwatch.Restart();
                Move engineMove = Search.FindBestMove(board, tt);
                string engineSan = MoveNotation.ToAlgebraicNotation(engineMove);
                Console.WriteLine($"Engine> {engineSan}\n");
                board.MakeMove(engineMove);
                //stopwatch to time search time
                stopwatch.Stop();
                Console.WriteLine($"Search took {stopwatch.ElapsedMilliseconds} ms");

                if ((engineMove.Flags & MoveFlags.Checkmate) != 0)
                {
                    Console.WriteLine("Checkmate! " + (!isWhiteToMove ? "White" : "Black") + " wins.");
                    break;
                }

                Utils.PrintBoard(board);
                Console.WriteLine();
            }
        }

        /// <summary>
        /// This method is no longer used for generating moves; 
        /// we’ve inlined move‐generation into Main to prevent re‐generation on bad input.
        /// It’s kept here in case you need it later, but it is not called.
        /// </summary>
        private static bool TryParseSanMove(string inputSan, Board board, out Move move)
        {
            // (Unused – see Main for the “single‐generation, inner‐loop” approach.)
            move = default;
            return false;
        }
    }
}
