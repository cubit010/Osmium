using System;
using System.Numerics;

namespace Osmium
{
    internal static class SpecialMoveGen
    {
        private const ulong FileA = 0x0101010101010101UL;
        private const ulong FileH = 0x8080808080808080UL;
        private const ulong Rank1 = 0x00000000000000FFUL;
        private const ulong Rank8 = 0xFF00000000000000UL;
        private const ulong Rank7 = 0x00FF000000000000UL;   
        private const ulong Rank2 = 0x000000000000FF00UL;
        private const ulong Rank4 = 0x00000000FF000000;
        private const ulong Rank5 = 0x000000FF00000000;

        // Generate capture moves into span
        public static void GenerateCaptureMoves(Board board, Span<Move> moves, ref int count, bool isWhite)
        {
            ulong enemyOcc = board.occupancies[isWhite ? (int)Color.Black : (int)Color.White];
            ulong occ = board.occupancies[2] & ~board.bitboards[isWhite ? 11 : 5];
            // Exclude enemy king from capture targets
            ulong enemyKing = board.bitboards[isWhite ? (int)Piece.BlackKing : (int)Piece.WhiteKing];
            enemyOcc &= ~enemyKing;

            ulong pawns = board.bitboards[(int)(isWhite ? Piece.WhitePawn : Piece.BlackPawn)];


            // Pawn captures (unchanged for clarity and correctness)
            if (isWhite)
            {
                ulong leftCaps = (pawns & ~FileA) << 7 & enemyOcc;
                ulong rightCaps = (pawns & ~FileH) << 9 & enemyOcc;

                ulong normalLeft = leftCaps & ~Rank8;
                ulong normalRight = rightCaps & ~Rank8;

                foreach (int to in BitIter(normalLeft))
                {
                    Piece captured = MoveGen.FindCapturedPiece(board, (Square)to);
                    if (captured == Piece.BlackKing) continue;
                    moves[count++] = new Move((Square)(to - 7), (Square)to, Piece.WhitePawn, captured, MoveFlags.Capture);
                }
                foreach (int to in BitIter(normalRight))
                {
                    Piece captured = MoveGen.FindCapturedPiece(board, (Square)to);
                    if (captured == Piece.BlackKing) continue;
                    moves[count++] = new Move((Square)(to - 9), (Square)to, Piece.WhitePawn, captured, MoveFlags.Capture);
                }

                if (board.enPassantSquare != Square.None)
                {
                    int ep = (int)board.enPassantSquare;
                    ulong epBit = 1UL << ep;
                    ulong sources = (pawns & Rank5) & (((epBit >> 1) & ~FileH) | ((epBit << 1) & ~FileA));
                    foreach (int from in BitIter(sources))
                        moves[count++] = new Move((Square)from, (Square)ep, Piece.WhitePawn, Piece.BlackPawn, MoveFlags.EnPassant);
                }
            }
            else
            {
                ulong leftCaps = (pawns & ~FileH) >> 7 & enemyOcc;
                ulong rightCaps = (pawns & ~FileA) >> 9 & enemyOcc;

                ulong normalLeft = leftCaps & ~Rank1;
                ulong normalRight = rightCaps & ~Rank1;

                foreach (int to in BitIter(normalLeft))
                {
                    Piece captured = MoveGen.FindCapturedPiece(board, (Square)to);
                    if (captured == Piece.WhiteKing) continue;
                    moves[count++] = new Move((Square)(to + 7), (Square)to, Piece.BlackPawn, captured, MoveFlags.Capture);
                }
                foreach (int to in BitIter(normalRight))
                {
                    Piece captured = MoveGen.FindCapturedPiece(board, (Square)to);
                    if (captured == Piece.WhiteKing) continue;
                    moves[count++] = new Move((Square)(to + 9), (Square)to, Piece.BlackPawn, captured, MoveFlags.Capture);
                }

                if (board.enPassantSquare != Square.None)
                {
                    int ep = (int)board.enPassantSquare;
                    ulong epBit = 1UL << ep;
                    ulong sources = (pawns & Rank4) & (((epBit >> 1) & ~FileH) | ((epBit << 1) & ~FileA));
                    foreach (int from in BitIter(sources))
                        moves[count++] = new Move((Square)from, (Square)ep, Piece.BlackPawn, Piece.WhitePawn, MoveFlags.EnPassant);
                }
            }

            // --- Optimization: Cache bitboards and avoid repeated calculations ---
            ulong knightBB = board.bitboards[(int)(isWhite ? Piece.WhiteKnight : Piece.BlackKnight)];
            ulong bishopBB = board.bitboards[(int)(isWhite ? Piece.WhiteBishop : Piece.BlackBishop)];
            ulong rookBB = board.bitboards[(int)(isWhite ? Piece.WhiteRook : Piece.BlackRook)];
            ulong queenBB = board.bitboards[(int)(isWhite ? Piece.WhiteQueen : Piece.BlackQueen)];
            ulong kingBB = board.bitboards[(int)(isWhite ? Piece.WhiteKing : Piece.BlackKing)];

            // Knights
            for (ulong knights = knightBB; knights != 0;)
            {
                int from = BitOperations.TrailingZeroCount(knights);
                knights &= knights - 1;
                ulong attacks = MoveTables.KnightMoves[from] & enemyOcc;
                // Unroll BitIter for performance
                for (ulong att = attacks; att != 0;)
                {
                    int to = BitOperations.TrailingZeroCount(att);
                    att &= att - 1;
                    Piece captured = MoveGen.FindCapturedPiece(board, (Square)to);
                    if (captured == (isWhite ? Piece.BlackKing : Piece.WhiteKing)) continue;
                    moves[count++] = new Move((Square)from, (Square)to,
                                              isWhite ? Piece.WhiteKnight : Piece.BlackKnight,
                                              captured,
                                              MoveFlags.Capture);
                }
            }

            // King
            int kfrom = BitOperations.TrailingZeroCount(kingBB);
            ulong kAtt = MoveTables.KingMoves[kfrom] & enemyOcc;
            for (ulong att = kAtt; att != 0;)
            {
                int to = BitOperations.TrailingZeroCount(att);
                att &= att - 1;
                Piece captured = MoveGen.FindCapturedPiece(board, (Square)to);
                if (captured == (isWhite ? Piece.BlackKing : Piece.WhiteKing)) continue;
                moves[count++] = new Move((Square)kfrom, (Square)to,
                                          isWhite ? Piece.WhiteKing : Piece.BlackKing,
                                          captured,
                                          MoveFlags.Capture);
            }

            // Bishops and Queens (diagonals)
            ulong diag = bishopBB | queenBB;
            for (ulong d = diag; d != 0;)
            {
                int from = BitOperations.TrailingZeroCount(d);
                d &= d - 1;
                // --- Optimization: Inline Magics.GetBishopAttacks if possible, or cache results if called repeatedly with same args ---
                ulong att = Magics.GetBishopAttacks(from, occ) & enemyOcc;
                for (ulong a = att; a != 0;)
                {
                    int to = BitOperations.TrailingZeroCount(a);
                    a &= a - 1;
                    Piece captured = MoveGen.FindCapturedPiece(board, (Square)to);
                    if (captured == (isWhite ? Piece.BlackKing : Piece.WhiteKing)) continue;
                    ref Move move = ref moves[count++];
                    move.From = (Square)from;
                    move.To = (Square)to;
                    move.PieceMoved = ((1UL << from) & bishopBB) != 0
                                        ? (isWhite ? Piece.WhiteBishop : Piece.BlackBishop)
                                        : (isWhite ? Piece.WhiteQueen : Piece.BlackQueen);
                    move.PieceCaptured = captured;
                    move.PromotionPiece = Piece.None;
                    move.Flags = MoveFlags.Capture;
                }
            }

            // Rooks and Queens (orthogonals)
            ulong ortho = rookBB | queenBB;
            for (ulong r = ortho; r != 0;)
            {
                int from = BitOperations.TrailingZeroCount(r);
                r &= r - 1;
                // --- Optimization: Inline Magics.GetRookAttacks if possible, or cache results if called repeatedly with same args ---
                ulong att = Magics.GetRookAttacks(from, occ) & enemyOcc;
                for (ulong a = att; a != 0;)
                {
                    int to = BitOperations.TrailingZeroCount(a);
                    a &= a - 1;
                    Piece captured = MoveGen.FindCapturedPiece(board, (Square)to);
                    if (captured == (isWhite ? Piece.BlackKing : Piece.WhiteKing)) continue;
                    ref Move move = ref moves[count++];
                    move.From = (Square)from;
                    move.To = (Square)to;
                    move.PieceMoved = ((1UL << from) & rookBB) != 0
                                        ? (isWhite ? Piece.WhiteRook : Piece.BlackRook)
                                        : (isWhite ? Piece.WhiteQueen : Piece.BlackQueen);
                    move.PieceCaptured = captured;
                    move.PromotionPiece = Piece.None;
                    move.Flags = MoveFlags.Capture;
                }
            }
        }

