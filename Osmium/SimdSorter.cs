using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Osmium
{
    using System;
    using System.Diagnostics;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Runtime.Intrinsics;
    using System.Runtime.Intrinsics.X86;

    public static class SimdSorter
    {
        private static readonly Vector256<int> Perm1 = Vector256.Create(1, 0, 3, 2, 5, 4, 7, 6);
        private static readonly Vector256<int> Perm2 = Vector256.Create(2, 3, 0, 1, 6, 7, 4, 5);
        private static readonly Vector256<int> Perm4 = Vector256.Create(4, 5, 6, 7, 0, 1, 2, 3);

        // WARNING: Shared static buffer – not thread-safe!
        // Replace with [ThreadStatic] or per-thread buffer if enabling multi-threaded search
        // use threadstatic when multithreading
        // [ThreadStatic]
        private static readonly Move[] moveTmp = new Move[128];

        public static void SimdBitonicSort(Span<int> scores, Span<Move> moves)
        {
            int count = scores.Length;
            Debug.Assert(BitOperations.IsPow2((uint)count), "must be a power of two");
            Debug.Assert(count <= 128, "supported sizes, count greater than 128");
            Debug.Assert(count >= 8, "supported sizes, count less than 8");
            if (!Avx2.IsSupported) throw new PlatformNotSupportedException();

            // 1) Prepare index buffer
            int[] indices = new int[count];
            for (int i = 0; i < count; i++) indices[i] = i;

            // 2) Break into Vector256 lanes
            int vecCount = count / 8;
            var vS = new Vector256<int>[vecCount];
            var vI = new Vector256<int>[vecCount];

            var vecScoreSpan = MemoryMarshal.Cast<int, Vector256<int>>(scores);
            var vecIndexSpan = MemoryMarshal.Cast<int, Vector256<int>>(indices);
            for (int v = 0; v < vecCount; v++)
            {
                vS[v] = vecScoreSpan[v];
                vI[v] = vecIndexSpan[v];
            }

            // 3) Precompute the 8‑lane permute controls for strides < 8
            //var permControl = new Dictionary<int, Vector256<int>>()
            //{
            //    [1] = Vector256.Create(1, 0, 3, 2, 5, 4, 7, 6),
            //    [2] = Vector256.Create(2, 3, 0, 1, 6, 7, 4, 5),
            //    [4] = Vector256.Create(4, 5, 6, 7, 0, 1, 2, 3)
            //};

            // 4) The generic bitonic network
            for (int size = 2; size <= count; size <<= 1)
            {
                for (int stride = size >> 1; stride > 0; stride >>= 1)
                {
                    if (stride < 8)
                    {
                        // intra-vector compare/swaps
                        Vector256<int> ctrl = stride switch
                        {
                            1 => Perm1,
                            2 => Perm2,
                            4 => Perm4,
                            _ => throw new InvalidOperationException()
                        };

                        for (int v = 0; v < vecCount; v++)
                            CompareSwap(ref vS[v], ref vI[v], ctrl);
                    }
                    else
                    {
                        // inter-vector compare/swaps: swap whole 8‑lanes
                        int vecStride = stride >> 3; // how many vectors apart
                        for (int v = 0; v < vecCount; v++)
                        {
                            int w = v ^ vecStride;
                            if (w <= v || w >= vecCount) continue;

                            // compare whole vectors vS[v] vs vS[w]
                            var a = vS[v];
                            var b = vS[w];
                            var m = Avx2.CompareGreaterThan(a, b);

                            var lowV = Avx2.BlendVariable(a, b, m);
                            var highV = Avx2.BlendVariable(b, a, m);
                            vS[v] = lowV; vS[w] = highV;

                            // same for indices
                            var ai = vI[v];
                            var bi = vI[w];
                            var lowI = Avx2.BlendVariable(ai, bi, m);
                            var highI = Avx2.BlendVariable(bi, ai, m);
                            vI[v] = lowI; vI[w] = highI;
                        }
                    }
                }
            }

            // 5) Write back sorted scores & build final indices[]
            for (int v = 0; v < vecCount; v++)
            {
                vecScoreSpan[v] = vS[v];
                vecIndexSpan[v] = vI[v];
            }

            // 6) Permute moves once using the sorted indices
            Span<Move> tmp = moveTmp.AsSpan(0, count);
            moves.CopyTo(tmp);

            for (int i = 0; i < count; i++)
                moves[i] = tmp[indices[i]];
        }

        // Helper for the 8‑lane compare‑and‑swap
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        static void CompareSwap(ref Vector256<int> V, ref Vector256<int> I, Vector256<int> control)
        {
            // 1) Permute lanes according to the bitonic network step
            var pV = Avx2.PermuteVar8x32(V, control);
            var pI = Avx2.PermuteVar8x32(I, control);

            // 2) Build a mask where **pV > V** (i.e. the permuted lane is larger)
            //    NOTE: for ints we use CompareGreaterThan, reversing the args.
            var mask = Avx2.CompareGreaterThan(pV, V);

            // 3) Blend so that in each lane:
            //      - lowV holds the **larger** of (V, pV)
            //      - highV holds the **smaller**
            var lowV = Avx2.BlendVariable(V, pV, mask);
            var highV = Avx2.BlendVariable(pV, V, mask);

            var lowI = Avx2.BlendVariable(I, pI, mask);
            var highI = Avx2.BlendVariable(pI, I, mask);

            // 4) Finally write them back in the correct bitonic positions:
            //    lanes where mask==1 get lowV (the larger), mask==0 get highV (the smaller)
            V = Avx2.BlendVariable(highV, lowV, mask);
            I = Avx2.BlendVariable(highI, lowI, mask);
        }
    }

}
