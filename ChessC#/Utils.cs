using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessC_
{
    internal class Utils
    {
        public static string SquareToString(int square)
        {
            int rank = square / 8;
            int file = square % 8;
            return $"{(char)('a' + file)}{rank + 1}";
        }
        public static string SquareToString(Square square)
        {
            int rank = (int)square / 8;
            int file = (int)square % 8;
            return $"{(char)('a' + file)}{rank + 1}";
        }

        public static int StringToSquare(string s)
        {
            int file = s[0] - 'a';
            int rank = s[1] - '1';
            return rank * 8 + file;
        }
        public static string GetBoardString(Board board)
        {
            var sb = new StringBuilder();

            for (int rank = 7; rank >= 0; rank--)
            {
                sb.Append($"{rank + 1} "); // Rank labels
                for (int file = 0; file < 8; file++)
                {
                    int squareIndex = rank * 8 + file;
                    Piece piece = FindPieceAt(board, (Square)squareIndex);

                    if (piece == Piece.None)
                        sb.Append(". ");
                    else
                        sb.Append($"{PieceToChar(piece)} ");
                }
                sb.AppendLine();
            }

            sb.AppendLine("  a b c d e f g h\n"); // File labels

            return sb.ToString();
        }
        public static void PrintBoard(Board board)
        {
            Console.WriteLine(GetBoardString(board));
        }
        public static void PrintBitboard(ulong bitboard)
        {
            for (int rank = 7; rank >= 0; rank--)
            {
                Console.Write($"{rank + 1} ");
                for (int file = 0; file < 8; file++)
                {
                    int square = rank * 8 + file;
                    bool isSet = ((bitboard >> square) & 1UL) != 0;
                    Console.Write(isSet ? "1 " : ". ");
                }
                Console.WriteLine();
            }
            Console.WriteLine("  a b c d e f g h");
        }

        private static Piece FindPieceAt(Board board, Square sq)
        {
            ulong mask = 1UL << (int)sq;
            for (int i = 0; i < 12; i++)
            {
                if ((board.bitboards[i] & mask) != 0)
                    return (Piece)i;
            }
            return Piece.None;
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

                _ => '?'
            };
        }
    }
}
