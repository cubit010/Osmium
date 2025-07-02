using System;

namespace ChessC_
{
    public static class Magics
    {
        // Typical relevant‐bits counts (used at init time only)
        private const int RookRelevantBits = 12;
        private const int BishopRelevantBits = 9;

        // Precomputed “magic” constants for 64 squares
        private static readonly ulong[] rookMagics = new ulong[64] {
    0x2F830803E83E7BB5UL,
    0x2AD019CBF97C0D06UL,
    0xFA67787C03D7AC28UL,
    0x2C944FE6278C94C0UL,
    0x1A2F2673CDFD09D1UL,
    0x799D22FE6A211B39UL,
    0x2A54CD41CCA54D0EUL,
    0x194D66BF3FA99C77UL,
    0xEB5EC1351C531192UL,
    0xD4F38F9CDC877B8FUL,
    0xE39D4F57E6E753F8UL,
    0xBE9009B02C28971FUL,
    0xC0506F229097EF8AUL,
    0x0320C05F04C7F1F4UL,
    0xFADD1045027AA08BUL,
    0x12F651F47093B101UL,
    0x9E7E1C5603FA924AUL,
    0x98D8EC311C8005BDUL,
    0xDEA81FDFDDFC48F9UL,
    0x6E72AE16033AFB00UL,
    0x7C122843E84CAD61UL,
    0xB82744812ECFCC29UL,
    0x8AB615E1BE57B673UL,
    0x2AC67ABE1748C452UL,
    0xB5772CC0EFC8E8B8UL,
    0x4AF717003C673686UL,
    0x22C63EA455CB102DUL,
    0x0D896774F862519FUL,
    0xC8E788E29E00F6CEUL,
    0xD32CA6EC679E8C11UL,
    0x3A3067D89782E1DCUL,
    0x072BCA0DC232D150UL,
    0x7CB037789FABA74BUL,
    0x2BABB04167C62F70UL,
    0x46680F2E62CFB49EUL,
    0x6CF7669D4ADFB43DUL,
    0xB798062EC24C5CAFUL,
    0x6A6ED1D6F8FBC590UL,
    0x1931C091A3F47F81UL,
    0x210C8D73D967AB58UL,
    0x40E7DAC70AB59C67UL,
    0xE62C81A3F581A71DUL,
    0x4FAA04CABD0193FEUL,
    0x74FD227FD0934269UL,
    0x66A03F6EDD82476EUL,
    0x1F287BD8B1645A5FUL,
    0x71D5A73DEE8B3490UL,
    0xB4D766A9B9043044UL,
    0x67CF917BB39D97ADUL,
    0x647C525B55340F58UL,
    0x95E59FB89081BDDBUL,
    0x4B66B27710EC4CCAUL,
    0xD11168A1B19D97CBUL,
    0x66382446403FF97FUL,
    0x8C760BC516A00E21UL,
    0x50C123B7D4B3E36AUL,
    0x3072C3909DD2D8E6UL,
    0x8F8C764EB26F589EUL,
    0x778FAF24B7E315FEUL,
    0x0D98971ED70B47F3UL,
    0xC64C20D7C0FA3542UL,
    0x43F379FFEA576AF6UL,
    0x9F2F9301F1374D9CUL,
    0x8961D19545820021UL,
};

