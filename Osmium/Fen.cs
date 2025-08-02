using System;
using System.Globalization;
using System.Text;

namespace Osmium
{
    /// <summary>
    /// Utility class for parsing and generating FEN (Forsyth–Edwards Notation) strings.
    /// </summary>
    public static class Fen
    {
        internal static void LoadStartPos(Board board)
        {
            LoadFEN(board, "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        }
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
                
                board.enPassantSquare = (Square)((rankIdx) * 8 + fileIdx); // rank 4
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
        private static char PieceToChar(Piece piece)
        {
            return piece switch
            {
                Piece.WhitePawn => 'P',
                Piece.WhiteKnight => 'N',
                Piece.WhiteBishop => 'B',
                Piece.WhiteRook => 'R',
                Piece.WhiteQueen => 'Q',
                Piece.WhiteKing => 'K',
                Piece.BlackPawn => 'p',
                Piece.BlackKnight => 'n',
                Piece.BlackBishop => 'b',
                Piece.BlackRook => 'r',
                Piece.BlackQueen => 'q',
                Piece.BlackKing => 'k',
                _ => '.'
            };
        }
        internal static string ToFEN(Board board)
        {
            var sb = new StringBuilder();

            // 1) Piece placement
            for (int rank = 7; rank >= 0; rank--)
            {
                int empty = 0;
                for (int file = 0; file < 8; file++)
                {
                    int sq = rank * 8 + file;
                    Piece piece = MoveGen.FindPieceAt(board, sq);
                    if (piece == Piece.None)
                    {
                        empty++;
                    }
                    else
                    {
                        if (empty > 0)
                        {
                            sb.Append(empty);
                            empty = 0;
                        }
                        sb.Append(PieceToChar(piece));
                    }
                }
                if (empty > 0)
                    sb.Append(empty);
                if (rank > 0)
                    sb.Append('/');
            }

            // 2) Active color
            sb.Append(' ');
            sb.Append(board.sideToMove == Color.White ? 'w' : 'b');

            // 3) Castling rights
            sb.Append(' ');
            var castling = board.castlingRights;
            string castlingStr = "";
            if ((castling & Castling.WhiteKing) != 0) castlingStr += "K";
            if ((castling & Castling.WhiteQueen) != 0) castlingStr += "Q";
            if ((castling & Castling.BlackKing) != 0) castlingStr += "k";
            if ((castling & Castling.BlackQueen) != 0) castlingStr += "q";
            sb.Append(string.IsNullOrEmpty(castlingStr) ? "-" : castlingStr);

            // 4) En passant target square
            sb.Append(' ');
            if (board.enPassantSquare == Square.None)
            {
                sb.Append('-');
            }
            else
            {
                int file = (int)board.enPassantSquare % 8;
                int rank = (int)board.enPassantSquare / 8;
                sb.Append((char)('a' + file));
                sb.Append((char)('1' + rank));
            }

            // 5) Halfmove clock
            sb.Append(' ');
            sb.Append(board.halfmoveClock);

            // 6) Fullmove number
            sb.Append(' ');
            sb.Append(board.fullmoveNumber);

            return sb.ToString();
        }
    }
}