        // Generate promotion moves into span
        public static readonly Piece[] PromoW = { Piece.WhiteQueen, Piece.WhiteRook, Piece.WhiteBishop, Piece.WhiteKnight };
        public static readonly Piece[] PromoB = { Piece.BlackQueen, Piece.BlackRook, Piece.BlackBishop, Piece.BlackKnight};

        public static void GeneratePromotionMoves(Board board, Span<Move> moves, ref int count, bool isWhite)
        {
            ulong pawns = board.bitboards[(int)(isWhite ? Piece.WhitePawn : Piece.BlackPawn)];
            ulong occ = board.occupancies[2];
            ulong promoRank = isWhite ? Rank8 : Rank1;
            ulong promoFromRank = isWhite ? Rank7 : Rank2;
            int fwd = isWhite ? 8 : -8;
            Piece[] promos = isWhite
                ?ref PromoW
                :ref PromoB;

            ulong quiet = (pawns & promoFromRank) & ~occ;
            foreach (int to in BitIter(quiet))
            {
                int from = to - fwd;
                foreach (Piece p in promos)
                {

                    var mv = new Move((Square)from, (Square)to,
                                      isWhite ? Piece.WhitePawn : Piece.BlackPawn,
                                      Piece.None, MoveFlags.Promotion, p);

                    //unnessary check for in-check condition because during quiesec illegals are filtered anyways

                    //var undo = board.MakeSearchMove(board, mv);
                    //bool inCheck = MoveGen.IsInCheck(board, isWhite);
                    //board.UnmakeMove(mv, undo);
                    //if (!inCheck)
                    moves[count++] = mv;
                }
            }

            ulong enemyOcc = board.occupancies[isWhite ? (int)Color.Black : (int)Color.White] & ~board.bitboards[isWhite ? (int)Piece.BlackKing : (int)Piece.WhiteKing];
            int[] caps = isWhite ? new[] { 7, 9 } : new[] { -9, -7 };
            foreach (int shift in caps)
            {
                ulong capsBB = ((isWhite ? (pawns & ~(shift == 7 ? FileA : FileH)) << (shift == 7 ? 7 : 9) :
                                 (pawns & ~(shift == -9 ? FileH : FileA)) >> (shift == -9 ? 9 : 7))) & promoRank & enemyOcc;
                foreach (int to in BitIter(capsBB))
                {
                    int from = to - shift;
                    Piece cap = MoveGen.FindCapturedPiece(board, (Square)to);
                    if (cap == Piece.WhiteKing || cap == Piece.BlackKing) continue;
                    foreach (Piece p in promos)
                    {

                        var mv = new Move((Square)from, (Square)to,
                                          isWhite ? Piece.WhitePawn : Piece.BlackPawn,
                                          cap, MoveFlags.Promotion | MoveFlags.Capture, p);
                        //var undo = board.MakeSearchMove(board, mv);
                        //bool inCheck = MoveGen.IsInCheck(board, isWhite);
                        //board.UnmakeMove(mv, undo);
                        //if (!inCheck)
                        moves[count++] = mv;
                    }
                }
            }

        }

        private static IEnumerable<int> BitIter(ulong bb)
        {
            while (bb != 0)
            {
                int i = BitOperations.TrailingZeroCount(bb);
                yield return i;
                bb &= bb - 1;
            }
        }
    }
}
