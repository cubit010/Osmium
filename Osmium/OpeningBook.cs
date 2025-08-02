using Osmium;

public static class OpeningBook
{
    private static Dictionary<ulong, List<Move>> book = new();

    // You can fill this manually or generate from PGN/FEN later
    static OpeningBook()
    {
        // Example: e4 (White) and e5 (Black)
        // Assume you already parsed and have the Zobrist keys for those positions
        // and the Move struct you want to play from each
        // ulong keyAfterStart = Zobrist hash after 1. e4
        // Move bestReply = new Move(Square.e7, Square.e5, Piece.BlackPawn);
        // book[keyAfterStart] = new List<Move> { bestReply };
    }

    public static bool TryGetBookMove(ulong zobristKey, out Move move)
    {
        if (book.TryGetValue(zobristKey, out var moveList) && moveList.Count > 0)
        {
            // Optionally add randomness here for variety
            move = moveList[0];
            return true;
        }

        move = default;
        return false;
    }

    public static void AddEntry(ulong key, Move move)
    {
        if (!book.ContainsKey(key))
            book[key] = new List<Move>();

        book[key].Add(move);
    }
}
