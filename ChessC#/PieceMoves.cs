using System;
using System.Numerics;

namespace ChessC_
{
    internal static class PieceMoves
    {
        // ─── file / rank bit-masks ───────────────────────────────────────────────
        private const ulong FileA = 0x0101010101010101UL;
        private const ulong FileH = 0x8080808080808080UL;

        // ─── rook / bishop / queen ­helpers ──────────────────────────────────────
        internal static ulong RookAttacks(
            int square,
            ulong friendly,
            ulong enemy,
            bool isWhite,
            Board board,
            bool isQueen,
            Span<Move> moves,
            ref int count)
        {
            ulong occupancy = board.occupancies[(int)Color.White] | board.occupancies[(int)Color.Black];
            ulong attacks   = Magics.GetRookAttacks(square, occupancy);

            Piece movePiece = isWhite
                ? (isQueen ? Piece.WhiteQueen : Piece.WhiteRook)
                : (isQueen ? Piece.BlackQueen : Piece.BlackRook);

            ulong targets = attacks & ~friendly;
            while (targets != 0)
            {
                int  to   = BitOperations.TrailingZeroCount(targets);
                ulong toB = 1UL << to;

                Piece captured = (toB & enemy) != 0
                    ? MoveGen.FindCapturedPiece(board, (Square)to, isWhite)
                    : Piece.None;

                if (captured is Piece.WhiteKing or Piece.BlackKing)
                {
                    targets &= ~toB;
                    continue;
                }

                MoveFlags flag = captured != Piece.None ? MoveFlags.Capture : MoveFlags.None;
                moves[count++] = new Move((Square)square, (Square)to, movePiece, captured, flag);

                targets &= ~toB;
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
            Span<Move> moves,
            ref int count)
        {
            ulong occupancy = board.occupancies[(int)Color.White] | board.occupancies[(int)Color.Black];
            ulong attacks   = Magics.GetBishopAttacks(square, occupancy);

            Piece movePiece = isWhite
                ? (isQueen ? Piece.WhiteQueen : Piece.WhiteBishop)
                : (isQueen ? Piece.BlackQueen : Piece.BlackBishop);

            ulong targets = attacks & ~friendly;
            while (targets != 0)
            {
                int  to   = BitOperations.TrailingZeroCount(targets);
                ulong toB = 1UL << to;

                Piece captured = (toB & enemy) != 0
                    ? MoveGen.FindCapturedPiece(board, (Square)to, isWhite)
                    : Piece.None;

                if (captured is Piece.WhiteKing or Piece.BlackKing)
                {
                    targets &= ~toB;
                    continue;
                }

                MoveFlags flag = captured != Piece.None ? MoveFlags.Capture : MoveFlags.None;
                moves[count++] = new Move((Square)square, (Square)to, movePiece, captured, flag);

                targets &= ~toB;
            }

            return attacks & ~friendly;
        }

        internal static ulong QueenAttacks(
            int square,
            ulong friendly,
            ulong enemy,
            bool isWhite,
            Board board,
            Span<Move> moves,
            ref int count) =>
            BishopAttacks(square, friendly, enemy, isWhite, board, true, moves, ref count) |
            RookAttacks  (square, friendly, enemy, isWhite, board, true, moves, ref count);

        // ─── generic generators ──────────────────────────────────────────────────
        internal static void GenerateSliderMoves(Board board, Span<Move> moves, ref int count, bool isWhite)
        {
            ulong bishop, rook, queen, friendly, enemy;
            if (isWhite)
            {
                bishop   = board.bitboards[(int)Piece.WhiteBishop];
                rook     = board.bitboards[(int)Piece.WhiteRook];
                queen    = board.bitboards[(int)Piece.WhiteQueen];
                friendly = board.occupancies[(int)Color.White];
                enemy    = board.occupancies[(int)Color.Black];
            }
            else
            {
                bishop   = board.bitboards[(int)Piece.BlackBishop];
                rook     = board.bitboards[(int)Piece.BlackRook];
                queen    = board.bitboards[(int)Piece.BlackQueen];
                friendly = board.occupancies[(int)Color.Black];
                enemy    = board.occupancies[(int)Color.White];
            }

            // bishops
            ulong b = bishop;
            while (b != 0)
            {
                int sq = BitOperations.TrailingZeroCount(b);
                BishopAttacks(sq, friendly, enemy, isWhite, board, false, moves, ref count);
                b &= b - 1;
            }

            // rooks
            ulong r = rook;
            while (r != 0)
            {
                int sq = BitOperations.TrailingZeroCount(r);
                RookAttacks(sq, friendly, enemy, isWhite, board, false, moves, ref count);
                r &= r - 1;
            }

            // queens
            ulong q = queen;
            while (q != 0)
            {
                int sq = BitOperations.TrailingZeroCount(q);
                QueenAttacks(sq, friendly, enemy, isWhite, board, moves, ref count);
                q &= q - 1;
            }
        }

        internal static void GenerateKingMoves(Board board, Span<Move> moves, ref int count, bool isWhite)
        {
            ulong kingBB   = board.bitboards[isWhite ? (int)Piece.WhiteKing : (int)Piece.BlackKing];
            ulong friendly = board.occupancies[isWhite ? (int)Color.White : (int)Color.Black];
            Piece moveK    = isWhite ? Piece.WhiteKing : Piece.BlackKing;

            if (kingBB == 0) return;          // should never happen

            int fromSq = BitOperations.TrailingZeroCount(kingBB);
            ulong attacks = MoveTables.KingMoves[fromSq] & ~friendly;

            while (attacks != 0)
            {
                int toSq   = BitOperations.TrailingZeroCount(attacks);
                ulong toBB = 1UL << toSq;

                Piece captured = (toBB & board.occupancies[(int)(isWhite ? Color.Black : Color.White)]) != 0
                    ? MoveGen.FindCapturedPiece(board, (Square)toSq, isWhite)
                    : Piece.None;

                if (captured is Piece.WhiteKing or Piece.BlackKing)
                {
                    attacks &= ~toBB;
                    continue;
                }

                MoveFlags flag = captured != Piece.None ? MoveFlags.Capture : MoveFlags.None;
                moves[count++] = new Move((Square)fromSq, (Square)toSq, moveK, captured, flag);

                attacks &= ~toBB;
            }
        }

        internal static void GenerateKnightMoves(Board board, Span<Move> moves, ref int count, bool isWhite)
        {
            ulong knights  = board.bitboards[isWhite ? (int)Piece.WhiteKnight : (int)Piece.BlackKnight];
            ulong friendly = board.occupancies[isWhite ? (int)Color.White : (int)Color.Black];
            ulong enemy    = board.occupancies[isWhite ? (int)Color.Black : (int)Color.White];
            Piece moveN    = isWhite ? Piece.WhiteKnight : Piece.BlackKnight;

            while (knights != 0)
            {
                int fromSq = BitOperations.TrailingZeroCount(knights);
                ulong movesMask = MoveTables.KnightMoves[fromSq] & ~friendly;

                while (movesMask != 0)
                {
                    int  toSq  = BitOperations.TrailingZeroCount(movesMask);
                    ulong toBB = 1UL << toSq;

                    Piece captured = (toBB & enemy) != 0
                        ? MoveGen.FindCapturedPiece(board, (Square)toSq, isWhite)
                        : Piece.None;

                    if (captured is Piece.WhiteKing or Piece.BlackKing)
                    {
                        movesMask &= ~toBB;
                        continue;
                    }

                    MoveFlags flag = captured != Piece.None ? MoveFlags.Capture : MoveFlags.None;
                    moves[count++] = new Move((Square)fromSq, (Square)toSq, moveN, captured, flag);

                    movesMask &= ~toBB;
                }

                knights &= knights - 1;
            }
        }

        // ─── pawn generators ─────────────────────────────────────────────────────
        internal static void GeneratePawnMoves(Board board, Span<Move> moves, ref int count, bool isWhite)
        {
            if (board.enPassantSquare != Square.None)
                EnPassantAdd(board, moves, ref count, isWhite);

            ulong pawns, enemy, friendly;
            if (isWhite)
            {
                pawns = board.bitboards[(int)Piece.WhitePawn];
                enemy = board.occupancies[(int)Color.Black];
                friendly = board.occupancies[(int)Color.White];
                GenerateWhitePawn(pawns, enemy, friendly, moves, ref count, board);
            }
            else
            {
                pawns = board.bitboards[(int)Piece.BlackPawn];
                enemy = board.occupancies[(int)Color.White];
                friendly = board.occupancies[(int)Color.Black];
                GenerateBlackPawn(pawns, enemy, friendly, moves, ref count, board);
            }
        }

        internal static void GenerateWhitePawn(ulong pawns, ulong blackEnemy, ulong whiteFriendly, Span<Move> moves, ref int count, Board board)
        {
            ulong bit = 1UL;
            for (int i = 0; i < 64; i++)
            {
                if ((bit & pawns) != 0)
                {
                    WPawnAtks(i, blackEnemy, whiteFriendly, moves, ref count, board);
                }
                bit <<= 1;
            }
        }

        internal static void GenerateBlackPawn(ulong pawns, ulong whiteEnemy, ulong blackFriendly, Span<Move> moves, ref int count, Board board)
        {
            ulong bit = 1UL;
            for (int i = 0; i < 64; i++)
            {
                if ((bit & pawns) != 0)
                {
                    BPawnAtks(i, whiteEnemy, blackFriendly, moves, ref count, board);
                }
                bit <<= 1;
            }
        }

        private static void EnPassantAdd(Board board, Span<Move> moves, ref int count, bool isWhite)
        {
            if (isWhite)
                WEnPassant(board, moves, ref count);
            else
                BEnPassant(board, moves, ref count);
        }

        private static void WEnPassant(Board board, Span<Move> moves, ref int count)
        {
            int enPassantSq = (int)board.enPassantSquare;
            int file = enPassantSq % 8;
            if (file > 0)
            {
                if ((board.bitboards[(int)Piece.WhitePawn] & 1UL << (enPassantSq - 9)) != 0)
                {
                    moves[count++] = new((Square)(enPassantSq - 9), (Square)(enPassantSq), Piece.WhitePawn, Piece.BlackPawn, MoveFlags.EnPassant);
                }
            }
            if (file < 7)
            {
                if ((board.bitboards[(int)Piece.WhitePawn] & 1UL << (enPassantSq + 7)) != 0)
                {
                    moves[count++] = new((Square)(enPassantSq + 7), (Square)(enPassantSq), Piece.WhitePawn, Piece.BlackPawn, MoveFlags.EnPassant);
                }
            }
        }

        private static void BEnPassant(Board board, Span<Move> moves, ref int count)
        {
            int enPassantSq = (int)board.enPassantSquare;
            int file = enPassantSq % 8;
            if (file > 0)
            {
                if ((board.bitboards[(int)Piece.BlackPawn] & (1UL << (enPassantSq + 7))) != 0)
                {
                    moves[count++] = new((Square)(enPassantSq + 7), (Square)(enPassantSq), Piece.BlackPawn, Piece.WhitePawn, MoveFlags.EnPassant);
                }
            }
            if (file < 7)
            {
                if ((board.bitboards[(int)Piece.BlackPawn] & (1UL << (enPassantSq + 9))) != 0)
                {
                    moves[count++] = new((Square)(enPassantSq + 9), (Square)(enPassantSq), Piece.BlackPawn, Piece.WhitePawn, MoveFlags.EnPassant);
                }
            }
        }

        private static void WPawnAtks(int sq, ulong enemy, ulong friendly, Span<Move> moves, ref int count, Board board)
        {
            bool on7th = sq >= 48 && sq <= 55;
            bool on2nd = sq >= 8 && sq <= 15;

            if (on7th && (((1UL << (sq + 8)) & (enemy | friendly)) == 0))
            {
                moves[count++] = new((Square)sq, (Square)(sq + 8), Piece.WhitePawn, Piece.None, MoveFlags.Promotion, Piece.WhiteQueen);
                moves[count++] = new((Square)sq, (Square)(sq + 8), Piece.WhitePawn, Piece.None, MoveFlags.Promotion, Piece.WhiteRook);
                moves[count++] = new((Square)sq, (Square)(sq + 8), Piece.WhitePawn, Piece.None, MoveFlags.Promotion, Piece.WhiteKnight);
                moves[count++] = new((Square)sq, (Square)(sq + 8), Piece.WhitePawn, Piece.None, MoveFlags.Promotion, Piece.WhiteBishop);
            }
            else if (((1UL << (sq + 8)) & (enemy | friendly)) == 0)
            {
                moves[count++] = new((Square)sq, (Square)(sq + 8), Piece.WhitePawn);
                if (on2nd && (((1UL << (sq + 16)) & (enemy | friendly)) == 0))
                {
                    moves[count++] = new((Square)sq, (Square)(sq + 16), Piece.WhitePawn);
                }
            }

            if (((1UL << sq) & FileH) == 0 && ((1UL << (sq + 9)) & enemy) != 0)
            {
                Piece tookPiece = MoveGen.FindCapturedPiece(board, (Square)(sq + 9), true);

                if (tookPiece == Piece.WhiteKing || tookPiece == Piece.BlackKing) return;

                if (on7th)
                {
                    moves[count++] = new((Square)sq, (Square)(sq + 9), Piece.WhitePawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.WhiteQueen);
                    moves[count++] = new((Square)sq, (Square)(sq + 9), Piece.WhitePawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.WhiteRook);
                    moves[count++] = new((Square)sq, (Square)(sq + 9), Piece.WhitePawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.WhiteKnight);
                    moves[count++] = new((Square)sq, (Square)(sq + 9), Piece.WhitePawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.WhiteBishop);
                }
                else
                {
                    moves[count++] = new((Square)sq, (Square)(sq + 9), Piece.WhitePawn, tookPiece);
                }
            }

            if (((1UL << sq) & FileA) == 0 && ((1UL << (sq + 7)) & enemy) != 0)
            {
                Piece tookPiece = MoveGen.FindCapturedPiece(board, (Square)(sq + 7), true);

                if (tookPiece == Piece.WhiteKing || tookPiece == Piece.BlackKing) return;

                if (on7th)
                {
                    moves[count++] = new((Square)sq, (Square)(sq + 7), Piece.WhitePawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.WhiteQueen);
                    moves[count++] = new((Square)sq, (Square)(sq + 7), Piece.WhitePawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.WhiteRook);
                    moves[count++] = new((Square)sq, (Square)(sq + 7), Piece.WhitePawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.WhiteKnight);
                    moves[count++] = new((Square)sq, (Square)(sq + 7), Piece.WhitePawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.WhiteBishop);
                }
                else
                {
                    moves[count++] = new((Square)sq, (Square)(sq + 7), Piece.WhitePawn, tookPiece);
                }
            }
        }

        private static void BPawnAtks(int sq, ulong enemy, ulong friendly, Span<Move> moves, ref int count, Board board)
        {
            bool on2nd = sq >= 8 && sq <= 15;
            bool on7th = sq >= 48 && sq <= 55;

            if (on2nd && (((1UL << (sq - 8)) & (enemy | friendly)) == 0))
            {
                moves[count++] = new((Square)sq, (Square)(sq - 8), Piece.BlackPawn, Piece.None, MoveFlags.Promotion, Piece.BlackQueen);
                moves[count++] = new((Square)sq, (Square)(sq - 8), Piece.BlackPawn, Piece.None, MoveFlags.Promotion, Piece.BlackRook);
                moves[count++] = new((Square)sq, (Square)(sq - 8), Piece.BlackPawn, Piece.None, MoveFlags.Promotion, Piece.BlackKnight);
                moves[count++] = new((Square)sq, (Square)(sq - 8), Piece.BlackPawn, Piece.None, MoveFlags.Promotion, Piece.BlackBishop);
            }
            else if (((1UL << (sq - 8)) & (enemy | friendly)) == 0)
            {
                moves[count++] = new((Square)sq, (Square)(sq - 8), Piece.BlackPawn);
                if (on7th && (((1UL << (sq - 16)) & (enemy | friendly)) == 0))
                {
                    moves[count++] = new((Square)sq, (Square)(sq - 16), Piece.BlackPawn);
                }
            }

            if (((1UL << sq) & FileH) == 0 && ((1UL << (sq - 7)) & enemy) != 0)
            {
                Piece tookPiece = MoveGen.FindCapturedPiece(board, (Square)(sq - 7), false);
                if (tookPiece == Piece.WhiteKing || tookPiece == Piece.BlackKing) return;

                if (on2nd)
                {
                    moves[count++] = new((Square)sq, (Square)(sq - 7), Piece.BlackPawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.BlackQueen);
                    moves[count++] = new((Square)sq, (Square)(sq - 7), Piece.BlackPawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.BlackRook);
                    moves[count++] = new((Square)sq, (Square)(sq - 7), Piece.BlackPawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.BlackKnight);
                    moves[count++] = new((Square)sq, (Square)(sq - 7), Piece.BlackPawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.BlackBishop);
                }
                else
                {
                    moves[count++] = new((Square)sq, (Square)(sq - 7), Piece.BlackPawn, tookPiece);
                }
            }

            if (((1UL << sq) & FileA) == 0 && ((1UL << (sq - 9)) & enemy) != 0)
            {
                Piece tookPiece = MoveGen.FindCapturedPiece(board, (Square)(sq - 9), false);
                if (tookPiece == Piece.WhiteKing || tookPiece == Piece.BlackKing) return;

                if (on2nd)
                {
                    moves[count++] = new((Square)sq, (Square)(sq - 9), Piece.BlackPawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.BlackQueen);
                    moves[count++] = new((Square)sq, (Square)(sq - 9), Piece.BlackPawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.BlackRook);
                    moves[count++] = new((Square)sq, (Square)(sq - 9), Piece.BlackPawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.BlackKnight);
                    moves[count++] = new((Square)sq, (Square)(sq - 9), Piece.BlackPawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.BlackBishop);
                }
                else
                {
                    moves[count++] = new((Square)sq, (Square)(sq - 9), Piece.BlackPawn, tookPiece);
                }
            }
        }

    }
}
