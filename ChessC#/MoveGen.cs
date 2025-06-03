using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Net.NetworkInformation;
using System.Numerics;

namespace ChessC_
{
    internal static class MoveGen
    {   
        public static void FlagCheckAndMate(Board board, List<Move> moves, bool isWhite)
        {
            bool opponent = !isWhite;

            for (int i = 0; i < moves.Count; i++)
            {
                Move move = moves[i];

                // Make the move on the board
                board.MakeMove(move);

                // Check if opponent's king is in check, opponent defending
                bool inCheck = IsSquareAttacked(board, GetKingSquare(board, opponent), opponent);

                if (inCheck)
                {
                    move.AddMoveFlag(MoveFlags.Check);

                    // Generate opponent's legal moves after this move
                    List<Move> opponentMoves = FilteredLegalMoves(board, opponent);

                    if (opponentMoves.Count == 0)
                    {
                        // No legal moves for opponent and they are in check -> checkmate
                        move.AddMoveFlag(MoveFlags.Checkmate);
                    }
                }

                // Undo the move to restore board state
                board.UnmakeMove();
                    
                // Update the move in the list with the new flags
                moves[i] = move;
            }
        }
        private static void GenSemiLegal(Board board, List<Move> moves, bool isWhite)
        {
            GenerateKingMoves(board, moves, isWhite);
            GenerateKnightMoves(board, moves, isWhite);
            GenerateSliderMoves(board, moves, isWhite);
            GeneratePawnMoves(board, moves, isWhite);
            GenerateCastles(board, moves, isWhite);
        }
        public static List<Move> FilteredLegalMoves(Board board, bool isWhite)
        {
            List<Move> moves = new List<Move>();
            GenSemiLegal(board, moves, isWhite);
            FilterMoves(board, moves, isWhite);
            FlagCheckAndMate(board, moves, isWhite);
            return moves;
        }
        private static void FilterMoves(Board board, List<Move> moves, bool isWhite)
        {
            if (moves == null || moves.Count == 0)
                return;

            for (int i = moves.Count - 1; i >= 0; i--)
            {
                Move m = moves[i];
                if ((int)m.From < 0 || (int)m.From >= 64 || (int)m.To < 0 || (int)m.To >= 64)
                {
                    Console.WriteLine($"Invalid move index: {m}");
                    moves.RemoveAt(i);
                    continue;
                }

                if (m.PieceCaptured == Piece.WhiteKing || m.PieceCaptured == Piece.BlackKing)
                {
                    moves.RemoveAt(i);
                    continue;
                }

                if (SimMoveCheck(m, board, isWhite))
                    moves.RemoveAt(i);
            }
        }
        private static bool SimMoveCheck(Move move, Board board, bool isWhiteToMove)
        {
            // 1) Apply the move
            board.MakeMove(move);

            // 2) Find the king‐square of the side that *JUST MOVED*
            //    (isWhiteToMove == true means White just moved, so check White's king;
            //     isWhiteToMove == false means Black just moved, so check Black's king).
            int kingIdx = GetKingSquare(board, isWhiteToMove);

            // 3) Now ask: “Is that king under attack by the *other* color?”
            //    Because the defender’s color is exactly isWhiteToMove.
            bool inCheck = IsSquareAttacked(
                board,
                kingIdx,
                /*isDefenderWhite=*/ isWhiteToMove
            );

            // 4) Undo and return
            board.UnmakeMove();
            return inCheck;
        }
        public static int GetKingSquare(Board board, bool isWhite)
        {
            // pick the appropriate bitboard
            ulong kingBB = isWhite
                ? board.bitboards[(int)Piece.WhiteKing]
                : board.bitboards[(int)Piece.BlackKing];

            // TrailingZeroCount gives you the index (0–63) of the least significant 1-bit
            return BitOperations.TrailingZeroCount(kingBB);
        }
        
      
        // ─── file‐masks ──────────────────────────────────────────────────────
        private const ulong FileA = 0x0101010101010101UL;
        private const ulong FileH = 0x8080808080808080UL;

