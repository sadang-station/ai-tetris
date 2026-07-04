namespace AiTetris.Game;

public enum PieceKind
{
    I,
    O,
    T,
    S,
    Z,
    J,
    L
}

public enum Rotation
{
    North = 0,
    East = 1,
    South = 2,
    West = 3
}

public enum MoveCommand
{
    None,
    Left,
    Right,
    SoftDrop,
    HardDrop,
    RotateClockwise,
    RotateCounterClockwise,
    Hold,
    Pause,
    Quit
}

public readonly record struct CellPoint(int X, int Y);

public readonly record struct ActivePiece(PieceKind Kind, int X, int Y, Rotation Rotation)
{
    public ActivePiece Move(int deltaX, int deltaY) => this with { X = X + deltaX, Y = Y + deltaY };

    public ActivePiece WithRotation(Rotation rotation) => this with { Rotation = rotation };

    public IEnumerable<CellPoint> Cells => PieceShapes.GetCells(this);
}

public readonly record struct BoardMetrics(
    int AggregateHeight,
    int Holes,
    int Bumpiness,
    int MaxHeight,
    int WellDepth);

public sealed class Board
{
    public const int Width = 10;
    public const int VisibleHeight = 20;
    public const int BufferHeight = 4;
    public const int TotalHeight = VisibleHeight + BufferHeight;

    private readonly PieceKind?[,] _cells;

    public Board()
        : this(new PieceKind?[Width, TotalHeight])
    {
    }

    private Board(PieceKind?[,] cells)
    {
        _cells = cells;
    }

    public PieceKind? GetCell(int x, int y)
    {
        ThrowIfOutOfRange(x, y);
        return _cells[x, y];
    }

    public PieceKind? GetVisibleCell(int x, int visibleY)
    {
        if (visibleY < 0 || visibleY >= VisibleHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(visibleY));
        }

        return GetCell(x, visibleY + BufferHeight);
    }

    public void SetCell(int x, int y, PieceKind? kind)
    {
        ThrowIfOutOfRange(x, y);
        _cells[x, y] = kind;
    }

    public bool IsOccupied(int x, int y)
    {
        if (x < 0 || x >= Width || y >= TotalHeight)
        {
            return true;
        }

        return y >= 0 && _cells[x, y].HasValue;
    }

    public bool CanPlace(ActivePiece piece)
    {
        foreach (var cell in piece.Cells)
        {
            if (IsOccupied(cell.X, cell.Y))
            {
                return false;
            }
        }

        return true;
    }

    public void Place(ActivePiece piece)
    {
        foreach (var cell in piece.Cells)
        {
            if (cell.Y < 0)
            {
                continue;
            }

            if (cell.X < 0 || cell.X >= Width || cell.Y >= TotalHeight)
            {
                throw new InvalidOperationException("Cannot place a piece outside the board.");
            }

            _cells[cell.X, cell.Y] = piece.Kind;
        }
    }

    public int ClearFullLines()
    {
        var cleared = 0;
        var writeY = TotalHeight - 1;

        for (var readY = TotalHeight - 1; readY >= 0; readY--)
        {
            var full = true;
            for (var x = 0; x < Width; x++)
            {
                if (!_cells[x, readY].HasValue)
                {
                    full = false;
                    break;
                }
            }

            if (full)
            {
                cleared++;
                continue;
            }

            if (writeY != readY)
            {
                for (var x = 0; x < Width; x++)
                {
                    _cells[x, writeY] = _cells[x, readY];
                }
            }

            writeY--;
        }

        for (var y = writeY; y >= 0; y--)
        {
            for (var x = 0; x < Width; x++)
            {
                _cells[x, y] = null;
            }
        }

        return cleared;
    }

    public BoardMetrics GetMetrics()
    {
        var heights = new int[Width];
        var holes = 0;

        for (var x = 0; x < Width; x++)
        {
            var foundBlock = false;
            for (var y = 0; y < TotalHeight; y++)
            {
                if (_cells[x, y].HasValue)
                {
                    if (!foundBlock)
                    {
                        heights[x] = TotalHeight - y;
                        foundBlock = true;
                    }
                }
                else if (foundBlock)
                {
                    holes++;
                }
            }
        }

        var aggregateHeight = heights.Sum();
        var maxHeight = heights.Max();
        var bumpiness = 0;
        var wellDepth = 0;

        for (var x = 0; x < Width; x++)
        {
            if (x < Width - 1)
            {
                bumpiness += Math.Abs(heights[x] - heights[x + 1]);
            }

            var left = x == 0 ? TotalHeight : heights[x - 1];
            var right = x == Width - 1 ? TotalHeight : heights[x + 1];
            var wall = Math.Min(left, right);
            if (wall > heights[x])
            {
                wellDepth += wall - heights[x];
            }
        }

        return new BoardMetrics(aggregateHeight, holes, bumpiness, maxHeight, wellDepth);
    }

    public Board Clone()
    {
        var copy = new PieceKind?[Width, TotalHeight];
        Array.Copy(_cells, copy, _cells.Length);
        return new Board(copy);
    }

    public string Fingerprint()
    {
        var chars = new char[Width * TotalHeight];
        var index = 0;
        for (var y = 0; y < TotalHeight; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                chars[index++] = _cells[x, y] is { } kind ? kind.ToString()[0] : '.';
            }
        }

        return new string(chars);
    }

    private static void ThrowIfOutOfRange(int x, int y)
    {
        if (x < 0 || x >= Width)
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        if (y < 0 || y >= TotalHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(y));
        }
    }
}

