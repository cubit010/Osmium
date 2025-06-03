using System;
using System.Globalization;

namespace ChessC_
{
    /// <summary>
    /// Utility class for parsing and generating FEN (Forsyth–Edwards Notation) strings.
    /// </summary>
    public static class Fen
    {
        /// <summary>
        /// Loads a FEN string into the given board, resetting all state.
        /// </summary>
        internal static void LoadFEN(Board board, string fen)
        {
            var parts = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4)
                throw new ArgumentException("Invalid FEN: must contain at least 4 fields", nameof(fen));

            // 1) Piece placement
            Array.Clear(board.bitboards, 0, board.bitboards.Length);
            var rows = parts[0].Split('/');
            if (rows.Length != 8)
                throw new ArgumentException("Invalid FEN: board part must have 8 ranks", nameof(fen));

            for (int rank = 0; rank < 8; rank++)
            {
                int file = 0;
                foreach (char c in rows[rank])
                {
                    if (char.IsDigit(c))
                    {
                        file += c - '0';
                    }
                    else
                    {
                        int sq = (7 - rank) * 8 + file;
                        Piece piece = CharToPiece(c);
                        if (piece == Piece.None)
                            throw new ArgumentException($"Invalid FEN: unrecognized piece '{c}'", nameof(fen));
                        board.bitboards[(int)piece] |= 1UL << sq;
                        file++;
                    }
                }
                if (file != 8)
                    throw new ArgumentException("Invalid FEN: rank does not sum to 8 files", nameof(fen));
            }

            // 2) Active color
            board.sideToMove = parts[1] == "w" ? Color.White : Color.Black;

            // 3) Castling rights
            board.castlingRights = Castling.None;
            if (parts[2] != "-")
            {
                foreach (char c in parts[2])
                {
                    switch (c)
                    {
                        case 'K': board.castlingRights |= Castling.WhiteKing; break;
                        case 'Q': board.castlingRights |= Castling.WhiteQueen; break;
                        case 'k': board.castlingRights |= Castling.BlackKing; break;
                        case 'q': board.castlingRights |= Castling.BlackQueen; break;
                        default:
                            throw new ArgumentException($"Invalid FEN: bad castling char '{c}'", nameof(fen));
                    }
                }
            }

            // 4) En passant target
            board.enPassantSquare = Square.None;
            if (parts[3] != "-")
            {
                string ep = parts[3];
                int fileIdx = ep[0] - 'a';
                int rankIdx = ep[1] - '1';
                if (fileIdx < 0 || fileIdx > 7 || rankIdx < 0 || rankIdx > 7)
                    throw new ArgumentException($"Invalid FEN: bad en passant square '{ep}'", nameof(fen));

                int targetSquare = rankIdx * 8 + fileIdx;

                // Adjust to the square where the pawn that moved is
                // If the en passant square is on rank 3 → pawn came from rank 2 → now on rank 4
                if (rankIdx == 2) // '3' in FEN (White just moved)
                    board.enPassantSquare = (Square)((rankIdx + 1) * 8 + fileIdx); // rank 4
                else if (rankIdx == 5) // '6' in FEN (Black just moved)
                    board.enPassantSquare = (Square)((rankIdx - 1) * 8 + fileIdx); // rank 5
                else
                    throw new ArgumentException($"Invalid FEN: unexpected en passant square rank '{ep}'", nameof(fen));
            }

            // 5) Halfmove clock
            if (parts.Length > 4 && int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int half))
                board.halfmoveClock = half;
            else
                board.halfmoveClock = 0;

            // 6) Fullmove number
            if (parts.Length > 5 && int.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out int full))
                board.fullmoveNumber = full;
            else
                board.fullmoveNumber = 1;

            // Rebuild occupancies and compute initial Zobrist
            board.UpdateOccupancies();
            board.ComputeInitialZobrist();
        }

        private static Piece CharToPiece(char c)
        {
            return c switch
            {
                'P' => Piece.WhitePawn,
                'N' => Piece.WhiteKnight,
                'B' => Piece.WhiteBishop,
                'R' => Piece.WhiteRook,
                'Q' => Piece.WhiteQueen,
                'K' => Piece.WhiteKing,
                'p' => Piece.BlackPawn,
                'n' => Piece.BlackKnight,
                'b' => Piece.BlackBishop,
                'r' => Piece.BlackRook,
                'q' => Piece.BlackQueen,
                'k' => Piece.BlackKing,
                _ => Piece.None
            };
        }
    }
}
