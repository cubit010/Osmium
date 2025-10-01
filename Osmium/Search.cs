using System;
using System.Diagnostics;
using System.Formats.Asn1;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using static System.Formats.Asn1.AsnWriter;

namespace Osmium
{
	/// <summary>
	/// Span-based search with fixed move-buffer isolation per depth
	/// </summary>
	public static class Search
	{

		internal static volatile bool stopSearch = false;

		internal static bool useSIMD = Avx2.IsSupported ? true : false ;


		[ThreadStatic]
		private static Dictionary<ulong, int> _repetitionTable;

		public static int MaxDepth = Program.MaxDepth;
		public static int MaxQDepth = 8; // Quiescence search max depth

		private const int MateScore = 1_000_000;
		private static readonly Move[,] killerMoves = new Move[MaxDepth + 1, 2];
		public static long NodesVisited;
		public static long totalNodesVisited;

		//[ThreadStatic]
		//private static Stack<Move> _searchStack = new Stack<Move>(MaxDepth);
		 
		//[ThreadStatic]
		private static Move[][] _moveBuffer = new Move[MaxDepth][];

		private static int QBufferDepth = MaxQDepth; // Quiescence search buffer depth

		//[ThreadStatic]
		private static Move[][] _qBuffer = new Move[QBufferDepth][];

		//private static Board[] debugBoards = new Board[Program.MaxDepth + 1]; // board states per ply for debugging

	//-------------------------------------------------------------------

		private static int windowDefault = 75;
		private static int LmrSafetyMargin = 150; // tuning needed
		private static int NMPmargin = 160; //Null move pruning margin, can be tuned
		private static int reduxBase = 1;
		private static int reduxDiv = 5;
		private static int LMPSafeMoves = 5;
		private static int LMPMvDepthMult = 3;
		private static int futilityMargin = 180;
		private static int LMPDepth = 3;
		private static int reduxSafeMoves = 4;
		private static int LMRReduxFactor = 2;

	//-------------------------------------------------------------------
		public static int windowDefaultProp
		{
			get { return windowDefault; }
			set { windowDefault = value; }
		}
		public static int LmrSafetyMarginProp
		{
			get { return LmrSafetyMargin; }
			set { LmrSafetyMargin = value; }
		}
		public static int NMPmarginProp
		{
			get { return NMPmargin; }
			set { NMPmargin = value; }
		}
		public static int reduxBaseProp
		{
			get { return reduxBase; }
			set { reduxBase = value; }
		}
		public static int reduxDivProp
		{
			get { return reduxDiv; }
			set { reduxDiv = value; }
		}
		public static int LMPSafeMovesProp
		{
			get { return LMPSafeMoves; }
			set { LMPSafeMoves = value; }
		}
		public static int LMPMvDepthMultProp
		{
			get { return LMPMvDepthMult; }
			set { LMPMvDepthMult = value; }
		}
		public static int futilityMarginProp
		{
			get { return futilityMargin; }
			set { futilityMargin = value; }
		}
		public static int LMPDepthProp
		{
			get { return LMPDepth; }
			set { LMPDepth = value; }
		}
		public static int reduxSafeMovesProp
		{
			get { return reduxSafeMoves; }
			set { reduxSafeMoves = value; }
		}
		public static int LMRReduxFactorProp
		{
			get { return LMRReduxFactor; }
			set { LMRReduxFactor = value; }
		}

