using System;
using System.Collections.Generic;

namespace ChessC_
{
    public static class Search
    {
        public static int MaxDepth = 5;
        private const int MateScore = 1000000;

        internal static Move FindBestMove(Board board, TranspositionTable tt)
        {
            bool isWhite = board.sideToMove == Color.White;
            int alpha = int.MinValue + 1;
            int beta = int.MaxValue - 1;
            Move bestMove = default;
            int bestScore = int.MinValue;

            // 1) Generate all legal moves for the side to move
            List<Move> moves = MoveGen.FilteredLegalMoves(board, isWhite);

            // 2) Flag checks & mates at root (depth = MaxDepth)
            MoveGen.FlagCheckAndMate(board, moves, isWhite);

            // 3) Look for immediate mates in one ply and prune
            foreach (var move in moves)
            {
                if (move.Flags.HasFlag(MoveFlags.Checkmate))
                {
                    // We found mate immediately at the root
                    return move;
                }
            }

            // 4) Normal root‐level negamax loop
            foreach (var move in moves)
            {
                board.MakeMove(move);

                int score = -Negamax(
                    board,
                    MaxDepth - 1,
                    -beta,
                    -alpha,
                    !isWhite,
                    tt
                );

                board.UnmakeMove();

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = move;
                }

                alpha = Math.Max(alpha, score);
            }

            return bestMove;
        }

        private static int Negamax(
            Board board,
            int depth,
            int alpha,
            int beta,
            bool isWhiteToMove,
            TranspositionTable tt
        )
        {
            // 1) Check for leaf node
            if (depth == 0)
                return (int)Eval.EvalBoard(board, isWhiteToMove);

            ulong key = board.zobristKey;
            int origAlpha = alpha;

            // 2) Probe transposition table
            if (tt.Probe(key, depth, alpha, beta, out int ttScore, out Move ttMove))
                return ttScore;

            // 3) Generate all legal moves at this node
            List<Move> moves = MoveGen.FilteredLegalMoves(board, isWhiteToMove);

            // 3a) If no legal moves, it must be checkmate or stalemate
            if (moves.Count == 0)
            {
                int kingSq = MoveGen.GetKingSquare(board, isWhiteToMove);
                bool inCheck = MoveGen.IsSquareAttacked(board, kingSq, isWhiteToMove);
                // If in check and no moves → checkmate. Otherwise stalemate.
                return inCheck
                    ? -MateScore + (MaxDepth - depth)   // mate‐in‐(MaxDepth–depth) for the side to move
                    : 0;                                 // stalemate
            }

            // 4) Flag checks & mates for all moves at this node
            MoveGen.FlagCheckAndMate(board, moves, isWhiteToMove);

            // 5) If any move is a forced mate, return that right away
            foreach (var m in moves)
            {
                if (m.Flags.HasFlag(MoveFlags.Checkmate))
                {
                    // Since m delivers checkmate, score it as +MateScore – ply
                    return MateScore - (MaxDepth - depth);
                }
            }

            // 6) Normal negamax loop with alpha‐beta pruning
            int bestScore = int.MinValue;
            Move bestMove = default;

            foreach (var move in moves)
            {
                board.MakeMove(move);
                int score = -Negamax(
                    board,
                    depth - 1,
                    -beta,
                    -alpha,
                    !isWhiteToMove,
                    tt
                );
                board.UnmakeMove();

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = move;
                }

                alpha = Math.Max(alpha, score);
                if (alpha >= beta)
                    break;  // Beta‐cutoff
            }

            // 7) Store in transposition table
            NodeType flag;
            if (bestScore <= origAlpha) flag = NodeType.UpperBound;
            else if (bestScore >= beta) flag = NodeType.LowerBound;
            else flag = NodeType.Exact;
            tt.Store(key, depth, bestScore, bestMove, flag);

            return bestScore;
        }
    }
}
