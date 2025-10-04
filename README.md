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
- Algebraic Move Notation with Ambiguity Differentiation
- UCI protocol support (partial, not supporting full UCI commands yet)

## Usage

### The Engine is a console app, inputs taken through text, and out through console output, which only supports limited uci commands
- to run, download the .exe, and run it, a console window should pop up, and you can start typing commands into the chess engine
- or, download a GUI if you wish, and connect it to that and play against it, or have it play against other chess engines
### Not all of the commands are supported (yet), below is a list that is supported
- uci - uci info about author (me) and engine specifications, as well as any tuning options, however, values does not need to be adjusted to use the engine
- isready - sets up the engine
- ucinewgame - prepares the engine for a new game, with a fresh board
- quit - exits engine
- go movetime <timeMs> - start thinking with <timeMs> total time alotted
- go wtime <wtimeMs> btime <btimeMs> winc <wincMs> binc <bincMs> - can be only wtime and btime, increment is optional
- stop - early stop for search
- setoption <option> - allows user / interfaces to modify certain settings of the engine
- position <> - sets up the engine in a certain position
- position startpos - sets the engine at starting position
- position moves <move1> <move2> ... <moveN> -- from the current position (usually paired with startpos) make the moves in the list to reach a certain position
- position fen <fenStr> - parses a fen string and sets up the board according to the fen

