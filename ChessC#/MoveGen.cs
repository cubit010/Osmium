using System;
using System.Numerics;

namespace ChessC_
{
	internal static class MoveGen
	{
		public const ulong Rank4 = 0x00000000FF000000; // squares a4–h4
		public const ulong Rank5 = 0x000000FF00000000; // squares a5–h5
		public static void FlagCheckAndMate(Board board, List<Move> moves, bool isWhite)
		{
			bool opponent = !isWhite;

			for (int i = 0; i < moves.Count; i++)
			{
				Move move = moves[i];

				// Make the move on the board
				UndoInfo undo = board.MakeSearchMove(board, move);

				// Check if opponent's king is in check, opponent defending
				bool inCheck = IsSquareAttacked(board, GetKingSquare(board, opponent), opponent);

				if (inCheck)
				{
					move.AddMoveFlag(MoveFlags.Check);

					// Generate opponent's legal moves after this move
					List<Move> opponentMoves = FilteredLegalWithoutFlag(board, opponent);

					if (opponentMoves.Count == 0)
					{ 
						// No legal moves for opponent and they are in check -> checkmate
						move.AddMoveFlag(MoveFlags.Checkmate);
					}
				}

				// Undo the move to restore board state
				board.UnmakeMove(move, undo);

				// Update the move in the list with the new flags
				moves[i] = move;
			}
		}
		private static void GenSemiLegal(Board board, List<Move> moves, bool isWhite)
		{
			PieceMoves.GenerateKingMoves(board, moves, isWhite);
			PieceMoves.GenerateKnightMoves(board, moves, isWhite);
			PieceMoves.GenerateSliderMoves(board, moves, isWhite);
			PieceMoves.GeneratePawnMoves(board, moves, isWhite);
			GenerateCastles(board, moves, isWhite);
		}
		
