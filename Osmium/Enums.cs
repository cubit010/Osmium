
namespace Osmium
{
    public enum Square
    {
        A1, B1, C1, D1, E1, F1, G1, H1,
        A2, B2, C2, D2, E2, F2, G2, H2,
        A3, B3, C3, D3, E3, F3, G3, H3,
        A4, B4, C4, D4, E4, F4, G4, H4,
        A5, B5, C5, D5, E5, F5, G5, H5,
        A6, B6, C6, D6, E6, F6, G6, H6,
        A7, B7, C7, D7, E7, F7, G7, H7,
        A8, B8, C8, D8, E8, F8, G8, H8, 
        None = -1
    }
    
    public enum Piece
    {
        WhitePawn = 0,
        WhiteKnight = 1,
        WhiteBishop = 2,
        WhiteRook = 3,
        WhiteQueen = 4,
        WhiteKing = 5,
        BlackPawn = 6,
        BlackKnight = 7,
        BlackBishop = 8,
        BlackRook = 9,
        BlackQueen = 10,
        BlackKing = 11,

        None = -1  
    }

    public enum Color
    {
        White, Black
    }
    
    [Flags]
    public enum Castling
    {
        None = 0b0000, 
        WhiteKing = 0b0001,     // 1
        WhiteQueen = 0b0010,    // 2
        BlackKing = 0b0100,     // 4
        BlackQueen = 0b1000     // 8
    }
    [Flags]
    public enum MoveFlags
    {
        None = 0,
        Capture = 1 << 0,
        Promotion = 1 << 1,
        EnPassant = 1 << 2,
        Castling = 1 << 3,
        Check = 1 << 4,
        Checkmate = 1 << 5,
    }

    public struct Move
    {
        public Square From;
        public Square To;
        public Piece PromotionPiece; 
        public MoveFlags Flags;

        public Piece PieceMoved;
        public Piece PieceCaptured;
        public bool Equals(Move other)
            => From == other.From
            && To == other.To
            && PromotionPiece == other.PromotionPiece;

        public override int GetHashCode()
            => ((int)From)
             | ((int)To << 6)
             | ((int)PromotionPiece << 12);

        public ushort Encode()
        {
            // Pack into 16 bits: From (6 bits) | To (6 bits) | Flags (4 bits)
            return (ushort)(((int)From & 0x3F) | (((int)To & 0x3F) << 6) | (((int)Flags & 0xF) << 12));
        }
        public static Move FromEncoded(ushort code)
        {
            Move move = new Move();
            move.From = (Square)(code & 0x3F);
            move.To = (Square)((code >> 6) & 0x3F);
            move.Flags = (MoveFlags)((code >> 12) & 0xF);
            return move;
        }

        public Move(
            Square from,
            Square to,
            Piece moved,
            Piece capture = Piece.None,
            MoveFlags flag = MoveFlags.None,
            Piece promotion = Piece.None)
        {
            From = from;
            To = to;
            PieceMoved = moved;
            PromotionPiece = promotion;
            PieceCaptured = capture;
            Flags = flag;

            //if (promotion != Piece.None)
            //    Flags |= MoveFlags.Promotion;
            //if (capture != Piece.None)
            //    Flags |= MoveFlags.Capture;
        }


        

        public override string ToString()
        {
            //UCI style
            string move = $"{From}{To}";
            if ((Flags & MoveFlags.Promotion) != 0)
                move += PromotionToChar(PromotionPiece);
            return move;
        }

        //must not be called in a foreach because foreach modifies flags by creating a separate copy
        public void AddMoveFlag(MoveFlags flag)
        {
            this.Flags |= flag;
        }

        private static char PromotionToChar(Piece piece)
        {
            return piece switch
            {
                Piece.WhiteQueen or Piece.BlackQueen => 'q',
                Piece.WhiteRook or Piece.BlackRook => 'r',
                Piece.WhiteBishop or Piece.BlackBishop => 'b',
                Piece.WhiteKnight or Piece.BlackKnight => 'n',
                _ => '?'
            };
        }
    }
}

