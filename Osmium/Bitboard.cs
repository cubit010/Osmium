using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Osmium
{
    public class Bitboard
    {
        public static readonly ulong[,] BetweenBB = new ulong[64, 64];

        static Bitboard()
        {
            for (int from = 0; from < 64; from++)
            {
                for (int to = 0; to < 64; to++)
                {
                    BetweenBB[from, to] = ComputeBetween(from, to);
                }
            }
        }

        private static ulong ComputeBetween(int from, int to)
        {
            if (from == to) return 0;

            int fromRank = from / 8, fromFile = from % 8;
            int toRank = to / 8, toFile = to % 8;

            int rankDiff = toRank - fromRank;
            int fileDiff = toFile - fromFile;

            // Determine direction
            int dr = Math.Sign(rankDiff);
            int df = Math.Sign(fileDiff);

            // Only continue if on same rank, file, or diagonal
            if (!(rankDiff == 0 || fileDiff == 0 || Math.Abs(rankDiff) == Math.Abs(fileDiff)))
                return 0;

            ulong mask = 0;
            int sq = from + dr * 8 + df;

            while (sq != to)
            {
                //if (sq < 0 || sq >= 64) break; // safety check
                mask |= 1UL << sq;
                sq += dr * 8 + df;
            }

            return mask;
        }


        //public static int PopCount(ulong bb)
        //{
        //    return System.Numerics.BitOperations.PopCount(bb);
        //}
        public static string Pretty(ulong bb)
        {
            var sb = new StringBuilder();
            for (int r = 7; r >= 0; r--)
            {
                for (int f = 0; f < 8; f++)
                {
                    int sq = r * 8 + f;
                    sb.Append(((bb >> sq) & 1UL) != 0 ? "1 " : ". ");
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
