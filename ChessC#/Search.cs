using System.Diagnostics;
using System.Reflection.Metadata;


namespace ChessC_
{
	public static class Search
	{
		public static int MaxDepth = Program.MaxDepth;
		private const int MateScore = 1_000_000;
		private static readonly Move[,] killerMoves = new Move[MaxDepth + 1, 2];
		public static long NodesVisited;

		internal static Move FindBestMoveLazySMP(Board board, TranspositionTable tt, int timeLimitMs, int threadCount)
		{
			Console.WriteLine("Lazy SMP search is not implemented yet.");
			return default; // Placeholder for lazy SMP implementation
		}
		
		internal static Move FindBestMove(Board board, TranspositionTable tt, int timeLimitMs)
		{
			Move lastBest = default;
			int lastScore = 0;
			var searchStart = Stopwatch.StartNew();
			
			int maxDepth = MaxDepth;
			for (int depth = 1; depth <= maxDepth; depth++)
			{
				var iterStart = Stopwatch.StartNew();

				if (searchStart.ElapsedMilliseconds >= timeLimitMs)
					break;
				NodesVisited = 0;
				// --- Aspiration Window ---
				int window = 50 + 3*depth; // Tune as needed
				int alpha = lastScore - window;
				int beta = lastScore + window;
				int score;
				Move bestMove;

				while (true)
				{
					bestMove = SearchAtDepth(board, tt, depth, alpha, beta, out score);

					if (score <= alpha)
					{
						// Fail-low, widen window
						alpha = int.MinValue + 1;
					}
					else if (score >= beta)
					{
						// Fail-high, widen window
						beta = int.MaxValue - 1;
					}
					else
					{
						break; // Score within window
					}
				}

				lastScore = score;
				lastBest = bestMove;

				iterStart.Stop();
				Console.WriteLine($"Depth {depth}: Nodes={NodesVisited}, Time={iterStart.ElapsedMilliseconds} ms, Best={lastBest}");
				
			}
			searchStart.Stop(); 

			double outScore = lastScore;

			if (board.sideToMove == Color.Black)
				outScore = -outScore;

			if (Math.Abs(outScore) > 900000)
			{
				outScore = Math.Sign(outScore) * ((MateScore - Math.Abs(outScore)) / 2);
				String mate = outScore > 0 ? "" : "-";
				mate += "M" + Math.Abs(outScore);
				Console.WriteLine($"Final evaluation: ({mate})");
			}
			else
			{
				Console.WriteLine($"Final evaluation: {outScore / 100.0}");
			}
			return lastBest;
		}

		// Search at a given depth with aspiration window support
		private static Move SearchAtDepth(Board board, TranspositionTable tt, int targetDepth, int alpha, int beta, out int bestScore)
		{
			bool isWhite = board.sideToMove == Color.White;
			Move bestMove = default;
			bestScore = int.MinValue;

			var moves = MoveGen.FilteredLegalMoves(board, isWhite);
			tt.Probe(board.zobristKey, targetDepth, alpha, beta, out _, out Move ttMove);
			moves = MoveOrdering.OrderMoves(board, moves, ttMove, targetDepth);

			// immediate mate
			foreach (var mv in moves)
				if ((mv.Flags & MoveFlags.Checkmate) != 0)
					return mv;

			foreach (var move in moves)
			{
				//var beforeBB = (ulong[])board.bitboards.Clone();
				UndoInfo undo = board.MakeSearchMove(board, move);
				int score = -PVS(board, targetDepth - 1, -beta, -alpha, !isWhite, tt);
				board.UnmakeMove(move, undo);

				// Drift check at root
				//for (int i = 0; i < 12; i++)
				//{
				//    if (board.bitboards[i] != beforeBB[i])
				//    {
				//        Console.WriteLine($"*** DRIFT at bitboard[{i}] after path: {string.Join(" -> ", _currentPath.Select(m => MoveNotation.ToAlgebraicNotation(m)))}");
				//        Console.WriteLine("Before:");
				//        Utils.PrintBitboard(beforeBB[i]);
				//        Console.WriteLine("After:");
				//        Utils.PrintBitboard(board.bitboards[i]);
				//        Debugger.Break();
				//        break;
				//    }
				//}

				if (score > bestScore)
				{
					bestScore = score;
					bestMove = move;
				}
				alpha = Math.Max(alpha, score);
			}
			return bestMove;
		}

