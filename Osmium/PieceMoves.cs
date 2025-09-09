using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace Osmium
{
	internal static class PieceMoves
	{
		// ─── file / rank bit-masks ───────────────────────────────────────────────
		private const ulong FileA = 0x0101010101010101UL;
		private const ulong FileH = 0x8080808080808080UL;

		// ─── rook / bishop / queen ­helpers ──────────────────────────────────────
		internal static ulong RookAttacks(
			int square,
			ulong friendly,
			ulong enemy,
			bool isWhite,
			Board board,
			bool isQueen,
			Span<Move> moves,
			ref int count)
		{
			ulong occupancy = board.occupancies[2];
			ulong attacks = Magics.GetRookAttacks(square, occupancy);

			Piece movePiece = isWhite
				? (isQueen ? Piece.WhiteQueen : Piece.WhiteRook)
				: (isQueen ? Piece.BlackQueen : Piece.BlackRook);

			ulong targets = attacks & ~friendly;
			while (targets != 0)
			{
				int to = BitOperations.TrailingZeroCount(targets);
				ulong toB = 1UL << to;

				Piece captured = (toB & enemy) != 0
					? MoveGen.FindCapturedPiece(board, (Square)to)
					: Piece.None;

				if (captured is Piece.WhiteKing or Piece.BlackKing)
				{
					targets &= ~toB;
					continue;
				}

				MoveFlags flag = captured != Piece.None ? MoveFlags.Capture : MoveFlags.None;
				moves[count++] = new Move((Square)square, (Square)to, movePiece, captured, flag);

				targets &= ~toB;
			}

			return attacks & ~friendly;
		}

		internal static ulong BishopAttacks(
			int square,
			ulong friendly,
			ulong enemy,
			bool isWhite,
			Board board,
			bool isQueen,
			Span<Move> moves,
			ref int count)
		{
			ulong occupancy = board.occupancies[2];
			ulong attacks = Magics.GetBishopAttacks(square, occupancy);

			Piece movePiece = isWhite
				? (isQueen ? Piece.WhiteQueen : Piece.WhiteBishop)
				: (isQueen ? Piece.BlackQueen : Piece.BlackBishop);

			ulong targets = attacks & ~friendly;
			while (targets != 0)
			{
				int to = BitOperations.TrailingZeroCount(targets);
				ulong toB = 1UL << to;

				Piece captured = (toB & enemy) != 0
					? MoveGen.FindCapturedPiece(board, (Square)to)
					: Piece.None;

				if (captured is Piece.WhiteKing or Piece.BlackKing)
				{
					targets &= ~toB;
					continue;
				}

				MoveFlags flag = captured != Piece.None ? MoveFlags.Capture : MoveFlags.None;
				moves[count++] = new Move((Square)square, (Square)to, movePiece, captured, flag);

				targets &= ~toB;
			}

			return attacks & ~friendly;
		}

		internal static ulong QueenAttacks(
			int square,
			ulong friendly,
			ulong enemy,
			bool isWhite,
			Board board,
			Span<Move> moves,
			ref int count) =>
			BishopAttacks(square, friendly, enemy, isWhite, board, true, moves, ref count) |
			RookAttacks(square, friendly, enemy, isWhite, board, true, moves, ref count);

		// ─── generic generators ──────────────────────────────────────────────────
		internal static void GenerateSliderMoves(Board board, Span<Move> moves, ref int count, bool isWhite)
		{
			ulong bishop, rook, queen, friendly, enemy;



            bishop = board.bitboards[(int)Piece.WhiteBishop + ((int)(board.sideToMove) * 6)];
            rook = board.bitboards[(int)Piece.WhiteRook + ((int)(board.sideToMove) * 6)];
            queen = board.bitboards[(int)Piece.WhiteQueen + ((int)(board.sideToMove) * 6)];
            friendly = board.occupancies[(int)board.sideToMove];
            enemy = board.occupancies[(int)board.sideToMove ^ 1];

			// bishops
			ulong b = bishop;
			while (b != 0)
			{
				int sq = BitOperations.TrailingZeroCount(b);
				BishopAttacks(sq, friendly, enemy, isWhite, board, false, moves, ref count);
				b &= b - 1;
			}

			// rooks
			ulong r = rook;
			while (r != 0)
			{
				int sq = BitOperations.TrailingZeroCount(r);
				RookAttacks(sq, friendly, enemy, isWhite, board, false, moves, ref count);
				r &= r - 1;
			}

			// queens
			ulong q = queen;
			while (q != 0)
			{
				int sq = BitOperations.TrailingZeroCount(q);
				QueenAttacks(sq, friendly, enemy, isWhite, board, moves, ref count);
				q &= q - 1;
			}
		}

		internal static void GenerateKingMoves(Board board, Span<Move> moves, ref int count, bool isWhite)
		{
            Piece moveK = isWhite ? Piece.WhiteKing : Piece.BlackKing;
            ulong kingBB = board.bitboards[(int)moveK];
			ulong friendly = board.occupancies[(int)board.sideToMove];
			

			//if (kingBB == 0) return;          // should never happen

			int fromSq = BitOperations.TrailingZeroCount(kingBB);
			ulong attacks = MoveTables.KingMoves[fromSq] & ~friendly;

			while (attacks != 0)
			{
				int toSq = BitOperations.TrailingZeroCount(attacks);
				ulong toBB = 1UL << toSq;

				Piece captured = (toBB & board.occupancies[(int)(isWhite ? Color.Black : Color.White)]) != 0
					? MoveGen.FindCapturedPiece(board, (Square)toSq)
					: Piece.None;

				if (captured is Piece.WhiteKing or Piece.BlackKing)
				{
					attacks &= ~toBB;
					continue;
				}

				MoveFlags flag = captured != Piece.None ? MoveFlags.Capture : MoveFlags.None;
				moves[count++] = new Move((Square)fromSq, (Square)toSq, moveK, captured, flag);

				attacks &= ~toBB;
			}
		}

		internal static void GenerateKnightMoves(Board board, Span<Move> moves, ref int count, bool isWhite)
		{
			ulong knights = board.bitboards[isWhite ? (int)Piece.WhiteKnight : (int)Piece.BlackKnight];
			ulong friendly = board.occupancies[(int)board.sideToMove];
			ulong enemy = board.occupancies[(int)board.sideToMove^1]; // bitflip using xor
			Piece moveN = isWhite ? Piece.WhiteKnight : Piece.BlackKnight;

			while (knights != 0)
			{
				int fromSq = BitOperations.TrailingZeroCount(knights);
				ulong movesMask = MoveTables.KnightMoves[fromSq] & ~friendly;

				while (movesMask != 0)
				{
					int toSq = BitOperations.TrailingZeroCount(movesMask);
					ulong toBB = 1UL << toSq;

					Piece captured = (toBB & enemy) != 0
						? MoveGen.FindCapturedPiece(board, (Square)toSq)
						: Piece.None;

					if (captured is Piece.WhiteKing or Piece.BlackKing)
					{
						movesMask &= ~toBB;
						continue;
					}

					MoveFlags flag = captured != Piece.None ? MoveFlags.Capture : MoveFlags.None;
					moves[count++] = new Move((Square)fromSq, (Square)toSq, moveN, captured, flag);

					movesMask &= ~toBB;
				}

				knights &= knights - 1;
			}
		}

		// ─── pawn generators ─────────────────────────────────────────────────────


		internal static void GeneratePawnMoves(Board board, Span<Move> moves, ref int count, bool isWhite)
		{
			ulong pawns, enemy, friendly;
			if (isWhite)
			{
				pawns = board.bitboards[(int)Piece.WhitePawn];
				enemy = board.occupancies[(int)Color.Black];
				friendly = board.occupancies[(int)Color.White];
				GenerateWhitePawn(pawns, enemy, friendly, moves, ref count, board);
			}
			else
			{
				pawns = board.bitboards[(int)Piece.BlackPawn];
				enemy = board.occupancies[(int)Color.White];
				friendly = board.occupancies[(int)Color.Black];
				GenerateBlackPawn(pawns, enemy, friendly, moves, ref count, board);
			}
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		internal static void GenerateWhitePawn(ulong pawns, ulong blackEnemy, ulong whiteFriendly, Span<Move> moves, ref int count, Board board)
		{

			while (pawns != 0)
			{
				int sq = BitOperations.TrailingZeroCount(pawns);
				WPawnAtks(sq, blackEnemy, whiteFriendly, moves, ref count, board);
				pawns &= pawns - 1; // clear the lowest set bit
			}
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		internal static void GenerateBlackPawn(ulong pawns, ulong whiteEnemy, ulong blackFriendly, Span<Move> moves, ref int count, Board board)
		{
			while (pawns != 0)
			{
				int sq = BitOperations.TrailingZeroCount(pawns);
				BPawnAtks(sq, whiteEnemy, blackFriendly, moves, ref count, board);
				pawns &= pawns - 1; // clear the lowest set bit
			}
		}

		private static void WPawnAtks(int sq, ulong enemy, ulong friendly, Span<Move> moves, ref int count, Board board)
		{
			bool checkEP = board.enPassantSquare != Square.None;
			bool on7th = sq >= 48 && sq <= 55;
			bool on2nd = sq >= 8 && sq <= 15;
			ulong occ = board.occupancies[2];

			if ((MoveTables.PawnMoves[0, sq] & occ) == 0)
			{
				int to = sq + 8;
				if (on7th)
				{
					moves[count++] = new((Square)sq, (Square)(to), Piece.WhitePawn, Piece.None, MoveFlags.Promotion, Piece.WhiteQueen);
					moves[count++] = new((Square)sq, (Square)(to), Piece.WhitePawn, Piece.None, MoveFlags.Promotion, Piece.WhiteRook);
					moves[count++] = new((Square)sq, (Square)(to), Piece.WhitePawn, Piece.None, MoveFlags.Promotion, Piece.WhiteKnight);
					moves[count++] = new((Square)sq, (Square)(to), Piece.WhitePawn, Piece.None, MoveFlags.Promotion, Piece.WhiteBishop);
				} 
				else
				{
                    moves[count++] = new((Square)sq, (Square)(to), Piece.WhitePawn);
                }
				if (on2nd && (occ & MoveTables.PawnDouble[0, sq & 7]) == 0)
				{
					moves[count++] = new((Square)sq, (Square)(sq + 16), Piece.WhitePawn);
				}
				
				
			}
			 
			//left caps and promos and ep
			if ((MoveTables.PawnCaptures[0, 0, sq] & enemy) != 0)
			{
				Piece tookPiece = MoveGen.FindCapturedPiece(board, (Square)(sq + 7));
				if (tookPiece == Piece.BlackKing) return;
				int to = sq + 7;
				if (on7th)
				{
					
					moves[count++] = new((Square)sq, (Square)(to), Piece.WhitePawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.WhiteQueen);
					moves[count++] = new((Square)sq, (Square)(to), Piece.WhitePawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.WhiteRook);
					moves[count++] = new((Square)sq, (Square)(to), Piece.WhitePawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.WhiteKnight);
					moves[count++] = new((Square)sq, (Square)(to), Piece.WhitePawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.WhiteBishop);
				}
				else
				{
					moves[count++] = new((Square)sq, (Square)(to), Piece.WhitePawn, tookPiece);
				}

			} else if (checkEP && board.enPassantSquare == (Square)(sq + 7))
			{
				moves[count++] = new((Square)sq, board.enPassantSquare, Piece.WhitePawn, Piece.BlackPawn, MoveFlags.EnPassant | MoveFlags.Capture);
			}

			//right caps and promos and ep
			if ((MoveTables.PawnCaptures[0, 1, sq] & enemy) != 0)
			{
				Piece tookPiece = MoveGen.FindCapturedPiece(board, (Square)(sq + 9));
				if (tookPiece == Piece.BlackKing) return;
				int to = sq + 9;
				if (on7th)
				{
					moves[count++] = new((Square)sq, (Square)(to), Piece.WhitePawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.WhiteQueen);
					moves[count++] = new((Square)sq, (Square)(to), Piece.WhitePawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.WhiteRook);
					moves[count++] = new((Square)sq, (Square)(to), Piece.WhitePawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.WhiteKnight);
					moves[count++] = new((Square)sq, (Square)(to), Piece.WhitePawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.WhiteBishop);
				}
				else
				{
					moves[count++] = new((Square)sq, (Square)(to), Piece.WhitePawn, tookPiece);
				}
			} else if (checkEP && board.enPassantSquare == (Square)(sq + 9))
			{
				moves[count++] = new((Square)sq, board.enPassantSquare, Piece.WhitePawn, Piece.BlackPawn, MoveFlags.EnPassant | MoveFlags.Capture);
			}


		}

		private static void BPawnAtks(int sq, ulong enemy, ulong friendly, Span<Move> moves, ref int count, Board board)
		{
			bool checkEP = board.enPassantSquare != Square.None;
			bool on7th = sq >= 48 && sq <= 55;
			bool on2nd = sq >= 8 && sq <= 15;
			ulong occ = board.occupancies[2];

			if ((MoveTables.PawnMoves[1, sq] & occ) == 0)
			{
				if (on2nd)
				{
					moves[count++] = new((Square)sq, (Square)(sq - 8), Piece.BlackPawn, Piece.None, MoveFlags.Promotion, Piece.BlackQueen);
					moves[count++] = new((Square)sq, (Square)(sq - 8), Piece.BlackPawn, Piece.None, MoveFlags.Promotion, Piece.BlackRook);
					moves[count++] = new((Square)sq, (Square)(sq - 8), Piece.BlackPawn, Piece.None, MoveFlags.Promotion, Piece.BlackKnight);
					moves[count++] = new((Square)sq, (Square)(sq - 8), Piece.BlackPawn, Piece.None, MoveFlags.Promotion, Piece.BlackBishop);
				}
				//double move mask to check legality
				else 
				{
                    moves[count++] = new((Square)sq, (Square)(sq - 8), Piece.BlackPawn);
                }
				if (on7th && (occ & MoveTables.PawnDouble[1, sq & 7]) == 0)
				{
					moves[count++] = new((Square)sq, (Square)(sq - 16), Piece.BlackPawn);
				}
								
				
				
			}

			//left caps and promos and ep
			if ((MoveTables.PawnCaptures[1, 0, sq] & enemy) != 0)
			{
				Piece tookPiece = MoveGen.FindCapturedPiece(board, (Square)(sq - 7));
				if (tookPiece == Piece.WhiteKing) return;

				if (on2nd)
				{ 
					moves[count++] = new((Square)sq, (Square)(sq - 7), Piece.BlackPawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.BlackQueen);
					moves[count++] = new((Square)sq, (Square)(sq - 7), Piece.BlackPawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.BlackRook);
					moves[count++] = new((Square)sq, (Square)(sq - 7), Piece.BlackPawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.BlackKnight);
					moves[count++] = new((Square)sq, (Square)(sq - 7), Piece.BlackPawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.BlackBishop);
				}
				else
				{
					moves[count++] = new((Square)sq, (Square)(sq - 7), Piece.BlackPawn, tookPiece);
				}

			}
			else if (checkEP && board.enPassantSquare == (Square)(sq - 7))
			{
				moves[count++] = new((Square)sq, board.enPassantSquare, Piece.BlackPawn, Piece.WhitePawn, MoveFlags.EnPassant | MoveFlags.Capture);
			}

			//right caps and promos and ep
			if ((MoveTables.PawnCaptures[1, 1, sq] & enemy) != 0)
			{
				Piece tookPiece = MoveGen.FindCapturedPiece(board, (Square)(sq - 9));
				if (tookPiece == Piece.WhiteKing) return;
				if (on2nd)
				{
					moves[count++] = new((Square)sq, (Square)(sq - 9), Piece.BlackPawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.BlackQueen);
					moves[count++] = new((Square)sq, (Square)(sq - 9), Piece.BlackPawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.BlackRook);
					moves[count++] = new((Square)sq, (Square)(sq - 9), Piece.BlackPawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.BlackKnight);
					moves[count++] = new((Square)sq, (Square)(sq - 9), Piece.BlackPawn, tookPiece, MoveFlags.Promotion | MoveFlags.Capture, Piece.BlackBishop);
				}
				else
				{
					moves[count++] = new((Square)sq, (Square)(sq - 9), Piece.BlackPawn, tookPiece);
				}
			}
			else if (checkEP && board.enPassantSquare == (Square)(sq - 9))
			{
				moves[count++] = new((Square)sq, board.enPassantSquare, Piece.BlackPawn, Piece.WhitePawn, MoveFlags.EnPassant | MoveFlags.Capture);
			}
		}

	}
}