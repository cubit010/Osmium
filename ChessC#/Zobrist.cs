
namespace ChessC_
{
	/// <summary>
	/// Zobrist hashing constants and utilities for the Chess engine.
	/// </summary>
	static class Zobrist
	{
		// 12 piece types (WhitePawn..BlackKing) × 64 squares
		public static readonly ulong[,] PieceSquare = new ulong[12, 64];

		// Side to move toggle
		public static readonly ulong SideToMove;

		// 16 possible castling rights bitmasks (4 bits => 0..15)
		public static readonly ulong[] CastlingRights = new ulong[16];

		// 8 possible en passant files (a-h)
		public static readonly ulong[] EnPassantFile = new ulong[8];

		// Static RNG for reproducible random numbers
		private static readonly Random rng = new Random(123456);

		// Static constructor initializes all Zobrist arrays
		static Zobrist()
		{
			// Initialize piece-square keys
			for (int piece = 0; piece < 12; piece++)
			{
				for (int sq = 0; sq < 64; sq++)
				{
					PieceSquare[piece, sq] = RandomU64();
				}
			}
				
			// Side to move key
			SideToMove = RandomU64();

			// Castling rights keys
			for (int i = 0; i < CastlingRights.Length; i++)
			{
				CastlingRights[i] = RandomU64();
			}

			// En passant file keys
			for (int file = 0; file < EnPassantFile.Length; file++)
			{
				EnPassantFile[file] = RandomU64();
			}
		}

		
		// Generates a random 64-bit value using the RNG.
		
		private static ulong RandomU64()
		{
			// Combine two 32-bit random values into one 64-bit
			ulong high = (ulong)rng.Next();
			ulong low = (uint)rng.Next();
			return (high << 32) | low;
		}
	}
}