		private static bool HasSufficientMaterial(Board board, bool isWhiteToMove)
		{
			// Piece indices: 0-5 white (Pawn, Knight, Bishop, Rook, Queen, King), 6-11 black
			int colorOffset = isWhiteToMove ? 0 : 6;
			int pawnCount = Bitboard.PopCount(board.bitboards[colorOffset + 0]);
			int knightCount = Bitboard.PopCount(board.bitboards[colorOffset + 1]);
			int bishopCount = Bitboard.PopCount(board.bitboards[colorOffset + 2]);
			int rookCount = Bitboard.PopCount(board.bitboards[colorOffset + 3]);
			int queenCount = Bitboard.PopCount(board.bitboards[colorOffset + 4]);

			// Any pawn, rook, or queen is sufficient
			if (pawnCount > 0 || rookCount > 0 || queenCount > 0)
				return true;

			// Two bishops is sufficient
			if (bishopCount >= 2)
				return true;

			// Bishop + knight is sufficient
			if (bishopCount >= 1 && knightCount >= 1)
				return true;

			// Two knights is not sufficient (mate is not possible without help)
			// Only king, or king+bishop, or king+knight is not sufficient
			return false;
		}
		//currently broken because of the new nmp, lmr, and futility pruning

		//[ThreadStatic]
		//private static List<Move> _currentPath;

		private static int Negamax(Board board, int depth, int alpha, int beta, bool isWhiteToMove, TranspositionTable tt)
		{
			//if (_currentPath == null)
			//    _currentPath = new List<Move>();

			NodesVisited++;
			if (depth == 0)
			{
				return Quiesce(board, alpha, beta, isWhiteToMove);
			}

			ulong key = board.zobristKey;
			int origAlpha = alpha;
			if (tt.Probe(key, depth, alpha, beta, out int ttScore, out Move ttMove))
				return ttScore;

			int kingSq = MoveGen.GetKingSquare(board, isWhiteToMove);
			bool inCheck = MoveGen.IsSquareAttacked(board, kingSq, isWhiteToMove);

			// Null Move Pruning 
			if (depth >= 3 && !inCheck && HasSufficientMaterial(board, isWhiteToMove))
			{
				var undoNull = board.MakeNullMove();
				int nullScore = -Negamax(board, depth - 3, -beta, -beta + 1, !isWhiteToMove, tt);
				board.UnmakeNullMove(undoNull);

				if (nullScore >= beta)
					return beta;
			}

			var moves = MoveGen.FilteredLegalMoves(board, isWhiteToMove);
			if (moves.Count == 0)
			{
				return inCheck ? -MateScore + (MaxDepth - depth) : 0;
			}

			moves = MoveOrdering.OrderMoves(board, moves, ttMove, depth);

			int bestScore = int.MinValue;
			Move bestMove = default;
			int moveCount = 0;

			foreach (var move in moves)
			{
				moveCount++;

				//_currentPath.Add(move);
				//var beforeBB = (ulong[])board.bitboards.Clone();

				// --- Futility Pruning ---
				// Only at depth 1, not in check, quiet move (no capture, no promotion)
				if (depth == 1 &&
					!inCheck &&
					move.PieceCaptured == Piece.None &&
					(move.Flags & MoveFlags.Promotion) == 0)
				{
					int staticEval = Eval.EvalBoard(board, isWhiteToMove);
					int futilityMargin = 100; // tuning needed
					if (staticEval + futilityMargin <= alpha)
						continue; // Prune this move
				}

				// --- Late Move Reductions (LMR) ---
				// Only for quiet moves, not in check, not first few moves, depth >= 3
				bool canReduce = 
					depth >= 3 &&
					moveCount > 3 && 
					!inCheck && 
					move.PieceCaptured == Piece.None && 
					(move.Flags & MoveFlags.Promotion) == 0;

				int score;
				if (canReduce)
				{
					// Reduced-depth search
					UndoInfo undo = board.MakeSearchMove(board, move);
					score = -Negamax(board, depth - 2, -beta, -alpha, !isWhiteToMove, tt);
					board.UnmakeMove(move, undo);

					// If reduction fails high, re-search at full depth
					if (score > alpha)
					{
						undo = board.MakeSearchMove(board, move);
						score = -Negamax(board, depth - 1, -beta, -alpha, !isWhiteToMove, tt);
						board.UnmakeMove(move, undo);
					}
				}
				else
				{
					UndoInfo undo = board.MakeSearchMove(board, move);
					score = -Negamax(board, depth - 1, -beta, -alpha, !isWhiteToMove, tt);
					board.UnmakeMove(move, undo);
				}

				//for (int i = 0; i < 12; i++)
				//{
				//    if (board.bitboards[i] != beforeBB[i])
				//    {
				//        Console.WriteLine($"*** DRIFT at ply bitboard[{i}] after path: {string.Join(" -> ", _currentPath.Select(m => MoveNotation.ToAlgebraicNotation(m)))}");
				//        Debugger.Break();
				//        break;
				//    }
				//}

				//_currentPath.RemoveAt(_currentPath.Count - 1);

				if (score > bestScore)
				{
					bestScore = score;
					bestMove = move;
				}
				alpha = Math.Max(alpha, score);
				if (alpha >= beta)
				{
					if (move.PieceCaptured == Piece.None)
					{
						if (!move.Equals(killerMoves[depth, 0]))
						{
							killerMoves[depth, 1] = killerMoves[depth, 0];
							killerMoves[depth, 0] = move;
						}
						MoveOrdering.RecordHistory(move, depth);
					}
					break;
				}
			}

			NodeType flag = bestScore <= origAlpha ? NodeType.UpperBound
						  : bestScore >= beta ? NodeType.LowerBound
						  : NodeType.Exact;
			tt.Store(key, depth, bestScore, bestMove, flag);
			return bestScore;
		}
		private static bool IsGoodCapture(Board board, Move move)
		{
			// Simple MVV-LVA: only allow captures where captured >= moved
			return ((int)move.PieceCaptured)%6 >= ((int)move.PieceMoved)%6;
		}