        // ─── initialization ──────────────────────────────────────────────────
        private static Piece FindCapturedPiece(Board board, Square sq, bool isWhite)
        {
            ulong mask = 1UL << (int)sq;
            //searches other color for cap piece
            int start = isWhite ? 6 : 0;
            int end = isWhite ? 12 : 6;

            for (int i = start; i < end; i++)
            {
                if ((board.bitboards[i] & mask) != 0)
                    return (Piece)i;
            }

            return Piece.None;
        }
        
        public static bool IsAttackedByWhite(Board board, int sq) => IsSquareAttacked(board, sq, false);
        public static bool IsAttackedByBlack(Board board, int sq) => IsSquareAttacked(board, sq, true);
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
        public static bool IsSquareAttacked(Board board, int square, bool isWhiteDefend)
        {

            // PAWN ATTACKS
            if (isWhiteDefend)
            {
                // We are defending white's square → check if black can attack it (with -7 and -9)
                if ((square % 8 > 0 && ((1UL << (square + 9)) & board.bitboards[(int)Piece.BlackPawn]) != 0) || // capture left
                    (square % 8 < 7 && ((1UL << (square + 7)) & board.bitboards[(int)Piece.BlackPawn]) != 0))   // capture right
                    return true;
            }
            else
            {
                // We are defending black's square → check if white can attack it (with +7 and +9)
                if ((square % 8 > 0 && ((1UL << (square - 7)) & board.bitboards[(int)Piece.WhitePawn]) != 0) || // capture left
                    (square % 8 < 7 && ((1UL << (square - 9)) & board.bitboards[(int)Piece.WhitePawn]) != 0))   // capture right
                    return true;
            }

            // KNIGHT ATTACKS
            ulong knights = isWhiteDefend ? board.bitboards[(int)Piece.BlackKnight] : board.bitboards[(int)Piece.WhiteKnight];
            if ((knights & MoveTables.KnightMoves[square]) != 0)
                return true;

            // KING ATTACKS
            ulong kings = isWhiteDefend ? board.bitboards[(int)Piece.BlackKing] : board.bitboards[(int)Piece.WhiteKing];
            if ((kings & MoveTables.KingMoves[square]) != 0)
                return true;

            // ROOK/QUEEN RAYS
            if (VertAtkCheck(square, isWhiteDefend, board))
            {
                return true;
            }
            // BISHOP/QUEEN RAYS
            if (DiagAtkCheck(square, isWhiteDefend, board))
            {
                return true;
            }
            return false;
        }
       
        private static bool IsEdgeWrap(int from, int to, int dir)
        {
            int fromFile = from % 8;
            int toFile = to % 8;

            return dir switch
            {
                1 or -1 => Math.Abs(toFile - fromFile) != 1,
                9 or -7 => toFile != fromFile + 1,
                7 or -9 => toFile != fromFile - 1,
                _ => false,
            };
        }
        private static ulong RookAttacks(
            int square,
            ulong friendly,
            ulong enemy,
            bool isWhite,
            Board board,
            bool isQueen,
            List<Move> moves
        )
        {
            ulong movesBit = 0UL;
            int[] directions = { 8, -8, 1, -1 };
            Piece movePiece = isWhite
                ? (isQueen ? Piece.WhiteQueen : Piece.WhiteRook)
                : (isQueen ? Piece.BlackQueen : Piece.BlackRook);

            // For each orthogonal direction…
            foreach (int dir in directions)
            {
                int current = square;
                while (true)
                {
                    int target = current + dir;

                    // 1) Off‐board?
                    if (target < 0 || target >= 64)
                        break;

                    // 2) Edge‐wrap between 'current' and 'target'?
                    if (IsEdgeWrap(current, target, dir))
                        break;

                    ulong toBB = 1UL << target;

                    // 3) Friendly piece blocking
                    if ((toBB & friendly) != 0UL)
                        break;

                    // 4) Capture
                    if ((toBB & enemy) != 0UL)
                    {
                        Piece capturedPiece = FindCapturedPiece(board, (Square)target, isWhite);
                        moves.Add(new Move((Square)square, (Square)target, movePiece, capturedPiece, MoveFlags.Capture));
                        break;
                    }

                    // 5) Non-capture: record the bit
                    movesBit |= toBB;

                    // 6) Advance one step along this ray
                    current = target;
                }
            }

            return movesBit;
        }