public static class PieceShapes
{
    private static readonly Dictionary<(PieceKind Kind, Rotation Rotation), CellPoint[]> Offsets = new()
    {
        [(PieceKind.I, Rotation.North)] = new[] { new CellPoint(0, 1), new CellPoint(1, 1), new CellPoint(2, 1), new CellPoint(3, 1) },
        [(PieceKind.I, Rotation.East)] = new[] { new CellPoint(2, 0), new CellPoint(2, 1), new CellPoint(2, 2), new CellPoint(2, 3) },
        [(PieceKind.I, Rotation.South)] = new[] { new CellPoint(0, 2), new CellPoint(1, 2), new CellPoint(2, 2), new CellPoint(3, 2) },
        [(PieceKind.I, Rotation.West)] = new[] { new CellPoint(1, 0), new CellPoint(1, 1), new CellPoint(1, 2), new CellPoint(1, 3) },

        [(PieceKind.O, Rotation.North)] = new[] { new CellPoint(1, 0), new CellPoint(2, 0), new CellPoint(1, 1), new CellPoint(2, 1) },
        [(PieceKind.O, Rotation.East)] = new[] { new CellPoint(1, 0), new CellPoint(2, 0), new CellPoint(1, 1), new CellPoint(2, 1) },
        [(PieceKind.O, Rotation.South)] = new[] { new CellPoint(1, 0), new CellPoint(2, 0), new CellPoint(1, 1), new CellPoint(2, 1) },
        [(PieceKind.O, Rotation.West)] = new[] { new CellPoint(1, 0), new CellPoint(2, 0), new CellPoint(1, 1), new CellPoint(2, 1) },

        [(PieceKind.T, Rotation.North)] = new[] { new CellPoint(1, 0), new CellPoint(0, 1), new CellPoint(1, 1), new CellPoint(2, 1) },
        [(PieceKind.T, Rotation.East)] = new[] { new CellPoint(1, 0), new CellPoint(1, 1), new CellPoint(2, 1), new CellPoint(1, 2) },
        [(PieceKind.T, Rotation.South)] = new[] { new CellPoint(0, 1), new CellPoint(1, 1), new CellPoint(2, 1), new CellPoint(1, 2) },
        [(PieceKind.T, Rotation.West)] = new[] { new CellPoint(1, 0), new CellPoint(0, 1), new CellPoint(1, 1), new CellPoint(1, 2) },

        [(PieceKind.S, Rotation.North)] = new[] { new CellPoint(1, 0), new CellPoint(2, 0), new CellPoint(0, 1), new CellPoint(1, 1) },
        [(PieceKind.S, Rotation.East)] = new[] { new CellPoint(1, 0), new CellPoint(1, 1), new CellPoint(2, 1), new CellPoint(2, 2) },
        [(PieceKind.S, Rotation.South)] = new[] { new CellPoint(1, 1), new CellPoint(2, 1), new CellPoint(0, 2), new CellPoint(1, 2) },
        [(PieceKind.S, Rotation.West)] = new[] { new CellPoint(0, 0), new CellPoint(0, 1), new CellPoint(1, 1), new CellPoint(1, 2) },

        [(PieceKind.Z, Rotation.North)] = new[] { new CellPoint(0, 0), new CellPoint(1, 0), new CellPoint(1, 1), new CellPoint(2, 1) },
        [(PieceKind.Z, Rotation.East)] = new[] { new CellPoint(2, 0), new CellPoint(1, 1), new CellPoint(2, 1), new CellPoint(1, 2) },
        [(PieceKind.Z, Rotation.South)] = new[] { new CellPoint(0, 1), new CellPoint(1, 1), new CellPoint(1, 2), new CellPoint(2, 2) },
        [(PieceKind.Z, Rotation.West)] = new[] { new CellPoint(1, 0), new CellPoint(0, 1), new CellPoint(1, 1), new CellPoint(0, 2) },

        [(PieceKind.J, Rotation.North)] = new[] { new CellPoint(0, 0), new CellPoint(0, 1), new CellPoint(1, 1), new CellPoint(2, 1) },
        [(PieceKind.J, Rotation.East)] = new[] { new CellPoint(1, 0), new CellPoint(2, 0), new CellPoint(1, 1), new CellPoint(1, 2) },
        [(PieceKind.J, Rotation.South)] = new[] { new CellPoint(0, 1), new CellPoint(1, 1), new CellPoint(2, 1), new CellPoint(2, 2) },
        [(PieceKind.J, Rotation.West)] = new[] { new CellPoint(1, 0), new CellPoint(1, 1), new CellPoint(0, 2), new CellPoint(1, 2) },

        [(PieceKind.L, Rotation.North)] = new[] { new CellPoint(2, 0), new CellPoint(0, 1), new CellPoint(1, 1), new CellPoint(2, 1) },
        [(PieceKind.L, Rotation.East)] = new[] { new CellPoint(1, 0), new CellPoint(1, 1), new CellPoint(1, 2), new CellPoint(2, 2) },
        [(PieceKind.L, Rotation.South)] = new[] { new CellPoint(0, 1), new CellPoint(1, 1), new CellPoint(2, 1), new CellPoint(0, 2) },
        [(PieceKind.L, Rotation.West)] = new[] { new CellPoint(0, 0), new CellPoint(1, 0), new CellPoint(1, 1), new CellPoint(1, 2) }
    };

