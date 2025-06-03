using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessC_
{
    internal class Bitboard
    {
        //unused for now
        public static ulong SetBit(ulong bb, int square) => bb | (1UL << square);
        public static ulong ClearBit(ulong bb, int square) => bb & ~(1UL << square);
        public static bool GetBit(ulong bb, int square) => ((bb >> square) & 1UL) != 0;
        public static int PopCount(ulong bb)
        {
            return System.Numerics.BitOperations.PopCount(bb);
        }
    }
}
