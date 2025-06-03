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

	internal class Board
	{
		public ulong[] bitboards = new ulong[12]; // 6 piece types × 2 colors
		public ulong[] occupancies = new ulong[3]; // White, Black, Both

		public Color sideToMove;
		public Castling castlingRights;
		public Square enPassantSquare;

		public int halfmoveClock;
		public int fullmoveNumber;

		public ulong zobristKey;
		
		// History stack
		private Stack<BoardState> history = new Stack<BoardState>();

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

		// Restore board state
		private void RestoreState(BoardState state)
		{
			bitboards = (ulong[])state.Bitboards.Clone();
			occupancies = (ulong[])state.Occupancies.Clone();
			sideToMove = state.SideToMove;
			castlingRights = state.CastlingRights;
			enPassantSquare = state.EnPassantSquare;
			halfmoveClock = state.HalfmoveClock;
			fullmoveNumber = state.FullmoveNumber;
			zobristKey = state.ZobristKey;	
		}

		// Applies a move and pushes state for undo
		public void MakeMove(Move move)
		{

			history.Push(SaveState());
			ApplyMoveInternal(move);
		}

		// Undoes the last made move
		public void UnmakeMove()
		{
			if (history.Count > 0)
			{
				var prev = history.Pop();
				RestoreState(prev);
			}
		}

        // Actual logic to mutate bitboards, occupancies, castling, en passant, etc.
        // Actual logic to mutate bitboards, occupancies, castling, en passant, etc.
        private void ApplyMoveInternal(Move move)
        {
            // Zobrist: remove old state
            zobristKey ^= Zobrist.SideToMove;
            zobristKey ^= Zobrist.CastlingRights[(int)castlingRights];
            if (enPassantSquare != Square.None)
                zobristKey ^= Zobrist.EnPassantFile[(int)enPassantSquare % 8];

            // Clear en passant
            enPassantSquare = Square.None;

            // Update castling rights
            UpdateCastlingRights(move);
            zobristKey ^= Zobrist.CastlingRights[(int)castlingRights];

            // Move piece from Source to Destination
            int fromIdx = (int)move.From;
            int toIdx = (int)move.To;
            int moved = (int)move.PieceMoved;

            // Zobrist: remove moving piece from source
            zobristKey ^= Zobrist.PieceSquare[moved, fromIdx];

            // Remove from source
            bitboards[moved] &= ~(1UL << fromIdx);

            // Handle captures
            if (move.Flags.HasFlag(MoveFlags.EnPassant))
            {
                int capIdx = (moved == (int)Piece.WhitePawn) ? toIdx - 8 : toIdx + 8;
                int capPiece = (moved == (int)Piece.WhitePawn) ? (int)Piece.BlackPawn : (int)Piece.WhitePawn;
                bitboards[capPiece] &= ~(1UL << capIdx);
                zobristKey ^= Zobrist.PieceSquare[capPiece, capIdx];
            }
            else if (move.PieceCaptured != Piece.None)
            {
                int captured = (int)move.PieceCaptured;
                bitboards[captured] &= ~(1UL << toIdx);
                zobristKey ^= Zobrist.PieceSquare[captured, toIdx];
            }

            // Promotions
            if (move.Flags.HasFlag(MoveFlags.Promotion))
            {
                // (A) The pawn was already “removed from fromIdx” above,
                //     and we do NOT place a pawn at toIdx. So do NOT manipulate
                //     bitboards[moved] at toIdx.

                // (B) Instead, put the promotion piece on toIdx:
                bitboards[moved] &= ~(1UL << toIdx);
                zobristKey ^= Zobrist.PieceSquare[moved, toIdx];

                // now add the promoted piece (queen/rook/bishop/knight)
                int promo = (int)move.PromotionPiece;
                bitboards[promo] |= (1UL << toIdx);
                zobristKey ^= Zobrist.PieceSquare[promo, toIdx];
            }
            else
            {
                // Normal (non‐promotion) placement of the moved piece
                bitboards[moved] |= (1UL << toIdx);
                zobristKey ^= Zobrist.PieceSquare[moved, toIdx];
            }

            // Update occupancies
            occupancies[(int)Color.White] = 0UL;
            occupancies[(int)Color.Black] = 0UL;
            for (int i = 0; i < 6; i++)
            {
                occupancies[(int)Color.White] |= bitboards[i];
                occupancies[(int)Color.Black] |= bitboards[i + 6];
            }
            occupancies[2] = occupancies[0] | occupancies[1];

            // En passant target
            if (move.PieceMoved == Piece.WhitePawn && toIdx - fromIdx == 16)
            {
                enPassantSquare = (Square)(fromIdx + 16);
                zobristKey ^= Zobrist.EnPassantFile[fromIdx % 8];
            }
            else if (move.PieceMoved == Piece.BlackPawn && fromIdx - toIdx == 16)
            {
                enPassantSquare = (Square)(fromIdx - 16);
                zobristKey ^= Zobrist.EnPassantFile[fromIdx % 8];
            }

            // Castling move
            if (move.Flags.HasFlag(MoveFlags.Castling))
            {
                if (move.PieceMoved == Piece.WhiteKing && toIdx - fromIdx == 2)
                {
                    bitboards[(int)Piece.WhiteRook] &= ~(1UL << (int)Square.H1);
                    zobristKey ^= Zobrist.PieceSquare[(int)Piece.WhiteRook, (int)Square.H1];
                    bitboards[(int)Piece.WhiteRook] |= (1UL << (int)Square.F1);
                    zobristKey ^= Zobrist.PieceSquare[(int)Piece.WhiteRook, (int)Square.F1];
                }
                else if (move.PieceMoved == Piece.WhiteKing && fromIdx - toIdx == 2)
                {
                    bitboards[(int)Piece.WhiteRook] &= ~(1UL << (int)Square.A1);
                    zobristKey ^= Zobrist.PieceSquare[(int)Piece.WhiteRook, (int)Square.A1];
                    bitboards[(int)Piece.WhiteRook] |= (1UL << (int)Square.D1);
                    zobristKey ^= Zobrist.PieceSquare[(int)Piece.WhiteRook, (int)Square.D1];
                }
                else if (move.PieceMoved == Piece.BlackKing && toIdx - fromIdx == 2)
                {
                    bitboards[(int)Piece.BlackRook] &= ~(1UL << (int)Square.H8);
                    zobristKey ^= Zobrist.PieceSquare[(int)Piece.BlackRook, (int)Square.H8];
                    bitboards[(int)Piece.BlackRook] |= (1UL << (int)Square.F8);
                    zobristKey ^= Zobrist.PieceSquare[(int)Piece.BlackRook, (int)Square.F8];
                }
                else if (move.PieceMoved == Piece.BlackKing && fromIdx - toIdx == 2)
                {
                    bitboards[(int)Piece.BlackRook] &= ~(1UL << (int)Square.A8);
                    zobristKey ^= Zobrist.PieceSquare[(int)Piece.BlackRook, (int)Square.A8];
                    bitboards[(int)Piece.BlackRook] |= (1UL << (int)Square.D8);
                    zobristKey ^= Zobrist.PieceSquare[(int)Piece.BlackRook, (int)Square.D8];
                }
            }

            // Update side to move and move counters
            sideToMove = sideToMove == Color.White ? Color.Black : Color.White;
            if (move.PieceMoved == Piece.WhitePawn || move.PieceCaptured != Piece.None)
                halfmoveClock = 0;
            else
                halfmoveClock++;

            if (sideToMove == Color.White)
                fullmoveNumber++;
        }

        /// <summary>
        /// Recomputes occupancies[White], occupancies[Black], and occupancies[Both]
        /// from the per‐piece bitboards.
        /// </summary>
        public void UpdateOccupancies()
        {
            occupancies[(int)Color.White] = 0UL;
            occupancies[(int)Color.Black] = 0UL;
            for (int p = 0; p < 6; p++)
            {
                occupancies[(int)Color.White] |= bitboards[p];
                occupancies[(int)Color.Black] |= bitboards[p + 6];
            }
            occupancies[2] = occupancies[(int)Color.White] | occupancies[(int)Color.Black];
        }
    }
}