	//-------------------------------------------------------------------
		static Search()
		{
			for (int i = 0; i < MaxDepth; i++)
			{
				_moveBuffer[i] = new Move[256]; // Adjust size as needed
			}
			for (int i = 0; i < QBufferDepth; i++)
			{
				_qBuffer[i] = new Move[128]; // Adjust size as needed
			}
		}
		static Stopwatch searchTimer = new Stopwatch();
		internal static bool mustStop = false;
		static int classTimeLimitMs;
		public static Move UCISearch(Board board, TranspositionTable tt, int timeLimitMs)
		{ 
			stopSearch = false;
			mustStop = false;
			classTimeLimitMs = timeLimitMs;
			bool is3fold = board.IsThreefoldRepetition();

			uint oldIter = 0;

			long lastNodeCount = 0;
			if (is3fold || board.halfmoveClock > 50 || !HasSufficientMaterial(board))
			{
				if (is3fold) threeFoldDetections++;
				return default;
			}

			tt.NewSearch();
			Move lastBest = default;
			int lastScore = 0;
			searchTimer.Restart();

			bool notTrusted = false;
			//Board comp = board.Clone();
			for (int depth = 1; depth <= MaxDepth; depth++)
			{
				//if (!board.Equals(comp))
				//{
				//	Console.WriteLine($"error in between depth {depth-1}, {depth}");
				//	if (BitOperations.PopCount(board.bitboards[11]) > 1)
				//	{
				//		Console.WriteLine($"Error in search: More than one black king detected on the board, at searchdepth {depth}");
				//		Utils.PrintBitboard(board.bitboards[11]);
				//	}
				//	if (BitOperations.PopCount(board.bitboards[5]) > 1)
				//	{
				//		Console.WriteLine($"Error in search: More than one white king detected on the board, at searchdepth {depth}");
				//		Utils.PrintBitboard(board.bitboards[5]);
				//	}
				//	checkOverlap(board);
				//	for (int i = 0; i < 12; i++)
				//	{
				//		if (board.bitboards[i] != comp.bitboards[i])
				//		{
				//			Console.WriteLine($"Bitboard {i} differs at ply {ply}! Expected");
				//			Utils.PrintBitboard(comp.bitboards[i]);
				//			Console.WriteLine("Got:");
				//			Utils.PrintBitboard(board.bitboards[i]);
				//			Console.WriteLine("movestack:");
				//			PrintStack(_searchStack);
				//			Environment.Exit(1);
				//		}
				//	}
				//	Environment.Exit(1);
				//}
				if ((timeLimitMs - searchTimer.ElapsedMilliseconds) < oldIter * 6)
				{
					//Console.WriteLine("stopped bc est time not enough");
					break;
				}
				else
				{
					//Console.WriteLine("continuing to next depth");
				}

				var iterStart = Stopwatch.StartNew();
				if (searchTimer.ElapsedMilliseconds >= timeLimitMs)
				{
					notTrusted = true;
					break;
				}

				NodesVisited = 0;
				int window = windowDefault + 3 * depth;
				int alpha = lastScore - window;
				int beta = lastScore + window;
				int score;
				Move bestMove;

				// Aspirating window
				while (true)
				{
					bestMove = SearchAtDepth(board, tt, depth, alpha, beta, out score);
					if (score <= alpha)
						alpha = int.MinValue + 1;
					else if (score >= beta)
						beta = int.MaxValue - 1;
					else
						break;
				}
				if (mustStop)
				{
					notTrusted = true;
					break;
				}
				if (!notTrusted)
				{
					lastBest = bestMove;
					lastScore = score;
				}
				iterStart.Stop();
				oldIter = (uint)iterStart.ElapsedMilliseconds;
				lastNodeCount = NodesVisited;

				// Calculate deltas from current TT stats
				
				Console.WriteLine(
					$"info depth {depth} nodes {NodesVisited} time {iterStart.ElapsedMilliseconds} score cp {(lastScore)} nps {1000.0 * NodesVisited / Math.Max(0.8, iterStart.ElapsedMilliseconds),12:F2} pv {lastBest.ToString().ToLowerInvariant()}");
																											//negate lastScore,
				//Console.Write(notTrusted ? "Not enough nodes searched at depth " + depth + ", using best from depth " + (depth-1) + "\n" : "");
				totalNodesVisited += NodesVisited;
			}
			
			return lastBest;
		}
		public static Move FindBestMove(Board board, TranspositionTable tt, int timeLimitMs)
		{
			bool is3fold = board.IsThreefoldRepetition();
			classTimeLimitMs = timeLimitMs;

			mustStop = false;
			stopSearch = false;

			if (is3fold || board.halfmoveClock > 50 || !HasSufficientMaterial(board))
			{
				if (is3fold) threeFoldDetections++;
				//if (!Program.UCImode)
					//Console.WriteLine("3-fold repetition or insufficient material detected at root, returning no move.");
				return default;
			}

			uint pastUsableHits = 0;
			uint pastLookups = 0;
			uint pastMoveOnlyHits = 0;
			uint pastCutoffs = 0;
			totalNodesVisited = 0;
			long lastNodeCount = 0;
			tt.NewSearch();
			Move lastBest = default;
			int lastScore = 0;
			uint oldIter = 0;
			searchTimer.Restart();

			bool notTrusted = false;
			for (int depth = 1; depth <= MaxDepth; depth++)
			{
				if ((timeLimitMs - searchTimer.ElapsedMilliseconds) < oldIter * 6)
				{
					//Console.WriteLine("stopped bc est time not enough");
					break;
				}
				//else
				//{
				//	Console.WriteLine("continuing to next depth");
				//}

				var iterStart = Stopwatch.StartNew();
				if (searchTimer.ElapsedMilliseconds >= timeLimitMs)
				{
					notTrusted = true;
					break;
				}


				NodesVisited = 0;
				int window = windowDefault + 3 * depth;
				int alpha = lastScore - window;
				int beta = lastScore + window;
				int score;
				Move bestMove;

				// Aspirating window
				while (true)
				{
					bestMove = SearchAtDepth(board, tt, depth, alpha, beta, out score);
					if (score <= alpha)
						alpha = int.MinValue + 1;
					else if (score >= beta)
						beta = int.MaxValue - 1;
					else
						break;
				}
				if (mustStop)
				{
					notTrusted = true;
					break;
				}
				if (!notTrusted)
				{
					lastBest = bestMove;
					lastScore = score;
				}
				iterStart.Stop();
				oldIter = (uint)iterStart.ElapsedMilliseconds;
				lastNodeCount = NodesVisited;
				

				
				uint dLookups;
				uint dUsable;
				uint dMoveHints;
				uint dCutoffs;
				double iterHitRate;
				double iterMoveHintRate;
				// Calculate deltas from current TT stats
				if (Program.TTStats)
				{
					dLookups = tt.Lookups - pastLookups;
					dUsable = tt.UsableHits - pastUsableHits;
					dMoveHints = tt.MoveOnlyHits - pastMoveOnlyHits;
					dCutoffs = tt.CutoffsFromTT - pastCutoffs;

					iterHitRate = dLookups > 0 ? 100.0 * dUsable / dLookups : 0.0;
					iterMoveHintRate = dLookups > 0 ? 100.0 * dMoveHints / dLookups : 0.0;
				}
				Console.Write(
					$"Depth {depth,-2}: Nodes:{NodesVisited,-10} | Time: {iterStart.ElapsedMilliseconds,-6} ms | " +
					$"Best={MoveNotation.ToAlgebraicNotation(lastBest),6} | NPS: {1000.0 * NodesVisited / Math.Max(0.8, iterStart.ElapsedMilliseconds),12:F2} | " +
					$"Eval: {(board.sideToMove == Color.Black ? -lastScore / 100.0 : lastScore / 100.0),3} | "

				);

				if (Program.TTStats)
					Console.WriteLine(
					$"TT usable: {iterHitRate:F2}% ({dUsable.ToString("N0")}/{dLookups.ToString("N0")}) | move-hint: {iterMoveHintRate:F2}% ({dMoveHints.ToString("N0")}) | " +
					$"cutoffs: {dCutoffs.ToString("N0")}"
					);
				else
				{
					 Console.WriteLine();
				}
				//Console.Write(notTrusted ? "Not enough nodes searched at depth " + depth + ", using best from depth " + (depth-1) + "\n" : "");
				totalNodesVisited += NodesVisited;

				// Update past counters
				pastUsableHits = tt.UsableHits;
				pastLookups = tt.Lookups;
				pastMoveOnlyHits = tt.MoveOnlyHits;
				pastCutoffs = tt.CutoffsFromTT;
			}

			searchTimer.Stop();

			double outScore = board.sideToMove == Color.Black ? -lastScore : lastScore;
			if (Math.Abs(outScore) > 900000)
			{
				outScore = Math.Sign(outScore) * ((MateScore - Math.Abs(outScore)) / 2);
				Console.WriteLine($"Final evaluation: {(outScore > 0 ? "" : "-")}M{Math.Abs(outScore)}");
			}
			else
			{
				Console.WriteLine($"Final evaluation: {outScore / 100.0}");
			}

			Console.WriteLine($"Reduced: {reduced}, Re-searched: {researched} ({(100.0 * researched / Math.Max(1, reduced)):F2}%)");
			Console.WriteLine($"Null-window: {nullwindow}, Null-researched: {nullReseached} ({(100.0 * nullReseached / Math.Max(1, nullwindow)):F2}%)");
			Console.WriteLine($"Total 3 fold exits detected: {threeFoldDetections}");
			if (Program.TTStats)
			{
				// Full TT summary with existing fields only
				double overallUsableHitRate = tt.Lookups > 0 ? 100.0 * tt.UsableHits / tt.Lookups : 0.0;
				Console.WriteLine("=== Transposition Table Summary ===");
				Console.WriteLine($"Lookups: {tt.Lookups.ToString("N0")}");
				Console.WriteLine($"UsableHits (score usable for cutoff): {tt.UsableHits.ToString("N0")} ({overallUsableHitRate:F2}%)");
				Console.WriteLine($"MoveOnlyHits (only move hint returned): {tt.MoveOnlyHits}");
				Console.WriteLine($"CutoffsFromTT: {tt.CutoffsFromTT.ToString("N0")}");
				Console.WriteLine($"TagCollisions: {tt.TagCollisions.ToString("N0")}");
				//Console.WriteLine($"AgeMismatches: {tt.AgeMismatches.ToString("N0")}");
				//Console.WriteLine($"DepthTooLow: {tt.DepthTooLow.ToString("N0")}");
			}
			// Reset counters
			reduced = 0;
			researched = 0;
			nullwindow = 0;
			nullReseached = 0;
			totalNodesVisited = 0;
			threeFoldDetections = 0;

			return lastBest;
		}

