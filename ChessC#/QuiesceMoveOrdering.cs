using ChessC_;

public static class QuiesceMoveOrdering
{
    public struct ScoredMove(Move move, int score)
    {
        public Move Move = move;
        public int Score = score;
    }
    private static ScoredMove[] scoredMoveBuffer = new ScoredMove[256];
    private const int CaptureBase = 100_000;
    private const int PromoBase = 90_000;
    private static readonly int[,] MVVLVA = new int[6, 6]
        {
            {   900,   700,   700,   500,   100,  -9000 },
            {  2900,  2700,  2700,  2500,  2100,  -7000 },
            {  2900,  2700,  2700,  2500,  2100,  -7000 },
            {  4900,  4700,  4700,  4500,  4100,  -5000 },
            {  8900,  8700,  8700,  8500,  8100,   -100 },
            { 99900, 99700, 99700, 99500, 99100,      0 }
        };
    /// <summary>
    /// Orders moves for quiescence: captures by descending MVV/LVA,
    /// then promotions by piece value, others unchanged.
    /// </summary>
    public static void OrderQuiesceMoves(Board board, Span<Move> moves)
    {
        int n = moves.Length;
        if (n <= 1) return;

        // Temporary buffer for scored moves
        Span<ScoredMove> buffer = new Span<ScoredMove>(scoredMoveBuffer);
        int count = 0;

        for (int i = 0; i < n; i++)
        {
            var mv = moves[i];
            int score;

            // Only captures and promotions get positive scores
            if (mv.PieceCaptured != Piece.None)
            {
                score = CaptureBase + MVVLVA[(int)mv.PieceMoved % 6, (int)mv.PieceCaptured % 6];
            }
            else if ((mv.Flags & MoveFlags.Promotion) != 0)
            {
                score = PromoBase + (int)mv.PromotionPiece * 100;
            }
            else
            {
                // Quiet moves: lowest priority, score zero
                score = 0;
            }

            buffer[count++] = new ScoredMove(mv, score);
        }

        // Sort scored moves descending by score
        buffer.Slice(0, count).Sort((a, b) => b.Score.CompareTo(a.Score));

        // Write back only the top count moves, leave the rest (if any) in original order
        for (int i = 0; i < count; i++)
        {
            moves[i] = buffer[i].Move;
        }
        // Optionally: leave moves[count..] as-is (quiet tail)
    }
}