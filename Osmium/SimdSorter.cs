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
    using System.Reflection.Metadata;
    using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using System.Runtime.Intrinsics;
	using System.Runtime.Intrinsics.X86;

	public static class SimdSorter
	{

        private static readonly int[] PrebuiltIndices = Enumerable.Range(0, 256).ToArray();

        //intra vectors are element for element, 

        // -1s are take high val, 0s are take low val
        // Mask# for bitonic step
        // Mask#B for final shuffle in descending direction (high-low)
        // Mask#F for final shuffle in ascending direction (low-high)

        private static readonly Vector256<int> Mask1 = Vector256.Create(0, -1, -1, 0, 0, -1, -1, 0);

		private static readonly Vector256<int> Mask1B = Vector256.Create(-1, 0, -1, 0, -1, 0, -1, 0);
		private static readonly Vector256<int> Mask1F = Vector256.Create(0, -1, 0, -1, 0, -1, 0, -1);
		private static readonly Vector256<int> Mask1C = Vector256.Create(0, -1, 0, -1, -1, 0, -1, 0);

		private static readonly Vector256<int> Perm1 = Vector256.Create(1, 0, 3, 2, 5, 4, 7, 6);


		private static readonly Vector256<int> Mask2 = Vector256.Create(0, 0, -1, -1, /**/ -1, -1, 0, 0);

		private static readonly Vector256<int> Mask2B = Vector256.Create(-1, -1, 0, 0, /**/ -1, -1, 0, 0);
		private static readonly Vector256<int> Mask2F = Vector256.Create(0, 0, -1, -1, /**/ 0, 0, -1, -1);

		private static readonly Vector256<int> Perm2 = Vector256.Create(2, 3, 0, 1, 6, 7, 4, 5);

		//no mask 4 bitonic because 4 is only used in final, not bitonic, cannot be done anyways with 8 elements

		private static readonly Vector256<int> Mask4B = Vector256.Create(-1, -1, -1, -1, 0, 0, 0, 0);
		private static readonly Vector256<int> Mask4F = Vector256.Create(0, 0, 0, 0, -1, -1, -1, -1);

		private static readonly Vector256<int> Perm4 = Vector256.Create(4, 5, 6, 7, 0, 1, 2, 3);

		// Shared static buffer – not thread-safe!
		// Replace with [ThreadStatic] or per-thread buffer if enabling multi-threaded search
		// use threadstatic when multithreading
		// [ThreadStatic]
		private static readonly Move[] moveTmp = new Move[256];

        private static int[] scoreBuf = new int[256];
        private static int[] indexBuf = new int[256];

        internal const bool doDebugSimd = false;



		public static void SimdBitonicSort(Span<int> scores, Span<Move> moves, int pow2)
		{


            if (doDebugSimd)
            {
                Console.WriteLine("Before sort: ");
                for (int i = 0; i < scores.Length; i++)
                {   
                    Console.WriteLine($"{scores[i] % 100}-{MoveNotation.ToAlgebraicNotation(moves[i], moves)}");
                }
                Console.WriteLine("\n----");
            }
            
            int count = scores.Length;
            //Console.WriteLine("count at start " + count);
            if (!Avx2.IsSupported) throw new PlatformNotSupportedException();


            // 1) Prepare padded buffers
            Span<int> paddedScores = scoreBuf.AsSpan(0, pow2);
            Span<int> indices = indexBuf.AsSpan(0, pow2);

            indices.Clear();
            paddedScores.Clear();

            // Copy real scores
            scores.CopyTo(paddedScores);

            // Fill index buffer (0..count-1 for real, then pad)

            PrebuiltIndices.AsSpan(0, count).CopyTo(indices);

            // Scores beyond 'count' are left at 0 (default int)

            // 2) Break into Vector256 lanes
            int vecCount = pow2 / 8;

            // Cast the int spans to Vector256<int> spans
            Span<Vector256<int>> vS = MemoryMarshal.Cast<int, Vector256<int>>(paddedScores);
            Span<Vector256<int>> vI = MemoryMarshal.Cast<int, Vector256<int>>(indices);


            var vecScoreSpan = MemoryMarshal.Cast<int, Vector256<int>>(paddedScores);
            var vecIndexSpan = MemoryMarshal.Cast<int, Vector256<int>>(indices);

            for (int v = 0; v < vecCount; v++)
            {
                vS[v] = vecScoreSpan[v];
                vI[v] = vecIndexSpan[v];
            }
            if (doDebugSimd)
            {
                Console.WriteLine("Vecs: " + vecCount);
                Console.WriteLine($"Initial vectorized: ");
                for (int i = 0; i < vecCount; i++)
                {
                    for (int j = 0; j < 8; j++)
                    {
                        Console.Write($"{vS[i].GetElement(j),6} - {vI[i].GetElement(j),2}, ");
                    }
                    Console.Write("\n");
                }
                Console.WriteLine("\n----");
            }
            //Console.WriteLine("vecCount " + vecCount);

            //if only 1, sort single reversely
            if (vecCount == 1)
            {
                //Console.WriteLine("only 1 vec");
                SortSingle(ref vS[0], ref vI[0], ascending: false);
                for (int i = 0; i < count; i++)
                {
                    moveTmp[i] = moves[vI[0].GetElement(i)];
                    scores[i] = vS[0].GetElement(i);
                }
                moveTmp.AsSpan(0, count).CopyTo(moves);
                return;
            }

            //sort all vectors first, ascend, desc, etc
            for (int v = 0; v < vecCount; v += 2)
            {
                SortSingle(ref vS[v], ref vI[v], ascending: true);
                SortSingle(ref vS[v + 1], ref vI[v + 1], ascending: false);
            }
            if (doDebugSimd)
            {
                Console.WriteLine("initial sortSingle: ");
                for (int i = 0; i < vecCount; i++)
                {
                    for (int j = 0; j < 8; j++)
                    {
                        Console.Write($"{vS[i].GetElement(j),6} - {vI[i].GetElement(j),2}, ");
                    }
                    Console.Write("\n");
                }
                Console.WriteLine("\n----");
            }
            // less than, not equal, because last step needs to be ordered descending
            for (int size = 8; size < pow2 / 2 /*div 2*/; size *= 2)
            {
                for (int stride = size; stride >= 8; stride /= 2)
                {
                    //Console.WriteLine("did stride " + stride);
                    //does 8+ strides that are bigger than a vector
                    SortLarge(vS, vI, size, vecCount, stride);
                }
                bool ascendVec = false;

                for (int v = 0; v < vecCount; v++)
                {
                    //flips with vec stride, (so 4 vectors, with stride 8 or vec stride 1, flips every time, <><>, stride 16, flips every 2 vectors, <<>>)
                    //flips on v==0, to be true first, then flip for false for v==1
                    if ((v % (size / 4 )) == 0) /* div8 times 2, div by 4, to get vec stride times 2 because since they're merged, they need to be treated as pairs, so stride 8s, or vec stride1, needs to have intras be flipped every 2, instead of every 1*/
                    {
                        ascendVec = !ascendVec;
                    }
                    //does 4,2,1 strides
                    SortIntra(ref vS[v], ref vI[v], ascending: ascendVec);
                }
                if (doDebugSimd)
                {
                    Console.WriteLine($"after stride {size} merge: ");
                    for (int i = 0; i < vecCount; i++)
                    {
                        for (int j = 0; j < 8; j++)
                        {
                            Console.Write($"{vS[i].GetElement(j),6}-{vI[i].GetElement(j),2}, ");
                        }
                        Console.Write("\n");
                    }
                    Console.WriteLine("----");
                }
            }

            SortLargeFinal(vS, vI, pow2 >> 1 /*div by 2, bc the final stride is count/2, ex. 16 elem needs stride 8421 comparisons*/, vecCount); //final merge of all vectors, stride size == count/2
            for (int v = 0; v < vecCount; v++)
                SortIntra(ref vS[v], ref vI[v], ascending: false); //final reverse order sort

            if (doDebugSimd)
            {
                Console.WriteLine($"after stride {pow2 / 2} final merge: ");
                for (int i = 0; i < vecCount; i++)
                {
                    for (int j = 0; j < 8; j++)
                    {
                        Console.Write($"{vS[i].GetElement(j),6}-{vI[i].GetElement(j),2}, ");
                    }
                    Console.Write("\n");
                }
                Console.WriteLine("\n----");
            }

            Span<int> sortedIndices = indices.Slice(0, count);
            Span<int> sortedScores = paddedScores.Slice(0, count);

            // Copy scores back if you want them in original scores span
            sortedScores.CopyTo(scores);

            // Reorder moves
            for (int i = 0; i < count; i++)
                moveTmp[i] = moves[sortedIndices[i]];
            moveTmp.AsSpan(0, count).CopyTo(moves);

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
                    
                    //Console.WriteLine("Sort verification:");
                    for (int i = 0; i < scores.Length; i++)
                    {
                        Console.Write($"{scores[i],6} - {MoveNotation.ToAlgebraicNotation(moves[i], moves),-6} ");
                        if(i%8==7)
                            Console.WriteLine();
                    }
                    if (!sorted)
                        Environment.Exit(1);
                    
                }
            }
        }
        public static void SimdBitonicSort(Span<int> scores, Span<Move> moves)
		{
			if (doDebugSimd)
			{
				Console.WriteLine("Before sort: ");
				for (int i = 0; i < scores.Length; i++)
				{
					Console.WriteLine($"{scores[i]}-{MoveNotation.ToAlgebraicNotation(moves[i], moves)}");
				}
				Console.WriteLine("\n----");
            }
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
            if (doDebugSimd)
            {
                Console.WriteLine($"Vecs (2) : " + vecCount);
                Console.WriteLine(Environment.StackTrace);
                Console.WriteLine($"Initial merge: ");
                for (int i = 0; i < vecCount; i++)
                {
                    for (int j = 0; j < 8; j++)
                    {
                        Console.Write($"{vS[i].GetElement(j)%100}-{vI[i].GetElement(j)}, ");
                    }
                    Console.Write("\n");
                }
                Console.WriteLine("\n----");
            }
            //if only 1, sort single reversely
   //         if (vecCount == 1)
			//{
			//	SortSingle(ref vS[0], ref vI[0], ascending: false);
   //             for (int i = 0; i < count; i++)
   //                 moveTmp[i] = moves[vI[0].GetElement(i)];
   //             moveTmp.AsSpan(0, count).CopyTo(moves);
   //             return;
			//}

			//sort all vectors first, ascend, desc, etc
			for (int v = 0; v < vecCount; v += 2)
			{
				SortSingle(ref vS[v], ref vI[v], ascending: true);
				SortSingle(ref vS[v+1], ref vI[v+1], ascending: false);
				if(doDebugSimd)
				{
                    Console.WriteLine("initial intrasort: ");
                    for (int i = 0; i < vecCount; i++)
                    {
                        for (int j = 0; j < 8; j++)
						{
							Console.Write($"{vS[i].GetElement(j)%100}-{vI[i].GetElement(j)}, ");
                        }
						Console.Write("\n");
                    }
                    Console.WriteLine("\n----");
                }
				
			}
			
			// less than, not equal, because last step needs to be ordered descending
			for (int size = 8; size < count/2 /*div 2*/; size *= 2)
			{
				for (int stride = size; stride >= 8; stride /= 2)
				{
					//does 8+ strides that are bigger than a vector
					//SortLarge(ref vS, ref vI, stride, vecCount);
				}
				bool ascendVec = false;

                for (int v = 0; v < vecCount; v++)
				{
                    //flips with vec stride, (so 4 vectors, with stride 8 or vec stride 1, flips every time, <><>, stride 16, flips every 2 vectors, <<>>)
					//flips on v==0, to be true first, then flip for false for v==1
                    if ((v % (size >> 2 /*div by 4, to get vec stride times 2 because since they're merged, they need to be treated as pairs, so stride 8s, or vec stride1, needs to have intras be flipped every 2, instead of every 1*/)) == 0)
					{
						ascendVec = !ascendVec;
					}
					//does 4,2,1 strides
					SortIntra(ref vS[v], ref vI[v], ascending: ascendVec);
				}
                if (doDebugSimd)
                {
					Console.WriteLine($"after stride {size} merge: ");
                    for (int i = 0; i < vecCount; i++)
                    {
                        for (int j = 0; j < 8; j++)
                        {
                            Console.Write($"{vS[i].GetElement(j)%100}-{vI[i].GetElement(j)}, ");
                        }
                        Console.Write("\n");
                    }
					Console.WriteLine("----");
                }
            }

			//SortLargeFinal(ref vS, ref vI, count >>1 /*div by 2, bc the final stride is count/2, ex. 16 elem needs stride 8421 comparisons*/, vecCount); //final merge of all vectors, stride size == count/2
            for (int v = 0; v < vecCount; v++)
                SortIntra(ref vS[v], ref vI[v], ascending: false); //final reverse order sort
            
			if (doDebugSimd)
            {
                Console.WriteLine($"after stride {count/2} final merge: ");
                for (int i = 0; i < vecCount; i++)
                {
                    for (int j = 0; j < 8; j++)
                    {
                        Console.Write($"{vS[i].GetElement(j) % 100}-{vI[i].GetElement(j)}, ");
                    }
                    Console.Write("\n");
                }
                Console.WriteLine("\n----");
            }

            int[] flatIndices = new int[count];
            for (int v = 0; v < vecCount; v++)
            {
                var laneIdx = vI[v]; // Vector256<int>
				var laneScore = vS[v];
                for (int j = 0; j < 8; j++)
				{
                    flatIndices[v * 8 + j] = laneIdx.GetElement(j);
					scores[v * 8 + j] = laneScore.GetElement(j);
                }
            }

            // Then reorder moves
            for (int i = 0; i < count; i++)
                moveTmp[i] = moves[flatIndices[i]];

            moveTmp.AsSpan(0, count).CopyTo(moves);

        }

        //private static int[,] vecSwaps = new int[4, 16] 
        //{
        //	{1, 0, 3, 2, 5, 4, 7, 6, 9, 8, 11, 10, 13, 12, 15, 14}, //stride 2
        //	{2, 3, 0, 1, 6, 7, 4, 5, 10, 11, 8, 9, 14, 15, 12, 13}, //stride 4
        //	{4, 5, 6, 7, 0, 1, 2, 3, 12, 13, 14, 15, 8, 9, 10, 11}, //stride 8
        //	{8, 9, 10, 11, 12, 13, 14, 15, 0, 1, 2, 3, 4, 5, 6, 7} //stride 16
        //};

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        static void SortIntra(ref Vector256<int> Vector, ref Vector256<int> Index, bool ascending)
		{
			CompareSwap(ref Vector, ref Index, Perm4, ascending ? Mask4F : Mask4B); // stride 4 merge
			CompareSwap(ref Vector, ref Index, Perm2, ascending ? Mask2F : Mask2B); // stride 2 merge
			CompareSwap(ref Vector, ref Index, Perm1, ascending ? Mask1F : Mask1B); // stride 1 merge
		}
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        static void SortSingle(ref Vector256<int> Vector, ref Vector256<int> Index, bool ascending)
		{
			// Bitonic sort network for 8 lanes
			CompareSwap(ref Vector, ref Index, Perm1, Mask1); // step 1, all pairs of 2s are done
            //Console.WriteLine("1:");
            //for (int i = 0; i < 8; i++)
            //    Console.Write(Vector.GetElement(i) / 1000 + "-" + Index.GetElement(i) + " ");
            CompareSwap(ref Vector, ref Index, Perm2, Mask2); // step 2
            //Console.WriteLine("2:");
            //for (int i = 0; i < 8; i++)
            //    Console.Write(Vector.GetElement(i) / 1000 + "-" + Index.GetElement(i) + " ");
            CompareSwap(ref Vector, ref Index, Perm1, Mask1C); // step 3, after which all 4s are done
            //Console.WriteLine("3:");
            //for (int i = 0; i < 8; i++)
            //    Console.Write(Vector.GetElement(i)/1000 + "-" + Index.GetElement(i) + " ");
            CompareSwap(ref Vector, ref Index, Perm4, ascending ? Mask4F : Mask4B); // step 4
            //Console.WriteLine("4:");
            //for (int i = 0; i < 8; i++)
            //    Console.Write(Vector.GetElement(i)/1000 + "-" + Index.GetElement(i) + " ");
            CompareSwap(ref Vector, ref Index, Perm2, ascending ? Mask2F : Mask2B); // step 5
            //Console.WriteLine("5:");
            //for (int i = 0; i < 8; i++)
            //    Console.Write(Vector.GetElement(i)/1000 + "-" + Index.GetElement(i) + " ");
            CompareSwap(ref Vector, ref Index, Perm1, ascending ? Mask1F : Mask1B); // step 6, all 8s are done
            //Console.WriteLine("6:");
            //for (int i = 0; i < 8; i++)
            //    Console.Write(Vector.GetElement(i) / 1000 + "-" + Index.GetElement(i) + " ");
            //Console.WriteLine("\n----");
        }

		// Helper for the 8‑lane compare‑and‑swap
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		static void CompareSwap(ref Vector256<int> V, ref Vector256<int> I, Vector256<int> control, Vector256<int> dirMask)
		{
			var pV = Avx2.PermuteVar8x32(V, control);
			var pI = Avx2.PermuteVar8x32(I, control);

			var comparedVector = Avx2.CompareGreaterThan(V, pV);
            //if (doDebugSimd)
            //{
            //    Console.Write("\norig: \t\t\t");
            //    for (int i = 0; i < 8; i++)
            //        Console.Write(V.GetElement(i) + " ");

            //    Console.Write("\nperm: \t\t\t");
            //    for (int i = 0; i < 8; i++)
            //        Console.Write(pV.GetElement(i) + " ");

            //    Console.Write("\ncomparison vec: \t");
            //    for (int i = 0; i < 8; i++)
            //        Console.Write(-comparedVector.GetElement(i) + " ");
            //}

            var compEqual = Avx2.CompareEqual(V, pV);
            //if (doDebugSimd)
            //{
            //    Console.Write("\nCompEqual before: \t");
            //    for (int i = 0; i < 8; i++)
            //        Console.Write(-compEqual.GetElement(i) + " ");



            //    Console.Write("\nCompEqual after: \t");
            //    for (int i = 0; i < 8; i++)
            //        Console.Write(-compEqual.GetElement(i) + " ");
            //}

            //if (doDebugSimd)
            //{
            //    Console.Write("\ncomparison vec: \t");
            //    for (int i = 0; i < 8; i++)
            //        Console.Write(-comparedVector.GetElement(i) + " ");

            //    Console.Write("\ndirMask: \t\t");
            //    for (int i = 0; i < 8; i++)
            //        Console.Write(-dirMask.GetElement(i) + " ");
            //}
            var mask = Avx2.AndNot(compEqual, Avx2.Xor(comparedVector, dirMask));


            //if (doDebugSimd)
            //{
            //    Console.Write("\nafter Xor: \t\t");
            //    for (int i = 0; i < 8; i++)
            //        Console.Write(-mask.GetElement(i) + " ");
            //}
            // BlendVariable is expensive; only call if mask is not all zero

            //if (!Avx.TestZ(mask, mask))
            {
                V = Avx2.BlendVariable(V, pV, mask);
                I = Avx2.BlendVariable(I, pI, mask);
            }


            //Console.WriteLine();
        }

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		static void CompareSwapVec(	ref Vector256<int> Va, ref Vector256<int> Ia,
									ref Vector256<int> Vb, ref Vector256<int> Ib,
									bool ascending)
		{
            // if v1>v2, and ascending, swap / set element mask to FFFFFFFF
            // if v1>v2, and descending, do not swap

            // example:
            // Va:	0  9  7  10 6  1  12 9 
            // Vb:  11 15 5  14 3  2  4  8
            // cmp: 0  0  -1 0  -1 0  -1 0
            // asc: 0  0  -1 0  -1 0  -1 0
			//if desc, xor all -1, (bitflip)
            // desc: -1 -1 0 -1 0 -1 0 -1

            var cmp = Avx2.CompareGreaterThan(Va, Vb);

			var equals = Avx2.CompareEqual(Va, Vb);
            // generates -1s where Va>Vb, assuming ascending

            var mask = ascending ? cmp : Avx2.Xor(cmp, Vector256.Create(-1));
            mask = Avx2.AndNot(equals, mask); //do not swap if equal
                                              // if descending, invert mask, because we want to swap when va > vb


            var VectA = Avx2.BlendVariable(Va, Vb, mask);
			var VectB = Avx2.BlendVariable(Vb, Va, mask);

			var IdxA = Avx2.BlendVariable(Ia, Ib, mask);
			var IdxB = Avx2.BlendVariable(Ib, Ia, mask);

			Va = VectA;
			Ia = IdxA;
			Vb = VectB;
			Ib = IdxB;
		}


		static void SortLarge(Span<Vector256<int>> Vecs, Span<Vector256<int>> Idxs, int strideMerge, int vecCount, int substride)
		{
			int vecStride = strideMerge >> 3; //stride in vector counts, same as div by 8
            int vecSub = substride >> 3; //substride in vector counts, same as div by 8
            int iterMax = vecCount / 2;

			int vNum = 0;
			bool ascend = false;
            for (int iterCount = 0; iterCount < iterMax; iterCount++)
			{
                //Console.WriteLine($"Iter {iterCount}, vnum {vNum}, vecstride {vecStride}, vecsub {vecSub}");
                if (iterCount % vecStride  == 0)
				{
                    ascend = !ascend;   
				}
                if (iterCount != 0 && iterCount % vecSub == 0) //do not flip on first
                    vNum += vecSub; //skip vecStride vectors
                
                //Console.WriteLine($"Large compare {vNum} with {vNum + vecSub}, ascending {ascend}, iter {iterCount}, vnum {vNum}\n");
                
                CompareSwapVec(ref Vecs[vNum], ref Idxs[vNum], ref Vecs[vNum + vecSub], ref Idxs[vNum + vecSub], ascending: ascend);

                vNum++;
            }
            //Console.WriteLine("end sortlarge\n");

        }
        static void SortLargeFinal(Span<Vector256<int>>Vecs, Span<Vector256<int>> Idxs, int stride, int vecCount)
        {
            int vecStride = stride >> 3; //stride in vector counts, same as div by 8
            int iterTotal = vecCount / 2;

            int vNum;
            
            for (vNum=0; vecStride >= 1; vecStride /= 2)
            {
                for (int iterCount = 0; iterCount < iterTotal; iterCount++)
                {
                    if (iterCount % vecStride == 0)
                    {
                        if (iterCount != 0) //do not flip on first
                            vNum += vecStride; //skip vecStride vectors
                    }
                    //Console.WriteLine($"Final large compare {vNum} with {vNum + vecStride}, vecstride {vecStride}");
                    CompareSwapVec(ref Vecs[vNum], ref Idxs[vNum], ref Vecs[vNum + vecStride], ref Idxs[vNum + vecStride], ascending: false);
                    vNum++;

                }
                vNum = 0;
            }

        }
    }
}
