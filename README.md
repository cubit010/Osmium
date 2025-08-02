Osmium Chess Engine
Osmium is a fast and modern chess engine written in C# targeting .NET 8.0. It uses a bitboard-based architecture and incorporates advanced move generation, search algorithms, and evaluation techniques to achieve efficient and accurate play.

Features
Position Representation
Fully bitboard-based engine

Move generation:

Pawns, knights, and kings use precomputed move tables

Bishops, rooks, and queens use magic bitboards

Move legality and position legality determined using magic bitboards and direct bitboard operations

Search Algorithms
Negamax search with:

Principal Variation Search (PVS)

Late Move Pruning (LMP)

Late Move Reductions (LMR)

Futility pruning

Null move pruning

Null window search

Aspiration windows

Quiescence search with delta pruning

Transposition table for repeated position caching

Move Ordering
Fully sorted move list using multiple heuristics:

MVV-LVA (Most Valuable Victim - Least Valuable Attacker)

History heuristic

Killer move heuristic

Counter move heuristic

Evaluation
Material evaluation with delta updates

Piece-square tables (PSTs)

King safety evaluation

Pawn structure analysis

Mobility evaluation

Other Capabilities
FEN parsing and position initialization

UCI protocol support (in development)
