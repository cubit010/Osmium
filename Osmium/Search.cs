using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using static System.Formats.Asn1.AsnWriter;

namespace Osmium
{
    /// <summary>
    /// Span-based search with fixed move-buffer isolation per depth
    /// </summary>
    public static class Search
    {

        internal static volatile bool stopSearch = false;

        internal const bool useSIMD = true;


        [ThreadStatic]
        private static Dictionary<ulong, int> _repetitionTable;

        public static int MaxDepth = Program.MaxDepth;
        public static int MaxQDepth = 8; // Quiescence search max depth

        private const int MateScore = 1_000_000;
        private static readonly Move[,] killerMoves = new Move[MaxDepth + 1, 2];
        public static long NodesVisited;
        public static long totalNodesVisited;

        //[ThreadStatic]
        //private static Stack<Move> _searchStack = new Stack<Move>(MaxDepth);

        [ThreadStatic]
        private static Move[][] _moveBuffer = new Move[MaxDepth][];

        private static int QBufferDepth = MaxQDepth; // Quiescence search buffer depth

        [ThreadStatic]
        private static Move[][] _qBuffer = new Move[QBufferDepth][];

        static Search()
        {
            for (int i = 0; i < MaxDepth; i++)
            {
                _moveBuffer[i] = new Move[256]; // Adjust size as needed
            }
            for (int i = 0; i < QBufferDepth; i++)
            {
                _qBuffer[i] = new Move[128]; // Adjust size as needed
            }
        }
        static Stopwatch searchTimer = new Stopwatch();
        static bool mustStop = false;
        static int classTimeLimitMs;
        public static Move UCISearch(Board board, TranspositionTable tt, int timeLimitMs)
        {
            stopSearch = false;
            mustStop = false;
            classTimeLimitMs = timeLimitMs;
            bool is3fold = board.IsThreefoldRepetition();

            if (is3fold || board.halfmoveClock > 50 || !HasSufficientMaterial(board))
            {
                if (is3fold) threeFoldDetections++;
                return default;
            }

            tt.NewSearch();
            Move lastBest = default;
            int lastScore = 0;
            searchTimer.Restart();

            for (int depth = 1; depth <= MaxDepth; depth++)
            {
                if (searchTimer.ElapsedMilliseconds >= timeLimitMs || stopSearch)
                    break;

                NodesVisited = 0;
                int window = 75 + 3 * depth;
                int alpha = lastScore - window;
                int beta = lastScore + window;
                int score;
                Move bestMove;

                // Aspirating window
                while (true)
                {
                    bestMove = SearchAtDepth(board, tt, depth, alpha, beta, out score);
                    if (score <= alpha)
                        alpha = int.MinValue + 1;
                    else if (score >= beta)
                        beta = int.MaxValue - 1;
                    else
                        break;
                }

                lastScore = score;
                lastBest = bestMove;

            }
            searchTimer.Stop();
            return lastBest;

        }
        public static Move FindBestMove(Board board, TranspositionTable tt, int timeLimitMs)
        {
            bool is3fold = board.IsThreefoldRepetition();
            classTimeLimitMs = timeLimitMs;

            mustStop = false;
            stopSearch = false;

            if (is3fold || board.halfmoveClock > 50 || !HasSufficientMaterial(board))
            {
                if (is3fold) threeFoldDetections++;
                if (!Program.UCImode)
                    Console.WriteLine("3-fold repetition or insufficient material detected at root, returning no move.");
                return default;
            }

            uint pastUsableHits = 0;
            uint pastLookups = 0;
            uint pastMoveOnlyHits = 0;
            uint pastCutoffs = 0;
            totalNodesVisited = 0;
            long lastNodeCount = 0;
            tt.NewSearch();
            Move lastBest = default;
            int lastScore = 0;

            searchTimer.Restart();

            for (int depth = 1; depth <= MaxDepth; depth++)
            {
                bool notTrusted = false;
                var iterStart = Stopwatch.StartNew();
                if (searchTimer.ElapsedMilliseconds >= timeLimitMs)
                    break;

                NodesVisited = 0;
                int window = 75 + 3 * depth;
                int alpha = lastScore - window;
                int beta = lastScore + window;
                int score;
                Move bestMove;

                // Aspirating window
                while (true)
                {
                    bestMove = SearchAtDepth(board, tt, depth, alpha, beta, out score);
                    if (score <= alpha)
                        alpha = int.MinValue + 1;
                    else if (score >= beta)
                        beta = int.MaxValue - 1;
                    else
                        break;
                }
                if ((double)NodesVisited / lastNodeCount > 0.8)
                {
                    lastScore = score;
                    lastBest = bestMove;
                } else
                {
                    notTrusted = true;
                }
                    iterStart.Stop();
                lastNodeCount = NodesVisited;

                uint dLookups;
                uint dUsable;
                uint dMoveHints;
                uint dCutoffs;
                double iterHitRate;
                double iterMoveHintRate;
                // Calculate deltas from current TT stats
                if (Program.TTStats)
                {
                    dLookups = tt.Lookups - pastLookups;
                    dUsable = tt.UsableHits - pastUsableHits;
                    dMoveHints = tt.MoveOnlyHits - pastMoveOnlyHits;
                    dCutoffs = tt.CutoffsFromTT - pastCutoffs;

                    iterHitRate = dLookups > 0 ? 100.0 * dUsable / dLookups : 0.0;
                    iterMoveHintRate = dLookups > 0 ? 100.0 * dMoveHints / dLookups : 0.0;
                }
                Console.Write(
                    $"Depth {depth,-2}: Nodes:{NodesVisited,-10} | Time: {iterStart.ElapsedMilliseconds,-6} ms | " +
                    $"Best={MoveNotation.ToAlgebraicNotation(lastBest),6} | NPS: {1000.0 * NodesVisited / Math.Max(0.8, iterStart.ElapsedMilliseconds),12:F2} | " +
                    $"Eval: {(board.sideToMove == Color.Black ? -lastScore / 100.0 : lastScore / 100.0),3} | "

                );

                if (Program.TTStats)
                    Console.WriteLine(
                    $"TT usable: {iterHitRate:F2}% ({dUsable.ToString("N0")}/{dLookups.ToString("N0")}) | move-hint: {iterMoveHintRate:F2}% ({dMoveHints.ToString("N0")}) | " +
                    $"cutoffs: {dCutoffs.ToString("N0")}"
                    );
                else
                {
                     Console.WriteLine();
                }
                Console.Write(notTrusted ? "Not enough nodes searched at depth " + depth + ", using best from depth " + (depth-1) + "\n" : "");
                totalNodesVisited += NodesVisited;

                // Update past counters
                pastUsableHits = tt.UsableHits;
                pastLookups = tt.Lookups;
                pastMoveOnlyHits = tt.MoveOnlyHits;
                pastCutoffs = tt.CutoffsFromTT;
            }

            searchTimer.Stop();

            double outScore = board.sideToMove == Color.Black ? -lastScore : lastScore;
            if (Math.Abs(outScore) > 900000)
            {
                outScore = Math.Sign(outScore) * ((MateScore - Math.Abs(outScore)) / 2);
                Console.WriteLine($"Final evaluation: {(outScore > 0 ? "" : "-")}M{Math.Abs(outScore)}");
            }
            else
            {
                Console.WriteLine($"Final evaluation: {outScore / 100.0}");
            }

            Console.WriteLine($"Reduced: {reduced}, Re-searched: {researched} ({(100.0 * researched / Math.Max(1, reduced)):F2}%)");
            Console.WriteLine($"Null-window: {nullwindow}, Null-researched: {nullReseached} ({(100.0 * nullReseached / Math.Max(1, nullwindow)):F2}%)");
            Console.WriteLine($"Total 3 fold exits detected: {threeFoldDetections}");
            if (Program.TTStats)
            {
                // Full TT summary with existing fields only
                double overallUsableHitRate = tt.Lookups > 0 ? 100.0 * tt.UsableHits / tt.Lookups : 0.0;
                Console.WriteLine("=== Transposition Table Summary ===");
                Console.WriteLine($"Lookups: {tt.Lookups.ToString("N0")}");
                Console.WriteLine($"UsableHits (score usable for cutoff): {tt.UsableHits.ToString("N0")} ({overallUsableHitRate:F2}%)");
                Console.WriteLine($"MoveOnlyHits (only move hint returned): {tt.MoveOnlyHits}");
                Console.WriteLine($"CutoffsFromTT: {tt.CutoffsFromTT.ToString("N0")}");
                Console.WriteLine($"TagCollisions: {tt.TagCollisions.ToString("N0")}");
                //Console.WriteLine($"AgeMismatches: {tt.AgeMismatches.ToString("N0")}");
                //Console.WriteLine($"DepthTooLow: {tt.DepthTooLow.ToString("N0")}");
            }
            // Reset counters
            reduced = 0;
            researched = 0;
            nullwindow = 0;
            nullReseached = 0;
            totalNodesVisited = 0;
            threeFoldDetections = 0;

            return lastBest;
        }