        private static ulong BishopAttacks(
            int square,
            ulong friendly,
            ulong enemy,
            bool isWhite,
            Board board,
            bool isQueen,
            List<Move> moves
        )
        {
            ulong movesBit = 0UL;
            int[] directions = { 9, 7, -9, -7 };
            Piece movePiece = isWhite
                ? (isQueen ? Piece.WhiteQueen : Piece.WhiteBishop)
                : (isQueen ? Piece.BlackQueen : Piece.BlackBishop);

            // For each diagonal direction…
            foreach (int dir in directions)
            {
                int current = square;
                while (true)
                {
                    int target = current + dir;

                    // 1) Off-board?
                    if (target < 0 || target >= 64)
                        break;

                    // 2) Edge-wrap between 'current' and 'target'?
                    if (IsEdgeWrap(current, target, dir))
                        break;

                    ulong toBB = 1UL << target;

                    // 3) Friendly piece blocking
                    if ((toBB & friendly) != 0UL)
                        break;

                    // 4) Capture
                    if ((toBB & enemy) != 0UL)
                    {
                        Piece capturedPiece = FindCapturedPiece(board, (Square)target, isWhite);
                        moves.Add(new Move((Square)square, (Square)target, movePiece, capturedPiece, MoveFlags.Capture));
                        break;
                    }

                    // 5) Non-capture: record the bit
                    movesBit |= toBB;

                    // 6) Advance one step along this ray
                    current = target;
                }
            }

            return movesBit;
        }
        private static ulong QueenAttacks(int square, ulong friendly, ulong enemy, bool isWhite, Board board, List<Move> moves)
        {
            return BishopAttacks(square, friendly, enemy, isWhite, board, true, moves) |
                   RookAttacks(square, friendly, enemy, isWhite, board, true, moves);
        }
        private static void GenerateSliderMoves(Board board, List<Move> moves, bool isWhite)
        {
            ulong bishop, rook, queen, friendly, enemy;
            Piece bPiece, rPiece, qPiece;
            if (isWhite)
            {
                bishop = board.bitboards[(int)Piece.WhiteBishop];
                rook = board.bitboards[(int)Piece.WhiteRook];
                queen = board.bitboards[(int)Piece.WhiteQueen];
                friendly = board.occupancies[(int)Color.White];
                enemy = board.occupancies[(int)Color.Black];
                bPiece = Piece.WhiteBishop;
                rPiece = Piece.WhiteRook;
                qPiece = Piece.WhiteQueen;
            }
            else
            {
                bishop = board.bitboards[(int)Piece.BlackBishop];
                rook = board.bitboards[(int)Piece.BlackRook];
                queen = board.bitboards[(int)Piece.BlackQueen];
                friendly = board.occupancies[(int)Color.Black];
                enemy = board.occupancies[(int)Color.White];
                bPiece = Piece.BlackBishop;
                rPiece = Piece.BlackRook;
                qPiece = Piece.BlackQueen;
            }

            ulong bit = 1UL;
            for (int i = 0; i < 64; i++)
            {
                if ((bit & bishop) != 0)
                {
                    ulong attacks = BishopAttacks(i, friendly, enemy, isWhite, board, false, moves);
                    for (int j = 0; j < 64; j++)
                    {
                        if ((attacks & (1UL << j)) != 0)
                        {
                            Move move = new((Square)i, (Square)j, bPiece);
                            moves.Add(move);
                        }
                    }
                } else if ((bit & rook) != 0)
                {
                    ulong attacks = RookAttacks(i, friendly, enemy, isWhite, board, false, moves);
                    for (int j = 0; j < 64; j++)
                    {
                        if ((attacks & (1UL << j)) != 0)
                        {
                            Move move = new((Square)i, (Square)j, rPiece);
                            moves.Add(move);
                        }
                    }
                } else if ((bit & queen) != 0)
                {
                    ulong attacks = QueenAttacks(i, friendly, enemy, isWhite, board, moves);
                    for (int j = 0; j < 64; j++)
                    {
                        if ((attacks & (1UL << j)) != 0)
                        {
                            Move move = new((Square)i, (Square)j, qPiece);
                            moves.Add(move);
                        }
                    }
                }
                bit <<= 1;
            }
        }
        private static void GenerateKingMoves(Board board, List<Move> moves, bool isWhite)
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
                            Piece capturedPiece = FindCapturedPiece(board, (Square)j, isWhite);
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
        private static void GenerateKnightMoves(Board board, List<Move> moves, bool isWhite)
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
                            Piece capturedPiece = FindCapturedPiece(board, (Square)j, isWhite);
                            Move move = new((Square)i, (Square)j, MovePieceN, capturedPiece);
                            moves.Add(move);
                        }
                    }
                }
                bit <<= 1;
            }
        }
        private static void GeneratePawnMoves(Board board, List<Move> moves, bool isWhite)
        {
            if(board.enPassantSquare != Square.None) EnPassantAdd(board, moves, isWhite);

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
        private static void GenerateWhitePawn(ulong pawns, ulong blackEnemy, ulong whiteFriendly, List<Move> moves, Board board)
        {
            ulong bit = 1UL;
            for (int i = 0; i < 64; i++) { 
                if ((bit & pawns) != 0)
                {
                    WPawnAtks(i, blackEnemy, whiteFriendly, moves, board);
                }
                bit <<= 1;
            }
        }
        private static void GenerateBlackPawn(ulong pawns, ulong whiteEnemy, ulong blackFriendly, List<Move> moves, Board board)
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
            if (isWhite) {
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
                if ((board.bitboards[(int)Piece.WhitePawn] & 1UL << (enPassantSq - 1) ) != 0)
                {
                    moves.Add(new((Square)(enPassantSq - 1), (Square)(enPassantSq + 8), Piece.WhitePawn, Piece.BlackPawn, MoveFlags.EnPassant));
                }
            } 
            if (file < 7)
            {
                // not on file H
                // <-
                //right to center en passant, shift left / up a digit to find if there's a pawn on right side
                if ((board.bitboards[(int)Piece.WhitePawn] & 1UL << (enPassantSq + 1)) != 0)
                {
                    moves.Add(new((Square)(enPassantSq + 1), (Square)(enPassantSq + 8), Piece.WhitePawn, Piece.BlackPawn, MoveFlags.EnPassant));
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
                if ((board.bitboards[(int)Piece.BlackPawn] & (1UL << (enPassantSq - 1))) != 0)
                {
                    moves.Add(new((Square)(enPassantSq - 1), (Square)(enPassantSq - 8), Piece.BlackPawn, Piece.WhitePawn, MoveFlags.EnPassant));
                }
            }
            if (file < 7)
            {
                // not on file H
                // right to center en passant, shift left / down a digit
                if ((board.bitboards[(int)Piece.BlackPawn] & (1UL << (enPassantSq + 1))) != 0)
                {
                    moves.Add(new((Square)(enPassantSq + 1), (Square)(enPassantSq - 8), Piece.BlackPawn, Piece.WhitePawn, MoveFlags.EnPassant));
                }
            }
        }
        private static void WPawnAtks(int sq, ulong enemy, ulong friendly, List<Move> moves, Board board)
        {
            bool on7th = sq >= 48 && sq <= 55; // white
            bool on2nd = sq >= 8 && sq <= 15;  // white
            
            if (on7th && (((1UL << (sq + 8)) & (enemy | friendly)) == 0)){
                //promotion push
                moves.Add(new((Square)sq, (Square)(sq + 8), Piece.WhitePawn, Piece.None, MoveFlags.Promotion, Piece.WhiteQueen));
                moves.Add(new((Square)sq, (Square)(sq + 8), Piece.WhitePawn, Piece.None, MoveFlags.Promotion, Piece.WhiteRook));
                moves.Add(new((Square)sq, (Square)(sq + 8), Piece.WhitePawn, Piece.None, MoveFlags.Promotion, Piece.WhiteKnight));
                moves.Add(new((Square)sq, (Square)(sq + 8), Piece.WhitePawn, Piece.None, MoveFlags.Promotion, Piece.WhiteBishop));
            }
            else if (((1UL << (sq + 8)) & (enemy | friendly)) == 0)
            { 
                //single push
                moves.Add(new((Square)sq, (Square)(sq+8), Piece.WhitePawn));

                if (on2nd && (((1UL << (sq + 16)) & (enemy | friendly)) == 0))
                {
                    //double push
                    board.enPassantSquare = (Square)(sq + 16); // Set en passant square
                    moves.Add(new((Square)sq, (Square)(sq + 16), Piece.WhitePawn));
                }
            }
            if (((1UL << sq) & FileH) == 0 && ((1UL << (sq+9)) & enemy) != 0)
            {
                Piece tookPiece = FindCapturedPiece(board, (Square)(sq + 9), true);
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
                Piece tookPiece = FindCapturedPiece(board, (Square)(sq + 7), true);
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
                    board.enPassantSquare = (Square)(sq - 16); // Set en passant square
                    moves.Add(new((Square)sq, (Square)(sq - 16), Piece.BlackPawn));
                }
            }

            // Capture right (->)
            if (((1UL << sq) & FileH) == 0 && ((1UL << (sq - 7)) & enemy) != 0)
            {
                Piece tookPiece = FindCapturedPiece(board, (Square)(sq - 7), false);
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
                Piece tookPiece = FindCapturedPiece(board, (Square)(sq - 9), false);
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
        private static bool DiagAtkCheck(int square, bool isWhiteDefend, Board board)
        {
            int[] directions = { 9, 7, -9, -7 };
            Piece atkPieceB = isWhiteDefend ? Piece.BlackBishop : Piece.WhiteBishop;
            Piece atkPieceQ = isWhiteDefend ? Piece.BlackQueen : Piece.WhiteQueen;
            ulong allOccupancy = board.occupancies[(int)Color.White] | board.occupancies[(int)Color.Black];

            foreach (int dir in directions)
            {
                int current = square;                // ① Start from the original square
                while (true)
                {
                    int target = current + dir;      // ② Move one step along the ray
                    if (target < 0 || target >= 64
                        || IsEdgeWrap(current, target, dir)) // ③ Check wrap against “current,” not “square”
                    {
                        break;
                    }

                    ulong toBB = 1UL << target;
                    // ④ If a bishop or queen is on target, it attacks “square”
                    if ((toBB & (board.bitboards[(int)atkPieceB] | board.bitboards[(int)atkPieceQ])) != 0)
                        return true;

                    // ⑤ If anything (friendly or enemy) blocks, stop this ray
                    if ((toBB & allOccupancy) != 0)
                        break;

                    current = target;  // ⑥ Advance “current” and continue
                }
            }

            return false;
        }

        private static bool VertAtkCheck(int square, bool isWhiteDefend, Board board)
        {
            int[] directions = { 8, -8, 1, -1 };
            Piece atkPieceR = isWhiteDefend ? Piece.BlackRook : Piece.WhiteRook;
            Piece atkPieceQ = isWhiteDefend ? Piece.BlackQueen : Piece.WhiteQueen;
            ulong allOccupancy = board.occupancies[(int)Color.White] | board.occupancies[(int)Color.Black];

            foreach (int dir in directions)
            {
                int current = square;                // ① Start from the original square
                while (true)
                {
                    int target = current + dir;      // ② Move one step along the ray
                    if (target < 0 || target >= 64
                        || IsEdgeWrap(current, target, dir)) // ③ Check wrap against “current”
                    {
                        break;
                    }

                    ulong toBB = 1UL << target;
                    // ④ If a rook or queen is on target, it attacks “square”
                    if ((toBB & (board.bitboards[(int)atkPieceR] | board.bitboards[(int)atkPieceQ])) != 0)
                        return true;

                    // ⑤ If anything blocks, stop this ray
                    if ((toBB & allOccupancy) != 0)
                        break;

                    current = target; // ⑥ Advance “current”
                }
            }

            return false;
        }

        private static void GenerateCastles(Board board, List<Move> moves, bool isWhite)
        {
            int kingFrom = GetKingSquare(board, isWhite);
            var rights = board.castlingRights;

            if (isWhite)
            {
                // King-side
                if (rights.HasFlag(Castling.WhiteKing))
                {
                    // squares f1(5), g1(6) must be empty
                    if (((board.occupancies[2] >> 5) & 1UL) == 0
                     && ((board.occupancies[2] >> 6) & 1UL) == 0
                     // f1 and g1 not attacked
                     && !IsSquareAttacked(board, 5, true)
                     && !IsSquareAttacked(board, 6, true))
                    {
                        moves.Add(new(
                            (Square)kingFrom,         // e1
                            Square.G1,                // g1
                            Piece.WhiteKing,
                            Piece.None,
                            MoveFlags.Castling,
                            Piece.None));
                    }
                }
                // Queen-side
                if (rights.HasFlag(Castling.WhiteQueen))
                {
                    // squares d1(3), c1(2), b1(1) must be empty (b1 only needs empty, not attacked)
                    if (((board.occupancies[2] >> 3) & 1UL) == 0
                     && ((board.occupancies[2] >> 2) & 1UL) == 0
                     && ((board.occupancies[2] >> 1) & 1UL) == 0
                     // d1 and c1 not attacked
                     && !IsSquareAttacked(board, 3, true)
                     && !IsSquareAttacked(board, 2, true))
                    {
                        moves.Add(new(
                            (Square)kingFrom,         // e1
                            Square.C1,                // c1
                            Piece.WhiteKing,
                            Piece.None,
                            MoveFlags.Castling,
                            Piece.None));
                    }
                }
            }
            else
            {
                // Black king-side (e8->g8)
                if (rights.HasFlag(Castling.BlackKing))
                {
                    if (((board.occupancies[2] >> 61) & 1UL) == 0  // f8 = 61
                     && ((board.occupancies[2] >> 62) & 1UL) == 0  // g8 = 62
                     && !IsSquareAttacked(board, 61, false)
                     && !IsSquareAttacked(board, 62, false))
                    {
                        moves.Add(new(
                            (Square)kingFrom,         // e8
                            Square.G8,
                            Piece.BlackKing,
                            Piece.None,
                            MoveFlags.Castling,
                            Piece.None));
                    }
                }
                // Black queen-side (e8->c8)
                if (rights.HasFlag(Castling.BlackQueen))
                {
                    if (((board.occupancies[2] >> 59) & 1UL) == 0  // d8 = 59
                     && ((board.occupancies[2] >> 58) & 1UL) == 0  // c8 = 58
                     && ((board.occupancies[2] >> 57) & 1UL) == 0  // b8 = 57
                     && !IsSquareAttacked(board, 59, false)
                     && !IsSquareAttacked(board, 58, false))
                    {
                        moves.Add(new(
                            (Square)kingFrom,
                            Square.C8,
                            Piece.BlackKing,
                            Piece.None,
                            MoveFlags.Castling,
                            Piece.None));
                    }
                }
            }
        }
            
    }
}