    public static IReadOnlyList<CellPoint> GetOffsets(PieceKind kind, Rotation rotation) => Offsets[(kind, rotation)];

    public static IEnumerable<CellPoint> GetCells(ActivePiece piece)
    {
        foreach (var offset in GetOffsets(piece.Kind, piece.Rotation))
        {
            yield return new CellPoint(piece.X + offset.X, piece.Y + offset.Y);
        }
    }
}

public static class GameRules
{
    public static int GetLineClearScore(int linesCleared) => linesCleared switch
    {
        0 => 0,
        1 => 100,
        2 => 300,
        3 => 500,
        4 => 800,
        _ => throw new ArgumentOutOfRangeException(nameof(linesCleared))
    };

    public static ActivePiece CreateSpawnPiece(PieceKind kind)
    {
        var x = kind == PieceKind.O ? 4 : 3;
        return new ActivePiece(kind, x, 1, Rotation.North);
    }

    public static bool TryRotate(Board board, ActivePiece piece, bool clockwise, out ActivePiece rotated)
    {
        var to = Normalize((int)piece.Rotation + (clockwise ? 1 : -1));
        if (piece.Kind == PieceKind.O)
        {
            rotated = piece.WithRotation(to);
            return board.CanPlace(rotated);
        }

        foreach (var kick in GetKicks(piece.Kind, piece.Rotation, to))
        {
            var candidate = piece.WithRotation(to).Move(kick.X, kick.Y);
            if (board.CanPlace(candidate))
            {
                rotated = candidate;
                return true;
            }
        }

        rotated = piece;
        return false;
    }

