using System;
using System.Collections.Generic;

namespace ChessC_
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
			enPassantSquare = Square.None;
		}
		public ulong[] bitboards = new ulong[12]; // 6 piece types × 2 colors
		public ulong[] occupancies = new ulong[3]; // White, Black, Both

		public Color sideToMove;
		public Castling castlingRights;
		public Square enPassantSquare;

		public int halfmoveClock;
		public int fullmoveNumber;

		public ulong zobristKey;
		
		// History stack
		private Stack<BoardState> history = [];

		// Put in a constructor or reset method
		public void ComputeInitialZobrist()
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

		// Applies a move and pushes state for undo
		public UndoInfo MakeSearchMove(Board board, Move move)
		{
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
		public void UnmakeMove(Move move, UndoInfo undoInfo)
		{
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
                Piece capPiece = (moved == (int)Piece.WhitePawn) ? Piece.BlackPawn : Piece.WhitePawn;
                bitboards[(int)capPiece] |= (1UL << capIdx);
            }
            else if (move.PieceCaptured != Piece.None)
            {
                int captured = (int)move.PieceCaptured;
                bitboards[captured] |= (1UL << toIdx);
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
        private void ApplyMoveInternal(Move move)
        {
            int fromIdx = (int)move.From;
            int toIdx = (int)move.To;
            int moved = (int)move.PieceMoved;
            ulong fromMask = 1UL << fromIdx;
            ulong toMask = 1UL << toIdx;

            // Remove old Zobrist state
            zobristKey ^= Zobrist.SideToMove;
            zobristKey ^= Zobrist.CastlingRights[(int)castlingRights];
            if (enPassantSquare != Square.None)
                zobristKey ^= Zobrist.EnPassantFile[(int)enPassantSquare % 8];

            // Clear en passant
            enPassantSquare = Square.None;

            // Update castling rights and Zobrist
            UpdateCastlingRights(move);
            zobristKey ^= Zobrist.CastlingRights[(int)castlingRights];

            // Remove moving piece from source
            bitboards[moved] &= ~fromMask;
            zobristKey ^= Zobrist.PieceSquare[moved, fromIdx];

            // Handle captures (including en passant)
            if ((move.Flags & MoveFlags.EnPassant) != 0)
            {
                int capIdx = (moved == (int)Piece.WhitePawn) ? toIdx - 8 : toIdx + 8;
                ulong capMask = 1UL << capIdx;
                int capPiece = (moved == (int)Piece.WhitePawn) ? (int)Piece.BlackPawn : (int)Piece.WhitePawn;
                bitboards[capPiece] &= ~capMask;
                zobristKey ^= Zobrist.PieceSquare[capPiece, capIdx];
            }
            else if (move.PieceCaptured != Piece.None)
            {
                int captured = (int)move.PieceCaptured;
                bitboards[captured] &= ~toMask;
                zobristKey ^= Zobrist.PieceSquare[captured, toIdx];
            }

            // Promotions
            if ((move.Flags & MoveFlags.Promotion) != 0)
            {
                int promo = (int)move.PromotionPiece;
                bitboards[promo] |= toMask;
                zobristKey ^= Zobrist.PieceSquare[promo, toIdx];
            }
            else
            {
                bitboards[moved] |= toMask;
                zobristKey ^= Zobrist.PieceSquare[moved, toIdx];
            }

            // Castling move (move rook)
            if ((move.Flags & MoveFlags.Castling) != 0)
            {
                int rookFrom, rookTo;
                int rookPiece;
                ulong rookFromMask, rookToMask;
                if (move.PieceMoved == Piece.WhiteKing)
                {
                    rookPiece = (int)Piece.WhiteRook;
                    if (toIdx - fromIdx == 2)
                    {
                        // White king-side
                        rookFrom = (int)Square.H1; rookTo = (int)Square.F1;
                    }
                    else
                    {
                        // White queen-side
                        rookFrom = (int)Square.A1; rookTo = (int)Square.D1;
                    }
                }
                else
                {
                    rookPiece = (int)Piece.BlackRook;
                    if (toIdx - fromIdx == 2)
                    {
                        // Black king-side
                        rookFrom = (int)Square.H8; rookTo = (int)Square.F8;
                    }
                    else
                    {
                        // Black queen-side
                        rookFrom = (int)Square.A8; rookTo = (int)Square.D8;
                    }
                }
                rookFromMask = 1UL << rookFrom;
                rookToMask = 1UL << rookTo;
                bitboards[rookPiece] &= ~rookFromMask;
                bitboards[rookPiece] |= rookToMask;
                zobristKey ^= Zobrist.PieceSquare[rookPiece, rookFrom];
                zobristKey ^= Zobrist.PieceSquare[rookPiece, rookTo];
            }

            // En passant target (double pawn push: set to square behind destination square)
            if (move.PieceMoved == Piece.WhitePawn && toIdx - fromIdx == 16)
            {
                enPassantSquare = (Square)toIdx - 8;
                zobristKey ^= Zobrist.EnPassantFile[toIdx % 8];
            }
            else if (move.PieceMoved == Piece.BlackPawn && fromIdx - toIdx == 16)
            {
                enPassantSquare = (Square)toIdx + 8;
                zobristKey ^= Zobrist.EnPassantFile[toIdx % 8];
            }

            // Update occupancies once at the end
            UpdateOccupancies();

            // Update side to move and move counters
            sideToMove = sideToMove == Color.White ? Color.Black : Color.White;
            if (move.PieceMoved == Piece.WhitePawn || move.PieceMoved == Piece.BlackPawn || move.PieceCaptured != Piece.None)
                halfmoveClock = 0;
            else
                halfmoveClock++;

            if (sideToMove == Color.White)
                fullmoveNumber++;
        }
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