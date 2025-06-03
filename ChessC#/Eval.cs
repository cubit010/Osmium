using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace ChessC_
{
	internal class Eval
	{
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
		public static readonly int[] pieceValues =
		{
	    // P, N, B, R, Q,
			100,  320,  330,  500,  900,
		   -100, -320, -330, -500, -900
		};

		public static readonly int[] WPawnBonus =
		{
			0,  0,  0,  0,  0,  0,  0,  0,
			5, 10, 10,-20,-20, 10, 10,  5,
			5, -5,-10,  0,  0,-10, -5,  5,
			0,  0,  0, 20, 20,  0,  0,  0,
			5,  5, 10, 25, 25, 10,  5,  5,
			10, 10, 20, 30, 30, 20, 10, 10,
			50, 50, 50, 50, 50, 50, 50, 50,
			0,  0,  0,  0,  0,  0,  0,  0   
		};
		public static readonly int[] WKnightBonus =
		{
            -50,-40,-30,-30,-30,-30,-40,-50,
			-40,-20,  0,  5,  5,  0,-20,-40,
			-30,  5, 10, 15, 15, 10,  5,-30,
			-30,  0, 15, 20, 20, 15,  0,-30,
			-30,  5, 15, 20, 20, 15,  5,-30,
			-30,  0, 10, 15, 15, 10,  0,-30,
			-40,-20,  0,  0,  0,  0,-20,-40,
			-50,-40,-30,-30,-30,-30,-40,-50
        };
		public static readonly int[] WBishopBonus =
		{
            -20,-10,-10,-10,-10,-10,-10,-20,
			-10,  5,  0,  0,  0,  0,  5,-10,
			-10, 10, 10, 10, 10, 10, 10,-10,
			-10,  0, 10, 10, 10, 10,  0,-10,
			-10,  5,  5, 10, 10,  5,  5,-10,
			-10,  0,  5, 10, 10,  5,  0,-10,
			-10,  0,  0,  0,  0,  0,  0,-10,
			-20,-10,-10,-10,-10,-10,-10,-20

        };
		public static readonly int[] WRookBonus = {
             0,  0,  5,  10, 10, 5,  0,  0,
			-5,  0,  0,  0,  0,  0,  0, -5,
			-5,  0,  0,  0,  0,  0,  0, -5,
			-5,  0,  0,  0,  0,  0,  0, -5,
			-5,  0,  0,  0,  0,  0,  0, -5,
			-5,  0,  0,  0,  0,  0,  0, -5,
			 5,  10, 10, 10, 10, 10, 10, 5,
			 0,  0,  0,  0,  0,  0,  0,  0,
        };
		public static readonly int[] WQueenBonus =
		{
			-20,-10,-10, -5, -5,-10,-10,-20
			-10,  0,  5,  0,  0,  0,  0,-10,
			-10,  5,  5,  5,  5,  5,  0,-10,
			  0,  0,  5,  5,  5,  5,  0, -5,
			 -5,  0,  5,  5,  5,  5,  0, -5,
			-10,  0,  5,  5,  5,  5,  0,-10,
			-10,  0,  0,  0,  0,  0,  0,-10,
			-20,-10,-10, -5, -5,-10,-10,-20,
		};
		public static readonly int[] WKingBonus =
		{
			 20,  30,  10,   0,   0,  10,  30,  20,
			 20,  20,   0,   0,   0,   0,  20,  20,
			-10, -20, -20, -20, -20, -20, -20, -10,
			-20, -30, -30, -40, -40, -30, -30, -20,
			-30, -40, -40, -50, -50, -40, -40, -30,
			-30, -40, -40, -50, -50, -40, -40, -30,
			-30, -40, -40, -50, -50, -40, -40, -30,
			-30, -40, -40, -50, -50, -40, -40, -30,
		};

        public static int EvalBoard(Board board, bool isWhite)
		{
			ulong fP, fN, fB, fR, fQ, fK;
			ulong eP, eN, eB, eR, eQ, eK;
			int score = 0;

			if (isWhite)
			{
				fP = board.bitboards[(int)Piece.WhitePawn];
				fN = board.bitboards[(int)Piece.WhiteKnight];
				fB = board.bitboards[(int)Piece.WhiteBishop];
				fR = board.bitboards[(int)Piece.WhiteRook];
				fQ = board.bitboards[(int)Piece.WhiteQueen];
				fK = board.bitboards[(int)Piece.WhiteKing];

				eP = board.bitboards[(int)Piece.BlackPawn];
				eN = board.bitboards[(int)Piece.BlackKnight];
				eB = board.bitboards[(int)Piece.BlackBishop];
				eR = board.bitboards[(int)Piece.BlackRook];
				eQ = board.bitboards[(int)Piece.BlackQueen];
				eK = board.bitboards[(int)Piece.BlackKing];
			}
			else
			{
				fP = board.bitboards[(int)Piece.BlackPawn];
				fN = board.bitboards[(int)Piece.BlackKnight];
				fB = board.bitboards[(int)Piece.BlackBishop];
				fR = board.bitboards[(int)Piece.BlackRook];
				fQ = board.bitboards[(int)Piece.BlackQueen];
				fK = board.bitboards[(int)Piece.BlackKing];

				eP = board.bitboards[(int)Piece.WhitePawn];
				eN = board.bitboards[(int)Piece.WhiteKnight];
				eB = board.bitboards[(int)Piece.WhiteBishop];
				eR = board.bitboards[(int)Piece.WhiteRook];
				eQ = board.bitboards[(int)Piece.WhiteQueen];
				eK = board.bitboards[(int)Piece.WhiteKing];
			}
			score += evalMaterials(fP, fN, fB, fR, fQ, eP, eN, eB, eR, eQ);
			score += EvalKnight(fN, eN, isWhite);
			score += EvalPawn(fP, eP, isWhite);
			score += EvalKing(fK, eK, isWhite);
            score += EvalBishop(fB, eB, isWhite);
            score += EvalQueen(fQ, eQ, isWhite);
			score += EvalRook(fR, eR, isWhite);
            return score;
		}
		private static int evalMaterials(
			ulong fP, ulong fN, ulong fB, ulong fR, ulong fQ,
			ulong eP, ulong eN, ulong eB, ulong eR, ulong eQ
		)
		{
			int score = 0;
			score += Bitboard.PopCount(fP) * pieceValues[0];
			score += Bitboard.PopCount(fN) * pieceValues[1];
			score += Bitboard.PopCount(fB) * pieceValues[2];
			score += Bitboard.PopCount(fR) * pieceValues[3];
			score += Bitboard.PopCount(fQ) * pieceValues[4];

			score += Bitboard.PopCount(eP) * pieceValues[5];
			score += Bitboard.PopCount(eN) * pieceValues[6];
			score += Bitboard.PopCount(eB) * pieceValues[7];
			score += Bitboard.PopCount(eR) * pieceValues[8];
			score += Bitboard.PopCount(eQ) * pieceValues[9];

			return score;
		}
		private static int EvalPawn(ulong fP, ulong eP, bool isWhite)
		{
			ulong wP = isWhite ? fP : eP; // White pawns
            ulong bP = isWhite ? eP : fP; // Black pawns
            int score = 0;
			if (isWhite)
			{
				for (int i = 0; i < 64; i++)
				{
					if ((wP & (1UL << i)) != 0)
					{
						score += WPawnBonus[i];
					}
					if ((bP & (1UL << i)) != 0)
					{
						score -= WPawnBonus[Mirror64(i)]; // Mirror for opponent
					}
				}
			} else
			{
                for (int i = 0; i < 64; i++)
                {
                    if ((wP & (1UL << i)) != 0)
                    {
                        score -= WPawnBonus[i]; 
                    }
                    if ((bP & (1UL << i)) != 0)
                    {
                        score += WPawnBonus[Mirror64(i)];
                    }
                }
            }
				return score;
		}
		private static int EvalKnight(ulong fN, ulong eN, bool isWhite)
		{
            ulong wN = isWhite ? fN : eN; // White pawns
            ulong bN = isWhite ? eN : fN; // Black pawns
            int score = 0;
            if (isWhite)
            {
                for (int i = 0; i < 64; i++)
                {
                    if ((wN & (1UL << i)) != 0)
                    {
                        score += WKnightBonus[i];
                    }
                    if ((bN & (1UL << i)) != 0)
                    {
                        score -= WKnightBonus[Mirror64(i)]; // Mirror for opponent
                    }
                }
            }
            else
            {
                for (int i = 0; i < 64; i++)
                {
                    if ((wN & (1UL << i)) != 0)
                    {
                        score -= WKnightBonus[i];
                    }
                    if ((bN & (1UL << i)) != 0)
                    {
                        score += WKnightBonus[Mirror64(i)];
                    }
                }
            }
            return score;
        }
		private static int EvalKing(ulong fK, ulong eK, bool isWhite)
		{
			int score = 0;
			int i = 0;
			int wKsq, bKsq;
			if (isWhite)
			{
				wKsq = BitOperations.TrailingZeroCount(fK); bKsq = BitOperations.TrailingZeroCount(eK);
				score += WKingBonus[wKsq];
				score -= WKingBonus[Mirror64(bKsq)];
			} else
			{
                wKsq = BitOperations.TrailingZeroCount(eK); bKsq = BitOperations.TrailingZeroCount(fK);
				score -= WKingBonus[wKsq];
				score += WKingBonus[Mirror64(bKsq)];
            }
            return score;
        }
        private static int EvalBishop(ulong fB, ulong eB, bool isWhite)
        {
            ulong wB = isWhite ? fB : eB; // White bishops
            ulong bB = isWhite ? eB : fB; // Black bishops
            int score = 0;
            if (isWhite)
            {
                for (int i = 0; i < 64; i++)
                {
                    if ((wB & (1UL << i)) != 0)
                    {
                        score += WBishopBonus[i];
                    }
                    if ((bB & (1UL << i)) != 0)
                    {
                        score -= WBishopBonus[Mirror64(i)]; // Mirror for opponent
                    }
                }
            }
            else
            {
                for (int i = 0; i < 64; i++)
                {
                    if ((wB & (1UL << i)) != 0)
                    {
                        score -= WBishopBonus[i];
                    }
                    if ((bB & (1UL << i)) != 0)
                    {
                        score += WBishopBonus[Mirror64(i)];
                    }
                }
            }
            return score;
        }
		private static int EvalQueen(ulong fQ, ulong eQ, bool isWhite)
		{
            ulong wQ = isWhite ? fQ : eQ; // White queens
            ulong bQ = isWhite ? eQ : fQ; // Black queens
            int score = 0;
            if (isWhite)
            {
                for (int i = 0; i < 64; i++)
                {
                    if ((wQ & (1UL << i)) != 0)
                    {
                        score += WQueenBonus[i];
                    }
                    if ((bQ & (1UL << i)) != 0)
                    {
                        score -= WQueenBonus[Mirror64(i)]; // Mirror for opponent
                    }
                }
            }
            else
            {
                for (int i = 0; i < 64; i++)
                {
                    if ((wQ & (1UL << i)) != 0)
                    {
                        score -= WQueenBonus[i];
                    }
                    if ((bQ & (1UL << i)) != 0)
                    {
                        score += WQueenBonus[Mirror64(i)];
                    }
                }
            }
            return score;
        }
		private static int EvalRook(ulong fR, ulong eR, bool isWhite)
		{
            ulong wR = isWhite ? fR : eR; // White rooks
            ulong bR = isWhite ? eR : fR; // Black rooks
            int score = 0;
            if (isWhite)
            {
                for (int i = 0; i < 64; i++)
                {
                    if ((wR & (1UL << i)) != 0)
                    {
                        score += WRookBonus[i];
                    }
                    if ((bR & (1UL << i)) != 0)
                    {
                        score -= WRookBonus[Mirror64(i)]; // Mirror for opponent
                    }
                }
            }
            else
            {
                for (int i = 0; i < 64; i++)
                {
                    if ((wR & (1UL << i)) != 0)
                    {
                        score -= WRookBonus[i];
                    }
                    if ((bR & (1UL << i)) != 0)
                    {
                        score += WRookBonus[Mirror64(i)];
                    }
                }
            }
            return score;
        }
        static int Mirror64(int sq)
        {
            // sq is 0..63 with 0=a1, 1=b1, … 7=h1, 8=a2, …, 63=h8.
            // We want to flip rank 1<->8, 2<->7, … so:
            int file = sq % 8;
            int rank = sq / 8;           // 0..7 for ranks 1..8
            int flippedRank = 7 - rank;  // 7->0, 6->1, …, 0->7
            return flippedRank * 8 + file;
        }
    }
}