    private static Rotation Normalize(int rotation)
    {
        var value = rotation % 4;
        if (value < 0)
        {
            value += 4;
        }

        return (Rotation)value;
    }

    private static IReadOnlyList<CellPoint> GetKicks(PieceKind kind, Rotation from, Rotation to)
    {
        return kind == PieceKind.I
            ? GetIKicks(from, to)
            : GetJlStzKicks(from, to);
    }

    private static IReadOnlyList<CellPoint> GetJlStzKicks(Rotation from, Rotation to) => (from, to) switch
    {
        (Rotation.North, Rotation.East) => new[] { new CellPoint(0, 0), new CellPoint(-1, 0), new CellPoint(-1, 1), new CellPoint(0, -2), new CellPoint(-1, -2) },
        (Rotation.East, Rotation.North) => new[] { new CellPoint(0, 0), new CellPoint(1, 0), new CellPoint(1, -1), new CellPoint(0, 2), new CellPoint(1, 2) },
        (Rotation.East, Rotation.South) => new[] { new CellPoint(0, 0), new CellPoint(1, 0), new CellPoint(1, -1), new CellPoint(0, 2), new CellPoint(1, 2) },
        (Rotation.South, Rotation.East) => new[] { new CellPoint(0, 0), new CellPoint(-1, 0), new CellPoint(-1, 1), new CellPoint(0, -2), new CellPoint(-1, -2) },
        (Rotation.South, Rotation.West) => new[] { new CellPoint(0, 0), new CellPoint(1, 0), new CellPoint(1, 1), new CellPoint(0, -2), new CellPoint(1, -2) },
        (Rotation.West, Rotation.South) => new[] { new CellPoint(0, 0), new CellPoint(-1, 0), new CellPoint(-1, -1), new CellPoint(0, 2), new CellPoint(-1, 2) },
        (Rotation.West, Rotation.North) => new[] { new CellPoint(0, 0), new CellPoint(-1, 0), new CellPoint(-1, -1), new CellPoint(0, 2), new CellPoint(-1, 2) },
        (Rotation.North, Rotation.West) => new[] { new CellPoint(0, 0), new CellPoint(1, 0), new CellPoint(1, 1), new CellPoint(0, -2), new CellPoint(1, -2) },
        _ => new[] { new CellPoint(0, 0) }
    };

    private static IReadOnlyList<CellPoint> GetIKicks(Rotation from, Rotation to) => (from, to) switch
    {
        (Rotation.North, Rotation.East) => new[] { new CellPoint(0, 0), new CellPoint(-2, 0), new CellPoint(1, 0), new CellPoint(-2, -1), new CellPoint(1, 2) },
        (Rotation.East, Rotation.North) => new[] { new CellPoint(0, 0), new CellPoint(2, 0), new CellPoint(-1, 0), new CellPoint(2, 1), new CellPoint(-1, -2) },
        (Rotation.East, Rotation.South) => new[] { new CellPoint(0, 0), new CellPoint(-1, 0), new CellPoint(2, 0), new CellPoint(-1, 2), new CellPoint(2, -1) },
        (Rotation.South, Rotation.East) => new[] { new CellPoint(0, 0), new CellPoint(1, 0), new CellPoint(-2, 0), new CellPoint(1, -2), new CellPoint(-2, 1) },
        (Rotation.South, Rotation.West) => new[] { new CellPoint(0, 0), new CellPoint(2, 0), new CellPoint(-1, 0), new CellPoint(2, 1), new CellPoint(-1, -2) },
        (Rotation.West, Rotation.South) => new[] { new CellPoint(0, 0), new CellPoint(-2, 0), new CellPoint(1, 0), new CellPoint(-2, -1), new CellPoint(1, 2) },
        (Rotation.West, Rotation.North) => new[] { new CellPoint(0, 0), new CellPoint(1, 0), new CellPoint(-2, 0), new CellPoint(1, -2), new CellPoint(-2, 1) },
        (Rotation.North, Rotation.West) => new[] { new CellPoint(0, 0), new CellPoint(-1, 0), new CellPoint(2, 0), new CellPoint(-1, 2), new CellPoint(2, -1) },
        _ => new[] { new CellPoint(0, 0) }
    };
}

