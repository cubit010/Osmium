
using System.Numerics;
using System.Runtime.CompilerServices;

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
        public static readonly ulong[] FileMasks = 
        {
            0x0101010101010101,
            0x0202020202020202,
            0x0404040404040404,
            0x0808080808080808,
            0x1010101010101010,
            0x2020202020202020,
            0x4040404040404040,
            0x8080808080808080
        };

        public static readonly int[] pieceValues =
		{
	    // P, N, B, R, Q,
			100,  320,  330,  500,  900,
		   -100, -320, -330, -500, -900
		};

        // Pawn PST: [phase, square]
        public static readonly int[,] WPawnPST = new int[3, 64]
        {
            // Opening
            {
                0,  0,  0,  0,  0,  0,  0,  0,
                5, 10, 10,-20,-20, 10, 10,  5,
                5, -5,-10,  0,  0,-10, -5,  5,
                0,  0,  0, 20, 20,  0,  0,  0,
                5,  5, 10, 25, 25, 10,  5,  5,
                10, 10, 20, 30, 30, 20, 10, 10,
                50, 50, 50, 50, 50, 50, 50, 50,
                0,  0,  0,  0,  0,  0,  0,  0
            },
            // Middlegame
            {
                0,  0,  0,  0,  0,  0,  0,  0,
                5, 10, 10,-20,-20, 10, 10,  5,
                5, -5,-10,  0,  0,-10, -5,  5,
                0,  0,  0, 20, 20,  0,  0,  0,
                5,  5, 10, 25, 25, 10,  5,  5,
                10, 10, 20, 30, 30, 20, 10, 10,
                50, 50, 50, 50, 50, 50, 50, 50,
                0,  0,  0,  0,  0,  0,  0,  0
            },
            // Endgame
            {
                0,  0,  0,  0,  0,  0,  0,  0,
                10, 10, 10, 10, 10, 10, 10, 10,
                5,  5, 10, 20, 20, 10,  5,  5,
                0,  0,  0, 20, 20,  0,  0,  0,
                5,  5, 10, 25, 25, 10,  5,  5,
                10, 10, 20, 30, 30, 20, 10, 10,
                50, 50, 50, 50, 50, 50, 50, 50,
                0,  0,  0,  0,  0,  0,  0,  0
            }
        };

        // Knight PST
        public static readonly int[,] WKnightPST = new int[3, 64]
        {
            // Opening
            {
                -50,-40,-30,-30,-30,-30,-40,-50,
                -40,-20,  0,  5,  5,  0,-20,-40,
                -30,  5, 10, 15, 15, 10,  5,-30,
                -30,  0, 15, 20, 20, 15,  0,-30,
                -30,  5, 15, 20, 20, 15,  5,-30,
                -30,  0, 10, 15, 15, 10,  0,-30,
                -40,-20,  0,  0,  0,  0,-20,-40,
                -50,-40,-30,-30,-30,-30,-40,-50
            },
            // Middlegame
            {
                -50,-40,-30,-30,-30,-30,-40,-50,
                -40,-20,  0,  5,  5,  0,-20,-40,
                -30,  5, 10, 15, 15, 10,  5,-30,
                -30,  0, 15, 20, 20, 15,  0,-30,
                -30,  5, 15, 20, 20, 15,  5,-30,
                -30,  0, 10, 15, 15, 10,  0,-30,
                -40,-20,  0,  0,  0,  0,-20,-40,
                -50,-40,-30,-30,-30,-30,-40,-50
            },
            // Endgame
            {
                -40,-20,-10,-10,-10,-10,-20,-40,
                -20,  0,  5, 10, 10,  5,  0,-20,
                -10,  5, 10, 15, 15, 10,  5,-10,
                -10, 10, 15, 20, 20, 15, 10,-10,
                -10,  5, 15, 20, 20, 15,  5,-10,
                -10,  0, 10, 15, 15, 10,  0,-10,
                -20,  0,  0,  0,  0,  0,  0,-20,
                -40,-20,-10,-10,-10,-10,-20,-40
            }
        };

        // Bishop PST
        public static readonly int[,] WBishopPST = new int[3, 64]
        {
            // Opening
            {
                -20,-10,-10,-10,-10,-10,-10,-20,
                -10,  5,  0,  0,  0,  0,  5,-10,
                -10, 10, 10, 10, 10, 10, 10,-10,
                -10,  0, 10, 10, 10, 10,  0,-10,
                -10,  5,  5, 10, 10,  5,  5,-10,
                -10,  0,  5, 10, 10,  5,  0,-10,
                -10,  0,  0,  0,  0,  0,  0,-10,
                -20,-10,-10,-10,-10,-10,-10,-20
            },
            // Middlegame
            {
                -20,-10,-10,-10,-10,-10,-10,-20,
                -10,  5,  0,  0,  0,  0,  5,-10,
                -10, 10, 10, 10, 10, 10, 10,-10,
                -10,  0, 10, 10, 10, 10,  0,-10,
                -10,  5,  5, 10, 10,  5,  5,-10,
                -10,  0,  5, 10, 10,  5,  0,-10,
                -10,  0,  0,  0,  0,  0,  0,-10,
                -20,-10,-10,-10,-10,-10,-10,-20
            },
            // Endgame
            {
                -10,-10,-10,-10,-10,-10,-10,-10,
                -10, 10,  0,  0,  0,  0, 10,-10,
                -10, 10, 10, 10, 10, 10, 10,-10,
                -10,  0, 10, 10, 10, 10,  0,-10,
                -10, 10, 10, 10, 10, 10, 10,-10,
                -10, 10, 10, 10, 10, 10, 10,-10,
                -10, 10,  0,  0,  0,  0, 10,-10,
                -10,-10,-10,-10,-10,-10,-10,-10
            }
        };

        // Rook PST
        public static readonly int[,] WRookPST = new int[3, 64]
        {
            // Opening
            {
                0,  0,  5,  10, 10, 5,  0,  0,
                -5,  0,  0,  0,  0,  0,  0, -5,
                -5,  0,  0,  0,  0,  0,  0, -5,
                -5,  0,  0,  0,  0,  0,  0, -5,
                -5,  0,  0,  0,  0,  0,  0, -5,
                -5,  0,  0,  0,  0,  0,  0, -5,
                5,  10, 10, 10, 10, 10, 10, 5,
                0,  0,  0,  0,  0,  0,  0,  0
            },
            // Middlegame
            {
                0,  0,  5,  10, 10, 5,  0,  0,
                -5,  0,  0,  0,  0,  0,  0, -5,
                -5,  0,  0,  0,  0,  0,  0, -5,
                -5,  0,  0,  0,  0,  0,  0, -5,
                -5,  0,  0,  0,  0,  0,  0, -5,
                -5,  0,  0,  0,  0,  0,  0, -5,
                5,  10, 10, 10, 10, 10, 10, 5,
                0,  0,  0,  0,  0,  0,  0,  0
            },
            // Endgame
            {
                0,  0,  5, 10, 10,  5,  0,  0,
                5, 10, 10, 10, 10, 10, 10,  5,
                -5,  0,  0,  0,  0,  0,  0, -5,
                -5,  0,  0,  0,  0,  0,  0, -5,
                -5,  0,  0,  0,  0,  0,  0, -5,
                -5,  0,  0,  0,  0,  0,  0, -5,
                5, 10, 10, 10, 10, 10, 10,  5,
                0,  0,  0,  0,  0,  0,  0,  0
            }
        };

        // Queen PST
        public static readonly int[,] WQueenPST = new int[3, 64]
        {
            // Opening
            {
                -20,-10,-10, -5, -5,-10,-10,-20,
                -10,  0,  5,  0,  0,  0,  0,-10,
                -10,  5,  5,  5,  5,  5,  0,-10,
                0,  0,  5,  5,  5,  5,  0, -5,
                -5,  0,  5,  5,  5,  5,  0, -5,
                -10,  0,  5,  5,  5,  5,  0,-10,
                -10,  0,  0,  0,  0,  0,  0,-10,
                -20,-10,-10, -5, -5,-10,-10,-20
            },
            // Middlegame
            {
                -20,-10,-10, -5, -5,-10,-10,-20,
                -10,  0,  5,  0,  0,  0,  0,-10,
                -10,  5,  5,  5,  5,  5,  0,-10,
                0,  0,  5,  5,  5,  5,  0, -5,
                -5,  0,  5,  5,  5,  5,  0, -5,
                -10,  0,  5,  5,  5,  5,  0,-10,
                -10,  0,  0,  0,  0,  0,  0,-10,
                -20,-10,-10, -5, -5,-10,-10,-20
            },
            // Endgame
            {
                -10,-10,-10, -5, -5,-10,-10,-10,
                -10,  0,  5,  0,  0,  0,  0,-10,
                -10,  5,  5,  5,  5,  5,  0,-10,
                0,  0,  5,  5,  5,  5,  0, -5,
                -5,  0,  5,  5,  5,  5,  0, -5,
                -10,  0,  5,  5,  5,  5,  0,-10,
                -10,  0,  0,  0,  0,  0,  0,-10,
                -10,-10,-10, -5, -5,-10,-10,-10
            }
        };

        // King PST
        public static readonly int[,] WKingPST = new int[3, 64]
        {
            // Opening
            {
                20, 30, 10,  0,  0, 10, 30, 20,
                20, 20,  0,  0,  0,  0, 20, 20,
                -10,-20,-20,-20,-20,-20,-20,-10,
                -20,-30,-30,-40,-40,-30,-30,-20,
                -30,-40,-40,-50,-50,-40,-40,-30,
                -30,-40,-40,-50,-50,-40,-40,-30,
                -30,-40,-40,-50,-50,-40,-40,-30,
                -30,-40,-40,-50,-50,-40,-40,-30
            },
            // Middlegame
            {
                20, 30, 10,  0,  0, 10, 30, 20,
                20, 20,  0,  0,  0,  0, 20, 20,
                -10,-20,-20,-20,-20,-20,-20,-10,
                -20,-30,-30,-40,-40,-30,-30,-20,
                -30,-40,-40,-50,-50,-40,-40,-30,
                -30,-40,-40,-50,-50,-40,-40,-30,
                -30,-40,-40,-50,-50,-40,-40,-30,
                -30,-40,-40,-50,-50,-40,-40,-30
            },
            // Endgame
            {
                -50,-30,-30,-30,-30,-30,-30,-50,
                -30,-30,  0,  0,  0,  0,-30,-30,
                -30,  0, 20, 30, 30, 20,  0,-30,
                -30,  0, 30, 40, 40, 30,  0,-30,
                -30,  0, 30, 40, 40, 30,  0,-30,
                -30,  0, 20, 30, 30, 20,  0,-30,
                -30,-30,  0,  0,  0,  0,-30,-30,
                -50,-30,-30,-30,-30,-30,-30,-50
            }
        };

        private static readonly int[] PiecePhase = { 0, 1, 1, 2, 4, 0, 1, 1, 2, 4 }; // P,N,B,R,Q for both colors
        private const int MaxGamePhase = 24; // 4*2 (Q) + 2*4 (R) + 1*4 (B) + 1*4 (N)


        const int EvalCacheSize = 1 << 24; // 2^23 = 16mil entries
        static ulong[] evalCacheKeys = new ulong[EvalCacheSize];
        static int[] evalCacheValues = new int[EvalCacheSize];
        public static int EvalBoard(Board board, bool isWhite)
        {
            // Use zobristKey XOR 1 for black, 0 for white to distinguish side to move
            ulong cacheKey = board.zobristKey ^ (isWhite ? 0UL : 1UL);
            int index = (int)(cacheKey & (EvalCacheSize - 1)); // fast mod

            if (evalCacheKeys[index] == cacheKey)
                return evalCacheValues[index];

            int score = 0;

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

            // Cache popcounts for all relevant bitboards
            int fPCount = Bitboard.PopCount(fP);
            int fNCount = Bitboard.PopCount(fN);
            int fBCount = Bitboard.PopCount(fB);
            int fRCount = Bitboard.PopCount(fR);
            int fQCount = Bitboard.PopCount(fQ);

            int ePCount = Bitboard.PopCount(eP);
            int eNCount = Bitboard.PopCount(eN);
            int eBCount = Bitboard.PopCount(eB);
            int eRCount = Bitboard.PopCount(eR);
            int eQCount = Bitboard.PopCount(eQ);
            
            //bishop pairs score
            if (fBCount >= 2) score += 30;
            if (eBCount >= 2) score -= 30;

            // Game phase calculation
            int gamePhase = MaxGamePhase;
            gamePhase -= fQCount * PiecePhase[4];
            gamePhase -= eQCount * PiecePhase[9];
            gamePhase -= fRCount * PiecePhase[3];
            gamePhase -= eRCount * PiecePhase[8];
            gamePhase -= fBCount * PiecePhase[2];
            gamePhase -= eBCount * PiecePhase[7];
            gamePhase -= fNCount * PiecePhase[1];
            gamePhase -= eNCount * PiecePhase[6];
            gamePhase = Math.Min(MaxGamePhase, Math.Max(0, gamePhase));


            // Convert to phase index: 0 = endgame, 1 = middlegame, 2 = opening
            int phase = gamePhase > 16 ? 0 : (gamePhase > 8 ? 1 : 2);

            score += EvalMaterialsSIMD(fPCount, fNCount, fBCount, fRCount, fQCount,
                       ePCount, eNCount, eBCount, eRCount, eQCount)
                   + EvalKnight(fN, eN, isWhite, phase)
                   + EvalPawn(fP, eP, isWhite, phase)
                   + EvalKing(fK, eK, isWhite, phase)
                   + EvalBishop(fB, eB, isWhite, phase)
                   + EvalQueen(fQ, eQ, isWhite, phase)
                   + EvalRook(fR, eR, isWhite, phase)
                   // For both Kings
                   + EvalKingSafety(board, isWhite, fK, eK)

                   + EvalPawnStructure(fP, eP, isWhite)
                   + EvalMobility(board, isWhite);


            evalCacheKeys[index] = cacheKey;
            evalCacheValues[index] = score;
            return score;
        }
        public static int EvalMaterialsExternal(Board board, bool isWhitePerspective)
        {
            if (isWhitePerspective)
            {
                return EvalMaterialsSIMD(
                    Bitboard.PopCount(board.bitboards[(int)Piece.WhitePawn]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.WhiteKnight]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.WhiteBishop]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.WhiteRook]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.WhiteQueen]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.BlackPawn]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.BlackKnight]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.BlackBishop]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.BlackRook]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.BlackQueen])
                );
            }
            else
            {
                return EvalMaterialsSIMD(
                    Bitboard.PopCount(board.bitboards[(int)Piece.BlackPawn]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.BlackKnight]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.BlackBishop]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.BlackRook]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.BlackQueen]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.WhitePawn]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.WhiteKnight]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.WhiteBishop]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.WhiteRook]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.WhiteQueen])
                );
            }
        }
        
        public static int EvalMatAndPST(Board board, bool isWhitePerspective)
        {
            int score = 0;
            if (isWhitePerspective)
            {
                score += EvalMaterialsSIMD(
                    Bitboard.PopCount(board.bitboards[(int)Piece.WhitePawn]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.WhiteKnight]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.WhiteBishop]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.WhiteRook]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.WhiteQueen]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.BlackPawn]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.BlackKnight]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.BlackBishop]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.BlackRook]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.BlackQueen])
                );
                score += EvalKnight(board.bitboards[(int)Piece.WhiteKnight], board.bitboards[(int)Piece.BlackKnight], true, 0)
                       + EvalPawn(board.bitboards[(int)Piece.WhitePawn], board.bitboards[(int)Piece.BlackPawn], true, 0)
                       + EvalKing(board.bitboards[(int)Piece.WhiteKing], board.bitboards[(int)Piece.BlackKing], true, 0)
                       + EvalBishop(board.bitboards[(int)Piece.WhiteBishop], board.bitboards[(int)Piece.BlackBishop], true, 0)
                       + EvalQueen(board.bitboards[(int)Piece.WhiteQueen], board.bitboards[(int)Piece.BlackQueen], true, 0)
                       + EvalRook(board.bitboards[(int)Piece.WhiteRook], board.bitboards[(int)Piece.BlackRook], true, 0);

            }
            else
            {
                score += EvalMaterialsSIMD(
                    Bitboard.PopCount(board.bitboards[(int)Piece.BlackPawn]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.BlackKnight]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.BlackBishop]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.BlackRook]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.BlackQueen]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.WhitePawn]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.WhiteKnight]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.WhiteBishop]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.WhiteRook]),
                    Bitboard.PopCount(board.bitboards[(int)Piece.WhiteQueen])
                );
                score += EvalKnight(board.bitboards[(int)Piece.BlackKnight], board.bitboards[(int)Piece.WhiteKnight], false, 0)
                       + EvalPawn(board.bitboards[(int)Piece.BlackPawn], board.bitboards[(int)Piece.WhitePawn], false, 0)
                       + EvalKing(board.bitboards[(int)Piece.BlackKing], board.bitboards[(int)Piece.WhiteKing], false, 0)
                       + EvalBishop(board.bitboards[(int)Piece.BlackBishop], board.bitboards[(int)Piece.WhiteBishop], false, 0)
                       + EvalQueen(board.bitboards[(int)Piece.BlackQueen], board.bitboards[(int)Piece.WhiteQueen], false, 0)
                       + EvalRook(board.bitboards[(int)Piece.BlackRook], board.bitboards[(int)Piece.WhiteRook], false, 0);
            }

            return score;
        }

        private static int EvalMaterials(
            int fP, int fN, int fB, int fR, int fQ,
            int eP, int eN, int eB, int eR, int eQ)
        {
            int score = 0;
            score += fP * pieceValues[0];
            score += fN * pieceValues[1];
            score += fB * pieceValues[2];
            score += fR * pieceValues[3];
            score += fQ * pieceValues[4];

            score += eP * pieceValues[5];
            score += eN * pieceValues[6];
            score += eB * pieceValues[7];
            score += eR * pieceValues[8];
            score += eQ * pieceValues[9];

            return score;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int EvalPawn(ulong fP, ulong eP, bool isWhite, int phase) => EvalSIMDPieces(fP, eP, isWhite, WPawnPST, phase);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int EvalKnight(ulong fN, ulong eN, bool isWhite, int phase) => EvalSIMDPieces(fN, eN, isWhite, WKnightPST, phase);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int EvalBishop(ulong fB, ulong eB, bool isWhite, int phase) => EvalSIMDPieces(fB, eB, isWhite, WBishopPST, phase);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int EvalQueen(ulong fQ, ulong eQ, bool isWhite, int phase) => EvalSIMDPieces(fQ, eQ, isWhite, WQueenPST, phase);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int EvalRook(ulong fR, ulong eR, bool isWhite, int phase) => EvalSIMDPieces(fR, eR, isWhite, WRookPST, phase);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int EvalKing(ulong fK, ulong eK, bool isWhite, int phase)
        {
            int score = 0;
            int wKsq, bKsq;
            if (isWhite)
            {
                wKsq = BitOperations.TrailingZeroCount(fK);
                bKsq = BitOperations.TrailingZeroCount(eK);
                score += WKingPST[phase, wKsq];
                score -= WKingPST[phase, MirrorColor(bKsq)];
            }
            else
            {
                wKsq = BitOperations.TrailingZeroCount(eK);
                bKsq = BitOperations.TrailingZeroCount(fK);
                score -= WKingPST[phase, wKsq];
                score += WKingPST[phase, MirrorColor(bKsq)];
            }
            return score;
        }
        private static int EvalPiece(ulong f, ulong e, bool isWhite, int[,] pst, int phase)
        {
            int score = 0;
            // Evaluate friendly pieces
            while (f != 0)
            {
                int sq = BitOperations.TrailingZeroCount(f);
                score += isWhite ? pst[phase, sq] : -pst[phase, sq];
                f &= f - 1; // Clear least significant bit
            }
            // Evaluate enemy pieces (mirrored)
            while (e != 0)
            {
                int sq = BitOperations.TrailingZeroCount(e);
                int mirrored = MirrorColor(sq);
                score += isWhite ? -pst[phase, mirrored] : pst[phase, mirrored];
                e &= e - 1; // Clear least significant bit
            }
            return score;
        }

        [ThreadStatic]
        private static int[] _matCounts, _matValues;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EvalMaterialsSIMD(
        int fP, int fN, int fB, int fR, int fQ,
        int eP, int eN, int eB, int eR, int eQ)
        {
            // Lazy‑allocate buffers of length >= 10 and a multiple of Vector<int>.Count
            int vecLen = Vector<int>.Count;
            int len = ((10 + vecLen - 1) / vecLen) * vecLen;  // round up to multiple
            if (_matCounts == null || _matCounts.Length < len)
            {
                _matCounts = new int[len];
                _matValues = new int[len];
                // Copy your static pieceValues[0..9] into _matValues[0..9],
                // leave 10..len-1 = 0
                for (int i = 0; i < 10; i++)
                    _matValues[i] = pieceValues[i];
                for (int i = 10; i < len; i++)
                    _matValues[i] = 0;
            }

            // Fill counts
            _matCounts[0] = fP;
            _matCounts[1] = fN;
            _matCounts[2] = fB;
            _matCounts[3] = fR;
            _matCounts[4] = fQ;
            _matCounts[5] = eP;
            _matCounts[6] = eN;
            _matCounts[7] = eB;
            _matCounts[8] = eR;
            _matCounts[9] = eQ;
            // rest of _matCounts (i=10..len-1) stay zero

            int total = 0;
            // Vectorized dot-product in chunks of Vector<int>.Count
            for (int i = 0; i < len; i += vecLen)
            {
                var vc = new Vector<int>(_matCounts, i);
                var vv = new Vector<int>(_matValues, i);
                total += Vector.Dot(vc, vv);
            }

            return total;
        }

        private static int VEC = Vector<int>.Count;
        private static readonly Vector<int> vOne = new Vector<int>(1);

        [ThreadStatic]
        private static int[] _simdBuffer;

        // … your other Eval members …

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int EvalSIMDPieces(ulong f, ulong e, bool isWhite, int[,] pst, int phase)
        {
            // Lazy‑allocate thread‑static buffer
            if (_simdBuffer == null)
                _simdBuffer = new int[VEC];

            int total = 0;
            var buf = _simdBuffer;
            var pstTable = pst;  // local alias

            // Inline helper to process bits for one color
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void Process(ref ulong bits, bool invert, bool mirror)
            {
                while (bits != 0)
                {
                    // Gather up to VEC scores
                    int cnt = 0;
                    do
                    {
                        int sq = BitOperations.TrailingZeroCount(bits);
                        bits &= bits - 1;
                        int idx = mirror ? MirrorColor(sq) : sq;
                        int v = pstTable[phase, idx];
                        buf[cnt++] = invert ? -v : v;
                    }
                    while (bits != 0 && cnt < VEC);

                    // Use SIMD if full batch, else scalar
                    if (cnt == VEC)
                    {
                        total += Vector.Dot(new Vector<int>(buf), vOne);
                    }
                    else
                    {
                        // scalar tail
                        for (int i = 0; i < cnt; i++)
                            total += buf[i];
                    }
                }
            }

            // Process friendly pieces: invert sign if black
            var bitsF = f;
            Process(ref bitsF, invert: !isWhite, mirror: false);

            // Process enemy pieces: invert sign if white, mirror squares
            var bitsE = e;
            Process(ref bitsE, invert: isWhite, mirror: true);

            return total;
        }
        static int MirrorColor(int sq)
		{
			int file = sq % 8;
			int rank = sq / 8;           
			int flippedRank = 7 - rank;  
			return flippedRank * 8 + file;
		}

        private static readonly int[] KingZoneOffsets = {
            -9, -8, -7,
            -1,     1,
             7,  8,  9
        };
        private static bool IsPawnOn(Board board, bool isWhite, int square)
        {
            if (square < 0 || square >= 64) return false;
            ulong mask = 1UL << square;
            int index = (int)(isWhite ? Piece.WhitePawn : Piece.BlackPawn);
            return (board.bitboards[index] & mask) != 0;
        }
        private static int EvalKingSafety(Board board, bool isWhite, ulong fK, ulong eK)
        {
            int score = 0;

            // Friendly king safety
            score += EvaluateOneKing(board, isWhite, fK);

            // Enemy king safety (we reverse the penalty to make it a bonus)
            score -= EvaluateOneKing(board, !isWhite, eK);

            return score;
        }
        private static int EvaluateOneKing(Board board, bool isWhite, ulong kingBB)
        {
            int penalty = 0;
            int kingSq = BitOperations.TrailingZeroCount(kingBB);
            int zonePenalty = 0;

            foreach (int offset in KingZoneOffsets)
            {
                int neighborSq = kingSq + offset;
                if (neighborSq < 0 || neighborSq >= 64) continue;

                // Ensure no wraparound (like A1 to H1)
                int fileDiff = Math.Abs((kingSq % 8) - (neighborSq % 8));
                if (fileDiff > 1) continue;

                if (MoveGen.IsSquareAttacked(board, neighborSq, !isWhite))
                    zonePenalty += 10;
            }

            // Add pawn shield penalty
            int shieldPenalty = 0;
            if (isWhite && kingSq >= 48)
            {
                if (!IsPawnOn(board, isWhite, kingSq - 8)) shieldPenalty += 15;
                if (kingSq % 8 != 0 && !IsPawnOn(board, isWhite, kingSq - 9)) shieldPenalty += 10;
                if (kingSq % 8 != 7 && !IsPawnOn(board, isWhite, kingSq - 7)) shieldPenalty += 10;
            }
            else if (!isWhite && kingSq < 16)
            {
                if (!IsPawnOn(board, isWhite, kingSq + 8)) shieldPenalty += 15;
                if (kingSq % 8 != 0 && !IsPawnOn(board, isWhite, kingSq + 7)) shieldPenalty += 10;
                if (kingSq % 8 != 7 && !IsPawnOn(board, isWhite, kingSq + 9)) shieldPenalty += 10;
            }

            penalty += zonePenalty + shieldPenalty;
            return -penalty; // More penalty → worse safety
        }
        private static int EvalPawnStructure(ulong fP, ulong eP, bool isWhite)
        {
            int score = 0;

            // Evaluate friendly pawns
            for (int file = 0; file < 8; file++)
            {
                ulong fileMask = FileMasks[file];
                int count = Bitboard.PopCount(fP & fileMask);

                if (count > 1)
                    score += -15 * (count - 1); // Doubled pawn penalty

                if (count == 1)
                {
                    bool leftEmpty = file == 0 || (fP & FileMasks[file - 1]) == 0;
                    bool rightEmpty = file == 7 || (fP & FileMasks[file + 1]) == 0;
                    if (leftEmpty && rightEmpty)
                        score += -20; // Isolated pawn penalty

                    // Passed pawn detection
                    ulong forwardMask = isWhite
                        ? 0xFFFFFFFFFFFFFFFF << (file + 1) * 8
                        : 0xFFFFFFFFFFFFFFFF >> (8 - file) * 8;

                    ulong relevantMask = fileMask;
                    if (file > 0) relevantMask |= FileMasks[file - 1];
                    if (file < 7) relevantMask |= FileMasks[file + 1];

                    ulong blockers = eP & relevantMask & forwardMask;
                    if (blockers == 0)
                        score += 20; // Passed pawn bonus
                }
            }

            // Evaluate enemy pawns (apply opposite penalty/bonus)
            for (int file = 0; file < 8; file++)
            {
                ulong fileMask = FileMasks[file];
                int count = Bitboard.PopCount(eP & fileMask);

                if (count > 1)
                    score += 15 * (count - 1); // Enemy doubled pawn penalty

                if (count == 1)
                {
                    bool leftEmpty = file == 0 || (eP & FileMasks[file - 1]) == 0;
                    bool rightEmpty = file == 7 || (eP & FileMasks[file + 1]) == 0;
                    if (leftEmpty && rightEmpty)
                        score += 20; // Enemy isolated pawn penalty

                    ulong forwardMask = isWhite
                        ? 0xFFFFFFFFFFFFFFFF >> (8 - file) * 8
                        : 0xFFFFFFFFFFFFFFFF << (file + 1) * 8;

                    ulong relevantMask = fileMask;
                    if (file > 0) relevantMask |= FileMasks[file - 1];
                    if (file < 7) relevantMask |= FileMasks[file + 1];

                    ulong blockers = fP & relevantMask & forwardMask;
                    if (blockers == 0)
                        score += -20; // Enemy passed pawn bonus
                }
            }

            return score;
        }
        private static int EvalMobility(Board board, bool isWhiteToMove)
        {
            // compute “raw” mobility for White and Black exactly as before:
            int wMob = RawMobility(board, true);
            int bMob = RawMobility(board, false);

            // now fold in side-to-move:
            return isWhiteToMove
                ? (wMob - bMob)
                : (bMob - wMob);
        }
        private static int RawMobility(Board board, bool isWhite)
        {
            int score = 0;

            // Precompute occupancy & side bitboards
            ulong whiteOcc = board.occupancies[(int)Color.White];
            ulong blackOcc = board.occupancies[(int)Color.Black];
            ulong occupied = whiteOcc | blackOcc;

            // Friendly / enemy occupancy
            ulong friendlyOcc = isWhite ? whiteOcc : blackOcc;
            ulong enemyOcc = isWhite ? blackOcc : whiteOcc;

            // 1) Knights: +1 per attacked square
            ulong knights = board.bitboards[(int)(isWhite ? Piece.WhiteKnight : Piece.BlackKnight)];
            while (knights != 0)
            {
                int sq = BitOperations.TrailingZeroCount(knights);
                knights &= knights - 1;
                // use your precomputed table
                ulong attacks = MoveTables.KnightMoves[sq] & ~friendlyOcc;
                score += Bitboard.PopCount(attacks);
            }

            // 2) Bishops: +1 per attacked square
            ulong bishops = board.bitboards[(int)(isWhite ? Piece.WhiteBishop : Piece.BlackBishop)];
            while (bishops != 0)
            {
                int sq = BitOperations.TrailingZeroCount(bishops);
                bishops &= bishops - 1;
                ulong attacks = Magics.GetBishopAttacks(sq, occupied) & ~friendlyOcc;
                score += Bitboard.PopCount(attacks);
            }

            // 3) Rooks: +2 per attacked square
            ulong rooks = board.bitboards[(int)(isWhite ? Piece.WhiteRook : Piece.BlackRook)];
            while (rooks != 0)
            {
                int sq = BitOperations.TrailingZeroCount(rooks);
                rooks &= rooks - 1;
                ulong attacks = Magics.GetRookAttacks(sq, occupied) & ~friendlyOcc;
                score += 2 * Bitboard.PopCount(attacks);
            }

            // 4) Queens: +2 per attacked square
            ulong queens = board.bitboards[(int)(isWhite ? Piece.WhiteQueen : Piece.BlackQueen)];
            while (queens != 0)
            {
                int sq = BitOperations.TrailingZeroCount(queens);
                queens &= queens - 1;
                // combine rook & bishop attacks cheaply
                ulong attacks = Magics.GetBishopAttacks(sq, occupied)
                               | Magics.GetRookAttacks(sq, occupied);
                attacks &= ~friendlyOcc;
                score += 2 * Bitboard.PopCount(attacks);
            }

            // Finally subtract the enemy’s mobility in the caller:
            // score = whiteScore - blackScore when isWhite == true
            return score;
        }
    }
}