		private static int reduced = 0;
		private static int researched = 0;
		private static int nullwindow = 0;
		private static int nullReseached = 0;

		private static int ply = 0;
		private static int threeFoldDetections = 0;

		private static Move SearchAtDepth(Board board, TranspositionTable tt, int depth, int alpha, int beta, out int bestScore) 
		{
			//Console.WriteLine("Start of depth " + depth);
			//if (BitOperations.PopCount(board.bitboards[11]) > 1)
			//{
			//	Console.WriteLine($"Error in search: More than one black king detected on the board, at searchdepth {depth}");
			//	Utils.PrintBitboard(board.bitboards[11]);
			//}
			//if (BitOperations.PopCount(board.bitboards[5]) > 1)
			//{
			//	Console.WriteLine($"Error in search: More than one white king detected on the board, at searchdepth {depth}");
			//	Utils.PrintBitboard(board.bitboards[5]);
			//}

			//Console.WriteLine("running at depth" + depth);
			ply = 0;
			//Console.WriteLine(Fen.ToFEN(board));
			bool isWhite = board.sideToMove == Color.White; 
			bestScore = int.MinValue;
			Move bestMove = default;
			
			Span<Move> full = new Span<Move>(_moveBuffer[0]);
			int count = 0;
			MoveGen.FilteredLegalMoves(board, full, ref count, isWhite);

			Span<Move> moves = full.Slice(0, count);

			MoveGen.FlagCheckAndMate(board, moves, count, isWhite);
  
			Move ttMove = default;

			if (!tt.Probe(board.zobristKey, depth, alpha, beta, out _, out ushort ttMoveCode))
				ttMove = default;
			else
				ttMove = Move.FromEncoded(ttMoveCode);  

			if(useSIMD)
				MoveOrdering.OrderMoves(board, moves, ttMove, depth);
			else
				MoveOrdering.OrderMovesScalar(board, moves, ttMove, depth);
			//Console.WriteLine("total moves" + moves.Length);

			foreach (var mv in moves)
				if ((mv.Flags & MoveFlags.Checkmate) != 0)
					return mv;

			//foreach (Move mv in moves)
			//{
			//    if ((int)mv.PieceMoved / 6 != 0)
			//    {
			//        Console.WriteLine("wrong somehow");
			//    }
			//}

			//print order to check for moveordering quality
			//for (int i = 0; i < moves.Length; i++)
			//{
			//	Console.Write(MoveNotation.ToAlgebraicNotation(moves[i], moves) + "  |  ");
			//}
			Console.WriteLine();
			//Console.WriteLine("total moves" + moves.Length);
			ulong bKingStart = board.bitboards[11];
	//        foreach (Move mv in moves)
	//        {
	//            Console.Write(mv.PieceMoved);
				//Console.WriteLine(mv.ToString());
	//            if ((int)mv.PieceMoved > 5)
	//            {
	//                Console.WriteLine("wrong somehow");
	//            }
	//        }
			//Span<Move> span2 = new Move[256];
			//moves.CopyTo(span2);
			//Console.WriteLine("final moveset");
			//if (moves.SequenceEqual(span2))
			//	Console.WriteLine("does equal right after creation");
			//else
			//	Console.WriteLine("does not equal right after");
			foreach (Move mv in moves)
			{
				//if (!moves.SequenceEqual(span2))
				//{
				//	Console.WriteLine("moves changed within");
				//}
				//if((int)mv.PieceMoved > 5)
				//{
				//	Console.WriteLine(mv.From + " " + mv.To);
				//}
				//Console.WriteLine(mv.PieceMoved);
				//_searchStack.Push(mv);

				//debugBoards[0] = board.Clone();

			   

				var undo = board.MakeSearchMove(board, mv);

				int score;

				score = -PVS(board, depth - 1, -beta, -alpha, !isWhite, tt);

				board.UnmakeMove(mv, undo);

				//if (!debugBoards[0].Equals(board))
				//{
				//	Console.Write($"error after unmaking {mv} in root search at depth");
				//	if (BitOperations.PopCount(board.bitboards[11]) > 1)
				//	{
				//		Console.WriteLine($"Error in search: More than one black king detected on the board, at searchdepth {depth}");
				//		Utils.PrintBitboard(board.bitboards[11]);
				//	}
				//	if (BitOperations.PopCount(board.bitboards[5]) > 1)
				//	{
				//		Console.WriteLine($"Error in search: More than one white king detected on the board, at searchdepth {depth}");
				//		Utils.PrintBitboard(board.bitboards[5]);
				//	}
				//	checkOverlap(board);
				//	for (int i = 0; i < 12; i++)
				//	{
				//		if (board.bitboards[i] != debugBoards[0].bitboards[i])
				//		{
				//			Console.WriteLine($"Bitboard {i} differs at ply {ply}! Expected");
				//			Utils.PrintBitboard(debugBoards[0].bitboards[i]);
				//			Console.WriteLine("Got:");
				//			Utils.PrintBitboard(board.bitboards[i]);
				//			Console.WriteLine("movestack:");
				//			PrintStack(_searchStack);
				//			Environment.Exit(1);
				//		}
				//	}
				//	Environment.Exit(1);
				//}



				if (score > bestScore)
				{
					bestScore = score;
					bestMove = mv;
				}
				alpha = Math.Max(alpha, score);
				//if (bKingStart != board.bitboards[11])
				//{
				//	Console.WriteLine("TEST");
				//	Console.WriteLine("Error in search: Black king moved but bitboard not updated");
				//	//Utils.PrintBitboard(board.bitboards[11]);
				//	//Utils.PrintBitboard(bKingStart);
				//	if (_searchStack.Count > 0)
				//	{
				//		PrintStack(_searchStack);
				//	}
				//	else
				//		Console.WriteLine("Search stack empty?");
				//	Environment.Exit(1);
				//}
				//_searchStack.Pop();
			}
			
			return bestMove;
		}