public struct XorShift32
{
    private uint _state;

    public XorShift32(int seed)
        : this((uint)seed)
    {
    }

    public XorShift32(uint seed)
    {
        _state = seed == 0 ? 0x6D2B79F5u : seed;
    }

    public uint State => _state;

    public uint NextUInt()
    {
        var x = _state;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        _state = x == 0 ? 0x6D2B79F5u : x;
        return _state;
    }

    public int Next(int exclusiveMax)
    {
        if (exclusiveMax <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
        }

        return (int)(NextUInt() % (uint)exclusiveMax);
    }

    public double NextDouble() => NextUInt() / (double)uint.MaxValue;
}

public sealed class PieceBag
{
    private readonly Queue<PieceKind> _queue;
    private XorShift32 _random;

    public PieceBag(int seed)
        : this(seed, new XorShift32(seed), new Queue<PieceKind>())
    {
    }

    private PieceBag(int seed, XorShift32 random, Queue<PieceKind> queue)
    {
        Seed = seed;
        _random = random;
        _queue = queue;
    }

    public int Seed { get; }

    public PieceKind Next()
    {
        EnsureQueued(1);
        return _queue.Dequeue();
    }

    public IReadOnlyList<PieceKind> Peek(int count)
    {
        EnsureQueued(count);
        return _queue.Take(count).ToArray();
    }

    public PieceBag Clone() => new(Seed, new XorShift32(_random.State), new Queue<PieceKind>(_queue));

    private void EnsureQueued(int count)
    {
        while (_queue.Count < count)
        {
            FillBag();
        }
    }

    private void FillBag()
    {
        var pieces = Enum.GetValues<PieceKind>();
        for (var i = pieces.Length - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (pieces[i], pieces[j]) = (pieces[j], pieces[i]);
        }

        foreach (var piece in pieces)
        {
            _queue.Enqueue(piece);
        }
    }
}

public sealed class GameState
{
    private GameState(Board board, PieceBag bag)
    {
        Board = board;
        Bag = bag;
        ActivePiece = GameRules.CreateSpawnPiece(Bag.Next());
        PieceSerial = 1;
        IsGameOver = !Board.CanPlace(ActivePiece);
    }

    private GameState(
        Board board,
        PieceBag bag,
        ActivePiece activePiece,
        PieceKind? holdPiece,
        bool holdUsed,
        int score,
        int lines,
        bool isGameOver,
        int pieceSerial)
    {
        Board = board;
        Bag = bag;
        ActivePiece = activePiece;
        HoldPiece = holdPiece;
        HoldUsed = holdUsed;
        Score = score;
        Lines = lines;
        IsGameOver = isGameOver;
        PieceSerial = pieceSerial;
    }

    public Board Board { get; }

    public PieceBag Bag { get; }

    public ActivePiece ActivePiece { get; private set; }

    public PieceKind? HoldPiece { get; private set; }

    public bool HoldUsed { get; private set; }

    public int Score { get; private set; }

    public int Lines { get; private set; }

    public bool IsGameOver { get; private set; }

    public int PieceSerial { get; private set; }

    public static GameState Create(int seed) => new(new Board(), new PieceBag(seed));

    public GameState Clone() => new(
        Board.Clone(),
        Bag.Clone(),
        ActivePiece,
        HoldPiece,
        HoldUsed,
        Score,
        Lines,
        IsGameOver,
        PieceSerial);

    public IReadOnlyList<PieceKind> NextPieces(int count) => Bag.Peek(count);

