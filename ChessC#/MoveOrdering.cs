using System;
using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace ChessC_
{
    internal static class MoveOrdering
    {
        private static ScoredMove[] scoredMoveBuffer = new ScoredMove[256];

        private const int MaxMoves = 256; // max moves per ply, adjust if needed

        [ThreadStatic]
        private static Move[] pvBuffer;

        [ThreadStatic]
        private static Move[] killerBuffer;

        [ThreadStatic]
        private static (Move move, int score)[] captureBuffer;

        [ThreadStatic]
        private static (Move move, int score)[] promotionBuffer;

        [ThreadStatic]
        private static (Move move, int score)[] quietBuffer;


        private static readonly int[] PieceValues = { 100, 320, 330, 500, 900, 20000 };
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
        //[MethodImpl(MethodImplOptions.NoInlining)]
        //public static void OrderMoves(Board board, Span<Move> moves, Move? pvMove = null, int depth = 0)
        //{
        //    int count = moves.Length;
        //    if (count <= 1)
        //        return;

        //    // Lazy initialize threadstatic buffers
        //    if (pvBuffer == null || pvBuffer.Length < count) pvBuffer = new Move[count];
        //    if (killerBuffer == null || killerBuffer.Length < count) killerBuffer = new Move[count];
        //    if (captureBuffer == null || captureBuffer.Length < count) captureBuffer = new (Move move, int score)[count];
        //    if (promotionBuffer == null || promotionBuffer.Length < count) promotionBuffer = new (Move move, int score)[count];
        //    if (quietBuffer == null || quietBuffer.Length < count) quietBuffer = new (Move move, int score)[count];

        //    Move? killer0 = null, killer1 = null;
        //    if ((uint)depth <= (uint)Search.MaxDepth)
        //    {
        //        killer0 = killerMoves[depth, 0];
        //        killer1 = killerMoves[depth, 1];
        //    }

        //    int pvCount = 0, killerCount = 0, captureCount = 0, promotionCount = 0, quietCount = 0;

        //    // Bucket moves and assign scores
        //    foreach (var move in moves)
        //    {
        //        if (move.From == move.To)
        //            continue;

        //        if (pvMove.HasValue && move.Equals(pvMove.Value))
        //        {
        //            pvBuffer[pvCount++] = move;
        //            continue;
        //        }

        //        if ((killer0.HasValue && move.Equals(killer0.Value)) || (killer1.HasValue && move.Equals(killer1.Value)))
        //        {
        //            killerBuffer[killerCount++] = move;
        //            continue;
        //        }

        //        if (move.PieceCaptured != Piece.None)
        //        {
        //            int score = 100_000 + MVVLVA[(int)move.PieceMoved % 6, (int)move.PieceCaptured % 6];
        //            captureBuffer[captureCount++] = (move, score);
        //            continue;
        //        }

        //        if ((move.Flags & MoveFlags.Promotion) != 0)
        //        {
        //            int score = 90_000 + (int)move.PromotionPiece * 100;
        //            promotionBuffer[promotionCount++] = (move, score);
        //            continue;
        //        }

        //        int quietScore = 50_000 + historyHeuristic[(int)move.From, (int)move.To];
        //        quietBuffer[quietCount++] = (move, quietScore);
        //    }

        //    // Sort buckets (except PV and killers, already "top priority")
        //    if (captureCount > 1)
        //        captureBuffer.AsSpan(0, captureCount).Sort((a, b) => b.score.CompareTo(a.score));
        //    if (promotionCount > 1)
        //        promotionBuffer.AsSpan(0, promotionCount).Sort((a, b) => b.score.CompareTo(a.score));
        //    if (quietCount > 1)
        //        quietBuffer.AsSpan(0, quietCount).Sort((a, b) => b.score.CompareTo(a.score));

        //    // Now merge capture, promotion, and quiet buffers by descending score:
        //    int cIdx = 0, pIdx = 0, qIdx = 0;
        //    int idx = 0;

        //    // Add PV moves first
        //    for (int i = 0; i < pvCount; i++)
        //        moves[idx++] = pvBuffer[i];
        //    // Add killer moves next
        //    for (int i = 0; i < killerCount; i++)
        //        moves[idx++] = killerBuffer[i];

        //    // Merge the 3 sorted arrays by score descending
        //    while (cIdx < captureCount || pIdx < promotionCount || qIdx < quietCount)
        //    {
        //        int cScore = cIdx < captureCount ? captureBuffer[cIdx].score : int.MinValue;
        //        int pScore = pIdx < promotionCount ? promotionBuffer[pIdx].score : int.MinValue;
        //        int qScore = qIdx < quietCount ? quietBuffer[qIdx].score : int.MinValue;

        //        if (cScore >= pScore && cScore >= qScore)
        //        {
        //            moves[idx++] = captureBuffer[cIdx++].move;
        //        }
        //        else if (pScore >= cScore && pScore >= qScore)
        //        {
        //            moves[idx++] = promotionBuffer[pIdx++].move;
        //        }
        //        else
        //        {
        //            moves[idx++] = quietBuffer[qIdx++].move;
        //        }
        //    }
        //}

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

            // Score all moves
            for (int i = 0; i < count; i++)
                scores[i] = ComputeScore(moves[i], pvMove, killer0, killer1);

            // Sort using SIMD + scalar fallback
            int pow2 = HighestPowerOfTwoLE(count);
            Span<int> prefixScores = scores.Slice(0, pow2);
            Span<Move> prefixMoves = moves.Slice(0, pow2);

            SimdBitonicSort(prefixScores, prefixMoves);

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

        /// <summary>Computes move score, with PV/Killer boosts.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ComputeScore(Move move, Move? pv, Move? k0, Move? k1)
        {
            if (pv.HasValue && move.Equals(pv.Value)) return int.MaxValue;
            if ((k0.HasValue && move.Equals(k0.Value)) ||
                (k1.HasValue && move.Equals(k1.Value)))
                return int.MaxValue - 1;

            if (move.PieceCaptured != Piece.None)
                return 100_000 + MVVLVA[(int)move.PieceMoved % 6, (int)move.PieceCaptured % 6];
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

        /// <summary>
        /// Hybrid SIMD + binary insertion sort dispatcher for up to 128 elements.
        /// </summary>
        public static void HybridSimdSort(Span<int> scores, Span<Move> moves)
        {
            int count = scores.Length;
            if (count <= 1) return;

            int pow2 = HighestPowerOfTwoLE(count);
            switch (pow2)
            {
                case 8: SimdBitonicSort(scores.Slice(0, 8), moves.Slice(0, 8)); break;
                case 16: SimdBitonicSort(scores.Slice(0, 16), moves.Slice(0, 16)); break;
                case 32: SimdBitonicSort(scores.Slice(0, 32), moves.Slice(0, 32)); break;
                case 64: SimdBitonicSort(scores.Slice(0, 64), moves.Slice(0, 64)); break;
                case 128: SimdBitonicSort(scores.Slice(0, 128), moves.Slice(0, 128)); break;
            }
            if (count > pow2)
                BinaryInsertionSort(scores.Slice(pow2, count - pow2), moves.Slice(pow2, count - pow2), count - pow2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int HighestPowerOfTwoLE(int n)
        {
            return 1 << (31 - BitOperations.LeadingZeroCount((uint)n));
        }

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

        /// <summary>
        /// Bitonic sort via index-permutation for power-of-two lengths up to 128.
        /// </summary>
        private static void SimdBitonicSort(Span<int> scores, Span<Move> moves)
        {
            int count = scores.Length;
            Debug.Assert(count == moves.Length);
            Debug.Assert(count == 8 || count == 16 || count == 32 || count == 64 || count == 128);

            if (!Avx2.IsSupported)
                throw new PlatformNotSupportedException("AVX2 is required for SIMD sorting");

            if (_scorebuffer == null || _scorebuffer.Length < MaxPowerOfTwo)
                _scorebuffer = new int[MaxPowerOfTwo];
            if (_moveBuffer == null || _moveBuffer.Length < MaxPowerOfTwo)
                _moveBuffer = new Move[MaxPowerOfTwo];

            // Copy to aligned buffers
            Span<int> scoreBuf = _scorebuffer.AsSpan(0, count);
            Span<Move> moveBuf = _moveBuffer.AsSpan(0, count);
            scores.CopyTo(scoreBuf);
            moves.CopyTo(moveBuf);

            // Perform bitonic sort (8-wide)
            for (int size = 2; size <= count; size <<= 1)
            {
                for (int stride = size >> 1; stride > 0; stride >>= 1)
                {
                    for (int i = 0; i < count; i += size)
                    {
                        for (int j = 0; j < size; j++)
                        {
                            int ix = i + j;
                            int jx = i + (j ^ stride);
                            if (jx <= ix) continue;

                            bool ascending = (j & size) == 0;
                            ref int a = ref scoreBuf[ix];
                            ref int b = ref scoreBuf[jx];
                            if ((a < b) == ascending)
                            {
                                (a, b) = (b, a);
                                (moveBuf[ix], moveBuf[jx]) = (moveBuf[jx], moveBuf[ix]);
                            }
                        }
                    }
                }
            }

            // Copy back
            scoreBuf.CopyTo(scores);
            moveBuf.CopyTo(moves);
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
            int capturedValue = move.PieceCaptured != Piece.None ? PieceValues[((int)move.PieceCaptured) % 6] : 0;
            var undo = board.MakeSearchMove(board, move);
            Color opp = board.sideToMove == Color.White ? Color.Black : Color.White;
            Move? rec = FindLeastValuableAttacker(board, move.To, opp);
            int score = rec == null ? capturedValue : capturedValue - SEE(board, rec.Value);
            board.UnmakeMove(move, undo);
            return score;
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