		public static List<Move> FilteredLegalMoves(Board board, bool isWhite)
		{
			List<Move> moves = [];
			GenSemiLegal(board, moves, isWhite);
			FilterMoves(board, moves, isWhite);
			FlagCheckAndMate(board, moves, isWhite);

			//Console.WriteLine(moves[0]);
			//Console.WriteLine(moves.Count);
			return moves;
		}
		public static List<Move> FilteredLegalWithoutFlag(Board board, bool isWhite)
		{
			List<Move> moves = [];
			GenSemiLegal(board, moves, isWhite);
			FilterMoves(board, moves, isWhite);
			return moves;
		}
		private static void FilterMoves(Board board, List<Move> moves, bool isWhite)
		{
			if (moves == null || moves.Count == 0)
				return;

			for (int i = moves.Count - 1; i >= 0; i--)
			{
				Move m = moves[i];
				//if ((int)m.From < 0 || (int)m.From >= 64 || (int)m.To < 0 || (int)m.To >= 64)
				//{
				//	Console.WriteLine($"Invalid move index: {m}");
				//	moves.RemoveAt(i);
				//	continue;
				//}

				if (SimMoveCheck(m, board, isWhite))
					moves.RemoveAt(i);
			}
		}
		private static bool SimMoveCheck(Move move, Board board, bool isWhiteToMove)
		{
			// 1) Apply the move
			UndoInfo undo = board.MakeSearchMove(board, move);

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
			board.UnmakeMove(move, undo);
			return inCheck;
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
		public static bool IsInCheck(Board board, bool isWhiteDefend)
		{
			int kingIdx = GetKingSquare(board, isWhiteDefend);
			return IsSquareAttacked(board, kingIdx, isWhiteDefend);
		}
		public static List<Move> GenerateCaptureMoves(Board board, bool isWhite)
		{
            if (IsInCheck(board, isWhite))
            {
                // return every legal move that gets out of check:
                return MoveGen.FilteredLegalMoves(board, isWhite);
            }
            var moves = new List<Move>();

			ulong enemyOcc = board.occupancies[isWhite ? (int)Color.Black : (int)Color.White];
            ulong occ = board.occupancies[2] & ~board.bitboards[isWhite ? 11 : 5];
            enemyOcc &= ~board.bitboards[isWhite? 11 : 5];

			// --- 1) Pawn captures (incl. promotions and en-passant) ---
			if (isWhite)
			{
				ulong pawns = board.bitboards[(int)Piece.WhitePawn];
				// left captures ( sq -> sq+7 )
				ulong leftCaps = (pawns & ~FileA) << 7 & enemyOcc;
				// right captures ( sq -> sq+9 )
				ulong rightCaps = (pawns & ~FileH) << 9 & enemyOcc;

				// promotions on 7th rank:
				ulong promoLeft = leftCaps & Rank8;
				ulong promoRight = rightCaps & Rank8;
				// normal captures:
				ulong normalLeft = leftCaps & ~Rank8;
				ulong normalRight = rightCaps & ~Rank8;

				// add promotions captures
				foreach (var to in BitIter(promoLeft))
					foreach (var promo in new[] { Piece.WhiteQueen, Piece.WhiteRook, Piece.WhiteBishop, Piece.WhiteKnight })
						moves.Add(new Move((Square)(to - 7), (Square)to, Piece.WhitePawn,
										  FindCapturedPiece(board, (Square)to, true),
										  MoveFlags.Promotion | MoveFlags.Capture, promo));

				foreach (var to in BitIter(promoRight))
					foreach (var promo in new[] { Piece.WhiteQueen, Piece.WhiteRook, Piece.WhiteBishop, Piece.WhiteKnight })
						moves.Add(new Move((Square)(to - 9), (Square)to, Piece.WhitePawn,
										  FindCapturedPiece(board, (Square)to, true),
										  MoveFlags.Promotion | MoveFlags.Capture, promo));

				// add normal pawn captures
				foreach (var to in BitIter(normalLeft))
					moves.Add(new Move((Square)(to - 7), (Square)to, Piece.WhitePawn,
									   FindCapturedPiece(board, (Square)to, true),
									   MoveFlags.Capture));
				foreach (var to in BitIter(normalRight))
					moves.Add(new Move((Square)(to - 9), (Square)to, Piece.WhitePawn,
									   FindCapturedPiece(board, (Square)to, true),
									   MoveFlags.Capture));

				// en-passant
				if (board.enPassantSquare != Square.None)
				{
					int epsq = (int)board.enPassantSquare; // pawn's landing square (e.g., e5 for black)
					int epCaptureSquare = epsq + 8;        // square behind black pawn (for white capture)

					ulong epCaptureBit = 1UL << epCaptureSquare;

					// pawns that can capture to epCaptureSquare must be on rank 5 (the rank behind epCaptureSquare)
					// And must be adjacent files to epCaptureSquare

					// Pawns on epCaptureSquare - 1 (left) or +1 (right) file, on rank 5
					ulong epSources = (pawns & Rank5) & (
										((epCaptureBit >> 1) & ~FileH) |   // left pawn
										((epCaptureBit << 1) & ~FileA)     // right pawn
									  );

					foreach (var from in BitIter(epSources))
						moves.Add(new Move((Square)from, (Square)epCaptureSquare, Piece.WhitePawn, Piece.BlackPawn, MoveFlags.EnPassant));
				}
			}
			else
			{
				// mirror for Black pawns
				ulong pawns = board.bitboards[(int)Piece.BlackPawn];
				ulong leftCaps = (pawns & ~FileH) >> 7 & enemyOcc;
				ulong rightCaps = (pawns & ~FileA) >> 9 & enemyOcc;

				ulong promoLeft = leftCaps & Rank1;
				ulong promoRight = rightCaps & Rank1;
				ulong normalLeft = leftCaps & ~Rank1;
				ulong normalRight = rightCaps & ~Rank1;

				foreach (var to in BitIter(promoLeft))
					foreach (var promo in new[] { Piece.BlackQueen, Piece.BlackRook, Piece.BlackBishop, Piece.BlackKnight })
						moves.Add(new Move((Square)(to + 7), (Square)to, Piece.BlackPawn,
										   FindCapturedPiece(board, (Square)to, false),
										   MoveFlags.Promotion | MoveFlags.Capture, promo));
				foreach (var to in BitIter(promoRight))
					foreach (var promo in new[] { Piece.BlackQueen, Piece.BlackRook, Piece.BlackBishop, Piece.BlackKnight })
						moves.Add(new Move((Square)(to + 9), (Square)to, Piece.BlackPawn,
										   FindCapturedPiece(board, (Square)to, false),
										   MoveFlags.Promotion | MoveFlags.Capture, promo));
				foreach (var to in BitIter(normalLeft))
					moves.Add(new Move((Square)(to + 7), (Square)to, Piece.BlackPawn,
									   FindCapturedPiece(board, (Square)to, false),
									   MoveFlags.Capture));
				foreach (var to in BitIter(normalRight))
					moves.Add(new Move((Square)(to + 9), (Square)to, Piece.BlackPawn,
									   FindCapturedPiece(board, (Square)to, false),
									   MoveFlags.Capture));
				if (board.enPassantSquare != Square.None)
				{
					int epsq = (int)board.enPassantSquare; // pawn's landing square (e.g., e4 for white)
					int epCaptureSquare = epsq - 8;        // square behind white pawn (for black capture)

					ulong epCaptureBit = 1UL << epCaptureSquare;

					// pawns that can capture to epCaptureSquare must be on rank 4
					// And must be adjacent files to epCaptureSquare

					ulong epSources = (pawns & Rank4) & (
										((epCaptureBit >> 1) & ~FileH) |
										((epCaptureBit << 1) & ~FileA)
									  );

					foreach (var from in BitIter(epSources))
						moves.Add(new Move((Square)from, (Square)epCaptureSquare, Piece.BlackPawn, Piece.WhitePawn, MoveFlags.EnPassant));
				}
			}

			// --- 2) Knight captures ---
			ulong knights = board.bitboards[(int)(isWhite ? Piece.WhiteKnight : Piece.BlackKnight)];
			while (knights != 0)
			{
				int from = BitOperations.TrailingZeroCount(knights);
				knights &= knights - 1;
				ulong attacks = MoveTables.KnightMoves[from] & enemyOcc;
				foreach (var to in BitIter(attacks))
					moves.Add(new Move((Square)from, (Square)to,
							   isWhite ? Piece.WhiteKnight : Piece.BlackKnight,
							   FindCapturedPiece(board, (Square)to, isWhite),
							   MoveFlags.Capture));
			}

			// --- 3) King captures ---
			ulong king = board.bitboards[(int)(isWhite ? Piece.WhiteKing : Piece.BlackKing)];
			int kfrom = BitOperations.TrailingZeroCount(king);
			ulong kAttacks = MoveTables.KingMoves[kfrom] & enemyOcc;
			foreach (var to in BitIter(kAttacks))
				moves.Add(new Move((Square)kfrom, (Square)to,
						   isWhite ? Piece.WhiteKing : Piece.BlackKing,
						   FindCapturedPiece(board, (Square)to, isWhite),
						   MoveFlags.Capture));

			// --- 4) Bishop & Queen diagonal captures ---
			ulong bishops = board.bitboards[(int)(isWhite ? Piece.WhiteBishop : Piece.BlackBishop)];
			ulong queens = board.bitboards[(int)(isWhite ? Piece.WhiteQueen : Piece.BlackQueen)];
			ulong diagSliders = bishops | queens;
			while (diagSliders != 0)
			{
				int from = BitOperations.TrailingZeroCount(diagSliders);
				diagSliders &= diagSliders - 1;
				ulong attacks = Magics.GetBishopAttacks(from, occ) & enemyOcc;
				foreach (var to in BitIter(attacks))
					moves.Add(new Move((Square)from, (Square)to,
							   ((1UL << from) & bishops) != 0
								 ? (isWhite ? Piece.WhiteBishop : Piece.BlackBishop)
								 : (isWhite ? Piece.WhiteQueen : Piece.BlackQueen),
							   FindCapturedPiece(board, (Square)to, isWhite),
							   MoveFlags.Capture));
			}

			// --- 5) Rook & Queen orthogonal captures ---
			ulong rooks = board.bitboards[(int)(isWhite ? Piece.WhiteRook : Piece.BlackRook)];
			queens = board.bitboards[(int)(isWhite ? Piece.WhiteQueen : Piece.BlackQueen)];
			ulong orthoSliders = rooks | queens;
			while (orthoSliders != 0)
			{
				int from = BitOperations.TrailingZeroCount(orthoSliders);
				orthoSliders &= orthoSliders - 1;
				ulong attacks = Magics.GetRookAttacks(from, occ) & enemyOcc;
				foreach (var to in BitIter(attacks))
					moves.Add(new Move((Square)from, (Square)to,
							   ((1UL << from) & rooks) != 0
								 ? (isWhite ? Piece.WhiteRook : Piece.BlackRook)
								 : (isWhite ? Piece.WhiteQueen : Piece.BlackQueen),
							   FindCapturedPiece(board, (Square)to, isWhite),
							   MoveFlags.Capture));
			}

			return moves;
		}

		// helper: iterate over set bits in a bitboard
		private static IEnumerable<int> BitIter(ulong bb)
		{
			while (bb != 0)
			{
				int i = BitOperations.TrailingZeroCount(bb);
				yield return i;
				bb &= bb - 1;
			}
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

		// ─── file-rank‐masks ──────────────────────────────────────────────────────
		private const ulong FileA = 0x0101010101010101UL;
		private const ulong FileH = 0x8080808080808080UL;
		private const ulong Rank1 = 0x00000000000000FFUL;
		private const ulong Rank8 = 0xFF00000000000000UL;

		// ─── initialization ──────────────────────────────────────────────────
		internal static Piece FindCapturedPiece(Board board, Square sq, bool isWhite)
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
			ulong occ = board.occupancies[2];

			// 1) Pawn attacks
			if (isWhiteDefend)
			{
				// Black pawns attack down‐board: from square+7 (NE) and +9 (NW)
				if (square + 7 < 64 && square % 8 < 7 &&
				   ((board.bitboards[(int)Piece.BlackPawn] >> (square + 7) & 1UL) != 0))
					return true;
				if (square + 9 < 64 && square % 8 > 0 &&
				   ((board.bitboards[(int)Piece.BlackPawn] >> (square + 9) & 1UL) != 0))
					return true;
			}
			else
			{
				// White pawns attack up‐board: from square−7 (SW) and −9 (SE)
				if (square - 7 >= 0 && square % 8 > 0 &&
				   ((board.bitboards[(int)Piece.WhitePawn] >> (square - 7) & 1UL) != 0))
					return true;
				if (square - 9 >= 0 && square % 8 < 7 &&
				   ((board.bitboards[(int)Piece.WhitePawn] >> (square - 9) & 1UL) != 0))
					return true;
			}

			// 2) Knight attacks
			ulong knights = isWhiteDefend
				? board.bitboards[(int)Piece.BlackKnight]
				: board.bitboards[(int)Piece.WhiteKnight];
			if ((knights & MoveTables.KnightMoves[square]) != 0)
				return true;

			// 3) King attacks
			ulong kings = isWhiteDefend
				? board.bitboards[(int)Piece.BlackKing]
				: board.bitboards[(int)Piece.WhiteKing];
			if ((kings & MoveTables.KingMoves[square]) != 0)
				return true;

			// 4) Rook/Queen rays via magics
			ulong rq = (isWhiteDefend
				? board.bitboards[(int)Piece.BlackRook] | board.bitboards[(int)Piece.BlackQueen]
				: board.bitboards[(int)Piece.WhiteRook] | board.bitboards[(int)Piece.WhiteQueen]);
			if ((Magics.GetRookAttacks(square, occ) & rq) != 0)
				return true;

			// 5) Bishop/Queen rays via magics
			ulong bq = (isWhiteDefend
				? board.bitboards[(int)Piece.BlackBishop] | board.bitboards[(int)Piece.BlackQueen]
				: board.bitboards[(int)Piece.WhiteBishop] | board.bitboards[(int)Piece.WhiteQueen]);
			if ((Magics.GetBishopAttacks(square, occ) & bq) != 0)
				return true;

			return false;
		}

        private static void GenerateCastles(Board board, List<Move> moves, bool isWhite)
        {
            int kingFrom = GetKingSquare(board, isWhite);

            var rights = board.castlingRights;
            ulong occ = board.occupancies[2];

            if (isWhite && kingFrom == (int)Square.E1 && !IsSquareAttacked(board, (int)Square.E1, true))
            {
                // King-side: e1 → g1
                if ((rights & Castling.WhiteKing) != 0
                    && ((occ & (1UL << (int)Square.F1)) == 0)
                    && ((occ & (1UL << (int)Square.G1)) == 0)
                    && !IsSquareAttacked(board, (int)Square.F1, true)
                    && !IsSquareAttacked(board, (int)Square.G1, true))
                {
                    moves.Add(new Move((Square)kingFrom, Square.G1, Piece.WhiteKing, Piece.None, MoveFlags.Castling));
                }

                // Queen-side: e1 → c1
                if ((rights & Castling.WhiteQueen) != 0
                    && ((occ & (1UL << (int)Square.D1)) == 0)
                    && ((occ & (1UL << (int)Square.C1)) == 0)
                    && ((occ & (1UL << (int)Square.B1)) == 0)
                    && !IsSquareAttacked(board, (int)Square.D1, true)
                    && !IsSquareAttacked(board, (int)Square.C1, true))
                {
                    moves.Add(new Move((Square)kingFrom, Square.C1, Piece.WhiteKing, Piece.None, MoveFlags.Castling));
                }
            }
            else if (!isWhite && kingFrom == (int)Square.E8 && !IsSquareAttacked(board, (int)Square.E8, false))
            {
                // King-side: e8 → g8
                if ((rights & Castling.BlackKing) != 0
                    && ((occ & (1UL << (int)Square.F8)) == 0)
                    && ((occ & (1UL << (int)Square.G8)) == 0)
                    && !IsSquareAttacked(board, (int)Square.F8, false)
                    && !IsSquareAttacked(board, (int)Square.G8, false))
                {
                    moves.Add(new Move((Square)kingFrom, Square.G8, Piece.BlackKing, Piece.None, MoveFlags.Castling));
                }

                // Queen-side: e8 → c8
                if ((rights & Castling.BlackQueen) != 0
                    && ((occ & (1UL << (int)Square.D8)) == 0)
                    && ((occ & (1UL << (int)Square.C8)) == 0)
                    && ((occ & (1UL << (int)Square.B8)) == 0)
                    && !IsSquareAttacked(board, (int)Square.D8, false)
                    && !IsSquareAttacked(board, (int)Square.C8, false))
                {
                    moves.Add(new Move((Square)kingFrom, Square.C8, Piece.BlackKing, Piece.None, MoveFlags.Castling));
                }
            }
        }
    }
}