    public bool ApplyCommand(MoveCommand command)
    {
        if (IsGameOver)
        {
            return false;
        }

        return command switch
        {
            MoveCommand.Left => TryMove(-1, 0),
            MoveCommand.Right => TryMove(1, 0),
            MoveCommand.SoftDrop => TrySoftDrop(),
            MoveCommand.HardDrop => HardDrop() >= 0,
            MoveCommand.RotateClockwise => TryRotate(clockwise: true),
            MoveCommand.RotateCounterClockwise => TryRotate(clockwise: false),
            MoveCommand.Hold => Hold(),
            _ => false
        };
    }

    public bool TryMove(int deltaX, int deltaY)
    {
        var candidate = ActivePiece.Move(deltaX, deltaY);
        if (!Board.CanPlace(candidate))
        {
            return false;
        }

        ActivePiece = candidate;
        return true;
    }

    public bool TrySoftDrop()
    {
        if (TryMove(0, 1))
        {
            Score += 1;
            return true;
        }

        LockPiece();
        return false;
    }

    public void StepGravity()
    {
        if (IsGameOver)
        {
            return;
        }

        if (!TryMove(0, 1))
        {
            LockPiece();
        }
    }

    public int HardDrop()
    {
        if (IsGameOver)
        {
            return -1;
        }

        var distance = GetDropDistance(ActivePiece);
        ActivePiece = ActivePiece.Move(0, distance);
        Score += distance * 2;
        LockPiece();
        return distance;
    }

    public bool TryRotate(bool clockwise)
    {
        if (!GameRules.TryRotate(Board, ActivePiece, clockwise, out var rotated))
        {
            return false;
        }

        ActivePiece = rotated;
        return true;
    }

    public bool Hold()
    {
        if (IsGameOver || HoldUsed)
        {
            return false;
        }

        var current = ActivePiece.Kind;
        ActivePiece = HoldPiece is { } held
            ? GameRules.CreateSpawnPiece(held)
            : GameRules.CreateSpawnPiece(Bag.Next());
        HoldPiece = current;
        HoldUsed = true;

        if (!Board.CanPlace(ActivePiece))
        {
            IsGameOver = true;
            return false;
        }

        return true;
    }

    public int GetDropDistance(ActivePiece piece)
    {
        var distance = 0;
        while (Board.CanPlace(piece.Move(0, distance + 1)))
        {
            distance++;
        }

        return distance;
    }

    public ActivePiece GetGhostPiece() => ActivePiece.Move(0, GetDropDistance(ActivePiece));

    public GameState CloneAfterLocking(ActivePiece droppedPiece, int extraScore = 0)
    {
        var clone = Clone();
        clone.ActivePiece = droppedPiece;
        clone.Score += extraScore;
        clone.LockPiece();
        return clone;
    }

    public bool TryApplyPlacement(bool useHold, int targetX, Rotation targetRotation, bool awardHardDrop)
    {
        if (IsGameOver)
        {
            return false;
        }

        if (useHold && !Hold())
        {
            return false;
        }

        var candidate = ActivePiece with { X = targetX, Rotation = targetRotation };
        if (!Board.CanPlace(candidate))
        {
            return false;
        }

        var distance = GetDropDistance(candidate);
        ActivePiece = candidate.Move(0, distance);
        if (awardHardDrop)
        {
            Score += distance * 2;
        }

        LockPiece();
        return true;
    }

    public void SetActivePieceForTesting(ActivePiece piece)
    {
        ActivePiece = piece;
    }

    private void LockPiece()
    {
        Board.Place(ActivePiece);
        var cleared = Board.ClearFullLines();
        Score += GameRules.GetLineClearScore(cleared);
        Lines += cleared;
        SpawnNext();
    }

    private void SpawnNext()
    {
        ActivePiece = GameRules.CreateSpawnPiece(Bag.Next());
        HoldUsed = false;
        PieceSerial++;

        if (!Board.CanPlace(ActivePiece))
        {
            IsGameOver = true;
        }
    }
}

