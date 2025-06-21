
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ChessC_
{
    public static class Search
    {


        public static int MaxDepth = Program.MaxDepth;
        private const int MateScore = 1_000_000;
        private static readonly Move[,] killerMoves = new Move[MaxDepth + 1, 2];
        public static long NodesVisited;


        internal static Move FindBestMoveLazySMP(Board board, TranspositionTable tt, int timeLimitMs, int threadCount)
        {
            Console.WriteLine("Lazy SMP search is not implemented yet.");
            return default; // Placeholder for lazy SMP implementation
        }


        internal static Move FindBestMove(Board board, TranspositionTable tt, int timeLimitMs)
        {
            Move lastBest = default;
            var searchStart = Stopwatch.StartNew();

            int maxDepth = MaxDepth;
            for (int depth = 1; depth <= maxDepth; depth++)
            {
                NodesVisited = 0;
                var iterStart = Stopwatch.StartNew();

                // Check time before starting this depth
                if (searchStart.ElapsedMilliseconds >= timeLimitMs)
                    break;

                lastBest = SearchAtDepth(board, tt, depth);

                iterStart.Stop();
                Console.WriteLine($"Depth {depth}: Nodes={NodesVisited}, Time={iterStart.ElapsedMilliseconds} ms, Best={lastBest}");

            }
            searchStart.Stop();
            return lastBest;
        }

        private static Move SearchAtDepth(Board board, TranspositionTable tt, int targetDepth)
        {
            bool isWhite = board.sideToMove == Color.White;
            int alpha = int.MinValue + 1;
            int beta = int.MaxValue - 1;
            Move bestMove = default;
            int bestScore = int.MinValue;

            var moves = MoveGen.FilteredLegalMoves(board, isWhite);
            tt.Probe(board.zobristKey, targetDepth, alpha, beta, out _, out Move ttMove);
            moves = MoveOrdering.OrderMoves(board, moves, ttMove, targetDepth);

            // immediate mate
            foreach (var mv in moves)
                if ((mv.Flags & MoveFlags.Checkmate) != 0)
                    return mv;

            foreach (var move in moves)
            {
               

                UndoInfo undo = board.MakeSearchMove(board, move);
                int score = -Negamax(board, targetDepth - 1, -beta, -alpha, !isWhite, tt);
                board.UnmakeMove(move, undo);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = move;
                }
                alpha = Math.Max(alpha, score);
            }

            return bestMove;
        }

        private static int Negamax(Board board, int depth, int alpha, int beta, bool isWhiteToMove, TranspositionTable tt)
        {
            NodesVisited++;
            if (depth == 0)
            {
               
                return Quiesce(board, alpha, beta, isWhiteToMove);
            }

            ulong key = board.zobristKey;
            int origAlpha = alpha;
            if (tt.Probe(key, depth, alpha, beta, out int ttScore, out Move ttMove))
                return ttScore;

            var moves = MoveGen.FilteredLegalMoves(board, isWhiteToMove);
            if (moves.Count == 0)
            {
                int kingSq = MoveGen.GetKingSquare(board, isWhiteToMove);
                bool inCheck = MoveGen.IsSquareAttacked(board, kingSq, isWhiteToMove);
                return inCheck ? -MateScore + (MaxDepth - depth) : 0;
            }

            moves = MoveOrdering.OrderMoves(board, moves, ttMove, depth);

            int bestScore = int.MinValue;
            Move bestMove = default;

            foreach (var move in moves)
            {

                UndoInfo undo = board.MakeSearchMove(board, move);
                int score = -Negamax(board, depth - 1, -beta, -alpha, !isWhiteToMove, tt);
                board.UnmakeMove(move, undo);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = move;
                }
                alpha = Math.Max(alpha, score);
                if (alpha >= beta)
                {
                    if (move.PieceCaptured == Piece.None)
                    {
                        if (!move.Equals(killerMoves[depth, 0]))
                        {
                            killerMoves[depth, 1] = killerMoves[depth, 0];
                            killerMoves[depth, 0] = move;
                        }
                        MoveOrdering.RecordHistory(move, depth);
                    }
                    break;
                }
            }

            NodeType flag = bestScore <= origAlpha ? NodeType.UpperBound
                          : bestScore >= beta ? NodeType.LowerBound
                                                    : NodeType.Exact;
            tt.Store(key, depth, bestScore, bestMove, flag);
            return bestScore;
        }

        private static int Quiesce(Board board, int alpha, int beta, bool isWhiteToMove)
        {
            int standPat = Eval.EvalBoard(board, isWhiteToMove);
            if (standPat >= beta) return beta;
            alpha = Math.Max(alpha, standPat);

            var capMoves = MoveGen.GenerateCaptureMoves(board, isWhiteToMove);
            MoveGen.FlagCheckAndMate(board, capMoves, isWhiteToMove);
            capMoves = MoveOrdering.OrderMoves(board, capMoves, null, 0);

            foreach (var move in capMoves)
            {

                UndoInfo undo = board.MakeSearchMove(board, move);
                int score = -Quiesce(board, -beta, -alpha, !isWhiteToMove);
                board.UnmakeMove(move, undo);

                if (score >= beta) return beta;
                alpha = Math.Max(alpha, score);
            }
            return alpha;
        }
    }
}