        private static readonly ulong[] bishopMagics = new ulong[64] {
    0x0C0D282AC7C899AFUL,
    0x9BDFC4ACE58B91E5UL,
    0x4C6B2E77CDAFF844UL,
    0xAB4EC02BE2DFCC6DUL,
    0x91811E1B29B35CAEUL,
    0x374D8439D56B696FUL,
    0xB698FDF1BA760E24UL,
    0x6D8FCAE5D6ECB5E2UL,
    0xD4510538161B6248UL,
    0xB7BBB6D83BC2DCD0UL,
    0xA0B8CA43AE8E951AUL,
    0xA66050192629C6E6UL,
    0xC9AF16B96818CEB2UL,
    0xA4397A43446BDE34UL,
    0xF919BC9F8B145E87UL,
    0x871663CE73BCA6C8UL,
    0x9D5FCA5475ED2181UL,
    0xA96313489E88CF3EUL,
    0x9B89CEE776A72D8BUL,
    0x32F5F5F95944AAA8UL,
    0xDE31D7EFFD3E9668UL,
    0x2D6175D3D9C28920UL,
    0x25B5853424D32E4CUL,
    0xC459F39B57579822UL,
    0x6672FB64EA16A58CUL,
    0x844E0F01DA87DBEAUL,
    0xA0379DC97708E7F3UL,
    0x4525EE60A626EA0EUL,
    0x1F8FF05A7E611E36UL,
    0xB377C9BB03E94D1CUL,
    0x91D5F54D18E8CAE8UL,
    0x8E843044AB88BBB9UL,
    0x25707BC0AB4AD035UL,
    0xF33C9774D021AEEAUL,
    0x7BD8654E0DD9CC66UL,
    0x22316CAB5E12AD73UL,
    0xFEC534AE437B4A85UL,
    0x3A11113E08822D08UL,
    0x8CF6C8B6AA50F1DEUL,
    0x429F1D80A33B270CUL,
    0x9A9F738DCD9260B0UL,
    0x077D46D6B5FA83BDUL,
    0x865E262A56FB497EUL,
    0x47DA14B99CD4CE70UL,
    0x55568AE7D71BB146UL,
    0x6AF785C37D3C0118UL,
    0xDF92AB851B583482UL,
    0xC65BD937EEF124E8UL,
    0x76A3106D03791FB2UL,
    0xE83459165C7621E5UL,
    0x9251718CA33F80BEUL,
    0x6AD2AA3B9A560DA3UL,
    0x07FAC33B5F4DEB64UL,
    0x1C537C873A4F356DUL,
    0x161B062BA7A509D9UL,
    0xA7ADE076C42BD09EUL,
    0x6298DD14E83FB89BUL,
    0x677772F8AEE8512EUL,
    0xCB0EF2A49B526E15UL,
    0x6DA3EE27B586235DUL,
    0xAAC579773E511BE1UL,
    0x222CCB8DEC2DDAA6UL,
    0xDCD469093E07328AUL,
    0xBD90B45C05AA5BDAUL,
};

        // These masks hold “only the squares between the edge and the sliding piece,”
        // i.e. the relevant blockers for magic indexing.
        public static readonly ulong[] rookMasks = new ulong[64];
        public static readonly ulong[] bishopMasks = new ulong[64];

        // Precomputed shifts = (64 - #relevantBits)
        private static readonly int[] rookShifts = new int[64];
        private static readonly int[] bishopShifts = new int[64];

        // Attack tables: for each square, a sub‐array of size 2^(#relevantBits).
        private static readonly ulong[][] rookAttackTables = new ulong[64][];
        private static readonly ulong[][] bishopAttackTables = new ulong[64][];

        static Magics()
        {
            for (int sq = 0; sq < 64; sq++)
            {
                // 1) Build the “mask of relevant blockers” for rook & bishop
                rookMasks[sq] = ComputeRookMask(sq);
                bishopMasks[sq] = ComputeBishopMask(sq);

                // 2) Count how many bits are set in that mask
                int rookBits = CountBits(rookMasks[sq]);
                int bishopBits = CountBits(bishopMasks[sq]);

                // 3) Precompute shift = (64 - relevantBits)
                rookShifts[sq] = 64 - rookBits;
                bishopShifts[sq] = 64 - bishopBits;

                // 4) Allocate each attack table, with size = 2^relevantBits
                rookAttackTables[sq] = new ulong[1 << rookBits];
                bishopAttackTables[sq] = new ulong[1 << bishopBits];

                // 5) Fill in each table by enumerating every possible blocker subset
                InitRookAttackTable(sq);
                InitBishopAttackTable(sq);
            }
        }

