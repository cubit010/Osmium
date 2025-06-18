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
			 0,  0,  0,  0,  0,  0,  0,  0
        };
		public static readonly int[] WQueenBonus =
		{
			-20,-10,-10, -5, -5,-10,-10,-20,
			-10,  0,  5,  0,  0,  0,  0,-10,
			-10,  5,  5,  5,  5,  5,  0,-10,
			  0,  0,  5,  5,  5,  5,  0, -5,
			 -5,  0,  5,  5,  5,  5,  0, -5,
			-10,  0,  5,  5,  5,  5,  0,-10,
			-10,  0,  0,  0,  0,  0,  0,-10,
			-20,-10,-10, -5, -5,-10,-10,-20
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
			-30, -40, -40, -50, -50, -40, -40, -30
		};

        public static int EvalBoard(Board board, bool isWhite)
        {
            int score = 0;

            // Use ternary operators to reduce branching and improve readability
            ulong fP = board.bitboards[(int)(isWhite ? Piece.WhitePawn : Piece.BlackPawn)];
            ulong fN = board.bitboards[(int)(isWhite ? Piece.WhiteKnight : Piece.BlackKnight)];
            ulong fB = board.bitboards[(int)(isWhite ? Piece.WhiteBishop : Piece.BlackBishop)];
            ulong fR = board.bitboards[(int)(isWhite ? Piece.WhiteRook : Piece.BlackRook)];
            ulong fQ = board.bitboards[(int)(isWhite ? Piece.WhiteQueen : Piece.BlackQueen)];
            ulong fK = board.bitboards[(int)(isWhite ? Piece.WhiteKing : Piece.BlackKing)];

            ulong eP = board.bitboards[(int)(isWhite ? Piece.BlackPawn : Piece.WhitePawn)];
            ulong eN = board.bitboards[(int)(isWhite ? Piece.BlackKnight : Piece.WhiteKnight)];
            ulong eB = board.bitboards[(int)(isWhite ? Piece.BlackBishop : Piece.WhiteBishop)];
            ulong eR = board.bitboards[(int)(isWhite ? Piece.BlackRook : Piece.WhiteRook)];
            ulong eQ = board.bitboards[(int)(isWhite ? Piece.BlackQueen : Piece.WhiteQueen)];
            ulong eK = board.bitboards[(int)(isWhite ? Piece.BlackKing : Piece.WhiteKing)];

            // Inline method calls to reduce overhead
            score += evalMaterials(fP, fN, fB, fR, fQ, eP, eN, eB, eR, eQ)
                   + EvalKnight(fN, eN, isWhite)
                   + EvalPawn(fP, eP, isWhite)
                   + EvalKing(fK, eK, isWhite)
                   + EvalBishop(fB, eB, isWhite)
                   + EvalQueen(fQ, eQ, isWhite)
                   + EvalRook(fR, eR, isWhite);

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
		private static int EvalPawn(ulong fP, ulong eP, bool isWhite) => EvalPiece(fP, eP, isWhite, WPawnBonus);
		private static int EvalKnight(ulong fN, ulong eN, bool isWhite) => EvalPiece(fN, eN, isWhite, WKnightBonus);
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
			}
			else
			{
				wKsq = BitOperations.TrailingZeroCount(eK); bKsq = BitOperations.TrailingZeroCount(fK);
				score -= WKingBonus[wKsq];
				score += WKingBonus[Mirror64(bKsq)];
			}
			return score;
		}
		private static int EvalBishop(ulong fB, ulong eB, bool isWhite) => EvalPiece(fB, eB, isWhite, WBishopBonus);
		private static int EvalQueen(ulong fQ, ulong eQ, bool isWhite) => EvalPiece(fQ, eQ, isWhite, WQueenBonus);
		private static int EvalRook(ulong fR, ulong eR, bool isWhite) => EvalPiece(fR, eR, isWhite, WRookBonus);
        private static int EvalPiece(ulong f, ulong e, bool isWhite, int[] bonusTable)
        {
            int score = 0;
            // Evaluate friendly pieces
            while (f != 0)
            {
                int sq = BitOperations.TrailingZeroCount(f);
                score += isWhite ? bonusTable[sq] : -bonusTable[sq];
                f &= f - 1; // Clear least significant bit
            }
            // Evaluate enemy pieces (mirrored)
            while (e != 0)
            {
                int sq = BitOperations.TrailingZeroCount(e);
                int mirrored = Mirror64(sq);
                score += isWhite ? -bonusTable[mirrored] : bonusTable[mirrored];
                e &= e - 1; // Clear least significant bit
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