		private static int PVS(Board board, int depth, int alpha, int beta, bool isWhite, TranspositionTable tt, bool hasReduced = false)
		{
			//checkOverlap(board);
			//if (BitOperations.PopCount(board.bitboards[11]) > 1)
			//{
			//	Console.WriteLine($"Error in search: More than one black king detected on the board, at searchdepth {depth}");
			//	Utils.PrintBitboard(board.bitboards[11]);
			//	PrintStack(_searchStack);
			//	Environment.Exit(123);
			//}
			//if (BitOperations.PopCount(board.bitboards[5]) > 1)
			//{
			//	Console.WriteLine($"Error in search: More than one white king detected on the board, at searchdepth {depth}");
			//	Utils.PrintBitboard(board.bitboards[5]);
			//	PrintStack(_searchStack);
			//	Environment.Exit(123);
			//}

			ply++;

			//debugBoards[ply] = board.Clone();

			NodesVisited++;

			//Console.WriteLine(Fen.ToFEN(board));

			if (((NodesVisited & 2047) == 0))
			{
				if (stopSearch || searchTimer.ElapsedMilliseconds >= classTimeLimitMs)
				{
					ply--;
					mustStop = true;
					return 0;
				}
			}

			bool is3fold = board.IsThreefoldRepetition();
			//if (!board.Equals(debugBoards[ply]))
			//{
			//	Console.WriteLine("Aft 3fR");
			//}
			if (is3fold || board.halfmoveClock > 50 || !HasSufficientMaterial(board))
			{
				if (is3fold) threeFoldDetections++;
				ply--;
				return 0;
			}

			if (depth <= 0)
			{
				ply--;
				return Quiesce(board, alpha, beta, isWhite, 1);
			}

			ulong key = board.zobristKey;
			int origAlpha = alpha;

			Move ttMove = default;
			if (tt.Probe(key, depth, alpha, beta, out int ttScore, out ushort ttMoveCode))
			{
				ply--;
				return ttScore;
			}
			else
			{
				ttMove = default;
			}
			//if (!board.Equals(debugBoards[ply]))
			//{
			//	Console.WriteLine("After Probe?");
			//}

			bool inCheck = MoveGen.IsInCheck(board, isWhite);
			//if (!board.Equals(debugBoards[ply]))
			//{
			//	Console.WriteLine("before NMP, after isCheck?");
			//}

			// Null move pruning
			if (depth >= 3 && !inCheck && HasSufficientMaterial(board))
			{
				var undoNull = board.MakeNullMove();
				int R = reduxBase + depth / reduxDiv; // Null move reduction, can be tuned
				int nullScore = -PVS(board, depth - R, -beta, -beta + 1, !isWhite, tt);
				board.UnmakeNullMove(undoNull);
				if (nullScore - NMPmargin - depth * 15 >= beta )
				{
					ply--;
					return beta;
				}
			}

			// Generate legal moves
			Span<Move> full = new Span<Move>(_moveBuffer[ply]);
			int count = 0;
			//if (!board.Equals(debugBoards[ply]))
			//{
			//	Console.WriteLine("shifted before movegen??");
			//}
			MoveGen.FilteredLegalWithoutFlag(board, full, ref count, isWhite);
			//if (!board.Equals(debugBoards[ply]))
			//{
			//	Console.WriteLine("MoveGen caused bitboard shift?");
			//}
			if (count == 0)
			{
				ply--;
				return inCheck ? -MateScore + (ply) : 0;
			}
			Span<Move> moves = full.Slice(0, count);

			if (useSIMD)
			{
				//Console.WriteLine("Using SIMD move ordering on " + Fen.ToFEN(board));
				MoveOrdering.OrderMoves(board, moves, ttMove, ply);
			}
			else
			{ 
				MoveOrdering.OrderMovesScalar(board, moves, ttMove, ply);
			}
			int best = int.MinValue;
			Move bestMove = default;
			int moveNum = 0;

			int staticEval = Eval.EvalMaterialsExternal(board, isWhite);
			int extension;
			ulong bKingStart = board.bitboards[11];
			foreach (var mv in moves)
			{
				//if(board.sideToMove == Color.White && mv.PieceMoved == Piece.BlackKing)
				//{
				//	Console.WriteLine("Error in search: Black king moved on white's turn");
				//}
				moveNum++;

				if (mustStop)
				{
					break;
				}

				extension = 0;
				if ((mv.Flags & (/*MoveFlags.Check |*/ MoveFlags.Promotion)) != 0)
					extension = 1;

				// --- Late Move Pruning (LMP) ---
				// Only for quiet moves, not in check, not first few moves, and at low depth
				if (depth <= 3 &&
					!inCheck &&
					mv.PieceCaptured == Piece.None &&
					(mv.Flags & MoveFlags.Promotion) == 0 &&
					moveNum > LMPSafeMoves + depth * LMPMvDepthMult) // threshold can be tuned
				{
					continue; // Prune this late quiet move
				}

				// --- Futility Pruning ---
				if (depth == 2 &&
					!inCheck &&
					mv.PieceCaptured == Piece.None &&
					(mv.Flags & MoveFlags.Promotion) == 0)
				{
					if (staticEval + futilityMargin <= alpha)
						continue; // Prune this move
				}


				bool isKiller = mv.Equals(killerMoves[ply, 0]) || mv.Equals(killerMoves[ply, 1]);
				bool isTTMove = mv.Equals(ttMove);

			
				bool canReduce =
					!hasReduced &&
					depth >= 4 &&
					moveNum > reduxSafeMoves &&
					!inCheck &&
					mv.PieceCaptured == Piece.None &&
					(mv.Flags & MoveFlags.Promotion) == 0 &&
					!isKiller &&
					!isTTMove &&
					staticEval + LmrSafetyMargin <= alpha;

				var undo = board.MakeSearchMove(board, mv);
				//_searchStack.Push(mv);

				int score;

				if (moveNum == 1)
				{
					score = -PVS(board, depth - 1 + extension, -beta, -alpha, !isWhite, tt, hasReduced);
				}
				else if (canReduce)
				{
					reduced++;
					int R = LMRReduxFactor + depth / 9; // LMR reduction, can be tuned
					score = -PVS(board, depth - R - extension, -alpha - 1, -alpha, !isWhite, tt, true);

					if (score > alpha)
					{
						researched++;
						score = -PVS(board, depth - 1 + extension, -alpha - 1, -alpha, !isWhite, tt, hasReduced);

						if (score > alpha && score < beta)
							score = -PVS(board, depth - 1 + extension, -beta, -alpha, !isWhite, tt, hasReduced);
					}
				}
				else
				{
					nullwindow++;
					score = -PVS(board, depth - 1 + extension, -alpha - 1, -alpha, !isWhite, tt, hasReduced);

					if (score > alpha && score < beta)
					{
						nullReseached++;
						score = -PVS(board, depth - 1 + extension, -beta, -alpha, !isWhite, tt, hasReduced);
					}
				}
				

				board.UnmakeMove(mv, undo);

				//if (!debugBoards[ply].Equals(board)){
				//	checkOverlap(board);
				//	for (int i = 0; i < 12; i++)
				//	{
				//		if (board.bitboards[i] != debugBoards[ply].bitboards[i])
				//		{
				//			Console.WriteLine($"Bitboard {i} differs at ply {ply}! Expected");
				//			Utils.PrintBitboard(debugBoards[ply].bitboards[i]);
				//			Console.WriteLine("Got:");
				//			Utils.PrintBitboard(board.bitboards[i]);
				//			Console.WriteLine("movestack:");
				//			Console.WriteLine(mv);
				//			PrintStack(_searchStack);
				//			Environment.Exit(1);
				//		}
				//	}
				//}
				//if (BitOperations.PopCount(board.bitboards[11]) > 1)
				//{
				//	Console.WriteLine($"Error in search after moveundo: More than one black king detected on the board, at searchdepth {depth}");
				//	Utils.PrintBitboard(board.bitboards[11]);
				//	PrintStack(_searchStack);
				//	Environment.Exit(123);
				//}
				//if (BitOperations.PopCount(board.bitboards[5]) > 1)
				//{
				//	Console.WriteLine($"Error in search after moveundo: More than one white king detected on the board, at searchdepth {depth}");
				//	Utils.PrintBitboard(board.bitboards[5]);
				//	PrintStack(_searchStack);
				//	Environment.Exit(123);
				//}


				//if (bKingStart != board.bitboards[11])
				//{
				//	Console.WriteLine("TEST");
				//	Console.WriteLine("Error in search: Black king moved but bitboard not updated");
				//	//Utils.PrintBitboard(board.bitboards[11]);
				//	//Utils.PrintBitboard(bKingStart);
				//	if (_searchStack.Count > 0)
				//		PrintStack(_searchStack);
				//	else
				//		Console.WriteLine("Search stack empty?");
				//	Environment.Exit(1);
				//}
				//_searchStack.Pop();

				if (score > best)
				{
					best = score;
					bestMove = mv;
				}

				alpha = Math.Max(alpha, score);

				if (alpha >= beta)
				{
					if ((mv.Flags & (MoveFlags.Capture)) == 0) // quiet move
					{
						if (!mv.Equals(killerMoves[ply, 0]))
						{
							killerMoves[ply, 1] = killerMoves[ply, 0]; // push down
							killerMoves[ply, 0] = mv;                  // save new killer
						}
						MoveOrdering.RecordHistory(mv, depth);
					}
					break;
				}
			}

			NodeType flag = best <= origAlpha ? NodeType.UpperBound :
							best >= beta ? NodeType.LowerBound : NodeType.Exact;
			tt.Store(key, depth, best, flag, bestMove.Encode());

			ply--;
			
			return best;
		}
		private static void PrintStack (Stack<Move> stack)
		{
			if (stack.Count == 0)
			{
				Console.WriteLine("Stack empty");
				return;
			}
			foreach (var move in stack.Reverse())
			{
				Console.WriteLine(move.From + " " + move.To + " " + move.PieceMoved + " " + move.PieceCaptured);
				Console.Write(MoveNotation.ToAlgebraicNotation(move) + "|\t");
			}
			Console.WriteLine();
		}
		//private static void OutputError(Board board, Move move)
		//{
		//    Console.WriteLine("Error in search: " + move);
		//    Console.WriteLine("FEN: " + Fen.ToFEN(board));
		//    Console.WriteLine("Board: \n" + board.ToString());

