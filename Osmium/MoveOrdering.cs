using System;
using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Osmium
{
	internal static class MoveOrdering
	{
		private static ScoredMove[] scoredMoveBuffer = new ScoredMove[256];

		private const int MaxMoves = 256; // max moves per ply, adjust if needed



		private static readonly int[] PieceValues = {	100, 320, 330, 500, 900, 20000, 
														100, 320, 330, 500, 900, 20000 };
		private static readonly int[,] MVVLVA = new int[6, 6]
		{
			{   900,   700,   700,   500,   100,  -9000 },
			{  2900,  2700,  2700,  2500,  2100,  -7000 },
			{  2900,  2700,  2700,  2500,  2100,  -7000 },
			{  4900,  4700,  4700,  4500,  4100,  -5000 },
			{  8900,  8700,  8700,  8500,  8100,   -100 },
			{ 99900, 99700, 99700, 99500, 99100,      0 }
		};

		private static readonly Move?[,] killerMoves = new Move?[Search.MaxDepth + 1, 2];
		private static readonly int[,] historyHeuristic = new int[64, 64];
		private struct ScoredMove(Move move, int score)
		{
			public Move Move = move;
			public int Score = score;
		}


		private const int MaxPowerOfTwo = 128;

		[ThreadStatic]
		private static Move[] _moveBuffer;
		[ThreadStatic]
		private static int[] _scorebuffer;
		[ThreadStatic] 
		private static int[] _indexBuffer;
		// MaxMoves = 256
		public static void OrderMoves(Board board, Span<Move> moves, Move? pvMove = null, int depth = 0)
		{
			int count = moves.Length;
			if (count <= 1) return;

			Move? killer0 = depth <= Search.MaxDepth ? killerMoves[depth, 0] : null;
			Move? killer1 = depth <= Search.MaxDepth ? killerMoves[depth, 1] : null;

			// Initialize thread-static buffers
			if (_scorebuffer == null || _scorebuffer.Length < MaxMoves)
				_scorebuffer = new int[MaxMoves];
			if (_moveBuffer == null || _moveBuffer.Length < MaxMoves)
				_moveBuffer = new Move[MaxMoves];

			Span<int> scores = _scorebuffer.AsSpan(0, count);
			Span<Move> tmpMoves = _moveBuffer.AsSpan(0, count);

			// 1) Pre‑insert PV/Killers
			int insertPos = 0;
			//ExtractAndInsert(moves, pvMove, ref insertPos);
			//ExtractAndInsert(moves, killer0, ref insertPos);
			//ExtractAndInsert(moves, killer1, ref insertPos);

			//int remainingCount = count - insertPos;
			//if (remainingCount <= 1)
			//	return;

			// 2) Score remaining moves
			for (int i = 0; i < count; i++)
				scores[i] = ComputeScore(moves[insertPos + i], pvMove, killer0, killer1, board);

			// 3) Sort remaining moves
		   int pow2 = HighestPowerOfTwoLE(count);
			if (pow2 < 8)
			{

				
				Span<int> tailScores = scores.Slice(0, count);
				Span<Move> tailMoves = moves.Slice(0, count);
				BinaryInsertionSort(tailScores, tailMoves, tailScores.Length);
				// Copy final sorted result back
				tailMoves.CopyTo(moves);


            }
			else
			{
				Span<int> prefixScores = scores.Slice(0, pow2);
				Span<Move> prefixMoves = moves.Slice(0, pow2);

				SimdSorter.SimdBitonicSort(prefixScores, prefixMoves);
				if (count > pow2)
				{
					Span<int> tailScores = scores.Slice(pow2, count - pow2);
					Span<Move> tailMoves = moves.Slice(pow2, count - pow2);
					BinaryInsertionSort(tailScores, tailMoves, tailScores.Length);

					// Merge two sorted sections
					MergeSortedSections(prefixScores, prefixMoves, tailScores, tailMoves, scores, tmpMoves);


                    // Copy final sorted result back
                    tmpMoves.CopyTo(moves);
				}
			}
		}

		// Static helper—no spans captured by closures
		private static void ExtractAndInsert(Span<Move> moves, Move? target, ref int insertPos)
		{
			if (!target.HasValue) return;
			int count = moves.Length;
			for (int i = insertPos; i < count; i++)
			{
				if (moves[i].Equals(target.Value))
				{
					if (i != insertPos)
						(moves[i], moves[insertPos]) = (moves[insertPos], moves[i]);
					insertPos++;
					return;
				}
			}
		}

		/// <summary>Computes move score, with PV/Killer boosts.</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		private static int ComputeScore(Move move, Move? pv, Move? k0, Move? k1, Board board)
		{
			if (pv.HasValue && move.Equals(pv.Value)) return int.MaxValue;
			if ((k0.HasValue && move.Equals(k0.Value)) ||
				(k1.HasValue && move.Equals(k1.Value)))
				return int.MaxValue - 1;

			if (move.PieceCaptured != Piece.None)
				return 100_000 + MVVLVA[(int)move.PieceMoved % 6, (int)move.PieceCaptured % 6];
                    //SEE(board, move);
			if ((move.Flags & MoveFlags.Promotion) != 0)
				return 90_000 + (int)move.PromotionPiece * 100;
			return 50_000 + historyHeuristic[(int)move.From, (int)move.To];
		}
		/// <summary>Merges two sorted spans into a final fully sorted span using provided buffers.</summary>
		private static void MergeSortedSections(
			Span<int> s1, Span<Move> m1,
			Span<int> s2, Span<Move> m2,
			Span<int> tempScores,
			Span<Move> outputMoves)
		{
			int i = 0, j = 0, k = 0;
			int n1 = s1.Length, n2 = s2.Length;

			while (i < n1 && j < n2)
			{
				if (s1[i] >= s2[j])
				{
					tempScores[k] = s1[i];
					outputMoves[k++] = m1[i++];
				}
				else
				{
					tempScores[k] = s2[j];
					outputMoves[k++] = m2[j++];
				}
			}

			while (i < n1)
			{
				tempScores[k] = s1[i];
				outputMoves[k++] = m1[i++];
			}
			while (j < n2)
			{
				tempScores[k] = s2[j];
				outputMoves[k++] = m2[j++];
			}
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int HighestPowerOfTwoLE(int n)
		{
			return 1 << (31 - BitOperations.LeadingZeroCount((uint)n));
		}

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static void BinaryInsertionSort(Span<int> scores, Span<Move> moves, int count)
		{
			for (int i = 1; i < count; i++)
			{
				int keyScore = scores[i];
				Move keyMove = moves[i];
				int left = 0, right = i;
				while (left < right)
				{
					int mid = (left + right) >> 1;
					if (scores[mid] < keyScore) right = mid;
					else left = mid + 1;
				}
				for (int j = i; j > left; j--)
				{
					scores[j] = scores[j - 1];
					moves[j] = moves[j - 1];
				}
				scores[left] = keyScore;
				moves[left] = keyMove;
			}
		}



		private static Span<T> GetBuffer<T>(ref T[] buffer, int length)
		{
			if (buffer == null || buffer.Length < length)
				buffer = new T[length];
			return buffer.AsSpan(0, length);
		}

		public static void RecordKiller(int depth, Move move)
		{
			if (depth < 0 || depth > Search.MaxDepth) return;
			if (!killerMoves[depth, 0].HasValue || !killerMoves[depth, 0].Value.Equals(move))
			{
				killerMoves[depth, 1] = killerMoves[depth, 0];
				killerMoves[depth, 0] = move;
			}
		}

		public static void RecordHistory(Move move, int depth)
		{
			if (depth < 0 || depth > Search.MaxDepth) return;
			int f = (int)move.From;
			int t = (int)move.To;
			if (f >= 0 && f < 64 && t >= 0 && t < 64)
				historyHeuristic[f, t] += depth * depth;
		}

        public static int SEE(Board board, Move move)
        {
            int to = (int)move.To;
            int stm = (int)board.sideToMove; // 0=White, 1=Black
            int[] gain = new int[32];
            int depth = 0;

            // Local copies
            ulong[] bbs = board.bitboards;
            ulong occ = board.occupancies[2];

            // Piece values
            int[] values = PieceValues;

            // Who is on move: 0=white, 1=black
            int side = stm;

            // Initial gain: value of captured piece
            gain[depth++] = move.PieceCaptured != Piece.None ? values[(int)move.PieceCaptured % 6] : 0;

            // Remove the attacking piece from the from square, add to the to square
            ulong fromMask = 1UL << (int)move.From;
            ulong toMask = 1UL << to;
            int moving = (int)move.PieceMoved;

            ulong[] attackers = new ulong[2];
            attackers[0] = GetAttackers(board, to, Color.White, occ);
            attackers[1] = GetAttackers(board, to, Color.Black, occ);

            // Remove the moving piece from its from square in attackers and occ
            occ &= ~fromMask;
            attackers[side] &= ~fromMask;

            // Remove the captured piece from the to square in occ (if any)
            if (move.PieceCaptured != Piece.None)
                occ &= ~toMask;

            // Now simulate the sequence of captures
            int lastPiece = moving;
            while (true)
            {
                // Find the least valuable attacker for the current side
                int bestPiece = -1;
                int bestValue = int.MaxValue;
                ulong att = attackers[side] & occ;
                for (int p = 0; p < 6; p++)
                {
                    ulong bb = bbs[p + 6 * side] & att;
                    if (bb != 0)
                    {
                        int sq = BitOperations.TrailingZeroCount(bb);
                        if (values[p] < bestValue)
                        {
                            bestValue = values[p];
                            bestPiece = p + 6 * side;
                        }
                    }
                }
                if (bestPiece == -1) break;

                // Add gain for this capture
                gain[depth] = values[bestPiece % 6] - gain[depth - 1];
                depth++;

                // Remove this piece from occ and attackers
                ulong pieceMask = bbs[bestPiece] & occ;
                occ &= ~pieceMask;
                attackers[side] &= ~pieceMask;

                // Switch side
                side = 1 - side;
            }

            // Negamax the gain array
            for (int i = depth - 1; i > 0; i--)
                gain[i - 1] = -Math.Max(-gain[i - 1], gain[i]);

            return gain[0];
        }

        // Helper: get all attackers to a square for a color
        private static ulong GetAttackers(Board board, int sq, Color color, ulong occ)
        {
            ulong attackers = 0UL;
            if (color == Color.White)
            {
                attackers |= board.bitboards[(int)Piece.WhitePawn] & MoveTables.PawnCaptures[0, 0, sq];
                attackers |= board.bitboards[(int)Piece.WhiteKnight] & MoveTables.KnightMoves[sq];
                attackers |= board.bitboards[(int)Piece.WhiteBishop] & Magics.GetBishopAttacks(sq, occ);
                attackers |= board.bitboards[(int)Piece.WhiteRook] & Magics.GetRookAttacks(sq, occ);
                attackers |= board.bitboards[(int)Piece.WhiteQueen] & (Magics.GetBishopAttacks(sq, occ) | Magics.GetRookAttacks(sq, occ));
                attackers |= board.bitboards[(int)Piece.WhiteKing] & MoveTables.KingMoves[sq];
            }
            else
            {
                attackers |= board.bitboards[(int)Piece.BlackPawn] & MoveTables.PawnCaptures[1, 0, sq];
                attackers |= board.bitboards[(int)Piece.BlackKnight] & MoveTables.KnightMoves[sq];
                attackers |= board.bitboards[(int)Piece.BlackBishop] & Magics.GetBishopAttacks(sq, occ);
                attackers |= board.bitboards[(int)Piece.BlackRook] & Magics.GetRookAttacks(sq, occ);
                attackers |= board.bitboards[(int)Piece.BlackQueen] & (Magics.GetBishopAttacks(sq, occ) | Magics.GetRookAttacks(sq, occ));
                attackers |= board.bitboards[(int)Piece.BlackKing] & MoveTables.KingMoves[sq];
            }
            return attackers;
        }

        private static Move? FindLeastValuableAttacker(Board board, Square target, Color color)
		{
			Span<Move> buf = stackalloc Move[256];
			int cnt = 0;
			MoveGen.GenerateAttackersToSquare(board, target, color, buf, ref cnt);

			Move? best = null;
			int bestVal = int.MaxValue;
			for (int i = 0; i < cnt; i++)
			{
				var mv = buf[i];
				int v = PieceValues[((int)mv.PieceMoved) % 6];
				if (v < bestVal)
				{
					best = mv;
					bestVal = v;
				}
			}
			return best;
		}
	}
}
