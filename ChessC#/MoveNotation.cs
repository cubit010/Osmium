
namespace ChessC_
{
    public static class MoveNotation
    {
        private static readonly Dictionary<Piece, char> PieceToChar = new()
        {
            { Piece.WhiteKnight, 'N' }, { Piece.BlackKnight, 'N' },
            { Piece.WhiteBishop, 'B' }, { Piece.BlackBishop, 'B' },
            { Piece.WhiteRook,   'R' }, { Piece.BlackRook,   'R' },
            { Piece.WhiteQueen,  'Q' }, { Piece.BlackQueen,  'Q' },
            { Piece.WhiteKing,   'K' }, { Piece.BlackKing,   'K' }
        };

        // Convert square enum to algebraic notation string like "e4"
        public static string SquareToString(Square sq)
        {
            if (sq == Square.None) return "";
            int sqIndex = (int)sq;
            int file = sqIndex % 8; // 0 = a, …, 7 = h
            int rank = sqIndex / 8; // 0 = rank1, …, 7 = rank8
            return $"{(char)('a' + file)}{rank + 1}";
        }

        /// <summary>
        /// Default call when you already know there is no ambiguity (or if you don't care).
        /// </summary>
        public static string ToAlgebraicNotation(Move move)
        {
            // Fallback to non‐disambiguating version
            return ToAlgebraicNotation(move, null);
        }

        public static string[] ToIndexedAlgebraicList(IEnumerable<Move> moves)
        {
            if (moves == null) return ["(null)"];
            return [.. moves.Select((m, i) => $"[{i}] {ToAlgebraicNotation(m)}")];
        }

        /// <summary>
        /// Call this overload with the full list of legal moves in the position.
        /// It will disambiguate automatically if two (or more) identical pieces can go to move.To.
        /// </summary>
        public static string ToAlgebraicNotation(Move move, Span<Move> legalMoves)
        {
            // 1) Handle castling
            if ((move.Flags & MoveFlags.Castling) != 0)
            {
                int fromFile = ((int)move.From) % 8;
                int toFile = ((int)move.To) % 8;
                string castle = toFile > fromFile ? "O-O" : "O-O-O";
                return castle + CheckOrMateSuffix(move.Flags);
            }

            Piece piece = move.PieceMoved;
            bool isPawn = piece == Piece.WhitePawn || piece == Piece.BlackPawn;

            string fromSq = SquareToString(move.From);
            string toSq = SquareToString(move.To);
            string promo = "";
            if ((move.Flags & MoveFlags.Promotion) != 0)
                promo = "=" + Char.ToUpper(PromotionChar(move.PromotionPiece));

            string capture = (move.Flags & MoveFlags.Capture) != 0 ? "x" : "";
            string checkOrMate = CheckOrMateSuffix(move.Flags);

            if (isPawn)
            {
                if ((move.Flags & MoveFlags.Capture) != 0)
                {
                    char fromFileChar = fromSq[0];
                    return $"{fromFileChar}x{toSq}{promo}{checkOrMate}";
                }
                else
                {
                    return $"{toSq}{promo}{checkOrMate}";
                }
            }
            else
            {
                char pieceChar = PieceToChar[piece];
                string disamb = "";

                // Disambiguation: scan span
                for (int i = 0; i < legalMoves.Length; i++)
                {
                    var m = legalMoves[i];
                    if (m.Equals(move)) continue;
                    if (m.PieceMoved == move.PieceMoved && m.To == move.To)
                    {
                        // ambiguity exists
                        bool fileUnique = true;
                        bool rankUnique = true;
                        for (int j = 0; j < legalMoves.Length; j++)
                        {
                            var o = legalMoves[j];
                            if (o.Equals(move)) continue;
                            if (o.PieceMoved == move.PieceMoved && o.To == move.To)
                            {
                                if (((int)o.From % 8) == ((int)move.From % 8)) fileUnique = false;
                                if (((int)o.From / 8) == ((int)move.From / 8)) rankUnique = false;
                            }
                        }
                        if (fileUnique)
                            disamb = ((char)('a' + ((int)move.From % 8))).ToString();
                        else if (rankUnique)
                            disamb = (((int)move.From / 8) + 1).ToString();
                        else
                            disamb = ((char)('a' + ((int)move.From % 8))).ToString() + (((int)move.From / 8) + 1).ToString();
                        break;
                    }
                }

                return $"{pieceChar}{disamb}{capture}{toSq}{promo}{checkOrMate}";
            }
        }

        private static string CheckOrMateSuffix(MoveFlags flags)
        {
            return (flags & MoveFlags.Checkmate) != 0 ? "#" :
                   (flags & MoveFlags.Check) != 0 ? "+" :
                   "";
        }

        private static char PromotionChar(Piece p)
        {
            return p switch
            {
                Piece.WhiteQueen or Piece.BlackQueen => 'Q',
                Piece.WhiteRook or Piece.BlackRook => 'R',
                Piece.WhiteBishop or Piece.BlackBishop => 'B',
                Piece.WhiteKnight or Piece.BlackKnight => 'N',
                _ => '?'
            };
        }
    }
}
