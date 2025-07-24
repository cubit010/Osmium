using System;
using System.Numerics;

namespace ChessC_
{
    internal static class SpecialMoveGen
    {
        private const ulong FileA = 0x0101010101010101UL;
        private const ulong FileH = 0x8080808080808080UL;
        private const ulong Rank1 = 0x00000000000000FFUL;
        private const ulong Rank8 = 0xFF00000000000000UL;
        private const ulong Rank4 = 0x00000000FF000000;
        private const ulong Rank5 = 0x000000FF00000000;

        // Generate capture moves into span
        public static void GenerateCaptureMoves(Board board, Span<Move> moves, ref int count, bool isWhite)
        {

            ulong enemyOcc = board.occupancies[isWhite ? (int)Color.Black : (int)Color.White];
            ulong occ = board.occupancies[2] & ~board.bitboards[isWhite ? 11 : 5];
            enemyOcc &= ~board.bitboards[isWhite ? 11 : 5];

            ulong pawns = board.bitboards[(int)(isWhite ? Piece.WhitePawn : Piece.BlackPawn)];
            if (isWhite)
            {
                ulong leftCaps = (pawns & ~FileA) << 7 & enemyOcc;
                ulong rightCaps = (pawns & ~FileH) << 9 & enemyOcc;
                //ulong promoLeft = leftCaps & Rank8;
                //ulong promoRight = rightCaps & Rank8;
                ulong normalLeft = leftCaps & ~Rank8;
                ulong normalRight = rightCaps & ~Rank8;

                //foreach (int to in BitIter(promoLeft))
                //    foreach (Piece promo in new[] { Piece.WhiteQueen, Piece.WhiteRook, Piece.WhiteBishop, Piece.WhiteKnight })
                //        moves[count++] = new Move((Square)(to - 7), (Square)to, Piece.WhitePawn,
                //                                  MoveGen.FindCapturedPiece(board, (Square)to, true),
                //                                  MoveFlags.Promotion | MoveFlags.Capture, promo);

                //foreach (int to in BitIter(promoRight))
                //    foreach (Piece promo in new[] { Piece.WhiteQueen, Piece.WhiteRook, Piece.WhiteBishop, Piece.WhiteKnight })
                //        moves[count++] = new Move((Square)(to - 9), (Square)to, Piece.WhitePawn,
                //                                  MoveGen.FindCapturedPiece(board, (Square)to, true),
                //                                  MoveFlags.Promotion | MoveFlags.Capture, promo);

                foreach (int to in BitIter(normalLeft))
                    moves[count++] = new Move((Square)(to - 7), (Square)to, Piece.WhitePawn,
                                              MoveGen.FindCapturedPiece(board, (Square)to, true),
                                              MoveFlags.Capture);
                foreach (int to in BitIter(normalRight))
                    moves[count++] = new Move((Square)(to - 9), (Square)to, Piece.WhitePawn,
                                              MoveGen.FindCapturedPiece(board, (Square)to, true),
                                              MoveFlags.Capture);

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
                //ulong promoLeft = leftCaps & Rank1;
                //ulong promoRight = rightCaps & Rank1;
                ulong normalLeft = leftCaps & ~Rank1;
                ulong normalRight = rightCaps & ~Rank1;

                //foreach (int to in BitIter(promoLeft))
                //    foreach (Piece promo in new[] { Piece.BlackQueen, Piece.BlackRook, Piece.BlackBishop, Piece.BlackKnight })
                //        moves[count++] = new Move((Square)(to + 7), (Square)to, Piece.BlackPawn,
                //                                  MoveGen.FindCapturedPiece(board, (Square)to, false),
                //                                  MoveFlags.Promotion | MoveFlags.Capture, promo);

                //foreach (int to in BitIter(promoRight))
                //    foreach (Piece promo in new[] { Piece.BlackQueen, Piece.BlackRook, Piece.BlackBishop, Piece.BlackKnight })
                //        moves[count++] = new Move((Square)(to + 9), (Square)to, Piece.BlackPawn,
                //                                  MoveGen.FindCapturedPiece(board, (Square)to, false),
                //                                  MoveFlags.Promotion | MoveFlags.Capture, promo);

                foreach (int to in BitIter(normalLeft))
                    moves[count++] = new Move((Square)(to + 7), (Square)to, Piece.BlackPawn,
                                              MoveGen.FindCapturedPiece(board, (Square)to, false),
                                              MoveFlags.Capture);
                foreach (int to in BitIter(normalRight))
                    moves[count++] = new Move((Square)(to + 9), (Square)to, Piece.BlackPawn,
                                              MoveGen.FindCapturedPiece(board, (Square)to, false),
                                              MoveFlags.Capture);

                if (board.enPassantSquare != Square.None)
                {
                    int ep = (int)board.enPassantSquare;
                    ulong epBit = 1UL << ep;
                    ulong sources = (pawns & Rank4) & (((epBit >> 1) & ~FileH) | ((epBit << 1) & ~FileA));
                    foreach (int from in BitIter(sources))
                        moves[count++] = new Move((Square)from, (Square)ep, Piece.BlackPawn, Piece.WhitePawn, MoveFlags.EnPassant);
                }
            }

            ulong knights = board.bitboards[(int)(isWhite ? Piece.WhiteKnight : Piece.BlackKnight)];
            while (knights != 0)
            {
                int from = BitOperations.TrailingZeroCount(knights);
                knights &= knights - 1;
                ulong attacks = MoveTables.KnightMoves[from] & enemyOcc;
                foreach (int to in BitIter(attacks))
                    moves[count++] = new Move((Square)from, (Square)to,
                                              isWhite ? Piece.WhiteKnight : Piece.BlackKnight,
                                              MoveGen.FindCapturedPiece(board, (Square)to, isWhite),
                                              MoveFlags.Capture);
            }

            ulong king = board.bitboards[(int)(isWhite ? Piece.WhiteKing : Piece.BlackKing)];
            int kfrom = BitOperations.TrailingZeroCount(king);
            ulong kAtt = MoveTables.KingMoves[kfrom] & enemyOcc;
            foreach (int to in BitIter(kAtt))
                moves[count++] = new Move((Square)kfrom, (Square)to,
                                          isWhite ? Piece.WhiteKing : Piece.BlackKing,
                                          MoveGen.FindCapturedPiece(board, (Square)to, isWhite),
                                          MoveFlags.Capture);

            ulong diag = (board.bitboards[(int)(isWhite ? Piece.WhiteBishop : Piece.BlackBishop)] |
                          board.bitboards[(int)(isWhite ? Piece.WhiteQueen : Piece.BlackQueen)]);
            while (diag != 0)
            {
                int from = BitOperations.TrailingZeroCount(diag);
                diag &= diag - 1;
                ulong att = Magics.GetBishopAttacks(from, occ) & enemyOcc;
                foreach (int to in BitIter(att))
                    moves[count++] = new Move((Square)from, (Square)to,
                                              ((1UL << from) & board.bitboards[(int)(isWhite ? Piece.WhiteBishop : Piece.BlackBishop)]) != 0
                                                ? (isWhite ? Piece.WhiteBishop : Piece.BlackBishop)
                                                : (isWhite ? Piece.WhiteQueen : Piece.BlackQueen),
                                              MoveGen.FindCapturedPiece(board, (Square)to, isWhite),
                                              MoveFlags.Capture);
            }

            ulong ortho = (board.bitboards[(int)(isWhite ? Piece.WhiteRook : Piece.BlackRook)] |
                          board.bitboards[(int)(isWhite ? Piece.WhiteQueen : Piece.BlackQueen)]);
            while (ortho != 0)
            {
                int from = BitOperations.TrailingZeroCount(ortho);
                ortho &= ortho - 1;
                ulong att = Magics.GetRookAttacks(from, occ) & enemyOcc;
                foreach (int to in BitIter(att))
                    moves[count++] = new Move((Square)from, (Square)to,
                                              ((1UL << from) & board.bitboards[(int)(isWhite ? Piece.WhiteRook : Piece.BlackRook)]) != 0
                                                ? (isWhite ? Piece.WhiteRook : Piece.BlackRook)
                                                : (isWhite ? Piece.WhiteQueen : Piece.BlackQueen),
                                              MoveGen.FindCapturedPiece(board, (Square)to, isWhite),
                                              MoveFlags.Capture);
            }
        }

