using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace ChessC_
{
    /// <summary>
    /// Span-based search with fixed move-buffer isolation per depth
    /// </summary>
    public static class Search
    {
        public static int MaxDepth = Program.MaxDepth;
        private static int MaxQDepth = 8; // Quiescence search max depth

        private const int MateScore = 1_000_000;
        private static readonly Move[,] killerMoves = new Move[MaxDepth + 1, 2];
        public static long NodesVisited;

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
        internal static Move FindBestMove(Board board, TranspositionTable tt, int timeLimitMs)
        {
            Move lastBest = default;
            int lastScore = 0;
            var sw = Stopwatch.StartNew();

            for (int depth = 1; depth <= MaxDepth; depth++)
            {
                var iterStart = Stopwatch.StartNew();
                if (sw.ElapsedMilliseconds >= timeLimitMs)
                    break;

                NodesVisited = 0;
                int window = 65 + 3 * depth;
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
                iterStart.Stop();
                Console.WriteLine($"Depth {depth,-2}: Nodes:{NodesVisited,-10} | Time: {iterStart.ElapsedMilliseconds,-6} ms | Best={MoveNotation.ToAlgebraicNotation(lastBest), 6} | NPS: {1000.0*NodesVisited/Math.Max(0.8, iterStart.ElapsedMilliseconds),12:F2}");
            }

            sw.Stop();
            // Final output formatting
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
            Console.WriteLine($"Reduced: {reduced}, Re-searched: {researched} ({(100.0 * researched / reduced):F2}%)");
            Console.WriteLine($"Null-window: {nullwindow}, Null-researched: {nullReseached} ({(100.0 * nullReseached / nullwindow):F2}%)");
            reduced = 0;
            researched = 0;
            nullwindow = 0;
            nullReseached = 0;
            return lastBest;
        }

        private static Move SearchAtDepth(Board board, TranspositionTable tt, int depth, int alpha, int beta, out int bestScore)
        {
            bool isWhite = board.sideToMove == Color.White;
            bestScore = int.MinValue;
            Move bestMove = default;

            // DEBUG: Compare move filters
            //MoveFilterDifferer.Compare(board, isWhite);

            // --- Generate legal moves ---
            Span<Move> full = new Span<Move>(_moveBuffer[depth-1]);
            int count = 0;
            MoveGen.FilteredLegalMoves(board, full, ref count, isWhite);
            Span<Move> moves = full.Slice(0, count);

            // TT probe and move ordering
            tt.Probe(board.zobristKey, depth, alpha, beta, out _, out Move ttMove);
            MoveOrdering.OrderMoves(board, moves, ttMove, depth);

            // Immediate checkmate
            foreach (var mv in moves)
                if ((mv.Flags & MoveFlags.Checkmate) != 0)
                    return mv;

            // Main search
            foreach (var mv in moves)
            {
                var undo = board.MakeSearchMove(board, mv);
                int score = -PVS(board, depth - 1, -beta, -alpha, !isWhite, tt);
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
        
        public static int reduced = 0;
        public static int researched = 0;
        public static int nullwindow = 0;
        public static int nullReseached = 0;

        private static int PVS(Board board, int depth, int alpha, int beta, bool isWhite, TranspositionTable tt)
        {
            NodesVisited++;

            if (depth == 0)//return Eval.EvalBoard(board, isWhite);
                return Quiesce(board, alpha, beta, isWhite, 1);

            ulong key = board.zobristKey;
            int origAlpha = alpha;

            if (tt.Probe(key, depth, alpha, beta, out int ttScore, out Move ttMove))
                return ttScore;

            int kingSq = MoveGen.GetKingSquare(board, isWhite);
            bool inCheck = MoveGen.IsSquareAttacked(board, kingSq, isWhite);

            // Null move pruning
            if (depth >= 3 && !inCheck && HasSufficientMaterial(board, isWhite))
            {
                var undoNull = board.MakeNullMove();
                int nullScore = -PVS(board, depth - (3 + depth * 2 / 9), -beta, -beta + 1, !isWhite, tt);
                board.UnmakeNullMove(undoNull);
                if (nullScore >= beta)
                    return beta;
            }


            // DEBUG: Compare move filters
            //MoveFilterDifferer.Compare(board, isWhite);


            // --- Generate legal moves ---
            Span<Move> full = new Span<Move>(_moveBuffer[depth - 1]);
            int count = 0;
            MoveGen.FilteredLegalWithoutFlag(board, full, ref count, isWhite);
            if (count == 0)
                return inCheck ? -MateScore + (MaxDepth - depth) : 0;
            Span<Move> moves = full.Slice(0, count);

            MoveOrdering.OrderMoves(board, moves, ttMove, depth);

            int best = int.MinValue;
            Move bestMove = default;
            int moveNum = 0;

            int staticEval = isWhite? board.materialDelta : -board.materialDelta;

            foreach (var mv in moves)
            {
                moveNum++;


                // --- Late Move Pruning (LMP) ---
                // Only for quiet moves, not in check, not first few moves, and at low depth
                if (depth <= 3 &&
                    !inCheck &&
                    mv.PieceCaptured == Piece.None &&
                    (mv.Flags & MoveFlags.Promotion) == 0 &&
                    moveNum > 3 + depth*8/9) // threshold can be tuned
                {
                    continue; // Prune this late quiet move
                }

                
                // --- Futility Pruning ---
                if (depth == 2 &&
                    !inCheck &&
                    mv.PieceCaptured == Piece.None &&
                    (mv.Flags & MoveFlags.Promotion) == 0)
                {
                    
                    //----------------------------------------
                    int futilityMargin = 180; // tuning needed
                    //----------------------------------------

                    if (staticEval + futilityMargin <= alpha)
                        continue; // Prune this move
                }

                int LmrSafetyMargin = 40; // tuning needed

                bool isKiller = mv.Equals(killerMoves[depth, 0]) || mv.Equals(killerMoves[depth, 1]);

                bool isTTMove = mv.Equals(ttMove);

                int safeMoves = 3 + depth / 2; // e.g. at depth 6, don't reduce first ~5
                bool canReduce =
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
                    score = -PVS(board, depth - 1, -beta, -alpha, !isWhite, tt);
                }
                else if (canReduce)
                {
                    reduced++;
                    int R = 2 + depth / 7;
                    score = -PVS(board, depth - R, -alpha - 1, -alpha, !isWhite, tt);
                    if (score > alpha)
                    {
                        researched++;
                        score = -PVS(board, depth - 1, -alpha - 1, -alpha, !isWhite, tt);
                        if (score > alpha && score < beta)
                            score = -PVS(board, depth - 1, -beta, -alpha, !isWhite, tt);
                    }
                }
                else
                {
                    nullwindow++;
                    score = -PVS(board, depth - 1, -alpha - 1, -alpha, !isWhite, tt);
                    if (score > alpha && score < beta) { 
                        nullReseached++;
                        score = -PVS(board, depth - 1, -beta, -alpha, !isWhite, tt);
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
                    // Store killer moves/history as in your Negamax
                    if (mv.PieceCaptured == Piece.None)
                    {
                        //below this
                        if (!mv.Equals(killerMoves[depth, 0]))
                        {
                            killerMoves[depth, 1] = killerMoves[depth, 0];
                            killerMoves[depth, 0] = mv;
                        }
                        MoveOrdering.RecordHistory(mv, depth);
                    }
                    break;
                }
            }

            NodeType flag = best <= origAlpha ? NodeType.UpperBound : best >= beta ? NodeType.LowerBound : NodeType.Exact;
            tt.Store(key, depth, best, bestMove, flag);
            return best;
        }

        private static int Quiesce(Board board, int alpha, int beta, bool isWhite, int qDepth)
        {
            if (qDepth >= MaxQDepth)
                return Eval.EvalBoard(board, isWhite);

            //Console.WriteLine($"Quiesce: Depth={qDepth}, Alpha={alpha}, Beta={beta}, IsWhite={isWhite}");
            bool inCheck = MoveGen.IsInCheck(board, isWhite);
            int stand = Eval.EvalMatAndPST(board, isWhite);
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

            // **FILTER OUT ILLEGAL CAPTURE MOVES IN BULK**
            //if (!inCheck)
            //{
            //    MoveFiltering.FilterMoves(board, moves, ref qCount, isWhite);

            //    //now qCount is trimmed to only the legal ones
            //    moves = moves.Slice(0, qCount);
            //}

            //QuiesceMoveOrdering.OrderQuiesceMoves(board, moves);
            MoveOrdering.OrderMoves(board, moves);
            foreach (var mv in moves)
            {
                //delta prune
                if ((mv.Flags & MoveFlags.Promotion) == 0 && !IsGoodCapture(mv))
                    continue;
                
                var undo = board.MakeSearchMove(board, mv);
                if (MoveGen.IsInCheck(board, !isWhite))
                {
                    // if our king is now in check, this move is illegal → skip it
                    board.UnmakeMove(mv, undo);
                    continue;
                }
                int score = -Quiesce(board, -beta, -alpha, !isWhite, qDepth+1);
                board.UnmakeMove(mv, undo);

                if (score >= beta) return beta;
                alpha = Math.Max(alpha, score);
            }

            return alpha;
        }

        private static bool HasSufficientMaterial(Board board, bool isWhiteToMove)
        {
            int offset = isWhiteToMove ? 0 : 6;
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
        private static bool IsGoodCapture(Move mv)
        {
            return ((int)mv.PieceCaptured % 6) >= ((int)mv.PieceMoved % 6);
        }
    }
}
