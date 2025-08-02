public static class MoveTables
{
    public static readonly ulong[] KnightMoves = new ulong[64];
    public static readonly ulong[] KingMoves = new ulong[64];

    public static readonly ulong[,] PawnMoves = new ulong[2,64];
    // note: files only, use ifs
    public static readonly ulong[,] PawnDouble = new ulong[2,8];
    public static readonly ulong[,,] PawnCaptures = new ulong[2, 2, 64];



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

            PawnMoves[0, sq] = ComputeWPawnSingle(sq); // White pawn single move
            PawnMoves[1, sq] = ComputeBPawnSingle(sq); // Black pawn single move

            PawnCaptures[0, 0, sq] = ComputeWLPcaps(sq); // White pawn captures
            PawnCaptures[0, 1, sq] = ComputeWRPcaps(sq); // White pawn captures

            PawnCaptures[1, 0, sq] = ComputeBLPcaps(sq); // Black pawn captures
            PawnCaptures[1, 1, sq] = ComputeBRPcaps(sq); // Black pawn captures
        }
        for (int i = 0; i < 8; i++)
        {
            PawnDouble[0, i] = ComputeWPawnDouble( 8 + i); // White pawn double move
            PawnDouble[1, i] = ComputeBPawnDouble(48 + i); // Black pawn double move
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

    /* Board visualization

     A  B  C  D  E  F  G  H 

     0  1  2  3  4  5  6  7    1
     8  9  10 11 12 13 14 15   2
     16 17 18 19 20 21 22 23   3
     24 25 26 27 28 29 30 31   4
     32 33 34 35 36 37 38 39   5
     40 41 42 43 44 45 46 47   6
     48 49 50 51 52 53 54 55   7
     56 57 58 59 60 61 62 63   8

     */

    /* Board visualization

    A  B  C  D  E  F  G  H 

    64 65 66 67 68 69 70 71   

    56 57 58 59 60 61 62 63   8
    48 49 50 51 52 53 54 55   7
    40 41 42 43 44 45 46 47   6
    32 33 34 35 36 37 38 39   5
    24 25 26 27 28 29 30 31   4
    16 17 18 19 20 21 22 23   3
    8  9  10 11 12 13 14 15   2
    0  1  2  3  4  5  6  7    1

    */

    private static ulong ComputeWPawnSingle(int sq)
    {
        return 1UL << (sq + 8);
    }
    private static ulong ComputeWLPcaps(int sq)
    {
        if ((sq & 7) != 0) return 1UL << (7 + sq);
        else return 0UL; // Capture left, if not on file A
    }
    private static ulong ComputeWRPcaps(int sq)
    {
        if ((sq & 7) != 7) return 1UL << (9 + sq);
        else return 0UL; // Capture right, if not on file H
    }

    private static ulong ComputeWPawnDouble(int sq)
    {
       return 1UL << (sq + 16);
    }

    private static ulong ComputeBPawnSingle(int sq)
    {
        return 1UL << (sq - 8);
        
    }
    private static ulong ComputeBLPcaps(int sq)
    {
        if ((sq & 7) != 7) return 1UL << (sq - 7);
        else return 0UL; // Capture left, if not on file A
    }
    private static ulong ComputeBRPcaps(int sq)
    {
        if ((sq & 7) != 0) return 1UL << (sq - 9);
        else return 0UL; // Capture left, if not on file A
    }

    private static ulong ComputeBPawnDouble(int sq)
    {
        return 1UL << (sq - 16);
    }
}