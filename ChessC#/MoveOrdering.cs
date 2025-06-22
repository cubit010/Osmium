
namespace ChessC_
{
	internal static class MoveOrdering
	{
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

		public static List<Move> OrderMoves(
			Board board,
			List<Move> moves,
			Move? pvMove = null,
			int depth = 0
		)
		{
			int count = moves.Count;
			if (count <= 1)
				return moves; // No need to allocate a new list

			// Precompute PV and killer moves for O(1) lookup
			HashSet<Move> specialMoves = [];
			bool hasPV = pvMove.HasValue;
			Move pv = hasPV ? pvMove.Value : default;
			if (hasPV && pv.From != pv.To && moves.Contains(pv))
				specialMoves.Add(pv);

			Move? k0 = null, k1 = null;
			if ((uint)depth <= (uint)Search.MaxDepth)
			{
				k0 = killerMoves[depth, 0];
				k1 = killerMoves[depth, 1];
				if (k0.HasValue) specialMoves.Add(k0.Value);
				if (k1.HasValue) specialMoves.Add(k1.Value);
			}

			List<ScoredMove> scoredMoves = new(count);
			foreach (var move in moves)
			{
				if (move.From == move.To) continue; // Prevent crash

				int score;
				if (hasPV && move.Equals(pv))
					score = int.MaxValue;
				else if (k0.HasValue && move.Equals(k0.Value))
					score = int.MaxValue - 1;
				else if (k1.HasValue && move.Equals(k1.Value))
					score = int.MaxValue - 2;
				else if (move.PieceCaptured != Piece.None)
					score = 100000 + MVVLVA[(int)move.PieceCaptured % 6, (int)move.PieceMoved % 6];
				else if ((move.Flags & MoveFlags.Promotion) != 0)
					score = 90000 + (int)move.PromotionPiece * 100;
				else
					score = 50000 + historyHeuristic[(int)move.From, (int)move.To];

				scoredMoves.Add(new ScoredMove(move, score));
			}

			scoredMoves.Sort((a, b) => b.Score.CompareTo(a.Score));

			
			List<Move> ordered = new(scoredMoves.Count);
			foreach (var sm in scoredMoves)
				ordered.Add(sm.Move);

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
