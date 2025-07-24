using System;
using System.Runtime.CompilerServices;

namespace ChessC_
{
    public static class Magics
    {
        // Typical relevant‐bits counts (used at init time only)
        private const int RookRelevantBits = 12;
        private const int BishopRelevantBits = 9;

        // Precomputed “magic” constants for 64 squares
        private static readonly ulong[] rookMagics = new ulong[64]
                {
            0x8A80104000800020UL, 0x140002000100040UL, 0x2801880A0017001UL, 0x100081001000420UL,
            0x200020010080420UL, 0x3001C0002010008UL, 0x8480008002000100UL, 0x2080088004402900UL,
            0x800098204000UL,    0x2024401000200040UL, 0x100802000801000UL, 0x120800800801000UL,
            0x208808088000400UL, 0x2802200800400UL,    0x2200800100020080UL, 0x801000060821100UL,
            0x80044006422000UL,  0x100808020004000UL,  0x12108A0010204200UL, 0x140848010000802UL,
            0x481828014002800UL, 0x8094004002004100UL, 0x4010040010010802UL, 0x20008806104UL,
            0x100400080208000UL, 0x2040002120081000UL, 0x21200680100081UL,   0x20100080080080UL,
            0x2000A00200410UL,   0x20080800400UL,      0x80088400100102UL,  0x80004600042881UL,
            0x4040008040800020UL,0x440003000200801UL,  0x4200011004500UL,   0x188020010100100UL,
            0x14800401802800UL,  0x2080040080800200UL,0x124080204001001UL, 0x200046502000484UL,
            0x480400080088020UL, 0x1000422010034000UL,0x30200100110040UL,  0x100021010009UL,
            0x2002080100110004UL,0x202008004008002UL,  0x20020004010100UL,  0x2048440040820001UL,
            0x101002200408200UL, 0x40802000401080UL,   0x4008142004410100UL,0x2060820C0120200UL,
            0x1001004080100UL,   0x20C020080040080UL,  0x2935610830022400UL,0x44440041009200UL,
            0x280001040802101UL, 0x2100190040002085UL,0x80C0084100102001UL,0x4024081001000421UL,
            0x20030A0244872UL,   0x12001008414402UL,   0x2006104900A0804UL, 0x1004081002402UL
                };

        private static readonly ulong[] bishopMagics = new ulong[64]
        {
            0x40040844404084UL,  0x2004208A004208UL,  0x10190041080202UL,  0x108060845042010UL,
            0x581104180800210UL, 0x2112080446200010UL,0x1080820820060210UL,0x3C0808410220200UL,
            0x4050404440404UL,   0x21001420088UL,     0x24D0080801082102UL,0x1020A0A020400UL,
            0x40308200402UL,     0x4011002100800UL,   0x401484104104005UL, 0x801010402020200UL,
            0x400210C3880100UL,  0x404022024108200UL, 0x810018200204102UL, 0x4002801A02003UL,
            0x85040820080400UL,  0x810102C808880400UL,0xE900410884800UL,   0x8002020480840102UL,
            0x220200865090201UL, 0x2010100A02021202UL,0x152048408022401UL, 0x20080002081110UL,
            0x4001001021004000UL,0x800040400A011000UL,0xE4004081011002UL,  0x1C004001012080UL,
            0x8004200962A00220UL,0x8422100208500202UL,0x2000402200300C08UL,0x8646020080080080UL,
            0x80020A0200100808UL,0x2010004880111000UL,0x623000A080011400UL,0x42008C0340209202UL,
            0x209188240001000UL, 0x400408A884001800UL,0x110400A6080400UL,  0x1840060A44020800UL,
            0x90080104000041UL,  0x201011000808101UL, 0x1A2208080504F080UL,0x8012020600211212UL,
            0x500861011240000UL, 0x180806108200800UL, 0x4000020E01040044UL,0x300000261044000AUL,
            0x802241102020002UL, 0x20906061210001UL,  0x5A84841004010310UL,0x4010801011C04UL,
            0xA010109502200UL,   0x4A02012000UL,      0x500201010098B028UL,0x8040002811040900UL,
            0x28000010020204UL,  0x6000020202D0240UL, 0x8918844842082200UL,0x4010011029020020UL
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
