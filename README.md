# Osmium Chess Engine

**Osmium** is a chess engine written in C# targeting .NET 8.0. It uses a bitboard-based architecture and incorporates advanced move generation, search algorithms, and evaluation techniques to achieve efficient and accurate play.

## Features

### Position Representation

- Bitboard-based position representation
- Move generation:
  - Pawns, knights, and kings use precomputed move tables
  - Bishops, rooks, and queens use magic bitboards
- Move and position legality determined via:
  - Bitboard operations
  - Magic bitboards

### Search Algorithms

- Negamax search with:
  - Principal Variation Search (PVS)
  - Late Move Pruning (LMP)
  - Late Move Reductions (LMR)
  - Futility pruning
  - Null move pruning
  - Null window search
  - Aspiration windows
- Quiescence search with delta pruning
- Transposition table for position caching

### Move Ordering

- Fully sorted move list using:
  - MVV-LVA (Most Valuable Victim - Least Valuable Attacker)
  - History heuristic
  - Killer moves
  - Counter moves

### Evaluation

- Material evaluation with delta updates
- 3-Phased Piece-square tables (PST)
- King safety evaluation
- Pawn structure analysis
- Mobility evaluation

### Additional Capabilities

- FEN parsing and position initialization
- 
- UCI protocol support (in development)
