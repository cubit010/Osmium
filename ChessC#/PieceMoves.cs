using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace ChessC_
{
    internal class PieceMoves
    {
        // ─── file-rank‐masks ──────────────────────────────────────────────────────
        private const ulong FileA = 0x0101010101010101UL;
        private const ulong FileH = 0x8080808080808080UL;
        internal static ulong RookAttacks(
            int square,
            ulong friendly,
            ulong enemy,
            bool isWhite,
            Board board,
            bool isQueen,
            List<Move> moves
        )
        {
            ulong occupancy = board.occupancies[(int)Color.White] | board.occupancies[(int)Color.Black];
            ulong attacks = Magics.GetRookAttacks(square, occupancy);
            Piece movePiece = isWhite
                ? (isQueen ? Piece.WhiteQueen : Piece.WhiteRook)
                : (isQueen ? Piece.BlackQueen : Piece.BlackRook);

            ulong targets = attacks & ~friendly;
            while (targets != 0)
            {
                int to = BitOperations.TrailingZeroCount(targets);
                ulong toBB = 1UL << to;
                Piece capturedPiece = (toBB & enemy) != 0UL
                    ? MoveGen.FindCapturedPiece(board, (Square)to, isWhite)
                    : Piece.None;

                if (capturedPiece == Piece.WhiteKing || capturedPiece == Piece.BlackKing)
                {
                    targets &= ~toBB;
                    continue;
                }

                MoveFlags flag = capturedPiece != Piece.None ? MoveFlags.Capture : MoveFlags.None;
                moves.Add(new Move((Square)square, (Square)to, movePiece, capturedPiece, flag));
                targets &= ~toBB;
            }
            return attacks & ~friendly;
        }

        internal static ulong BishopAttacks(
            int square,
            ulong friendly,
            ulong enemy,
            bool isWhite,
            Board board,
            bool isQueen,
            List<Move> moves
        )
        {
            ulong occupancy = board.occupancies[(int)Color.White] | board.occupancies[(int)Color.Black];
            ulong attacks = Magics.GetBishopAttacks(square, occupancy);
            Piece movePiece = isWhite
                ? (isQueen ? Piece.WhiteQueen : Piece.WhiteBishop)
                : (isQueen ? Piece.BlackQueen : Piece.BlackBishop);

            ulong targets = attacks & ~friendly;
            while (targets != 0)
            {
                int to = BitOperations.TrailingZeroCount(targets);
                ulong toBB = 1UL << to;
                Piece capturedPiece = (toBB & enemy) != 0UL
                    ? MoveGen.FindCapturedPiece(board, (Square)to, isWhite)
                    : Piece.None;

                if (capturedPiece == Piece.WhiteKing || capturedPiece == Piece.BlackKing)
                {
                    targets &= ~toBB;
                    continue;
                }

                MoveFlags flag = capturedPiece != Piece.None ? MoveFlags.Capture : MoveFlags.None;
                moves.Add(new Move((Square)square, (Square)to, movePiece, capturedPiece, flag));
                targets &= ~toBB;
            }
            return attacks & ~friendly;
        }

        internal static ulong QueenAttacks(int square, ulong friendly, ulong enemy, bool isWhite, Board board, List<Move> moves)
        {
            return BishopAttacks(square, friendly, enemy, isWhite, board, true, moves) |
                   RookAttacks(square, friendly, enemy, isWhite, board, true, moves);
        }
        internal static void GenerateSliderMoves(Board board, List<Move> moves, bool isWhite)
        {
            ulong bishop, rook, queen, friendly, enemy;
            if (isWhite)
            {
                bishop = board.bitboards[(int)Piece.WhiteBishop];
                rook = board.bitboards[(int)Piece.WhiteRook];
                queen = board.bitboards[(int)Piece.WhiteQueen];
                friendly = board.occupancies[(int)Color.White];
                enemy = board.occupancies[(int)Color.Black];
            }
            else
            {
                bishop = board.bitboards[(int)Piece.BlackBishop];
                rook = board.bitboards[(int)Piece.BlackRook];
                queen = board.bitboards[(int)Piece.BlackQueen];
                friendly = board.occupancies[(int)Color.Black];
                enemy = board.occupancies[(int)Color.White];
            }

            // Generate bishop moves
            ulong b = bishop;
            while (b != 0)
            {
                int sq = BitOperations.TrailingZeroCount(b);
                BishopAttacks(sq, friendly, enemy, isWhite, board, false, moves);
                b &= b - 1;
            }

            // Generate rook moves
            ulong r = rook;
            while (r != 0)
            {
                int sq = BitOperations.TrailingZeroCount(r);
                RookAttacks(sq, friendly, enemy, isWhite, board, false, moves);
                r &= r - 1;
            }

            // Generate queen moves
            ulong q = queen;
            while (q != 0)
            {
                int sq = BitOperations.TrailingZeroCount(q);
                QueenAttacks(sq, friendly, enemy, isWhite, board, moves);
                q &= q - 1;
            }
        }
        internal static void GenerateKingMoves(Board board, List<Move> moves, bool isWhite)
        {
            ulong king;
            ulong friendly;
            Piece MovePieceK;
            if (isWhite)
            {
                king = board.bitboards[(int)Piece.WhiteKing];
                friendly = board.occupancies[(int)Color.White];
                MovePieceK = Piece.WhiteKing;
            }
            else
            {
                king = board.bitboards[(int)Piece.BlackKing];
                friendly = board.occupancies[(int)Color.Black];
                MovePieceK = Piece.BlackKing;
            }
            ulong bit = 1UL;
            for (int i = 0; i < 64; i++)
            {
                if ((bit & king) != 0)
                {
                    ulong attacks = MoveTables.KingMoves[i] & ~friendly;
                    for (int j = 0; j < 64; j++)
                    {
                        if ((attacks & (1UL << j)) != 0)
                        {
                            Piece capturedPiece = MoveGen.FindCapturedPiece(board, (Square)j, isWhite);
                            if (capturedPiece == Piece.WhiteKing || capturedPiece == Piece.BlackKing)
                            {
                                break;
                            }
                            MoveFlags flag = capturedPiece != Piece.None ? MoveFlags.Capture : MoveFlags.None;
                            Move move = new((Square)i, (Square)j, MovePieceK, capturedPiece, flag);
                            moves.Add(move);
                        }
                    }
                    break;
                }
                bit <<= 1;
            }
        }
        internal static void GenerateKnightMoves(Board board, List<Move> moves, bool isWhite)
        {
            ulong knights;
            ulong friendly;
            Piece MovePieceN;

            if (isWhite)
            {
                knights = board.bitboards[(int)Piece.WhiteKnight];
                friendly = board.occupancies[(int)Color.White];
                MovePieceN = Piece.WhiteKnight;
            }
            else
            {
                knights = board.bitboards[(int)Piece.BlackKnight];
                friendly = board.occupancies[(int)Color.Black];
                MovePieceN = Piece.BlackKnight;
            }
            ulong bit = 1UL;
            for (int i = 0; i < 64; i++)
            {
                if ((bit & knights) != 0)
                {
                    ulong attacks = MoveTables.KnightMoves[i] & ~friendly;
                    for (int j = 0; j < 64; j++)
                    {
                        if ((attacks & (1UL << j)) != 0)
                        {
                            Piece capturedPiece = MoveGen.FindCapturedPiece(board, (Square)j, isWhite);
                            if (capturedPiece == Piece.WhiteKing || capturedPiece == Piece.BlackKing)
                            {
                                break;
                            }
                            Move move = new((Square)i, (Square)j, MovePieceN, capturedPiece);
                            moves.Add(move);
                        }
                    }
                }
                bit <<= 1;
            }
        }
        internal static void GeneratePawnMoves(Board board, List<Move> moves, bool isWhite)
        {
            if (board.enPassantSquare != Square.None) EnPassantAdd(board, moves, isWhite);

            ulong pawns, enemy, friendly;
            if (isWhite)
            {
                pawns = board.bitboards[(int)Piece.WhitePawn];
                enemy = board.occupancies[(int)Color.Black];
                friendly = board.occupancies[(int)Color.White];
                GenerateWhitePawn(pawns, enemy, friendly, moves, board);
            }
            else
            {
                pawns = board.bitboards[(int)Piece.BlackPawn];
                enemy = board.occupancies[(int)Color.White];
                friendly = board.occupancies[(int)Color.Black];
                GenerateBlackPawn(pawns, enemy, friendly, moves, board);
            }
        }
        internal static void GenerateWhitePawn(ulong pawns, ulong blackEnemy, ulong whiteFriendly, List<Move> moves, Board board)
        {
            ulong bit = 1UL;
            for (int i = 0; i < 64; i++)
            {
                if ((bit & pawns) != 0)
                {
                    WPawnAtks(i, blackEnemy, whiteFriendly, moves, board);
                }
                bit <<= 1;
            }
        }
        internal static void GenerateBlackPawn(ulong pawns, ulong whiteEnemy, ulong blackFriendly, List<Move> moves, Board board)
        {
            ulong bit = 1UL;
            for (int i = 0; i < 64; i++)
            {
                if ((bit & pawns) != 0)
                {
                    BPawnAtks(i, whiteEnemy, blackFriendly, moves, board);
                }
                bit <<= 1;
            }
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

        //from, to, pieceFrom, piece capture, flag, promotion
        //precondition: checks if board.enPassantSquare is set before execution, if none, don't execute
        private static void EnPassantAdd(Board board, List<Move> moves, bool isWhite)
        {
            if (isWhite)
            {
                WEnPassant(board, moves);
            }
            else
            {
                BEnPassant(board, moves);
            }
        }
        private static void WEnPassant(Board board, List<Move> moves)
        {

            int enPassantSq = (int)board.enPassantSquare;
            int file = enPassantSq % 8;
            if (file > 0)
            {
                // not on file A
                // ->
                //left to center en passant, shift right / down a digit
                if ((board.bitboards[(int)Piece.WhitePawn] & 1UL << (enPassantSq - 9)) != 0)
                {
                    moves.Add(new((Square)(enPassantSq - 9), (Square)(enPassantSq), Piece.WhitePawn, Piece.BlackPawn, MoveFlags.EnPassant));
                }
            }
            if (file < 7)
            {
                // not on file H
                // <-
                //right to center en passant, shift left / up a digit to find if there's a pawn on right side
                if ((board.bitboards[(int)Piece.WhitePawn] & 1UL << (enPassantSq + 7)) != 0)
                {
                    moves.Add(new((Square)(enPassantSq + 7), (Square)(enPassantSq), Piece.WhitePawn, Piece.BlackPawn, MoveFlags.EnPassant));
                }
            }

        }
        private static void BEnPassant(Board board, List<Move> moves)
        {
            int enPassantSq = (int)board.enPassantSquare;
            int file = enPassantSq % 8;
            if (file > 0)
            {
                // not on file A
                // left to center en passant, shift right / up a digit
                if ((board.bitboards[(int)Piece.BlackPawn] & (1UL << (enPassantSq + 7))) != 0)
                {
                    moves.Add(new((Square)(enPassantSq + 7), (Square)(enPassantSq), Piece.BlackPawn, Piece.WhitePawn, MoveFlags.EnPassant));
                }
            }
            if (file < 7)
            {
                // not on file H
                // right to center en passant, shift left / down a digit
                if ((board.bitboards[(int)Piece.BlackPawn] & (1UL << (enPassantSq + 9))) != 0)
                {
                    moves.Add(new((Square)(enPassantSq + 9), (Square)(enPassantSq), Piece.BlackPawn, Piece.WhitePawn, MoveFlags.EnPassant));
                }
            }
        }
        private static void WPawnAtks(int sq, ulong enemy, ulong friendly, List<Move> moves, Board board)
        {
            bool on7th = sq >= 48 && sq <= 55; // white
            bool on2nd = sq >= 8 && sq <= 15;  // white

            if (on7th && (((1UL << (sq + 8)) & (enemy | friendly)) == 0))
            {
                //promotion push
                moves.Add(new((Square)sq, (Square)(sq + 8), Piece.WhitePawn, Piece.None, MoveFlags.Promotion, Piece.WhiteQueen));
                moves.Add(new((Square)sq, (Square)(sq + 8), Piece.WhitePawn, Piece.None, MoveFlags.Promotion, Piece.WhiteRook));
                moves.Add(new((Square)sq, (Square)(sq + 8), Piece.WhitePawn, Piece.None, MoveFlags.Promotion, Piece.WhiteKnight));
                moves.Add(new((Square)sq, (Square)(sq + 8), Piece.WhitePawn, Piece.None, MoveFlags.Promotion, Piece.WhiteBishop));
            }
            else if (((1UL << (sq + 8)) & (enemy | friendly)) == 0)
            {
                //single push
                moves.Add(new((Square)sq, (Square)(sq + 8), Piece.WhitePawn));

                if (on2nd && (((1UL << (sq + 16)) & (enemy | friendly)) == 0))
                {
                    //double push
                    moves.Add(new((Square)sq, (Square)(sq + 16), Piece.WhitePawn));
                }
            }
            if (((1UL << sq) & FileH) == 0 && ((1UL << (sq + 9)) & enemy) != 0)
            {
                Piece tookPiece = MoveGen.FindCapturedPiece(board, (Square)(sq + 9), true);
                if (tookPiece == Piece.WhiteKing || tookPiece == Piece.BlackKing)
                {
                    return;
                }
                if (on7th)
                {
                    //take promote ->

                    moves.Add(new((Square)sq, (Square)(sq + 9), Piece.WhitePawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.WhiteQueen));
                    moves.Add(new((Square)sq, (Square)(sq + 9), Piece.WhitePawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.WhiteRook));
                    moves.Add(new((Square)sq, (Square)(sq + 9), Piece.WhitePawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.WhiteKnight));
                    moves.Add(new((Square)sq, (Square)(sq + 9), Piece.WhitePawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.WhiteBishop));
                }
                else
                {
                    //take ->
                    moves.Add(new((Square)sq, (Square)(sq + 9), Piece.WhitePawn, tookPiece));
                }
            }
            if (((1UL << sq) & FileA) == 0 && ((1UL << (sq + 7)) & enemy) != 0)
            {
                Piece tookPiece = MoveGen.FindCapturedPiece(board, (Square)(sq + 7), true);
                if (tookPiece == Piece.WhiteKing || tookPiece == Piece.BlackKing)
                {
                    return;
                }
                if (on7th)
                {
                    //take promote <-
                    moves.Add(new((Square)sq, (Square)(sq + 7), Piece.WhitePawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.WhiteQueen));
                    moves.Add(new((Square)sq, (Square)(sq + 7), Piece.WhitePawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.WhiteRook));
                    moves.Add(new((Square)sq, (Square)(sq + 7), Piece.WhitePawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.WhiteKnight));
                    moves.Add(new((Square)sq, (Square)(sq + 7), Piece.WhitePawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.WhiteBishop));
                }
                else
                {
                    //take <-
                    moves.Add(new((Square)sq, (Square)(sq + 7), Piece.WhitePawn, tookPiece));
                }
            }

        }
        private static void BPawnAtks(int sq, ulong enemy, ulong friendly, List<Move> moves, Board board)
        {
            bool on2nd = sq >= 8 && sq <= 15;   // black
            bool on7th = sq >= 48 && sq <= 55;  // black

            // Promotion push
            if (on2nd && (((1UL << (sq - 8)) & (enemy | friendly)) == 0))
            {
                moves.Add(new((Square)sq, (Square)(sq - 8), Piece.BlackPawn, Piece.None, MoveFlags.Promotion, Piece.BlackQueen));
                moves.Add(new((Square)sq, (Square)(sq - 8), Piece.BlackPawn, Piece.None, MoveFlags.Promotion, Piece.BlackRook));
                moves.Add(new((Square)sq, (Square)(sq - 8), Piece.BlackPawn, Piece.None, MoveFlags.Promotion, Piece.BlackKnight));
                moves.Add(new((Square)sq, (Square)(sq - 8), Piece.BlackPawn, Piece.None, MoveFlags.Promotion, Piece.BlackBishop));
            }

            // Single push
            else if (((1UL << (sq - 8)) & (enemy | friendly)) == 0)
            {
                moves.Add(new((Square)sq, (Square)(sq - 8), Piece.BlackPawn));

                // Double push
                if (on7th && (((1UL << (sq - 16)) & (enemy | friendly)) == 0))
                {
                    moves.Add(new((Square)sq, (Square)(sq - 16), Piece.BlackPawn));
                }
            }

            // Capture right (->)
            if (((1UL << sq) & FileH) == 0 && ((1UL << (sq - 7)) & enemy) != 0)
            {
                Piece tookPiece = MoveGen.FindCapturedPiece(board, (Square)(sq - 7), false);
                if (tookPiece == Piece.WhiteKing || tookPiece == Piece.BlackKing)
                {
                    return;
                }
                if (on2nd)
                {
                    // take promote ->
                    moves.Add(new((Square)sq, (Square)(sq - 7), Piece.BlackPawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.BlackQueen));
                    moves.Add(new((Square)sq, (Square)(sq - 7), Piece.BlackPawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.BlackRook));
                    moves.Add(new((Square)sq, (Square)(sq - 7), Piece.BlackPawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.BlackKnight));
                    moves.Add(new((Square)sq, (Square)(sq - 7), Piece.BlackPawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.BlackBishop));
                }
                else
                {
                    // take ->
                    moves.Add(new((Square)sq, (Square)(sq - 7), Piece.BlackPawn, tookPiece));
                }
            }

            // Capture left (<-)
            if (((1UL << sq) & FileA) == 0 && ((1UL << (sq - 9)) & enemy) != 0)
            {
                Piece tookPiece = MoveGen.FindCapturedPiece(board, (Square)(sq - 9), false);
                if (tookPiece == Piece.WhiteKing || tookPiece == Piece.BlackKing)
                {
                    return;
                }
                if (on2nd)
                {
                    // take promote <-
                    moves.Add(new((Square)sq, (Square)(sq - 9), Piece.BlackPawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.BlackQueen));
                    moves.Add(new((Square)sq, (Square)(sq - 9), Piece.BlackPawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.BlackRook));
                    moves.Add(new((Square)sq, (Square)(sq - 9), Piece.BlackPawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.BlackKnight));
                    moves.Add(new((Square)sq, (Square)(sq - 9), Piece.BlackPawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.BlackBishop));
                }
                else
                {
                    // take <-
                    moves.Add(new((Square)sq, (Square)(sq - 9), Piece.BlackPawn, tookPiece));
                }
            }
        }
    }
}
