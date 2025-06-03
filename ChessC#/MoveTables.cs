public static class MoveTables
{
    public static readonly ulong[] KnightMoves = new ulong[64];
    public static readonly ulong[] KingMoves = new ulong[64];

    // File bitboards to help compute moves (you must have these somewhere, example below)
    private static readonly ulong FileA = 0x0101010101010101UL;
    private static readonly ulong FileB = FileA << 1;
    private static readonly ulong FileG = FileA << 6;
    private static readonly ulong FileH = FileA << 7;
    private static readonly ulong FileAB = FileA | FileB;
    private static readonly ulong FileGH = FileG | FileH;

    static MoveTables()
    {
        for (int sq = 0; sq < 64; sq++)
        {
            KnightMoves[sq] = ComputeKnightAttacks(sq);
            KingMoves[sq] = ComputeKingAttacks(sq);
        }
    }

    private static ulong ComputeKnightAttacks(int sq)
    {
        ulong bb = 1UL << sq;
        ulong attacks = 0UL;

        if ((bb >> 17 & ~FileH) != 0) attacks |= bb >> 17;
        if ((bb >> 15 & ~FileA) != 0) attacks |= bb >> 15;
        if ((bb >> 10 & ~FileGH) != 0) attacks |= bb >> 10;
        if ((bb >> 6 & ~FileAB) != 0) attacks |= bb >> 6;
        if ((bb << 17 & ~FileA) != 0) attacks |= bb << 17;
        if ((bb << 15 & ~FileH) != 0) attacks |= bb << 15;
        if ((bb << 10 & ~FileAB) != 0) attacks |= bb << 10;
        if ((bb << 6 & ~FileGH) != 0) attacks |= bb << 6;

        return attacks;
    }

    private static ulong ComputeKingAttacks(int sq)
    {
        ulong bb = 1UL << sq;
        ulong attacks = 0UL;

        if ((bb >> 8) != 0) attacks |= bb >> 8;
        if ((bb << 8) != 0) attacks |= bb << 8;
        if ((bb >> 1 & ~FileH) != 0) attacks |= bb >> 1;
        if ((bb << 1 & ~FileA) != 0) attacks |= bb << 1;
        if ((bb >> 9 & ~FileH) != 0) attacks |= bb >> 9;
        if ((bb >> 7 & ~FileA) != 0) attacks |= bb >> 7;
        if ((bb << 7 & ~FileH) != 0) attacks |= bb << 7;
        if ((bb << 9 & ~FileA) != 0) attacks |= bb << 9;

        return attacks;
    }
}