using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace ChessC_
{
    internal static class MoveGen
    {
        //currently unused, is only used by SEE but SEE isn't implemented
        public static void GenerateAttackersToSquare(Board board, Square target, Color color, Span<Move> result, ref int count)
        {
            Span<Move> allMoves = stackalloc Move[256];
            int tmpCount = 0;
            GenSemiLegal(board, allMoves, ref tmpCount, color == Color.White);

            for (int i = 0; i < tmpCount; i++)
            {
                var move = allMoves[i];
                if (move.To == target && move.PieceCaptured != Piece.None)
                    result[count++] = move;

                if ((move.Flags & MoveFlags.EnPassant) != 0 && move.To == target)
                    result[count++] = move;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void FlagCheckAndMate(Board board, Span<Move> moves, int count, bool isWhiteToMove)
        {
            
            bool opponent = !isWhiteToMove;

            for (int i = 0; i < count; i++)
            {
                UndoInfo undo = board.MakeSearchMove(board, moves[i]);

                bool inCheck = IsSquareAttacked(board, GetKingSquare(board, opponent), opponent);

                if (inCheck)
                {
                    moves[i].AddMoveFlag(MoveFlags.Check);
                    RunMateCheck(board, opponent, moves[i]);
                }
                board.UnmakeMove(moves[i], undo);
            }
        }
        
        public static void FlagCheckOnly(Board board, Span<Move> moves, int count, bool isWhiteToMove)
        {

            bool opponent = !isWhiteToMove;

            for (int i = 0; i < count; i++)
            {
                UndoInfo undo = board.MakeSearchMove(board, moves[i]);

                bool inCheck = IsSquareAttacked(board, GetKingSquare(board, opponent), opponent);

                if (inCheck)
                {
                    moves[i].AddMoveFlag(MoveFlags.Check);
                }
                board.UnmakeMove(moves[i], undo);
            }
        }

        public static void RunMateCheck(Board board, bool sideBeingChecked, Move move)
        {
            bool isDoubleCheck = IsDoubleCheck(board, sideBeingChecked, out int atkerSq, out Piece attackerPiece);

            if (IsEscapePossible(board, sideBeingChecked)) return;

            if (!isDoubleCheck)
            {
                // If it's a double check, we can't block or capture the attacker, if not, can block or capture
                // Checks if the attacker can be blocked

                if(CanCaptureCheckingPiece(board, sideBeingChecked, atkerSq)) return;

                if ((int)attackerPiece % 6 >= 2) { 
                    if (CanBlockCheck(board, sideBeingChecked, atkerSq)) return;
                }
            }
            
            move.AddMoveFlag(MoveFlags.Checkmate);
        }
        public static bool IsMateCheck(Board board, bool oppIsWhite)
        {
            bool isDoubleCheck = IsDoubleCheck(board, oppIsWhite, out int atkerSq, out Piece attackerPiece);

            if (IsEscapePossible(board, oppIsWhite)) return false;

            if (!isDoubleCheck)
            {
                // If it's a double check, we can't block or capture the attacker, if not, can block or capture
                // Checks if the attacker can be blocked

                if (CanCaptureCheckingPiece(board, oppIsWhite, atkerSq)) return false;

                if ((int)attackerPiece % 6 >= 2)
                {
                    if (CanBlockCheck(board, oppIsWhite, atkerSq)) return false;
                }
            }
            return true;
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

        56 57 58 59 60 61 62 63   8
        48 49 50 51 52 53 54 55   7
        40 41 42 43 44 45 46 47   6
        32 33 34 35 36 37 38 39   5
        24 25 26 27 28 29 30 31   4
        16 17 18 19 20 21 22 23   3
        8  9  10 11 12 13 14 15   2
        0  1  2  3  4  5  6  7    1

        */
        public static bool IsDoubleCheck(
            Board board,
            bool isWhiteDefend,
            out int atkerSq,
            out Piece attackerPiece)
        {
            atkerSq = -1;
            attackerPiece = Piece.None;
            int attackerCount = 0;

            // 1) King square
            int kingSq = MoveGen.GetKingSquare(board, isWhiteDefend);
            ulong occ = board.occupancies[2];

            // 2) Pawn attacks
            if (isWhiteDefend)
            {
                int sq1 = kingSq + 7, sq2 = kingSq + 9;
                if (sq1 < 64 && kingSq % 8 > 0 &&
                    (board.bitboards[(int)Piece.BlackPawn] & (1UL << sq1)) != 0)
                {
                    atkerSq = sq1;
                    attackerPiece = Piece.BlackPawn;
                    if (++attackerCount > 1) return true;
                }
                if (sq2 < 64 && kingSq % 8 < 7 &&
                    (board.bitboards[(int)Piece.BlackPawn] & (1UL << sq2)) != 0)
                {
                    atkerSq = sq2;
                    attackerPiece = Piece.BlackPawn;
                    if (++attackerCount > 1) return true;
                }
            }
            else
            {
                int sq1 = kingSq - 7, sq2 = kingSq - 9;
                if (sq1 >= 0 && kingSq % 8 < 7 &&
                    (board.bitboards[(int)Piece.WhitePawn] & (1UL << sq1)) != 0)
                {
                    atkerSq = sq1;
                    attackerPiece = Piece.WhitePawn;
                    if (++attackerCount > 1) return true;
                }
                if (sq2 >= 0 && kingSq % 8 > 0  &&
                    (board.bitboards[(int)Piece.WhitePawn] & (1UL << sq2)) != 0)
                {
                    atkerSq = sq2;
                    attackerPiece = Piece.WhitePawn;
                    if (++attackerCount > 1) return true;
                }
            }

            // 3) Knight attacks
            ulong knights = isWhiteDefend
                ? board.bitboards[(int)Piece.BlackKnight]
                : board.bitboards[(int)Piece.WhiteKnight];
            ulong knightHits = knights & MoveTables.KnightMoves[kingSq];
            while (knightHits != 0)
            {
                int sq = BitOperations.TrailingZeroCount(knightHits);
                knightHits &= knightHits - 1;

                atkerSq = sq;
                attackerPiece = isWhiteDefend ? Piece.BlackKnight : Piece.WhiteKnight;
                if (++attackerCount > 1) return true;
            }

            // 4) Rook/Queen attacks
            ulong rqBB = isWhiteDefend
                ? board.bitboards[(int)Piece.BlackRook] | board.bitboards[(int)Piece.BlackQueen]
                : board.bitboards[(int)Piece.WhiteRook] | board.bitboards[(int)Piece.WhiteQueen];
            ulong rookHits = Magics.GetRookAttacks(kingSq, occ) & rqBB;
            while (rookHits != 0)
            {
                int sq = BitOperations.TrailingZeroCount(rookHits);
                rookHits &= rookHits - 1;

                atkerSq = sq;
                attackerPiece = FindPieceAt(board, sq);
                if (++attackerCount > 1) return true;
            }

            // 5) Bishop/Queen attacks
            ulong bqBB = isWhiteDefend
                ? board.bitboards[(int)Piece.BlackBishop] | board.bitboards[(int)Piece.BlackQueen]
                : board.bitboards[(int)Piece.WhiteBishop] | board.bitboards[(int)Piece.WhiteQueen];
            ulong bishopHits = Magics.GetBishopAttacks(kingSq, occ) & bqBB;
            while (bishopHits != 0)
            {
                int sq = BitOperations.TrailingZeroCount(bishopHits);
                bishopHits &= bishopHits - 1;

                atkerSq = sq;
                attackerPiece = FindPieceAt(board, sq);
                if (++attackerCount > 1) return true;
            }

            // 0 or 1 attacker
            return false;
        }

        //is white to move, meaning iswhite being checked, meaning checking for ways for isWhite to escape
        public static bool IsEscapePossible(Board board, bool isWhiteToMove)
        {
            int kingSquare = GetKingSquare(board, isWhiteToMove);
            ulong kingMoves = MoveTables.KingMoves[kingSquare];

            // Friendly occupancy
            ulong friendlyPieces = isWhiteToMove
                ? board.occupancies[0]  // White pieces
                : board.occupancies[1]; // Black pieces

            // Valid escape targets = empty or enemy squares
            ulong possibleEscapes = kingMoves & ~friendlyPieces;

            while (possibleEscapes != 0)
            {
                int targetSquare = BitOperations.TrailingZeroCount(possibleEscapes);
                possibleEscapes &= possibleEscapes - 1;

                // We are checking if after moving to targetSquare, the king would still be under attack
                if (!IsSquareAttacked(board, targetSquare, isWhiteToMove))
                    return true; // Legal escape found
            }

            return false; // No legal escape
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool CanCaptureCheckingPiece(Board board, bool isWhiteToMove, int square)
        {
        //ulong occ;
            
            
            // occ = board.occupancies[2];
                

            // Pawn captures (note: capture direction is *opposite* of pawn push)
            if (isWhiteToMove)
            {
                // White pawns capture up-left and up-right
                if (square - 7 >= 0 && square % 8 > 0 &&
                    ((board.bitboards[(int)Piece.WhitePawn] & (1UL << (square - 7))) != 0))
                    return true;
                if (square - 9 >= 0 && square % 8 < 7 &&
                    ((board.bitboards[(int)Piece.WhitePawn] & (1UL << (square - 9))) != 0))
                    return true;
            }
            else
            {
                // Black pawns capture down-left and down-right
                if (square + 7 < 64 && square % 8 < 7 &&
                    ((board.bitboards[(int)Piece.BlackPawn] & (1UL << (square + 7))) != 0))
                    return true;
                if (square + 9 < 64 && square % 8 > 0 &&
                    ((board.bitboards[(int)Piece.BlackPawn] & (1UL << (square + 9))) != 0))
                    return true;
            }

            // Knights
            ulong knights = isWhiteToMove
                ? board.bitboards[(int)Piece.WhiteKnight]
                : board.bitboards[(int)Piece.BlackKnight];
            if ((knights & MoveTables.KnightMoves[square]) != 0)
                return true;

            // Rooks and Queens (rook-like movement)
            ulong rq = isWhiteToMove
                ? board.bitboards[(int)Piece.WhiteRook] | board.bitboards[(int)Piece.WhiteQueen]
                : board.bitboards[(int)Piece.BlackRook] | board.bitboards[(int)Piece.BlackQueen];
            if ((Magics.GetRookAttacks(square, board.occupancies[2]) & rq) != 0)
                return true;

            // Bishops and Queens (bishop-like movement)
            ulong bq = isWhiteToMove
                ? board.bitboards[(int)Piece.WhiteBishop] | board.bitboards[(int)Piece.WhiteQueen]
                : board.bitboards[(int)Piece.BlackBishop] | board.bitboards[(int)Piece.BlackQueen];
            if ((Magics.GetBishopAttacks(square, board.occupancies[2]) & bq) != 0)
                return true;

            return false;
        }
        

        public static bool IsSquareBlockable(Board board, bool isWhiteToMove, int square)
        {
            // occupancy of all pieces
            ulong occ = board.occupancies[2];

            // 1) Pawn pushes
            if (isWhiteToMove)
            {
                // Single push from square-8
                int from1 = square - 8;
                if (from1 >= 0 &&
                    (board.bitboards[(int)Piece.WhitePawn] & (1UL << from1)) != 0 &&
                    ((occ & (1UL << square)) == 0))
                {
                    return true;
                }

                // Double push from square-16 (only if on 4th rank = squares 32..39)
                int from2 = square - 16;
                if (square >= 32 && square <= 39 &&
                    from2 >= 0 &&
                    (board.bitboards[(int)Piece.WhitePawn] & (1UL << from2)) != 0 &&
                    // intermediate square must be empty
                    ((occ & (1UL << from1)) == 0) &&
                    ((occ & (1UL << square)) == 0))
                {
                    return true;
                }
            }
            else
            {
                // Single push from square+8
                int from1 = square + 8;
                if (from1 < 64 &&
                    (board.bitboards[(int)Piece.BlackPawn] & (1UL << from1)) != 0 &&
                    ((occ & (1UL << square)) == 0))
                {
                    return true;
                }

                // Double push from square+16 (only if on 5th rank = squares 24..31)
                int from2 = square + 16;
                if (square >= 24 && square <= 31 &&
                    from2 < 64 &&
                    (board.bitboards[(int)Piece.BlackPawn] & (1UL << from2)) != 0 &&
                    // intermediate square empty
                    ((occ & (1UL << from1)) == 0) &&
                    ((occ & (1UL << square)) == 0))
                {
                    return true;
                }
            }

            // 2) Knight hops
            ulong knights = isWhiteToMove
                ? board.bitboards[(int)Piece.WhiteKnight]
                : board.bitboards[(int)Piece.BlackKnight];
            if ((knights & MoveTables.KnightMoves[square]) != 0)
                return true;

            //king can't block

            // 4) Rook/Queen sliding (orthogonal)
            ulong rq = isWhiteToMove
                ? board.bitboards[(int)Piece.WhiteRook] | board.bitboards[(int)Piece.WhiteQueen]
                : board.bitboards[(int)Piece.BlackRook] | board.bitboards[(int)Piece.BlackQueen];
            if ((Magics.GetRookAttacks(square, occ) & rq) != 0)
                return true;

            // 5) Bishop/Queen sliding (diagonal)
            ulong bq = isWhiteToMove
                ? board.bitboards[(int)Piece.WhiteBishop] | board.bitboards[(int)Piece.WhiteQueen]
                : board.bitboards[(int)Piece.BlackBishop] | board.bitboards[(int)Piece.BlackQueen];
            if ((Magics.GetBishopAttacks(square, occ) & bq) != 0)
                return true;

            // No friendly move reaches that square
            return false;
        }

        public static bool CanBlockCheck(
            Board board,
            bool isWhiteDefend,
            int attackerSq)
        {
            int kingSq = GetKingSquare(board, isWhiteDefend);

            // Compute file/rank differences
            int fromRank = attackerSq >> 3, fromFile = attackerSq & 7;
            int toRank = kingSq >> 3, toFile = kingSq & 7;

            int dr = Math.Sign(toRank - fromRank);
            int df = Math.Sign(toFile - fromFile);

            // If not perfectly aligned on rank, file or diagonal, no interpose possible
            if (dr != 0 && df != 0 && Math.Abs(toRank - fromRank) != Math.Abs(toFile - fromFile))
                return false;

            // step offset: ±8 for file, ±1 for rank, ±9/±7 for diagonals
            int step = dr * 8 + df;

            // Walk from attacker toward the king, one square at a time
            int sq = attackerSq + step;
            while (sq != kingSq)
            {
                // If any piece can legally move (non‐capture) to `sq`, we can block
                if (IsSquareBlockable(board, !isWhiteDefend, sq))
                    return true;

                sq += step;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void GenSemiLegal(Board board, Span<Move> moves, ref int count, bool isWhite)
        {
            PieceMoves.GenerateKingMoves(board, moves, ref count, isWhite);
            PieceMoves.GenerateKnightMoves(board, moves, ref count, isWhite);
            PieceMoves.GenerateSliderMoves(board, moves, ref count, isWhite);
            PieceMoves.GeneratePawnMoves(board, moves, ref count, isWhite);
            GenerateCastles(board, moves, ref count, isWhite);
        }


        private static readonly Move[] _moveBuffer = new Move[256];

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void FilteredLegalMoves(Board board, Span<Move> result, ref int count, bool isWhite)
        {
            Span<Move> tmp = stackalloc Move[256];
            int tmpCount = 0;

            GenSemiLegal(board, tmp, ref tmpCount, isWhite);
            MoveFiltering.
                FilterMoves(board, tmp, ref tmpCount, isWhite);
            FlagCheckAndMate(board, tmp, tmpCount, isWhite);

            for (int i = 0; i < tmpCount; i++)
                result[count++] = tmp[i];
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        //public static void FilteredLegalWithoutFlag_Old(Board board, Span<Move> result, ref int count, bool isWhite)
        //{
        //    Span<Move> tmp = stackalloc Move[256];
        //    int tmpCount = 0;

        //    GenSemiLegal(board, tmp, ref tmpCount, isWhite);
        //    //MoveFiltering.
        //        FilterMoves(board, tmp, ref tmpCount, isWhite);

        //    for (int i = 0; i < tmpCount; i++)
        //        result[count++] = tmp[i];
        //}

        public static void FilteredLegalWithoutFlag(Board board, Span<Move> result, ref int count, bool isWhite)
        {
            Span<Move> tmp = stackalloc Move[256];
            int tmpCount = 0;

            GenSemiLegal(board, tmp, ref tmpCount, isWhite);
            MoveFiltering.FilterMoves(board, tmp, ref tmpCount, isWhite);

            for (int i = 0; i < tmpCount; i++)
                result[count++] = tmp[i];
        }

        //private static void FilterMoves(Board board, Span<Move> moves, ref int count, bool isWhite)
        //{
        //    int legalCount = 0;

        //    for (int i = 0; i < count; i++)
        //    {
        //        Move move = moves[i];
        //        if (!SimMoveCheck(move, board, isWhite))
        //        {
        //            moves[legalCount++] = move;
        //        }
        //    }

        //    count = legalCount;
        //}



        //[MethodImpl(MethodImplOptions.NoInlining)]
        //private static bool SimMoveCheck(Move move, Board board, bool isWhiteToMove)
        //{
        //    UndoInfo undo = board.MakeSearchMove(board, move);
        //    int kingIdx = GetKingSquare(board, isWhiteToMove);
        //    bool inCheck = IsSquareAttacked(board, kingIdx, isWhiteToMove);
        //    board.UnmakeMove(move, undo);
        //    return inCheck;
        //}

        public static bool IsInCheck(Board board, bool isWhiteDefend)
        {
            int kingIdx = GetKingSquare(board, isWhiteDefend);
            return IsSquareAttacked(board, kingIdx, isWhiteDefend);
        }

        public static int GetKingSquare(Board board, bool isWhite)
        {
            ulong kingBB = isWhite
                ? board.bitboards[(int)Piece.WhiteKing]
                : board.bitboards[(int)Piece.BlackKing];

            return BitOperations.TrailingZeroCount(kingBB);
        }

        private const ulong FileA = 0x0101010101010101UL;
        private const ulong FileH = 0x8080808080808080UL;
        private const ulong Rank1 = 0x00000000000000FFUL;
        private const ulong Rank8 = 0xFF00000000000000UL;
        private const ulong Rank4 = 0x00000000FF000000;
        private const ulong Rank5 = 0x000000FF00000000;

        internal static Piece FindCapturedPiece(Board board, Square sq, bool isWhite)
        {
            ulong mask = 1UL << (int)sq;
            int start = isWhite ? 6 : 0;
            int end = isWhite ? 12 : 6;

            for (int i = start; i < end; i++)
                if ((board.bitboards[i] & mask) != 0)
                    return (Piece)i;

            return Piece.None;
        }
        internal static Piece FindPieceAt(Board board, int sq)
        {
            ulong mask = 1UL << (int)sq;
            
            for (int i = 0; i < 12; i++)
                if ((board.bitboards[i] & mask) != 0)
                    return (Piece)i;

            return Piece.None;
        }

        public static bool IsSquareAttacked(Board board, int square, bool isWhiteDefend)
        {
            ulong occ = board.occupancies[2];

            // Pawn attacks
            if (isWhiteDefend)
            {
                // Black pawn attacks white king (from southeast or southwest)
                if (square % 8 < 7 && square + 7 < 64 &&
                    (board.bitboards[(int)Piece.BlackPawn] & (1UL << (square + 7))) != 0)
                    return true;
                if (square % 8 > 0 && square + 9 < 64 &&
                    (board.bitboards[(int)Piece.BlackPawn] & (1UL << (square + 9))) != 0)
                    return true;
            }
            else
            {
                // White pawn attacks black king (from northwest or northeast)
                if (square % 8 > 0 && square - 7 >= 0 &&
                    (board.bitboards[(int)Piece.WhitePawn] & (1UL << (square - 7))) != 0)
                    return true;
                if (square % 8 < 7 && square - 9 >= 0 &&
                    (board.bitboards[(int)Piece.WhitePawn] & (1UL << (square - 9))) != 0)
                    return true;
            }

            // Knight attacks
            ulong knights = isWhiteDefend
                ? board.bitboards[(int)Piece.BlackKnight]
                : board.bitboards[(int)Piece.WhiteKnight];
            if ((knights & MoveTables.KnightMoves[square]) != 0)
                return true;

            // King attacks (relevant for some edge cases outside check detection)
            ulong kings = isWhiteDefend
                ? board.bitboards[(int)Piece.BlackKing]
                : board.bitboards[(int)Piece.WhiteKing];
            if ((kings & MoveTables.KingMoves[square]) != 0)
                return true;

            // Rook/Queen attacks
            ulong rq = isWhiteDefend
                ? board.bitboards[(int)Piece.BlackRook] | board.bitboards[(int)Piece.BlackQueen]
                : board.bitboards[(int)Piece.WhiteRook] | board.bitboards[(int)Piece.WhiteQueen];
            if ((Magics.GetRookAttacks(square, occ) & rq) != 0)
                return true;

            // Bishop/Queen attacks
            ulong bq = isWhiteDefend
                ? board.bitboards[(int)Piece.BlackBishop] | board.bitboards[(int)Piece.BlackQueen]
                : board.bitboards[(int)Piece.WhiteBishop] | board.bitboards[(int)Piece.WhiteQueen];
            if ((Magics.GetBishopAttacks(square, occ) & bq) != 0)
                return true;

            return false;
        }


        private static void GenerateCastles(Board board, Span<Move> moves, ref int count, bool isWhite)
        {
            int kingFrom = GetKingSquare(board, isWhite);
            var rights = board.castlingRights;
            ulong occ = board.occupancies[2];

            if (isWhite && kingFrom == (int)Square.E1 && !IsSquareAttacked(board, (int)Square.E1, true))
            {
                if ((rights & Castling.WhiteKing) != 0
                    && ((occ & (1UL << (int)Square.F1)) == 0)
                    && ((occ & (1UL << (int)Square.G1)) == 0)
                    && !IsSquareAttacked(board, (int)Square.F1, true)
                    && !IsSquareAttacked(board, (int)Square.G1, true))
                {
                    moves[count++] = new Move((Square)kingFrom, Square.G1, Piece.WhiteKing, Piece.None, MoveFlags.Castling);
                }

                if ((rights & Castling.WhiteQueen) != 0
                    && ((occ & (1UL << (int)Square.D1)) == 0)
                    && ((occ & (1UL << (int)Square.C1)) == 0)
                    && ((occ & (1UL << (int)Square.B1)) == 0)
                    && !IsSquareAttacked(board, (int)Square.D1, true)
                    && !IsSquareAttacked(board, (int)Square.C1, true))
                {
                    moves[count++] = new Move((Square)kingFrom, Square.C1, Piece.WhiteKing, Piece.None, MoveFlags.Castling);
                }
            }
            else if (!isWhite && kingFrom == (int)Square.E8 && !IsSquareAttacked(board, (int)Square.E8, false))
            {
                if ((rights & Castling.BlackKing) != 0
                    && ((occ & (1UL << (int)Square.F8)) == 0)
                    && ((occ & (1UL << (int)Square.G8)) == 0)
                    && !IsSquareAttacked(board, (int)Square.F8, false)
                    && !IsSquareAttacked(board, (int)Square.G8, false))
                {
                    moves[count++] = new Move((Square)kingFrom, Square.G8, Piece.BlackKing, Piece.None, MoveFlags.Castling);
                }

                if ((rights & Castling.BlackQueen) != 0
                    && ((occ & (1UL << (int)Square.D8)) == 0)
                    && ((occ & (1UL << (int)Square.C8)) == 0)
                    && ((occ & (1UL << (int)Square.B8)) == 0)
                    && !IsSquareAttacked(board, (int)Square.D8, false)
                    && !IsSquareAttacked(board, (int)Square.C8, false))
                {
                    moves[count++] = new Move((Square)kingFrom, Square.C8, Piece.BlackKing, Piece.None, MoveFlags.Castling);
                }
            }
        }
    }
}