		//}

		internal static void checkOverlap(Board board)
		{
			// Check if any pieces overlap in the bitboards
			for (int i = 0; i < 12; i++)
			{
				for (int j = i + 1; j < 12; j++)
				{
					if ((board.bitboards[i] & board.bitboards[j]) != 0)
					{
						Console.WriteLine($"Overlap detected between pieces {i} and {j}");
						//Console.WriteLine("FEN: " + Fen.ToFEN(board));
						Utils.PrintBitboard(board.bitboards[i]);
						Utils.PrintBitboard(board.bitboards[j]);
						//throw new Exception("Bitboard overlap detected.");
					}
				}
			}
		}
		private static int Quiesce(Board board, int alpha, int beta, bool isWhite, int qDepth)
		{
			bool is3fold = board.IsThreefoldRepetition();

			if (is3fold || board.halfmoveClock > 50)
			{
				if (is3fold) threeFoldDetections++;
				if (isWhite)
					return board.materialDelta > 0 ? -10 : 0;
				else
					return board.materialDelta < 0 ? -10 : 0;
			}
			if (!HasSufficientMaterial(board))
			{
				return 0;
			}
			//checkOverlap(board);

			if (qDepth >= MaxQDepth)
				return Eval.EvalBoard(board, isWhite);

			//Console.WriteLine($"Quiesce: Depth={qDepth}, Alpha={alpha}, Beta={beta}, IsWhite={isWhite}");
			bool inCheck = MoveGen.IsInCheck(board, isWhite);
			int stand = Eval.EvalMaterialsExternal(board, isWhite);
			if (stand >= beta)
				return beta;

			alpha = Math.Max(alpha, stand);

			Span<Move> full = new Span<Move>(_qBuffer[qDepth - 1]);
			int qCount = 0;
			if (inCheck)
			{
				// Generate ALL legal moves
				MoveGen.FilteredLegalWithoutFlag(board, full, ref qCount, isWhite);
			}
			else
			{
				// Generate captures + promotions into single buffer
				SpecialMoveGen.GenerateCaptureMoves(board, full, ref qCount, isWhite);
				SpecialMoveGen.GeneratePromotionMoves(board, full, ref qCount, isWhite);

			}


			Span<Move> moves = full.Slice(0, qCount);

			if (useSIMD)
				MoveOrdering.OrderMoves(board, moves);
			else
				MoveOrdering.OrderMovesScalar(board, moves);
			

			foreach (var mv in moves)
			{
				//delta prune
				if (!inCheck && (mv.Flags & MoveFlags.Promotion) == 0 && !IsGoodCapture(board,mv))
					continue;


				//if ((mv.Flags & MoveFlags.Capture) != 0)
				//{
				//    int see = MoveOrdering.SEE(board, mv);
				//    if (see < 0)
				//        continue; // skip clearly losing captures
				//}


				var undo = board.MakeSearchMove(board, mv);
				if (MoveGen.IsInCheck(board, isWhite))
				{
					// if our king is now in check, this move is illegal → skip it
					board.UnmakeMove(mv, undo);
					continue;
				}
				int score = -Quiesce(board, -beta, -alpha, !isWhite, qDepth+1);
				board.UnmakeMove(mv, undo);
				//error check


				if (score >= beta) return beta;
				alpha = Math.Max(alpha, score);
			}

			return alpha;
		}

		private static bool HasSufficientMaterial(Board board)
		{
			int offset = (int)board.sideToMove * 6;
			int pawns = BitOperations.PopCount(board.bitboards[offset + (int)Piece.WhitePawn]);
			int knights = BitOperations.PopCount(board.bitboards[offset + (int)Piece.WhiteKnight]);
			int bishops = BitOperations.PopCount(board.bitboards[offset + (int)Piece.WhiteBishop]);
			int rooks = BitOperations.PopCount(board.bitboards[offset + (int)Piece.WhiteRook]);
			int queens = BitOperations.PopCount(board.bitboards[offset + (int)Piece.WhiteQueen]);

			if (pawns > 0 || rooks > 0 || queens > 0) return true;
			if (bishops >= 2) return true;
			if (bishops == 1 && knights >= 1) return true;
			return false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool IsGoodCapture(Board board,Move mv)
		{
			return ((int)mv.PieceCaptured % 6) >= ((int)mv.PieceMoved % 6);
			//return MoveOrdering.SEE(board, mv) >= 0 ||
			//       (mv.PieceCaptured != Piece.None &&
			//        mv.PieceCaptured != Piece.WhitePawn &&
			//        mv.PieceCaptured != Piece.BlackPawn);
		}
	}
}
