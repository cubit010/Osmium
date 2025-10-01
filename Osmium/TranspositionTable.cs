using System;

namespace Osmium
{
    public enum NodeType : byte
    {
        Exact = 0,
        LowerBound = 1,
        UpperBound = 2
    }

    public struct TTEntry
    {
        public ulong key;
        public byte depth8;
        public byte genBound8;
        public ushort move16;
        public short score16;

        public bool IsOccupied => key != 0;
        public byte Generation => (byte)(genBound8 >> 3);
        public NodeType Bound => (NodeType)(genBound8 & 0x3);
        public int Depth => depth8;
        public ushort Move => move16;
        public int Score => score16;

        public byte RelativeAge(byte currentGen)
        {
            return (byte)((32 + currentGen - Generation) & 31);
        }

        public bool Matches(ulong fullKey)
        {
            return key == fullKey;
        }

        public void Save(ulong fullKey, int depth, int score, NodeType boundType, ushort move, byte generation)
        {
            key = fullKey;
            depth8 = (byte)(depth);
            genBound8 = (byte)((generation << 3) | ((byte)boundType & 0x3));
            move16 = move;
            score16 = (short)score;
        }
    }
    public class TranspositionTable
    {
        private readonly TTEntry[] table;
        private readonly int clusterCount;
        private readonly int slotsPerBucket = 4; 
        private readonly ulong bucketMask;
        private byte generation;

        public uint Lookups { get; private set; }
        public uint UsableHits { get; private set; }
        public uint MoveOnlyHits { get; private set; }
        public uint CutoffsFromTT { get; private set; }
        public uint TagCollisions { get; private set; }
        public uint Hits { get; private set; }

        public TranspositionTable(int megabytes)
        {
            long bytes = megabytes * 1024L * 1024L;
            int entrySize = 16; // Your TTEntry size
            int totalEntries = (int)Math.Max(4, bytes / entrySize);

            // More clusters with exactly 4 slots each
            clusterCount = NextPowerOfTwo(totalEntries / slotsPerBucket);
            table = new TTEntry[clusterCount * slotsPerBucket];
            bucketMask = (ulong)(clusterCount - 1);
            generation = 0;

            //Console.WriteLine($"TT: {totalEntries:N0} entries, {clusterCount:N0} clusters, 4 slots each");
        }

        public bool Probe(ulong key, int depth, int alpha, int beta, out int score, out ushort bestMove)
        {
            if (Program.TTStats) Lookups++;

            score = 0;
            bestMove = 0;

            int clusterIndex = (int)(key & bucketMask) * slotsPerBucket;
            TTEntry foundEntry = default;
            bool hasMove = false;
            bool hasScore = false;

            // Search all 4 slots in the cluster
            for (int i = 0; i < slotsPerBucket; i++)
            {
                ref TTEntry entry = ref table[clusterIndex + i];

                if (!entry.IsOccupied || !entry.Matches(key))
                    continue;

                // Found matching entry
                if (entry.Move != 0 && !hasMove)
                {
                    bestMove = entry.Move;
                    hasMove = true;
                }

         
                if (entry.Depth >= depth && !hasScore)
                {
                    switch (entry.Bound)
                    {
                        case NodeType.Exact:
                            score = entry.Score;
                            hasScore = true;
                            if (Program.TTStats) { UsableHits++; Hits++; }
                            break;

                        case NodeType.LowerBound when entry.Score >= beta:
                            score = entry.Score;
                            hasScore = true;
                            if (Program.TTStats) { CutoffsFromTT++; UsableHits++; Hits++; }
                            break;

                        case NodeType.UpperBound when entry.Score <= alpha:
                            score = entry.Score;
                            hasScore = true;
                            if (Program.TTStats) { CutoffsFromTT++; UsableHits++; Hits++; }
                            break;
                    }
                }

                // If we found both move and usable score, we're done
                if (hasMove && hasScore)
                    break;
            }

            if (hasMove && !hasScore && Program.TTStats)
                MoveOnlyHits++;

            return hasScore;
        }

        public void Store(ulong key, int depth, int score, NodeType boundType, ushort move)
        {
            int clusterIndex = (int)(key & bucketMask) * slotsPerBucket;

            // Always overwrite matching entry if found
            for (int i = 0; i < slotsPerBucket; i++)
            {
                ref TTEntry entry = ref table[clusterIndex + i];
                if (entry.IsOccupied && entry.Matches(key))
                {
                    // Always overwrite 
                    entry.Save(key, depth, score, boundType, move, generation);
                    return;
                }
            }

            // No matching entry found, find replacement candidate
            int replaceIndex = 0;
            int lowestPriority = int.MaxValue;

            for (int i = 0; i < slotsPerBucket; i++)
            {
                ref TTEntry entry = ref table[clusterIndex + i];

                // Empty slot - use immediately
                if (!entry.IsOccupied)
                {
                    replaceIndex = i;
                    break;
                }

                // Calculate replacement priority (lower = more likely to replace)
                // Prioritize: old generation > shallow depth > non-exact bounds
                int age = entry.RelativeAge(generation);
                int priority = entry.Depth - (age * 8); // Heavy age penalty like before

                if (priority < lowestPriority)
                {
                    lowestPriority = priority;
                    replaceIndex = i;
                }
            }

            // Replace the selected entry
            ref TTEntry victim = ref table[clusterIndex + replaceIndex];
            victim.Save(key, depth, score, boundType, move, generation);
        }

        public void NewSearch()
        {
            unchecked { generation++; }
        }

        private static int NextPowerOfTwo(int v)
        {
            v = Math.Max(1, v);
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v++;
            return v;
        }
    }
}
