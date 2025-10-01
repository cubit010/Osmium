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



		private static readonly int[] PieceValues = {   100, 320, 330, 500, 900, 20000,
														100, 320, 330, 500, 900, 20000 };
		
		private static readonly int[,] MVVLVA = new int[6, 6]
        {
			// Victim:    P    N    B    R    Q    K
			/* P */ { 1050, 3050, 3150, 5050, 9050, 10050 },
			/* N */ { 1040, 3040, 3140, 5040, 9040, 10040 },
			/* B */ { 1030, 3030, 3130, 5030, 9030, 10030 },
			/* R */ { 1020, 3020, 3120, 5020, 9020, 10020 },
			/* Q */ { 1010, 3010, 3110, 5010, 9010, 10010 },
			/* K */ { 1000, 3000, 3100, 5000, 9000, 10000 }
        };

        private static readonly Move?[,] killerMoves = new Move?[Search.MaxDepth + 1, 2];
		internal static int[,] historyHeuristic = new int[64, 64];

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

        const bool orderDebug = false;
        internal const bool useQuickSort = false;
        public static void OrderMovesScalar(Board board, Span<Move> moves, Move? pvMove = null, int depth = 0){
            int count = moves.Length;
            if (count <= 1) return;

            if (_scorebuffer == null || _scorebuffer.Length < MaxMoves)
                _scorebuffer = new int[MaxMoves];
            if (_moveBuffer == null || _moveBuffer.Length < MaxMoves)
                _moveBuffer = new Move[MaxMoves];

            Move? killer0 = depth <= Search.MaxDepth ? killerMoves[depth, 0] : null;
            Move? killer1 = depth <= Search.MaxDepth ? killerMoves[depth, 1] : null;
            if (killer0 != null)
                Console.WriteLine($"Killer0 at depth {depth}: {killer0}");
            if (killer1 != null)
                Console.WriteLine($"Killer1 at depth {depth}: {killer1}");

            Span<int> scores = _scorebuffer.AsSpan(0, count);
            Span<Move> tmpMoves = _moveBuffer.AsSpan(0, count);

            for (int i = 0; i < count; i++)
                scores[i] = ComputeScore(moves[i], pvMove, killer0, killer1, board);

            if (useQuickSort)
                QuickSort(scores, moves, count);
            else
                BinaryInsertionSort(scores, moves, count);
        }

      

        internal const bool usePadding = true;
        
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


			//int remainingCount = count - insertPos;
			//if (remainingCount <= 1)
			//	return;

			// 2) Score remaining moves
			for (int i = 0; i < count; i++)
				scores[i] = ComputeScore(moves[insertPos + i], pvMove, killer0, killer1, board);

            // 3) Sort remaining moves
            if (!usePadding)
            {
                int pow2 = HighestPowerOfTwoLE(count);
                if (pow2 < 16)
                {
                    BinaryInsertionSort(scores, moves, scores.Length);
                    // Copy final sorted result back
                    moves.CopyTo(moves);
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
            else
            {
                //if (count < 8)
                //{
                //    BinaryInsertionSort(scores, moves, scores.Length);
                //    return;
                //}

                int pow2 = HighestPowerOfTwoLE(count);
                if (pow2 < 8)
                    pow2 = 8;
                if (count != pow2)
                {
                    pow2 <<= 1;
                }
                SimdSorter.SimdBitonicSort(scores, moves, pow2);

            }

            if (false)
            {
                bool sorted = true;
                for (int i = 1; i < count; i++)
                {
                    if (scores[i] > scores[i - 1]) // should never increase
                    {
                        sorted = false;
                        Console.WriteLine($"Not sorted at {i}, {scores[i]} > {scores[i - 1]}");
                        break;
                    }
                }

                if (!sorted)
                {
                    Console.WriteLine(board.zobristKey);
                    Console.WriteLine("historyHeuristic = {");
                    for (int i = 0; i < 64; i++)
                    {
                        Console.Write("    { ");
                        for (int j = 0; j < 64; j++)
                        {
                            Console.Write(historyHeuristic[i, j]);
                            if (j < 63) Console.Write(", ");
                        }
                        Console.WriteLine(" },");
                       
                    }
                    Console.WriteLine("}");
                    //Console.WriteLine("Sort verification:");
                    for (int i = 0; i < scores.Length; i++)
                    {
                        Console.Write($"{scores[i],6} - {MoveNotation.ToAlgebraicNotation(moves[i], moves),-6} ");
                        if (i % 8 == 7)
                            Console.WriteLine();
                    }
                    if (!sorted)
                        Environment.Exit(1);

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
            int score = 0;
            if (move.PieceCaptured != Piece.None)
                return 300_000 + MVVLVA[(int)move.PieceMoved % 6, (int)move.PieceCaptured % 6] + (((move.Flags & MoveFlags.Promotion) != 0) ? (int)move.PromotionPiece * 100: 0);
                    //SEE(board, move);
			if ((move.Flags & MoveFlags.Promotion) != 0)
				return 80_000 + (int)move.PromotionPiece * 100;
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

        //[MethodImpl(MethodImplOptions.AggressiveOptimization)]
        [MethodImpl(MethodImplOptions.NoInlining)]

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
        private static void QuickSort(Span<int> scores, Span<Move> moves, int count)
        {
            for (int i = 1; i < count; i++)
            {
                int keyScore = scores[i];
                Move keyMove = moves[i];
                int j = i - 1;
                while (j >= 0 && scores[j] < keyScore)
                {
                    scores[j + 1] = scores[j];
                    moves[j + 1] = moves[j];
                    j--;
                }
                scores[j + 1] = keyScore;
                moves[j + 1] = keyMove;
            }
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
