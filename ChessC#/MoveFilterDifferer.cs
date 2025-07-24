using System;
using System.Collections.Generic;

namespace ChessC_
{
    internal static class MoveFilterDifferer
    {
        //public static void Compare(Board board, bool isWhite)
        //{
        //    Span<Move> oldMoves = stackalloc Move[256];
        //    int oldCount = 0;
        //    MoveGen.FilteredLegalWithoutFlag_Old(board, oldMoves, ref oldCount, isWhite);

        //    Span<Move> newMoves = stackalloc Move[256];
        //    int newCount = 0;
        //    MoveGen.FilteredLegalWithoutFlag(board, newMoves, ref newCount, isWhite);

        //    var oldSet = new HashSet<Move>(oldMoves.Slice(0, oldCount).ToArray());
        //    var newSet = new HashSet<Move>(newMoves.Slice(0, newCount).ToArray());

        //    if (!oldSet.SetEquals(newSet))
        //    {
        //        Console.WriteLine("=== MOVE FILTER MISMATCH DETECTED ===");
        //        Console.WriteLine("FEN: " + Fen.ToFEN(board));

        //        Console.WriteLine("board: \n" + board.ToString());

        //        Console.WriteLine("\nOld Method Moves:");
        //        foreach (var mv in oldSet)
        //            Console.WriteLine("  " + mv);

        //        Console.WriteLine("\nNew Method Moves:");
        //        foreach (var mv in newSet)
        //            Console.WriteLine("  " + mv);

        //        Console.WriteLine("\nOnly in Old:");
        //        foreach (var mv in oldSet)
        //            if (!newSet.Contains(mv))
        //                Console.WriteLine("  " + mv);

        //        Console.WriteLine("\nOnly in New:");
        //        foreach (var mv in newSet)
        //            if (!oldSet.Contains(mv))
        //                Console.WriteLine("  " + mv);

        //        throw new Exception("FilteredLegalMoves mismatch.");
        //    }
        //}
    }
}
