
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
        public static string ToAlgebraicNotation(Move move, List<Move> legalMoves)
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
            bool isPawn = (piece == Piece.WhitePawn || piece == Piece.BlackPawn);

            string fromSq = SquareToString(move.From);
            string toSq = SquareToString(move.To);
            string promo = "";
            if ((move.Flags & MoveFlags.Promotion) != 0)
                promo = "=" + Char.ToUpper(PromotionChar(move.PromotionPiece));

            string capture = (move.Flags & MoveFlags.Capture) != 0 ? "x" : "";
            string checkOrMate = CheckOrMateSuffix(move.Flags);

            if (isPawn)
            {
                // Pawn moves: exd5 or d5
                if ((move.Flags & MoveFlags.Capture) != 0)
                {
                    // file of origin + 'x' + toSquare
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
                // Non-pawns: Nf3, Raxd1, etc.
                char pieceChar = PieceToChar[piece];

                // If there are other same-piece moves that also go to move.To, we must disambiguate.
                string disamb = "";
                if (legalMoves != null)
                {
                    // Find all other moves in legalMoves that:
                    //   1) move the same piece type (e.g. WhiteKnight),
                    //   2) have different 'From', but same 'To'
                    var ambiguous = legalMoves
                        .Where(m =>
                            m.PieceMoved == move.PieceMoved &&
                            m.To == move.To &&
                            m.From != move.From)
                        .ToList();

                    if (ambiguous.Count > 0)
                    {
                        // Among ambiguous candidates, check if file alone suffices:
                        bool fileUnique = ambiguous.All(m => ((int)m.From % 8) != ((int)move.From % 8));
                        if (fileUnique)
                        {
                            disamb = $"{(char)('a' + ((int)move.From % 8))}";
                        }
                        else
                        {
                            // If files overlap, check if rank alone suffices:
                            bool rankUnique = ambiguous.All(m => ((int)m.From / 8) != ((int)move.From / 8));
                            if (rankUnique)
                            {
                                disamb = $"{((int)move.From / 8) + 1}";
                            }
                            else
                            {
                                // Otherwise, include both file and rank
                                disamb = $"{(char)('a' + ((int)move.From % 8))}{((int)move.From / 8) + 1}";
                            }
                        }
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
