using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessC_
{
    public enum NodeType
    {
        Exact,
        LowerBound,
        UpperBound
    }

    public struct TTEntry
    {
        public ulong Key;
        public int Depth;
        public int Score;
        public Move BestMove;
        public NodeType Type;
        public byte Age;
    }

    public class TranspositionTable
    {
        private readonly TTEntry[] table;
        private readonly int sizeMask;
        private byte currentAge;

        public TranspositionTable(int megabytes)
        {
            int entries = (megabytes * 1024 * 1024) / sizeof(ulong) / 2;
            entries = 1 << (int)Math.Log2(entries);
            sizeMask = entries - 1;
            table = new TTEntry[entries];
            currentAge = 0;
        }

        public void NewSearch()
        {
            currentAge++;
        }

        public void Store(ulong key, int depth, int score, Move bestMove, NodeType type)
        {
            int index = (int)(key & (ulong)sizeMask);
            ref TTEntry entry = ref table[index];

            if (entry.Key != key || entry.Age != currentAge || depth > entry.Depth)
            {
                entry.Key = key;
                entry.Depth = depth;
                entry.Score = score;
                entry.BestMove = bestMove;
                entry.Type = type;
                entry.Age = currentAge;
            }
        }

        public bool Probe(ulong key, int depth, int alpha, int beta, out int score, out Move bestMove)
        {
            int index = (int)(key & (ulong)sizeMask);
            TTEntry entry = table[index];

            if (entry.Key == key)
            {
                bestMove = entry.BestMove;
                score = entry.Score;

                if (entry.Depth >= depth)
                {
                    if (entry.Type == NodeType.Exact)
                        return true;
                    if (entry.Type == NodeType.LowerBound && score >= beta)
                        return true;
                    if (entry.Type == NodeType.UpperBound && score <= alpha)
                        return true;
                }

                return false;
            }

            score = 0;
            bestMove = default;
            return false;
        }

        public bool TryGetBestMove(ulong key, out Move move)
        {
            int index = (int)(key & (ulong)sizeMask);
            TTEntry entry = table[index];
            if (entry.Key == key)
            {
                move = entry.BestMove;
                return true;
            }

            move = default;
            return false;
        }
    }
}
