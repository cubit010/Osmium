using System;
using System.Runtime.CompilerServices;

namespace ChessC_
{
    public enum NodeType : byte
    {
        Exact = 0,
        LowerBound = 1,
        UpperBound = 2
    }

    public class TranspositionTable
    {
        // 8 bytes per entry
        private readonly ulong[] entries;
        private readonly ulong[] fullKeys;   // optional for perfect safety
        private readonly int mask;
        private byte currentAge;

        public TranspositionTable(int megabytes, bool storeFullKeys = true)
        {
            int count = (megabytes * 1024 * 1024) / sizeof(ulong);
            count = 1 << (int)Math.Log2(count);
            mask = count - 1;

            entries = new ulong[count];
            fullKeys = storeFullKeys ? new ulong[count] : null;
        }

        public void NewSearch()
        {
            unchecked { currentAge++; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Pack(ushort score, ushort move, ushort tag, byte depth, byte age, NodeType type)
        {
            // Bits (LSB → MSB):
            // [ score:16 | move:16 | tag:16 | depth:7 | age:5 | type:2 | unused:10 ]
            return
                ((ulong)score & 0xFFFFUL) |
                (((ulong)move & 0xFFFFUL) << 16) |
                (((ulong)tag & 0xFFFFUL) << 32) |
                (((ulong)depth & 0x7FUL) << 48) |
                (((ulong)age & 0x1FUL) << 55) |
                (((ulong)(byte)type & 0x3UL) << 60);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort UnpackScore(ulong e) => (ushort)(e & 0xFFFF);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort UnpackMove(ulong e) => (ushort)((e >> 16) & 0xFFFF);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort UnpackTag(ulong e) => (ushort)((e >> 32) & 0xFFFF);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte UnpackDepth(ulong e) => (byte)((e >> 48) & 0x7F);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte UnpackAge(ulong e) => (byte)((e >> 55) & 0x1F);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static NodeType UnpackType(ulong e) => (NodeType)((e >> 60) & 0x3);

        public void Store(ulong key, int depth, int score, Move bestMove, NodeType type)
        {
            int idx = (int)(key & (ulong)mask);
            ushort tag = (ushort)(key & 0xFFFF);

            ulong old = entries[idx];
            byte oldAge = UnpackAge(old);
            byte oldDepth = UnpackDepth(old);

            if (oldAge != currentAge || depth > oldDepth)
            {
                var uscore = (ushort)score;
                var umove = EncodeMove(bestMove); // you must implement Encode→ushort
                entries[idx] = Pack(uscore, umove, tag, (byte)depth, currentAge, type);
                if (fullKeys != null)
                    fullKeys[idx] = key;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool Probe(ulong key, int depth, int alpha, int beta, out int score, out Move bestMove)
        {
            int idx = (int)(key & (ulong)mask);
            ulong entry = entries[idx];

            // quick tag check
            if (UnpackTag(entry) != (ushort)(key & 0xFFFF))
            {
                score = 0; bestMove = default;
                return false;
            }

            // optional full-key verify
            if (fullKeys != null && fullKeys[idx] != key)
            {
                score = 0; bestMove = default;
                return false;
            }

            // unpack
            byte age = UnpackAge(entry);
            byte edepth = UnpackDepth(entry);
            ushort uscore = UnpackScore(entry);
            ushort umove = UnpackMove(entry);
            NodeType t = UnpackType(entry);

            bestMove = DecodeMove(umove); // you must implement Decode←ushort
            score = (short)uscore;     // cast back to signed

            if (age != currentAge || edepth < depth)
                return false;

            if (t == NodeType.Exact)
                return true;
            if (t == NodeType.LowerBound && score >= beta)
                return true;
            if (t == NodeType.UpperBound && score <= alpha)
                return true;

            return false;
        }
        private static ushort EncodeMove(Move mv)
        {
            // assume From and To are enums 0–63, Promotion 0–15
            ushort f = (ushort)((byte)mv.From & 0x3F);
            ushort t = (ushort)(((byte)mv.To & 0x3F) << 6);
            ushort p = (ushort)(((byte)mv.PromotionPiece & 0x0F) << 12);
            return (ushort)(f | t | p);
        }

        // Unpacks a 16‑bit code back into a Move
        private static Move DecodeMove(ushort code)
        {
            var from = (Square)(code & 0x3F);
            var to = (Square)((code >> 6) & 0x3F);
            var promo = (Piece)((code >> 12) & 0x0F);
            return new Move(from, to, promo);
        }
    }
}