        private static int reduced = 0;
        private static int researched = 0;
        private static int nullwindow = 0;
        private static int nullReseached = 0;

        private static int ply = 0;
        private static int threeFoldDetections = 0;
        static Move a6 = new Move(Square.A7, Square.A6, Piece.BlackPawn);
        static Move Nc6 = new Move(Square.B8, Square.C6, Piece.BlackKnight);
        static Move Nxe4 = new Move(Square.F6, Square.E4, Piece.BlackKnight, Piece.WhitePawn, MoveFlags.Capture);
        private static Move SearchAtDepth(Board board, TranspositionTable tt, int depth, int alpha, int beta, out int bestScore) 
        {
            ply = 0;

            bool isWhite = board.sideToMove == Color.White; 
            bestScore = int.MinValue;
            Move bestMove = default;

            Span<Move> full = new Span<Move>(_moveBuffer[depth - 1]);
            int count = 0;
            MoveGen.FilteredLegalMoves(board, full, ref count, isWhite);
            Span<Move> moves = full.Slice(0, count);

            Move ttMove = default;
            if (!tt.Probe(board.zobristKey, depth, alpha, beta, out _, out ushort ttMoveCode))
                ttMove = default;
            else
                ttMove = Move.FromEncoded(ttMoveCode);  // you will need a method to decode ushort to Move

            if(useSIMD)
                MoveOrdering.OrderMovesTest(board, moves, ttMove, depth);
            else
                MoveOrdering.OrderMovesScalarTest(board, moves, ttMove, depth);

            foreach (var mv in moves)
                if ((mv.Flags & MoveFlags.Checkmate) != 0)
                    return mv;
            
            //print order to check for moveordering quality
            //for (int i = 0; i < moves.Length; i++)
            //{
            //    Console.Write(MoveNotation.ToAlgebraicNotation(moves[i], moves) + "  |  ");
            //}
            //Console.WriteLine();
            foreach (var mv in moves)
            {
                var undo = board.MakeSearchMove(board, mv);
                int score;
    
                score = -PVS(board, depth - 1, -beta, -alpha, !isWhite, tt);
                
                board.UnmakeMove(mv, undo);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = mv;
                }
                alpha = Math.Max(alpha, score);
            }

