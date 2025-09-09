using System;

namespace Osmium
{
    public static class SimdTester
    {
        // Maximum moves per position (adjust if needed)
        private const int MaxMoves = 256;

        public static void Run(Board board)
        {
            Utils.PrintBoard(board);
            // 1) Allocate a temporary span to hold generated moves
            Span<Move> moves = stackalloc Move[MaxMoves];
            int moveCount = 0;

            // 2) Generate all legal moves for the current board
            // Assume FilteredLegalWithoutFlag fills the span and sets moveCount
            MoveGen.FilteredLegalWithoutFlag(board, moves, ref moveCount, isWhite: board.sideToMove==Color.White);

            if (moveCount == 0)
            {
                Console.WriteLine("No legal moves generated.");
                return;
            }

            // Slice the span to the actual number of moves generated
            Span<Move> validMoves = moves.Slice(0, moveCount);

            // 3) Call the move ordering test function
            // Optionally pass PV move (null here) and depth 0
            MoveOrdering.OrderMovesTest(board, validMoves, pvMove: null, depth: 0);
        }
    }
}