        // -----------------------------------------------------------
        // Count bits in a 64‐bit word (classic “Brian Kernighan’s”)
        // -----------------------------------------------------------
        private static int CountBits(ulong bb)
        {
            int c = 0;
            while (bb != 0UL)
            {
                bb &= bb - 1;
                c++;
            }
            return c;
        }

        // -----------------------------------------------------------
        // Build “relevant blocker mask” for a rook on square s,
        // excluding the edge squares.  (We only need the inner rays.)
        // -----------------------------------------------------------
        private static ulong ComputeRookMask(int square)
        {
            ulong mask = 0UL;
            int r = square / 8;
            int f = square % 8;

            // Vertical up (r+1..6), vertical down (r-1..1)
            for (int rr = r + 1; rr <= 6; rr++) mask |= 1UL << (rr * 8 + f);
            for (int rr = r - 1; rr >= 1; rr--) mask |= 1UL << (rr * 8 + f);

            // Horizontal right (f+1..6), horizontal left (f-1..1)
            for (int ff = f + 1; ff <= 6; ff++) mask |= 1UL << (r * 8 + ff);
            for (int ff = f - 1; ff >= 1; ff--) mask |= 1UL << (r * 8 + ff);

            return mask;
        }

        // -----------------------------------------------------------
        // Build “relevant blocker mask” for a bishop on square s,
        // excluding edge squares (only interior diagonals).
        // -----------------------------------------------------------
        private static ulong ComputeBishopMask(int square)
        {
            ulong mask = 0UL;
            int r = square / 8;
            int f = square % 8;

            // Up-Right
            for (int rr = r + 1, ff = f + 1; rr <= 6 && ff <= 6; rr++, ff++)
                mask |= 1UL << (rr * 8 + ff);

            // Up-Left
            for (int rr = r + 1, ff = f - 1; rr <= 6 && ff >= 1; rr++, ff--)
                mask |= 1UL << (rr * 8 + ff);

            // Down-Right
            for (int rr = r - 1, ff = f + 1; rr >= 1 && ff <= 6; rr--, ff++)
                mask |= 1UL << (rr * 8 + ff);

            // Down-Left
            for (int rr = r - 1, ff = f - 1; rr >= 1 && ff >= 1; rr--, ff--)
                mask |= 1UL << (rr * 8 + ff);

            return mask;
        }

        // -----------------------------------------------------------
        // Enumerate every possible blocker subset for the “mask,”
        // returning an array of all 2^(#bits in mask) subsets.
        // -----------------------------------------------------------
        private static ulong[] GenerateBlockerSubsets(ulong mask)
        {
            int bitsCount = CountBits(mask);
            int subsetCount = 1 << bitsCount;
            var subsets = new ulong[subsetCount];

            for (int i = 0; i < subsetCount; i++)
            {
                ulong subset = 0UL;
                int bitIndex = 0;
                for (int sq = 0; sq < 64; sq++)
                {
                    if ((mask & (1UL << sq)) != 0)
                    {
                        if ((i & (1 << bitIndex)) != 0)
                            subset |= (1UL << sq);
                        bitIndex++;
                    }
                }
                subsets[i] = subset;
            }
            return subsets;
        }

        // -----------------------------------------------------------
        // On‐the‐fly compute rook attacks from “square” given a particular
        // blocker bitboard.  (This is only used to fill the table at init time.)
        // -----------------------------------------------------------
        private static ulong ComputeRookAttacks(int square, ulong blockers)
        {
            ulong attacks = 0UL;
            int r = square / 8;
            int f = square % 8;

            // 0 = up, 1 = down, 2 = right, 3 = left
            for (int dir = 0; dir < 4; dir++)
            {
                int rr = r, ff = f;
                while (true)
                {
                    switch (dir)
                    {
                        case 0: rr++; break; // up
                        case 1: rr--; break; // down
                        case 2: ff++; break; // right
                        case 3: ff--; break; // left
                    }
                    if (rr < 0 || rr > 7 || ff < 0 || ff > 7) break;
                    int sq = rr * 8 + ff;
                    attacks |= (1UL << sq);
                    if ((blockers & (1UL << sq)) != 0) break;
                }
            }
            return attacks;
        }