		private static int Quiesce(Board board, int alpha, int beta, bool isWhiteToMove)
		{
			int standPat = Eval.EvalBoard(board, isWhiteToMove);
			if (standPat >= beta) return beta;
			alpha = Math.Max(alpha, standPat);

			// Generate all captures
			var capMoves = MoveGen.GenerateCaptureMoves(board, isWhiteToMove);

			// --- Add promotions to quiescence ---
			var promoMoves = MoveGen.GeneratePromotionMoves(board, isWhiteToMove);
				

			// Combine captures and promotions
			var qMoves = new List<Move>(capMoves.Count + promoMoves.Count);
			qMoves.AddRange(capMoves);
			qMoves.AddRange(promoMoves);

			MoveGen.FlagCheckAndMate(board, qMoves, isWhiteToMove);
			qMoves = MoveOrdering.OrderMoves(board, qMoves, null, 0);

			foreach (var move in qMoves)
			{
				// --- Delta Pruning: skip bad captures ---
				if ((move.Flags & MoveFlags.Promotion) == 0 && !IsGoodCapture(board, move)) continue;

				//if (_currentPath != null) _currentPath.Add(move);
				//var beforeBB = (ulong[])board.bitboards.Clone();

				UndoInfo undo = board.MakeSearchMove(board, move);
				int score = -Quiesce(board, -beta, -alpha, !isWhiteToMove);
				board.UnmakeMove(move, undo);

				//if (_currentPath != null)
				//{
				//    for (int i = 0; i < 12; i++)
				//    {
				//        if (board.bitboards[i] != beforeBB[i])
				//        {
				//            Console.WriteLine($"*** DRIFT in quiesce bitboard[{i}] after path: {string.Join(" -> ", _currentPath.Select(m => MoveNotation.ToAlgebraicNotation(m)))}");
				//            Debugger.Break();
				//            break;
				//        }
				//    }
				//}

				//if (_currentPath != null) _currentPath.RemoveAt(_currentPath.Count - 1);

				if (score >= beta) return beta;
				alpha = Math.Max(alpha, score);
			}
			return alpha;
		}