            return bestMove;
        }

        internal static int LmrSafetyMargin = 110; // tuning needed
        internal static int NMPmargin = 130; // Null move pruning margin, can be tuned
        private static int PVS(Board board, int depth, int alpha, int beta, bool isWhite, TranspositionTable tt, bool hasReduced = false)
        {
            ply++;
            NodesVisited++;

            if (!mustStop && ((NodesVisited & 2047) == 0))
            {
                if (stopSearch || searchTimer.ElapsedMilliseconds >= classTimeLimitMs)
                {
                    mustStop = true;
                }
            }

            bool is3fold = board.IsThreefoldRepetition();

            if (is3fold || board.halfmoveClock > 50 || !HasSufficientMaterial(board))
            {
                if (is3fold) threeFoldDetections++;
                ply--;
                return 0;
            }

            if (depth <= 0)
            {
                ply--;
                return Quiesce(board, alpha, beta, isWhite, 1);
            }

            ulong key = board.zobristKey;
            int origAlpha = alpha;

            Move ttMove = default;
            if (tt.Probe(key, depth, alpha, beta, out int ttScore, out ushort ttMoveCode))
            {
                ply--;
                return ttScore;
            }
            else
            {
                ttMove = default;
            }

            bool inCheck = MoveGen.IsInCheck(board, isWhite);


            // Null move pruning
            if (depth >= 3 && !inCheck && HasSufficientMaterial(board))
            {
                var undoNull = board.MakeNullMove();
                int R = 1 + depth / 5; // Null move reduction, can be tuned
                int nullScore = -PVS(board, depth - R, -beta, -beta + 1, !isWhite, tt);
                board.UnmakeNullMove(undoNull);
                if (nullScore - NMPmargin - depth * 15 >= beta )
                {
                    ply--;
                    return beta;
                }
            }

            // Generate legal moves
            Span<Move> full = new Span<Move>(_moveBuffer[ply - 1]);
            int count = 0;
            MoveGen.FilteredLegalWithoutFlag(board, full, ref count, isWhite);

            if (count == 0)
            {
                ply--;
                return inCheck ? -MateScore + (ply) : 0;
            }
            Span<Move> moves = full.Slice(0, count);

            if (useSIMD)
            {
                MoveOrdering.OrderMoves(board, moves, ttMove, ply);
            }
            else
            { 
                MoveOrdering.OrderMovesScalar(board, moves, ttMove, ply);
            }
            int best = int.MinValue;
            Move bestMove = default;
            int moveNum = 0;

            int staticEval = Eval.EvalMaterialsExternal(board, isWhite);
            int extension;

            foreach (var mv in moves)
            {
                moveNum++;

                if (mustStop && moveNum > 1)
                {
                    break;
                }

                extension = 0;
                if ((mv.Flags & (MoveFlags.Check | MoveFlags.Promotion)) != 0)
                    extension = 1;

                // --- Late Move Pruning (LMP) ---
                // Only for quiet moves, not in check, not first few moves, and at low depth
                if (depth <= 3 &&
                    !inCheck &&
                    mv.PieceCaptured == Piece.None &&
                    (mv.Flags & MoveFlags.Promotion) == 0 &&
                    moveNum > 5 + depth * 3) // threshold can be tuned
                {
                    continue; // Prune this late quiet move
                }

                // --- Futility Pruning ---
                if (depth == 2 &&
                    !inCheck &&
                    mv.PieceCaptured == Piece.None &&
                    (mv.Flags & MoveFlags.Promotion) == 0)
                {
                    int futilityMargin = 180; // tuning needed
                    if (staticEval + futilityMargin <= alpha)
                        continue; // Prune this move
                }


                bool isKiller = mv.Equals(killerMoves[ply, 0]) || mv.Equals(killerMoves[ply, 1]);
                bool isTTMove = mv.Equals(ttMove);

                int safeMoves = 4;
                bool canReduce =
                    !hasReduced &&
                    depth >= 4 &&
                    moveNum > safeMoves &&
                    !inCheck &&
                    mv.PieceCaptured == Piece.None &&
                    (mv.Flags & MoveFlags.Promotion) == 0 &&
                    !isKiller &&
                    !isTTMove &&
                    staticEval + LmrSafetyMargin <= alpha;


                var undo = board.MakeSearchMove(board, mv);
                int score;


              
                
                if (moveNum == 1)
                {
                    score = -PVS(board, depth - 1 - extension, -beta, -alpha, !isWhite, tt, hasReduced);
                }
                else if (canReduce)
                {
                    reduced++;
                    int R = 2 + depth / 9; // LMR reduction, can be tuned
                    score = -PVS(board, depth - R - extension, -alpha - 1, -alpha, !isWhite, tt, true);

                    if (score > alpha)
                    {
                        researched++;
                        score = -PVS(board, depth - 1 - extension, -alpha - 1, -alpha, !isWhite, tt, hasReduced);

                        if (score > alpha && score < beta)
                            score = -PVS(board, depth - 1 - extension, -beta, -alpha, !isWhite, tt, hasReduced);
                    }
                }
                else
                {
                    nullwindow++;
                    score = -PVS(board, depth - 1 - extension, -alpha - 1, -alpha, !isWhite, tt, hasReduced);

                    if (score > alpha && score < beta)
                    {
                        nullReseached++;
                        score = -PVS(board, depth - 1 - extension, -beta, -alpha, !isWhite, tt, hasReduced);
                    }
                }
                

                board.UnmakeMove(mv, undo);

                if (score > best)
                {
                    best = score;
                    bestMove = mv;
                }

                alpha = Math.Max(alpha, score);

                if (alpha >= beta)
                {
                    if ((mv.Flags & (MoveFlags.Capture)) == 0) // quiet move
                    {
                        if (!mv.Equals(killerMoves[ply, 0]))
                        {
                            killerMoves[ply, 1] = killerMoves[ply, 0]; // push down
                            killerMoves[ply, 0] = mv;                  // save new killer
                        }
                        MoveOrdering.RecordHistory(mv, depth);
                    }
                    break;
                }
            }

            NodeType flag = best <= origAlpha ? NodeType.UpperBound :
                            best >= beta ? NodeType.LowerBound : NodeType.Exact;
            tt.Store(key, depth, best, flag, bestMove.Encode());

            ply--;
            return best;
        }
        private static void PrintStack (Stack<Move> stack)
        {
            
            foreach (var move in stack.Reverse())
            {
                Console.Write(MoveNotation.ToAlgebraicNotation(move) + "|\t");
            }
            Console.WriteLine();
        }
        //private static void OutputError(Board board, Move move)
        //{
        //    Console.WriteLine("Error in search: " + move);
        //    Console.WriteLine("FEN: " + Fen.ToFEN(board));
        //    Console.WriteLine("Board: \n" + board.ToString());

