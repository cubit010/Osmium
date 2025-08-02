using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace Osmium
{
    public class MoveFiltering
    {
        public static ulong AllAttacksTo(Board board, int targetSq, bool fromWhite)
        {
            ulong attackers = 0;
            ulong occ = board.occupancies[2];

            // Pawns
            if (fromWhite)
            {
                // White pawns attack diagonally forward (up)
                if (targetSq % 8 > 0)
                {
                    int from = targetSq - 9;
                    if (from >= 0 && (board.bitboards[(int)Piece.WhitePawn] & (1UL << from)) != 0)
                        attackers |= 1UL << from;
                }
                if (targetSq % 8 < 7)
                {
                    int from = targetSq - 7;
                    if (from >= 0 && (board.bitboards[(int)Piece.WhitePawn] & (1UL << from)) != 0)
                        attackers |= 1UL << from;
                }
            }
            else
            {
                // Black pawns attack diagonally down
                if (targetSq % 8 > 0)
                {
                    int from = targetSq + 7;
                    if (from < 64 && (board.bitboards[(int)Piece.BlackPawn] & (1UL << from)) != 0)
                        attackers |= 1UL << from;
                }
                if (targetSq % 8 < 7)
                {
                    int from = targetSq + 9;
                    if (from < 64 && (board.bitboards[(int)Piece.BlackPawn] & (1UL << from)) != 0)
                        attackers |= 1UL << from;
                }
            }

            // Knights
            ulong knightMap = MoveTables.KnightMoves[targetSq];
            attackers |= knightMap & board.bitboards[(int)(fromWhite ? Piece.WhiteKnight : Piece.BlackKnight)];

            // Kings (for completeness, if needed)
            ulong kingMap = MoveTables.KingMoves[targetSq];
            attackers |= kingMap & board.bitboards[(int)(fromWhite ? Piece.WhiteKing : Piece.BlackKing)];

            // Rooks and Queens
            ulong rookMap = Magics.GetRookAttacks(targetSq, occ);
            
            
            ulong rooks = board.bitboards[(int)(fromWhite ? Piece.WhiteRook : Piece.BlackRook)];
            ulong queens = board.bitboards[(int)(fromWhite ? Piece.WhiteQueen : Piece.BlackQueen)];
            attackers |= rookMap & (rooks | queens);

            // Bishops and Queens
            ulong bishopMap = Magics.GetBishopAttacks(targetSq, occ);
            ulong bishops = board.bitboards[(int)(fromWhite ? Piece.WhiteBishop : Piece.BlackBishop)];
            attackers |= bishopMap & (bishops | queens); // queens already declared above

            return attackers;
        }
        public static ulong FindCheckers(Board board, int kingSq, bool fromWhite)
        {
            ulong checkers = 0UL;
            ulong occ = board.occupancies[2];

            // ----------------
            // Pawn attacks
            // ----------------
            if (fromWhite)
            {
                // White pawns attack diagonally up
                if (kingSq % 8 > 0)
                {
                    int pawnSq = kingSq - 9;
                    if (pawnSq >= 0 && (board.bitboards[(int)Piece.WhitePawn] & (1UL << pawnSq)) != 0)
                        checkers |= 1UL << pawnSq;
                }
                if (kingSq % 8 < 7)
                {
                    int pawnSq = kingSq - 7;
                    if (pawnSq >= 0 && (board.bitboards[(int)Piece.WhitePawn] & (1UL << pawnSq)) != 0)
                        checkers |= 1UL << pawnSq;
                }
            }
            else
            {
                // Black pawns attack diagonally down
                if (kingSq % 8 > 0)
                {
                    int pawnSq = kingSq + 7;
                    if (pawnSq < 64 && (board.bitboards[(int)Piece.BlackPawn] & (1UL << pawnSq)) != 0)
                        checkers |= 1UL << pawnSq;
                }
                if (kingSq % 8 < 7)
                {
                    int pawnSq = kingSq + 9;
                    if (pawnSq < 64 && (board.bitboards[(int)Piece.BlackPawn] & (1UL << pawnSq)) != 0)
                        checkers |= 1UL << pawnSq;
                }
            }

            // ----------------
            // Knight attacks
            // ----------------
            ulong knightMap = MoveTables.KnightMoves[kingSq];
            ulong knights = board.bitboards[(int)(fromWhite ? Piece.WhiteKnight : Piece.BlackKnight)];
            checkers |= knightMap & knights;

            // ----------------
            // Rook + Queen attacks (horizontal/vertical)
            // ----------------
            ulong rookAttacks = Magics.GetRookAttacks(kingSq, occ);
            ulong rooks = board.bitboards[(int)(fromWhite ? Piece.WhiteRook : Piece.BlackRook)];
            ulong queens = board.bitboards[(int)(fromWhite ? Piece.WhiteQueen : Piece.BlackQueen)];
            checkers |= rookAttacks & (rooks | queens);

            // ----------------
            // Bishop + Queen attacks (diagonal)
            // ----------------
            ulong bishopAttacks = Magics.GetBishopAttacks(kingSq, occ);
            ulong bishops = board.bitboards[(int)(fromWhite ? Piece.WhiteBishop : Piece.BlackBishop)];
            checkers |= bishopAttacks & (bishops | queens);

            return checkers;
        }

        private static ulong[] pinnedMask = new ulong[64];

        //[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void FilterMoves(Board board, Span<Move> moves, ref int count, bool isWhite)
        {
            int kingSq = MoveGen.GetKingSquare(board, isWhite);
            ulong occ = board.occupancies[2];
            ulong enemyAttacks = AllAttacksTo(board, kingSq, !isWhite);
            ulong checkers = FindCheckers(board, kingSq, !isWhite);
            int king = isWhite ? 5 : 11;
            int checkCount = BitOperations.PopCount(checkers);
           
            ulong legalMask = 0xFFFFFFFFFFFFFFFFUL;

            // from-square → allowed destinations (for pinned pieces)
            ulong pinnedPieces = GeneratePins(board, kingSq, isWhite);
            //Console.WriteLine($"Pinned bitboard: \n{Bitboard.Pretty(pinnedPieces)}");
            // Single check → can capture checker or block
            //if (checkCount > 0)
            //{
            //    if (MoveGen.IsMateCheck(board, isWhite))
            //    {
            //        count = 0; // Properly clears move list
            //        //moves = moves[..0]; // No legal moves if in checkmate
            //        //Console.WriteLine("In checkmate, no legal moves available.");
            //        return;
            //    }
            //}
            if (checkCount == 1)
            {
                int checkerSq = BitOperations.TrailingZeroCount(checkers);
                //Console.WriteLine($"Checker square: {(Square)checkerSq}");
                Piece checker = MoveGen.FindPieceAt(board, checkerSq);

                ulong between = (Bitboard.BetweenBB[kingSq, checkerSq] | (1UL << checkerSq));
                legalMask = between;
            }
            else if (checkCount >= 2)
            {
                // Double check → only king moves allowed
                legalMask = 0;
            }
            //Console.WriteLine("Checker count: " + checkCount);

            int legalCount = 0;

            for (int i = 0; i < count; i++)
            {
                Move move = moves[i];
                int from = (int)move.From;
                int to = (int)move.To;

                ulong fromBB = 1UL << from;
                ulong toBB = 1UL << to;

                if (fromBB == 0 || toBB == 0)
                    continue;


                if (((int)move.PieceMoved == king))
                {
                    // King move: must not move into check
                    if (!IsSquareChecked(board, to, isWhite, kingSq))
                        moves[legalCount++] = move;
                }
                else if (checkCount == 0)
                {
                    // Not in check
                    if ((pinnedPieces & fromBB) != 0)
                    {
                        if ((pinnedMask[from] & toBB) != 0)
                            moves[legalCount++] = move;
                    }
                    else
                    {
                        moves[legalCount++] = move;
                    }
                }
                else if (checkCount == 1)
                {
                    if ((pinnedPieces & fromBB) != 0)
                    {
                        if (((pinnedMask[from] & legalMask) & toBB) != 0)
                            moves[legalCount++] = move;
                    }
                    else if ((legalMask & toBB) != 0)
                    {
                        moves[legalCount++] = move;
                    }
                }
                // else double check and not king → illegal
            }
            count = legalCount;
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

        public static readonly sbyte[] directions = [ 8, -8, 1, -1, 9, -9, 7, -7 ];
        public static ulong GeneratePins(Board board, int kingSq, bool isWhite)
        {
            ulong pinned = 0;
            ulong occ = board.occupancies[2];
            ulong friend = board.occupancies[isWhite ? 0 : 1];

            // Directions for sliding pieces
            //Console.WriteLine($"king square {(Square)kingSq}");
            foreach (sbyte dir in directions)
            {
                //Console.WriteLine($"Direction: {dir}");
                int sq = kingSq;
                
                bool foundBlocker = false;
                int blockerSq = -1;

                while (true)
                {
                    sq += dir;

                    //1.62%, need better optimization
                    if (!IsOnBoard(sq, dir))
                    {
                        //Console.WriteLine($"Direction {dir} out of bounds at square {(Square)sq}, came from {(Square)sq-dir}");
                        break;
                    }
                    ulong sqBB = 1UL << sq;

                    //no piece at sq
                    if ((occ & sqBB) == 0)
                        continue;

                    //piece at sq
                    //friend piece
                    if ((friend & sqBB) != 0)
                    {
                        if (foundBlocker)
                            //don't need to reset anything because break will exit the loop and go to the next direction
                            break; // 2nd blocker — cannot be pin

                        foundBlocker = true;
                        blockerSq = sq;
                        continue;
                    }
                    //enemy
                    else
                    {
                        // Enemy piece
                        Piece enemy = MoveGen.FindPieceAt(board, sq);
                        if (!IsSlider(enemy, dir))
                            break;

                        if (foundBlocker)
                        {
                            // Pinned piece found
                            pinned |= (1UL << blockerSq);

                            // Build the pin ray mask (legal move mask for pinned piece)
                            ulong mask = 0UL;
                            int step = dir;
                            int temp = kingSq + step;
                            while (temp != sq)
                            {
                                mask |= 1UL << temp;
                                temp += step;
                            }
                            mask |= 1UL << sq; // include attacker square
                            mask |= 1UL << kingSq; // (optional: include king if your logic uses it)
                            pinnedMask[blockerSq] = mask;
                        }
                        break;
                    }
                }
            }

            return pinned;
        }

        /* Board visualization down

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

        /* Board visualization nomral board

        A  B  C  D  E  F  G  H 

        56 57 58 59 60 61 62 63   8 7
        48 49 50 51 52 53 54 55   7 6
        40 41 42 43 44 45 46 47   6 5
        32 33 34 35 36 37 38 39   5 4
        24 25 26 27 28 29 30 31   4 3
        16 17 18 19 20 21 22 23   3 2
        8  9  10 11 12 13 14 15   2 1
        0  1  2  3  4  5  6  7    1 0

        +7 +8 +9
        -1  0 +1
        -9 -8 -7

        */

        private static bool IsOnBoard(int sq, int dir)
        {
            // the square is already moved, direction is where it came from, must determine if the square it came from is legal
            int file = sq % 8;
            int rank = sq / 8;
            
            switch (dir)
            {
                //the resulting file can't be 0 after a +1
                case 1: return file != 0;
                // resulting file can't be 7 after a -1
                case -1: return file != 7;

                case 9: return sq < 64 && file != 0;
                case -9: return sq >= 0 && file != 7 ;
                case 7: return sq < 64 && file != 7;
                case -7: return sq >= 0 && file != 0;
                default: return sq >= 0 && sq < 64;
            }
        }
        private static bool IsSlider(Piece piece, int dir)
        {
            int type = (int)piece % 6;

            bool isDiag = dir == 7 || dir == -7 || dir == 9 || dir == -9;
            bool isOrtho = dir == 1 || dir == -1 || dir == 8 || dir == -8;

            return
                (isDiag && (type == 2 || type == 4)) || // Bishop or Queen
                (isOrtho && (type == 3 || type == 4));  // Rook or Queen
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool IsSquareChecked(Board board, int square, bool isWhiteDefend, int Ksq)
        {
            //if white is to defend, white is to move

            //pretend king is moved, so that the old king can't block a potential check
            ulong occ = (board.occupancies[2] & ~(1UL << Ksq));

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
    }
}
 