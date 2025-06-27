
namespace ChessC_
{
	internal static class MoveOrdering
	{
		private static readonly int[] PieceValues = { 100, 320, 330, 500, 900, 20000 }; // Pawn, Knight, Bishop, Rook, Queen, King
		private static readonly int[,] MVVLVA = new int[6, 6]
		{
			{   900,   700,   700,   500,   100,  -9000 },
			{  2900,  2700,  2700,  2500,  2100,  -7000 },
			{  2900,  2700,  2700,  2500,  2100,  -7000 },
			{  4900,  4700,  4700,  4500,  4100,  -5000 },
			{  8900,  8700,  8700,  8500,  8100,   -100 },
			{ 99900, 99700, 99700, 99500, 99100,      0 }
		};

		private static readonly Move?[,] killerMoves =
			new Move?[Search.MaxDepth + 1, 2];

		private static readonly int[,] historyHeuristic = new int[64, 64];

		// Helper struct for scoring moves 
		private struct ScoredMove(Move move, int score)
		{
			public Move Move = move;
			public int Score = score;
		}
        public static int SEE(Board board, Move move)
        {
            // Get the value of the captured piece
            int capturedValue = move.PieceCaptured != Piece.None ? PieceValues[((int)move.PieceCaptured) % 6] : 0;

            // Save the board state and make the move
            UndoInfo undo = board.MakeSearchMove(board, move);

            // Find the least valuable recapture by the opponent
            Color opponent = board.sideToMove == Color.White ? Color.Black : Color.White;
            Move? recapture = FindLeastValuableAttacker(board, move.To, opponent);

            int score;
            if (recapture == null)
            {
                score = capturedValue;
            }
            else
            {
                // Recursive SEE: alternate sides
                score = capturedValue - SEE(board, recapture.Value);
            }

            // Undo the move to restore the board
            board.UnmakeMove(move, undo);

            return score;
        }
        private static Move? FindLeastValuableAttacker(Board board, Square target, Color color)
		{
			// Generate all pseudo-legal moves for this color
			var moves = MoveGen.GenerateAttackersToSquare(board, target, color);

			Move? best = null;
			int bestValue = int.MaxValue;

			foreach (var move in moves)
			{
				int value = PieceValues[((int)move.PieceMoved) % 6];
				if (value < bestValue)
				{
					best = move;
					bestValue = value;
				}
				
			}
			return best;
		}
        public static List<Move> OrderMoves(Board board, List<Move> moves, Move? pvMove = null, int depth = 0)
        {
            int count = moves.Count;
            if (count <= 1)
                return moves;

            // Inline matching for killer and PV
            Move? killer0 = null, killer1 = null;
            if ((uint)depth <= (uint)Search.MaxDepth)
            {
                killer0 = killerMoves[depth, 0];
                killer1 = killerMoves[depth, 1];
            }

            Span<ScoredMove> scoredMoves = stackalloc ScoredMove[count];
            int scoredCount = 0;

            foreach (var move in moves)
            {
                if (move.From == move.To)
                    continue;

                int score;

                // Score captures highest, then promotions, then history
                if (move.PieceCaptured != Piece.None)
                {
                    score = 100_000 + MVVLVA[(int)move.PieceMoved % 6, (int)move.PieceCaptured % 6];
                }
                else if ((move.Flags & MoveFlags.Promotion) != 0)
                {
                    score = 90_000 + (int)move.PromotionPiece * 100;
                }
                else
                {
                    score = 50_000 + historyHeuristic[(int)move.From, (int)move.To];
                }

                // Boost PV / killers
                if (pvMove.HasValue && move.Equals(pvMove.Value))
                    score = int.MaxValue;
                else if ((killer0.HasValue && move.Equals(killer0.Value)) ||
                         (killer1.HasValue && move.Equals(killer1.Value)))
                    score = int.MaxValue - 1;

                scoredMoves[scoredCount++] = new ScoredMove(move, score);
            }

            // Sort in descending order
            scoredMoves.Slice(0, scoredCount).Sort((a, b) => b.Score.CompareTo(a.Score));

            List<Move> ordered = new(scoredCount);
            for (int i = 0; i < scoredCount; i++)
                ordered.Add(scoredMoves[i].Move);

            return ordered;
        }


        public static void RecordKiller(int depth, Move move)
		{
			if (depth < 0 || depth > Search.MaxDepth) return;
			if (!killerMoves[depth, 0].HasValue
				|| !killerMoves[depth, 0].Value.Equals(move))
			{
				killerMoves[depth, 1] = killerMoves[depth, 0];
				killerMoves[depth, 0] = move;
			}
		}

		public static void RecordHistory(Move move, int depth)
		{
			if (depth < 0 || depth > Search.MaxDepth) return;
			int fromIdx = (int)move.From;
			int toIdx = (int)move.To;
			if (fromIdx >= 0 && fromIdx < 64 && toIdx >= 0 && toIdx < 64)
				historyHeuristic[fromIdx, toIdx] += depth * depth;
		}
	}
}
