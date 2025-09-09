
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Osmium
{
	// Helper struct to save board state for undo
	struct BoardState
	{
		public ulong[] Bitboards;
		public ulong[] Occupancies;
		public Color SideToMove;
		public Castling CastlingRights;
		public Square EnPassantSquare;
		public int HalfmoveClock;
		public int FullmoveNumber;
		public ulong ZobristKey;
	}
	public struct UndoInfo
	{
		public Castling PreviousCastlingRights;
		public Square PreviousEnPassantSquare;
		public int PreviousHalfmoveClock;
		public int PreviousFullmoveNumber;
		public ulong PreviousZobristKey;
	}

	public class Board
	{
		public Board()
		{
			InitMaterials();
			irreversibleTracker[0] = true;
		}

		//this is really just here for testing if the UCI newgame works
		public override bool Equals(object? obj)
		{
			if (obj == null || obj.GetType() != typeof(Board))
				return false;

			var other = (Board)obj;

			// Compare bitboards
			for (int i = 0; i < bitboards.Length; i++)
				if (bitboards[i] != other.bitboards[i])
					return false;

			// Compare occupancies
			for (int i = 0; i < occupancies.Length; i++)
				if (occupancies[i] != other.occupancies[i])
					return false;

			// Compare other simple fields
			if (materialDelta != other.materialDelta) return false;
			if (sideToMove != other.sideToMove) return false;
			if (castlingRights != other.castlingRights) return false;
			if (enPassantSquare != other.enPassantSquare) return false;
			if (halfmoveClock != other.halfmoveClock) return false;
			if (fullmoveNumber != other.fullmoveNumber) return false;
			if (zobristKey != other.zobristKey) return false;

			// Compare history stacks by enumerating to arrays (to compare order)
			var thisHistory = history.Reverse().ToArray();
			var otherHistory = other.history.Reverse().ToArray();

			if (thisHistory.Length != otherHistory.Length) return false;

			if (thisHistory.Length != otherHistory.Length)
			{
				return false;
			}
			for (int i = 0; i < thisHistory.Length; i++)
			{
				if (!thisHistory[i].Equals(otherHistory[i]))
					return false;
			}

			return true;
		}
		public override string ToString()
		{
			return Utils.GetBoardString(this);
		}

		public ulong[] bitboards = new ulong[12]; // 6 piece types × 2 colors
		public ulong[] occupancies = new ulong[3]; // White, Black, Both

		public int materialDelta;

		public Color sideToMove;
		public Castling castlingRights;
		public Square enPassantSquare;

		public int halfmoveClock;
		public int fullmoveNumber;

		public ulong zobristKey;
		
		// History stack
		private Stack<BoardState> history = [];

		private ulong[] repetitionStack = new ulong[2000];
		private bool[] irreversibleTracker = new bool[2000];
		private static int boardRepPly;

		public void InitRepStack()
		{
			boardRepPly = 0;
			irreversibleTracker[0] = true;
			repetitionStack[boardRepPly++] = zobristKey;
		}

		public void PushRepetition()
		{
			repetitionStack[boardRepPly++] = zobristKey;
		}

		public void PopRepetition()
		{
			boardRepPly--;
		}

		public bool IsThreefoldRepetition()
		{
			ulong key = repetitionStack[boardRepPly - 1];

			int count = 0;

			// Scan backwards until last irreversible
			for (int i = boardRepPly - 2; i >= 0; i--)
			{
				if (repetitionStack[i] == key && ++count == 2)
					return true;

				if (irreversibleTracker[i])
					break;
			}
			return false;
		}
        public void PrintRepCount()
        {
            ulong key = repetitionStack[boardRepPly - 1];

            int count = 0;

            // Scan backwards until last irreversible
            for (int i = boardRepPly - 2; i >= 0; i--)
            {
                if (repetitionStack[i] == key && ++count == 2)
                    Console.WriteLine("count: 3");

				if (irreversibleTracker[i])
				{
					Console.WriteLine("break early due to irreversible at ply " + i);
                    break;
				}
            }
            Console.WriteLine("count: " + (count+1));
        }
        public void ClearHistory()
		{
			history.Clear();
		}

		// Put in a constructor or reset method
		public static readonly int[] pieceValues =
		   [
			// P, N, B, R, Q,
				100,  320,  330,  500,  900, 0,
				-100, -320, -330, -500, -900, 0,
			];
		public void InitMaterials()
		{
			// Calculate material values based on piece types
			materialDelta = 0;
			for (int i = 0; i < 5; i++) // White pieces
				materialDelta += BitOperations.PopCount(bitboards[i]) * pieceValues[i];

			for (int i = 6; i < 11; i++) // Black pieces
				materialDelta -= BitOperations.PopCount(bitboards[i]) * pieceValues[i - 6];
		}
		public void ComputeZobrist()
		{
			zobristKey = 0UL;
			// 1) piece placements
			for (int p = 0; p < 12; p++)
				for (int sq = 0; sq < 64; sq++)
					if ((bitboards[p] & (1UL << sq)) != 0)
						zobristKey ^= Zobrist.PieceSquare[p, sq];

			// 2) side to move
			if (sideToMove == Color.Black)
				zobristKey ^= Zobrist.SideToMove;

			// 3) castling rights
			zobristKey ^= Zobrist.CastlingRights[(int)castlingRights];

			// 4) en passant file (if any)
			if (enPassantSquare != Square.None)
				zobristKey ^= Zobrist.EnPassantFile[(int)enPassantSquare % 8];
		}

		private void UpdateCastlingRights(Move move)
		{
			switch (move.PieceMoved)
			{
				case Piece.WhiteKing:
					castlingRights &= ~(Castling.WhiteKing | Castling.WhiteQueen);
					break;
				case Piece.BlackKing:
					castlingRights &= ~(Castling.BlackKing | Castling.BlackQueen);
					break;
				case Piece.WhiteRook:
					if (move.From == Square.H1) castlingRights &= ~Castling.WhiteKing;
					else if (move.From == Square.A1) castlingRights &= ~Castling.WhiteQueen;
					break;
				case Piece.BlackRook:
					if (move.From == Square.H8) castlingRights &= ~Castling.BlackKing;
					else if (move.From == Square.A8) castlingRights &= ~Castling.BlackQueen;
					break;
			}

			// Handle rook being captured
			if (move.PieceCaptured != Piece.None)
			{
				switch (move.PieceCaptured)
				{
					case Piece.WhiteRook:
						if (move.To == Square.H1) castlingRights &= ~Castling.WhiteKing;
						else if (move.To == Square.A1) castlingRights &= ~Castling.WhiteQueen;
						break;
					case Piece.BlackRook:
						if (move.To == Square.H8) castlingRights &= ~Castling.BlackKing;
						else if (move.To == Square.A8) castlingRights &= ~Castling.BlackQueen;
						break;
				}
			}
		}

		// Save current board state
		private BoardState SaveState()
		{
			return new BoardState
			{
				Bitboards = (ulong[])bitboards.Clone(),
				Occupancies = (ulong[])occupancies.Clone(),
				SideToMove = sideToMove,
				CastlingRights = castlingRights,
				EnPassantSquare = enPassantSquare,
				HalfmoveClock = halfmoveClock,
				FullmoveNumber = fullmoveNumber,
				ZobristKey = zobristKey
			};
		}

		public struct NullMoveUndoInfo
		{
			public Square PreviousEnPassantSquare;
			public int PreviousHalfmoveClock;
			public int PreviousFullmoveNumber;
			public ulong PreviousZobristKey;
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public NullMoveUndoInfo MakeNullMove()
		{
			var undo = new NullMoveUndoInfo
			{
				PreviousEnPassantSquare = enPassantSquare,
				PreviousHalfmoveClock = halfmoveClock,
				PreviousFullmoveNumber = fullmoveNumber,
				PreviousZobristKey = zobristKey
			};

			// Remove en passant from zobrist if set
			if (enPassantSquare != Square.None)
				zobristKey ^= Zobrist.EnPassantFile[(int)enPassantSquare % 8];

			enPassantSquare = Square.None;

			if (sideToMove == Color.Black)
				fullmoveNumber++;

            sideToMove ^= (Color)1;
            halfmoveClock++;
			zobristKey ^= Zobrist.SideToMove;

			return undo;
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void UnmakeNullMove(NullMoveUndoInfo undo)
		{
			enPassantSquare = undo.PreviousEnPassantSquare;
			halfmoveClock = undo.PreviousHalfmoveClock;
			fullmoveNumber = undo.PreviousFullmoveNumber;
			zobristKey = undo.PreviousZobristKey;

			sideToMove ^= (Color)1;
		}

		// Applies a move and pushes state for undo
		public UndoInfo MakeSearchMove(Board board, Move move)
		{
			//if (board.bitboards[5] == 0 && board.bitboards[11] == 0)
			//{
			//    // No pieces on the board, cannot make a move
			//    throw new InvalidOperationException("bad board, king missing.");
			//}
			UndoInfo undo = new()
			{
				PreviousCastlingRights = board.castlingRights,
				PreviousEnPassantSquare = board.enPassantSquare,
				PreviousHalfmoveClock = board.halfmoveClock,
				PreviousFullmoveNumber = board.fullmoveNumber,
				PreviousZobristKey = board.zobristKey
			};
			ApplyMoveInternal(move);
			return undo;
		}

		public void MakeRealMove(Move move)
		{
			history.Push(SaveState());
			ApplyMoveInternal(move);
		}

		// Undoes the last made move
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void UnmakeMove(Move move, UndoInfo undoInfo)
		{
			PopRepetition();

			// 1) Restore metadata
			castlingRights = undoInfo.PreviousCastlingRights;
			enPassantSquare = undoInfo.PreviousEnPassantSquare;
			halfmoveClock = undoInfo.PreviousHalfmoveClock;
			fullmoveNumber = undoInfo.PreviousFullmoveNumber;
			zobristKey = undoInfo.PreviousZobristKey;

			// 2) Reverse piece movement
			int fromIdx = (int)move.From;
			int toIdx = (int)move.To;
			int moved = (int)move.PieceMoved;
			
			// Handle promotions
			if ((move.Flags & MoveFlags.Promotion) != 0)
			{
				int promo = (int)move.PromotionPiece;
				// Remove promoted piece
				bitboards[promo] &= ~(1UL << toIdx);

				// remove promoted piece value from material count, add back pawn value

				materialDelta -= (pieceValues[promo] - pieceValues[moved]);

				// Restore pawn on source
				bitboards[moved] |= (1UL << fromIdx);
				
			}
			else
			{
				// Normal move: remove from destination, restore at source
				bitboards[moved] &= ~(1UL << toIdx);
				bitboards[moved] |= (1UL << fromIdx);
			}

			// Handle captures
			if ((move.Flags & MoveFlags.EnPassant) != 0)
			{
				int capIdx = (moved == (int)Piece.WhitePawn) ? toIdx - 8 : toIdx + 8;
				int capPiece = (int)move.PieceCaptured;
				bitboards[capPiece] |= (1UL << capIdx);

				//add 1 pawn to material count for side that got captured
				// add back the material value of the captured pawn
				materialDelta += pieceValues[capPiece];

			}
			else if (move.PieceCaptured != Piece.None)
			{
				int captured = (int)move.PieceCaptured;
				bitboards[captured] |= (1UL << toIdx);

				//add piece value to material count for side that got captured
				// add back the material value of the captured piece
				materialDelta += pieceValues[captured];
			}

			// Handle castling rook restoration
			if ((move.Flags & MoveFlags.Castling) != 0)
			{
				if (move.PieceMoved == Piece.WhiteKing && toIdx - fromIdx == 2)
				{
					// King-side: rook from F1 back to H1
					bitboards[(int)Piece.WhiteRook] &= ~(1UL << (int)Square.F1);
					bitboards[(int)Piece.WhiteRook] |= (1UL << (int)Square.H1);
				}
				else if (move.PieceMoved == Piece.WhiteKing && fromIdx - toIdx == 2)
				{
					// Queen-side: rook from D1 back to A1
					bitboards[(int)Piece.WhiteRook] &= ~(1UL << (int)Square.D1);
					bitboards[(int)Piece.WhiteRook] |= (1UL << (int)Square.A1);
				}
				else if (move.PieceMoved == Piece.BlackKing && toIdx - fromIdx == 2)
				{
					// King-side: rook from F8 back to H8
					bitboards[(int)Piece.BlackRook] &= ~(1UL << (int)Square.F8);
					bitboards[(int)Piece.BlackRook] |= (1UL << (int)Square.H8);
				}
				else if (move.PieceMoved == Piece.BlackKing && fromIdx - toIdx == 2)
				{
					// Queen-side: rook from D8 back to A8
					bitboards[(int)Piece.BlackRook] &= ~(1UL << (int)Square.D8);
					bitboards[(int)Piece.BlackRook] |= (1UL << (int)Square.A8);
				}
			}

			// 3) Flip side to move back
			sideToMove = (sideToMove == Color.White) ? Color.Black : Color.White;

			// 4) Recompute occupancies
			UpdateOccupancies();
		}

		// Actual logic to mutate bitboards, occupancies, castling, en passant, etc.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void ApplyMoveInternal(Move move)
		{
			//if (move.From == Square.None || move.To == Square.None) { Console.Write("null move"); throw new Exception(move.ToString()); }
			
			//if (this.bitboards[5] == 0 && this.bitboards[11] == 0)
			//{
			//    // No kings
			//    throw new InvalidOperationException("bad board, king missing.");
			//}

			int fromIdx = (int)move.From;
			int toIdx = (int)move.To;
			int moved = (int)move.PieceMoved;

			//if (fromIdx < 0 || fromIdx >= 64)
			//    throw new Exception($"Invalid fromIdx: {fromIdx} in move {move}");
			//if (toIdx < 0 || toIdx >= 64)
			//    throw new Exception($"Invalid toIdx: {toIdx} in move {move}, piece moved: {move.PieceMoved}");
			//if (moved < 0 || moved >= bitboards.Length)
			//    throw new Exception($"Invalid moved: {moved} in move {move}");

			ulong fromMask = 1UL << fromIdx;
			ulong toMask = 1UL << toIdx;
			int toFile = toIdx & 7;

			// Remove old Zobrist state (only if relevant)
			zobristKey ^= Zobrist.SideToMove;

			if (enPassantSquare != Square.None)
			{
				zobristKey ^= Zobrist.EnPassantFile[(int)enPassantSquare & 7];
				enPassantSquare = Square.None;
			}

			Castling oldRights = castlingRights;
			UpdateCastlingRights(move);
			if (oldRights != castlingRights)
			{
				zobristKey ^= Zobrist.CastlingRights[(int)oldRights];
				zobristKey ^= Zobrist.CastlingRights[(int)castlingRights];
			}

			// Move piece off 'from'
			ref ulong movedBB = ref bitboards[moved];
			movedBB &= ~fromMask;
			zobristKey ^= Zobrist.PieceSquare[moved, fromIdx];

			// Handle capture
			if ((move.Flags & MoveFlags.EnPassant) != 0)
			{
				int capIdx = (moved == (int)Piece.WhitePawn) ? toIdx - 8 : toIdx + 8;
				ulong capMask = 1UL << capIdx;
				int capPiece = (int)move.PieceCaptured;
				ref ulong capBB = ref bitboards[capPiece];
				// remove pawn ep material value

				materialDelta -= pieceValues[capPiece]; 

				capBB &= ~capMask;
				zobristKey ^= Zobrist.PieceSquare[capPiece, capIdx];
			}
			else if (move.PieceCaptured != Piece.None)
			{
				int captured = (int)move.PieceCaptured;
				ref ulong capBB = ref bitboards[captured];
				capBB &= ~toMask;
				// remove captured material value

				materialDelta -= pieceValues[captured]; // if white moved, we subtract from black, which is net -value to materialDelta
				
				zobristKey ^= Zobrist.PieceSquare[captured, toIdx];
			}

			// Handle promotions
			if ((move.Flags & MoveFlags.Promotion) != 0)
			{
				int promo = (int)move.PromotionPiece;
				ref ulong promoBB = ref bitboards[promo];

				materialDelta += pieceValues[promo]-pieceValues[moved]; // add promo value, remove pawn value

				promoBB |= toMask;
				zobristKey ^= Zobrist.PieceSquare[promo, toIdx];
			}
			else
			{
				movedBB |= toMask;
				zobristKey ^= Zobrist.PieceSquare[moved, toIdx];
			}

			// Handle castling
			if ((move.Flags & MoveFlags.Castling) != 0)
			{
				int rookFrom, rookTo;
				int rookPiece = (move.PieceMoved == Piece.WhiteKing) ? (int)Piece.WhiteRook : (int)Piece.BlackRook;

				if (toIdx > fromIdx)
				{
					// Kingside
					rookFrom = (rookPiece == (int)Piece.WhiteRook) ? (int)Square.H1 : (int)Square.H8;
					rookTo = (rookPiece == (int)Piece.WhiteRook) ? (int)Square.F1 : (int)Square.F8;
				}
				else
				{
					// Queenside
					rookFrom = (rookPiece == (int)Piece.WhiteRook) ? (int)Square.A1 : (int)Square.A8;
					rookTo = (rookPiece == (int)Piece.WhiteRook) ? (int)Square.D1 : (int)Square.D8;
				}

				ulong rookFromMask = 1UL << rookFrom;
				ulong rookToMask = 1UL << rookTo;

				ref ulong rookBB = ref bitboards[rookPiece];
				rookBB &= ~rookFromMask;
				rookBB |= rookToMask;

				zobristKey ^= Zobrist.PieceSquare[rookPiece, rookFrom];
				zobristKey ^= Zobrist.PieceSquare[rookPiece, rookTo];
			}

			// Set en passant square for double pawn push
			if ((move.Flags & MoveFlags.EnPassant) != 0)
			{
				enPassantSquare = (Square)((sideToMove == Color.White) ? toIdx - 8 : toIdx + 8);
				zobristKey ^= Zobrist.EnPassantFile[toFile];
			}

			// Update occupancies at end
			UpdateOccupancies();

			// Update side to move
			sideToMove ^= (Color)1;

			// Update halfmove clock
			if (move.PieceMoved == Piece.WhitePawn || move.PieceMoved == Piece.BlackPawn || move.PieceCaptured != Piece.None)
				halfmoveClock = 0;
			else
				halfmoveClock++;

			if (sideToMove == Color.White)
				fullmoveNumber++;

			//resets to false so no stale trues get used
			irreversibleTracker[boardRepPly] = false;

			if ((move.PieceMoved == Piece.WhitePawn) || (move.PieceMoved == Piece.BlackPawn) || (move.Flags & (MoveFlags.EnPassant | MoveFlags.Capture | MoveFlags.Promotion)) != 0)
				irreversibleTracker[boardRepPly] = true;
			PushRepetition();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void UpdateOccupancies()
		{
			// Local variables for speed
			ulong white = 0UL, black = 0UL;
			// Unroll loop for 6 piece types (0-5: white, 6-11: black)
			white |= bitboards[0];
			white |= bitboards[1];
			white |= bitboards[2];
			white |= bitboards[3];
			white |= bitboards[4];
			white |= bitboards[5];

			black |= bitboards[6];
			black |= bitboards[7];
			black |= bitboards[8];
			black |= bitboards[9];
			black |= bitboards[10];
			black |= bitboards[11];
			occupancies[(int)Color.White] = white;
			occupancies[(int)Color.Black] = black;
			occupancies[2] = white | black;
		}

	}
}