        // -----------------------------------------------------------
        // On‐the‐fly compute bishop attacks from “square” given a particular
        // blocker bitboard.  (This is only used to fill the table at init time.)
        // -----------------------------------------------------------
        private static ulong ComputeBishopAttacks(int square, ulong blockers)
        {
            ulong attacks = 0UL;
            int r = square / 8;
            int f = square % 8;

            // 0 = up-right, 1 = up-left, 2 = down-right, 3 = down-left
            for (int dir = 0; dir < 4; dir++)
            {
                int rr = r, ff = f;
                while (true)
                {
                    switch (dir)
                    {
                        case 0: rr++; ff++; break; // up-right
                        case 1: rr++; ff--; break; // up-left
                        case 2: rr--; ff++; break; // down-right
                        case 3: rr--; ff--; break; // down-left
                    }
                    if (rr < 0 || rr > 7 || ff < 0 || ff > 7) break;
                    int sq = rr * 8 + ff;
                    attacks |= (1UL << sq);
                    if ((blockers & (1UL << sq)) != 0) break;
                }
            }
            return attacks;
        }

        // -----------------------------------------------------------
        // Build the entire rook‐attack table for one square at init time.
        // -----------------------------------------------------------
        private static void InitRookAttackTable(int square)
        {
            ulong mask = rookMasks[square];
            int bits = CountBits(mask);
            int tableSize = 1 << bits;            // 2^bits
            var table = rookAttackTables[square];
            var subsets = GenerateBlockerSubsets(mask);

            for (int i = 0; i < tableSize; i++)
            {
                ulong blockers = subsets[i];
                // Compute the magic index in [0 .. tableSize-1]
                int index = (int)((blockers * rookMagics[square]) >> (64 - bits));
                table[index] = ComputeRookAttacks(square, blockers);
            }
        }

        // -----------------------------------------------------------
        // Build the entire bishop‐attack table for one square at init time.
        // -----------------------------------------------------------
        private static void InitBishopAttackTable(int square)
        {
            ulong mask = bishopMasks[square];
            int bits = CountBits(mask);
            int tableSize = 1 << bits;
            var table = bishopAttackTables[square];
            var subsets = GenerateBlockerSubsets(mask);

            for (int i = 0; i < tableSize; i++)
            {
                ulong blockers = subsets[i];
                int index = (int)((blockers * bishopMagics[square]) >> (64 - bits));
                table[index] = ComputeBishopAttacks(square, blockers);
            }
        }

        // -----------------------------------------------------------
        // PUBLIC: Get rook‐style attacks from “square” in O(1) time.
        // -----------------------------------------------------------
        public static ulong GetRookAttacks(int square, ulong blockers)
        {
            // 1) Restrict blockers to only the “relevant” squares
            blockers &= rookMasks[square];

            // 2) Multiply by magic, then right‐shift by precomputed shift
            int index = (int)((blockers * rookMagics[square]) >> rookShifts[square]);

            // 3) Index into the prebuilt table
            return rookAttackTables[square][index];
        }

        // -----------------------------------------------------------
        // PUBLIC: Get bishop‐style attacks from “square” in O(1) time.
        // -----------------------------------------------------------
        public static ulong GetBishopAttacks(int square, ulong blockers)
        {
            blockers &= bishopMasks[square];
            int index = (int)((blockers * bishopMagics[square]) >> bishopShifts[square]);
            return bishopAttackTables[square][index];
        }
    }
}