		private static int PVS(Board board, int depth, int alpha, int beta, bool isWhiteToMove, TranspositionTable tt)
		{
			NodesVisited++;
			if (depth == 0)
				return Quiesce(board, alpha, beta, isWhiteToMove);

			ulong key = board.zobristKey;
			int origAlpha = alpha;
			if (tt.Probe(key, depth, alpha, beta, out int ttScore, out Move ttMove))
				return ttScore;

			int kingSq = MoveGen.GetKingSquare(board, isWhiteToMove);
			bool inCheck = MoveGen.IsSquareAttacked(board, kingSq, isWhiteToMove);

			// Null Move Pruning
			if (depth >= 3 && !inCheck && HasSufficientMaterial(board, isWhiteToMove))
			{
				var undoNull = board.MakeNullMove();
				int nullScore = -PVS(board, depth - (3+depth*2/9), -beta, -beta + 1, !isWhiteToMove, tt);
				board.UnmakeNullMove(undoNull);

				if (nullScore >= beta)
					return beta;
			}

			var moves = MoveGen.FilteredLegalMoves(board, isWhiteToMove);
			if (moves.Count == 0)
				return inCheck ? -MateScore + (MaxDepth - depth) : 0;

			moves = MoveOrdering.OrderMoves(board, moves, ttMove, depth);

			int bestScore = int.MinValue;
			Move bestMove = default;
			int moveCount = 0;

			foreach (var move in moves)
			{
				moveCount++;

                // --- Late Move Pruning (LMP) ---
                // Only for quiet moves, not in check, not first few moves, and at low depth
                if (depth <= 3 &&
                    !inCheck &&
                    move.PieceCaptured == Piece.None &&
                    (move.Flags & MoveFlags.Promotion) == 0 &&
                    moveCount > 3 + depth*7/9) // threshold can be tuned
                {
                    continue; // Prune this late quiet move
                }

                // --- Futility Pruning ---
                if (depth == 2 &&
					!inCheck &&
					move.PieceCaptured == Piece.None &&
					(move.Flags & MoveFlags.Promotion) == 0)
				{
					int staticEval = Eval.EvalBoard(board, isWhiteToMove);

					//----------------------------------------
					int futilityMargin = 180; // tuning needed
					//----------------------------------------

					if (staticEval + futilityMargin <= alpha)
						continue; // Prune this move
				}

				// --- Late Move Reductions (LMR) ---
				bool canReduce =
					depth >= 3 &&
					moveCount > 3 &&
					!inCheck &&
					move.PieceCaptured == Piece.None &&
					(move.Flags & MoveFlags.Promotion) == 0;

				int score;
				UndoInfo undo = board.MakeSearchMove(board, move);

				if (moveCount == 1)
				{
					// First move: full window, no reduction
					score = -PVS(board, depth - 1, -beta, -alpha, !isWhiteToMove, tt);
				}
				else if (canReduce)
				{
                    int R = 3 + depth / 4;
                    // Reduced-depth null window search
                    score = -PVS(board, depth - R, -alpha - 1, -alpha, !isWhiteToMove, tt);
					// If reduction fails high, re-search at full depth and window
					if (score > alpha)
					{
						score = -PVS(board, depth - 1, -alpha - 1, -alpha, !isWhiteToMove, tt);
						if (score > alpha && score < beta)
							score = -PVS(board, depth - 1, -beta, -alpha, !isWhiteToMove, tt);
					}
				}
				else
				{
					// Null window search
					score = -PVS(board, depth - 1, -alpha - 1, -alpha, !isWhiteToMove, tt);
					if (score > alpha && score < beta)
						score = -PVS(board, depth - 1, -beta, -alpha, !isWhiteToMove, tt);
				}

				board.UnmakeMove(move, undo);

				if (score > bestScore)
				{
					bestScore = score;
					bestMove = move;
				}
				alpha = Math.Max(alpha, score);
				if (alpha >= beta)
				{
					// Store killer moves/history as in your Negamax
					if (move.PieceCaptured == Piece.None)
					{
						//below this
						if (!move.Equals(killerMoves[depth, 0]))
						{
							killerMoves[depth, 1] = killerMoves[depth, 0];
							killerMoves[depth, 0] = move;
						}
						MoveOrdering.RecordHistory(move, depth);
					}
					break;
				}
			}

			NodeType flag = bestScore <= origAlpha ? NodeType.UpperBound
						  : bestScore >= beta ? NodeType.LowerBound
						  : NodeType.Exact;
			tt.Store(key, depth, bestScore, bestMove, flag);
			return bestScore;
		}
	}
}