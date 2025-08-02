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
        public static int MaxDepth = Program.MaxDepth;
        private static int MaxQDepth = 8; // Quiescence search max depth

        private const int MateScore = 1_000_000;
        private static readonly Move[,] killerMoves = new Move[MaxDepth + 1, 2];
        public static long NodesVisited;

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
                iterStart.Stop();
                Console.WriteLine($"Depth {depth,-2}: Nodes:{NodesVisited,-10} | Time: {iterStart.ElapsedMilliseconds,-6} ms | Best={MoveNotation.ToAlgebraicNotation(lastBest), 6} | NPS: {1000.0*NodesVisited/Math.Max(0.8, iterStart.ElapsedMilliseconds),12:F2} | Eval: {(board.sideToMove == Color.Black ? -lastScore/100.0 : lastScore/100.0)}");
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
        
        public static int currentDepth = 0;
        private static Move SearchAtDepth(Board board, TranspositionTable tt, int depth, int alpha, int beta, out int bestScore)
        {
            ply = 0; // Reset ply for each search
            currentDepth = depth;
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
                //Console.WriteLine(MoveNotation.ToAlgebraicNotation(mv, moves));

                var undo = board.MakeSearchMove(board, mv);
                //_searchStack.Push(mv);    

                int score = -PVS(board, depth - 1, -beta, -alpha, !isWhite, tt);

                //_searchStack.Pop();
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
 

        //public static int depthOn = 0;
        public static int ply;
        private static int PVS(Board board, int depth, int alpha, int beta, bool isWhite, TranspositionTable tt, bool hasReduced = false)
        {
            ply++;
            //PrintStack(_searchStack);
            //Utils.PrintBoard(board);
            //checkOverlap(board);

            //Eval.TestPSTEvals(board, isWhite, 0);

            NodesVisited++;

            if (depth <= 0)
            {
                //Console.Out.WriteLine("d0:\t" + Fen.ToFEN(board));
                ply--;
                return Quiesce(board, alpha, beta, isWhite, 1);
            }
            ulong key = board.zobristKey;
            int origAlpha = alpha;

            // Use depth for TT probe
            if (tt.Probe(key, depth, alpha, beta, out int ttScore, out Move ttMove))
            {
                ply--;
                return ttScore;
            }
            int kingSq = MoveGen.GetKingSquare(board, isWhite);
            bool inCheck = MoveGen.IsSquareAttacked(board, kingSq, isWhite);

            // Null move pruning
            if (depth >= 3 && !inCheck && HasSufficientMaterial(board, isWhite))
            {
                var undoNull = board.MakeNullMove();
                int R = 2 + ply/5; // Null move reduction, can be tuned
                int nullScore = -PVS(board, depth - R, -beta, -beta + 1, !isWhite, tt);
                board.UnmakeNullMove(undoNull);
                if (nullScore >= beta)
                {
                    ply--;
                    return beta;
                }
            }


            // DEBUG: Compare move filters
            //MoveFilterDifferer.Compare(board, isWhite);


            // --- Generate legal moves ---
            Span<Move> full = new Span<Move>(_moveBuffer[ply - 1]);
            int count = 0;
            MoveGen.FilteredLegalWithoutFlag(board, full, ref count, isWhite);

            if (count == 0)
            {
                ply--;
                return inCheck ? -MateScore + (MaxDepth - depth) : 0;
            }
            Span<Move> moves = full.Slice(0, count);
            /**
            foreach (Move move in moves)
            {
                Console.WriteLine(MoveNotation.ToAlgebraicNotation(move, moves));
            }/**/
            MoveOrdering.OrderMoves(board, moves, ttMove, ply);

            int best = int.MinValue;
            Move bestMove = default;
            int moveNum = 0;

            int staticEval = Eval.EvalMaterialsExternal(board, isWhite);//isWhite? board.materialDelta : -board.materialDelta;
            int extension;
            foreach (var mv in moves)
            {
                moveNum++;
                extension = 0;
                // Example: extend for checks, promotions, and optionally captures in the first few plies
                if ((mv.Flags & (MoveFlags.Check | MoveFlags.Promotion)) != 0)
                    extension = 1;

                // --- Late Move Pruning (LMP) ---
                // Only for quiet moves, not in check, not first few moves, and at low depth
                if (depth <= 3 &&
                    !inCheck &&
                    mv.PieceCaptured == Piece.None &&
                    (mv.Flags & MoveFlags.Promotion) == 0 &&
                    moveNum > 4 + depth*3) // threshold can be tuned
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
                    int futilityMargin = 190; // tuning needed
                                              //----------------------------------------

                    if (staticEval + futilityMargin <= alpha)
                        continue; // Prune this move
                }

                int LmrSafetyMargin = 30; // tuning needed
               // Console.WriteLine("ply: " + ply);
                bool isKiller = mv.Equals(killerMoves[ply, 0]) || mv.Equals(killerMoves[ply, 1]);

                bool isTTMove = mv.Equals(ttMove);

                int safeMoves = 3; //+ (relativeDepth) *5/ 6; // e.g. at depth 6, don't reduce first ~5
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
                //_searchStack.Push(mv);

                int score;

                if (moveNum == 1)
                {
                    score = -PVS(board, depth - 1, -beta, -alpha, !isWhite, tt, hasReduced);


                }
                else if (canReduce)
                {
                    reduced++;
                    int R = 2 + depth/6; // LMR reduction, can be tuned
                    score = -PVS(board, depth - R, -alpha - 1, -alpha, !isWhite, tt, true);


                    if (score > alpha)
                    {
                        researched++;
                        score = -PVS(board, depth - 1, -alpha - 1, -alpha, !isWhite, tt, hasReduced);



                        if (score > alpha && score < beta)
                            score = -PVS(board, depth - 1, -beta, -alpha, !isWhite, tt, hasReduced);

                    }
                }
                else
                {
                    nullwindow++;
                    score = -PVS(board, depth - 1, -alpha - 1, -alpha, !isWhite, tt, hasReduced);

                    if (score > alpha && score < beta)
                    {
                        nullReseached++;
                        score = -PVS(board, depth - 1, -beta, -alpha, !isWhite, tt, hasReduced);

                    }
                }

                board.UnmakeMove(mv, undo);
                //_searchStack.Pop();
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
                        if (!mv.Equals(killerMoves[ply, 0]))
                        {
                            killerMoves[ply, 1] = killerMoves[ply, 0];
                            killerMoves[ply, 0] = mv;
                        }
                        MoveOrdering.RecordHistory(mv, ply);
                    }
                    break;
                }
            }

            NodeType flag = best <= origAlpha ? NodeType.UpperBound : best >= beta ? NodeType.LowerBound : NodeType.Exact;
            // Use depth for TT store
            tt.Store(key, depth, best, bestMove, flag);

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

        private static int Quiesce(Board board, int alpha, int beta, bool isWhite, int qDepth)
        {
            ////if (depthOn >11) 
            //    Console.WriteLine(qDepth +":\t" + Fen.ToFEN(board)); // Ensure FEN is up-to-date for debugging
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