        // Generate promotion moves into span
        public static void GeneratePromotionMoves(Board board, Span<Move> moves, ref int count, bool isWhite)
        {
            ulong pawns = board.bitboards[(int)(isWhite ? Piece.WhitePawn : Piece.BlackPawn)];
            ulong occ = board.occupancies[2];
            ulong promoRank = isWhite ? Rank8 : Rank1;
            int fwd = isWhite ? 8 : -8;
            Piece[] promos = isWhite
                ? new[] { Piece.WhiteQueen, Piece.WhiteRook, Piece.WhiteBishop, Piece.WhiteKnight }
                : new[] { Piece.BlackQueen, Piece.BlackRook, Piece.BlackBishop, Piece.BlackKnight };

            ulong quiet = ((isWhite ? pawns << 8 : pawns >> 8) & promoRank) & ~occ;
            foreach (int to in BitIter(quiet))
            {
                int from = to - fwd;
                foreach (Piece p in promos)
                {
                    var mv = new Move((Square)from, (Square)to,
                                      isWhite ? Piece.WhitePawn : Piece.BlackPawn,
                                      Piece.None, MoveFlags.Promotion, p);
                    var undo = board.MakeSearchMove(board, mv);
                    bool inCheck = MoveGen.IsInCheck(board, isWhite);
                    board.UnmakeMove(mv, undo);
                    if (!inCheck)
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
                    Piece cap = MoveGen.FindCapturedPiece(board, (Square)to, isWhite);
                    if (cap == Piece.WhiteKing || cap == Piece.BlackKing) continue;
                    foreach (Piece p in promos)
                    {
                        var mv = new Move((Square)from, (Square)to,
                                          isWhite ? Piece.WhitePawn : Piece.BlackPawn,
                                          cap, MoveFlags.Promotion | MoveFlags.Capture, p);
                        var undo = board.MakeSearchMove(board, mv);
                        bool inCheck = MoveGen.IsInCheck(board, isWhite);
                        board.UnmakeMove(mv, undo);
                        if (!inCheck)
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