        //}

        private static void checkOverlap(Board board)
        {
            // Check if any pieces overlap in the bitboards
            for (int i = 0; i < 12; i++)
            {
                for (int j = i + 1; j < 12; j++)
                {
                    if ((board.bitboards[i] & board.bitboards[j]) != 0)
                    {
                        Console.WriteLine($"Overlap detected between pieces {i} and {j}");
                        Console.WriteLine("FEN: " + Fen.ToFEN(board));
                        throw new Exception("Bitboard overlap detected.");
                    }
                }
            }
        }
        internal const bool useQuiescenceOrdering = false;
        private static int Quiesce(Board board, int alpha, int beta, bool isWhite, int qDepth)
        {
            bool is3fold = board.IsThreefoldRepetition();

            if (is3fold || board.halfmoveClock > 50 || !HasSufficientMaterial(board))
            {
                if (is3fold) threeFoldDetections++;
                return 0;
            }
            //checkOverlap(board);

            if (qDepth >= MaxQDepth)
                return Eval.EvalBoard(board, isWhite);

            //Console.WriteLine($"Quiesce: Depth={qDepth}, Alpha={alpha}, Beta={beta}, IsWhite={isWhite}");
            bool inCheck = MoveGen.IsInCheck(board, isWhite);
            int stand = Eval.EvalMaterialsExternal(board, isWhite);
            if (stand >= beta)
                return beta;

            alpha = Math.Max(alpha, stand);

            Span<Move> full = new Span<Move>(_qBuffer[qDepth - 1]);
            int qCount = 0;
            if (inCheck)
            {
                // Generate ALL legal moves
                MoveGen.FilteredLegalWithoutFlag(board, full, ref qCount, isWhite);
            }
            else
            {
                // Generate captures + promotions into single buffer
                SpecialMoveGen.GenerateCaptureMoves(board, full, ref qCount, isWhite);
                SpecialMoveGen.GeneratePromotionMoves(board, full, ref qCount, isWhite);

            }


            Span<Move> moves = full.Slice(0, qCount);

            //QuiesceMoveOrdering.OrderQuiesceMoves(board, moves);
            if (useQuiescenceOrdering)
            {
                if (useSIMD)
                    MoveOrdering.OrderMoves(board, moves);
                else
                    MoveOrdering.OrderMovesScalar(board, moves);
            }

            foreach (var mv in moves)
            {
                //delta prune
                if (!inCheck && (mv.Flags & MoveFlags.Promotion) == 0 && !IsGoodCapture(board,mv))
                    continue;


                //if ((mv.Flags & MoveFlags.Capture) != 0)
                //{
                //    int see = MoveOrdering.SEE(board, mv);
                //    if (see < 0)
                //        continue; // skip clearly losing captures
                //}


                var undo = board.MakeSearchMove(board, mv);
                if (MoveGen.IsInCheck(board, isWhite))
                {
                    // if our king is now in check, this move is illegal → skip it
                    board.UnmakeMove(mv, undo);
                    continue;
                }
                int score = -Quiesce(board, -beta, -alpha, !isWhite, qDepth+1);
                board.UnmakeMove(mv, undo);
                //error check


                if (score >= beta) return beta;
                alpha = Math.Max(alpha, score);
            }

            return alpha;
        }

        private static bool HasSufficientMaterial(Board board)
        {
            int offset = (int)board.sideToMove * 6;
            int pawns = BitOperations.PopCount(board.bitboards[offset + (int)Piece.WhitePawn]);
            int knights = BitOperations.PopCount(board.bitboards[offset + (int)Piece.WhiteKnight]);
            int bishops = BitOperations.PopCount(board.bitboards[offset + (int)Piece.WhiteBishop]);
            int rooks = BitOperations.PopCount(board.bitboards[offset + (int)Piece.WhiteRook]);
            int queens = BitOperations.PopCount(board.bitboards[offset + (int)Piece.WhiteQueen]);

            if (pawns > 0 || rooks > 0 || queens > 0) return true;
            if (bishops >= 2) return true;
            if (bishops == 1 && knights >= 1) return true;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsGoodCapture(Board board,Move mv)
        {
            return ((int)mv.PieceCaptured % 6) >= ((int)mv.PieceMoved % 6);
            //return MoveOrdering.SEE(board, mv) >= 0 ||
            //       (mv.PieceCaptured != Piece.None &&
            //        mv.PieceCaptured != Piece.WhitePawn &&
            //        mv.PieceCaptured != Piece.BlackPawn);
        }
    }
}
