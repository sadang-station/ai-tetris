using AiTetris.Game;

namespace AiTetris.Ai;

public interface IAiPlayer
{
    AiDifficultyPreset Difficulty { get; }

    PlannedMove ChooseMove(GameState state);
}

public sealed record AiDifficultyPreset(
    string Id,
    string DisplayName,
    bool AllowHold,
    int LookaheadDepth,
    double MistakeRate,
    int ThinkDelayMilliseconds,
    int CommandDelayMilliseconds,
    int SoftDropsBeforeHardDrop,
    bool UseHardDrop,
    int BeamWidth,
    NeuralNetwork Network)
{
    public static AiDifficultyPreset Beginner { get; } = new(
        "beginner",
        "초급",
        AllowHold: false,
        LookaheadDepth: 0,
        MistakeRate: 0.12,
        ThinkDelayMilliseconds: 360,
        CommandDelayMilliseconds: 140,
        SoftDropsBeforeHardDrop: 0,
        UseHardDrop: false,
        BeamWidth: 1,
        Network: NeuralNetwork.CreateBeginner());

    public static AiDifficultyPreset Intermediate { get; } = new(
        "intermediate",
        "중급",
        AllowHold: true,
        LookaheadDepth: 1,
        MistakeRate: 0.03,
        ThinkDelayMilliseconds: 170,
        CommandDelayMilliseconds: 85,
        SoftDropsBeforeHardDrop: 3,
        UseHardDrop: true,
        BeamWidth: 3,
        Network: NeuralNetwork.CreateIntermediate());

    public static AiDifficultyPreset Advanced { get; } = new(
        "advanced",
        "고급",
        AllowHold: true,
        LookaheadDepth: 1,
        MistakeRate: 0.0,
        ThinkDelayMilliseconds: 40,
        CommandDelayMilliseconds: 45,
        SoftDropsBeforeHardDrop: 1,
        UseHardDrop: true,
        BeamWidth: 6,
        Network: NeuralNetwork.CreateAdvanced());

    public static IReadOnlyList<AiDifficultyPreset> All { get; } = new[]
    {
        Beginner,
        Intermediate,
        Advanced
    };
}

public readonly record struct PlannedMove(
    bool UseHold,
    int TargetX,
    Rotation TargetRotation,
    double Score,
    PieceKind PieceKind,
    int LinesCleared)
{
    public bool IsLegalFor(GameState state)
    {
        var clone = state.Clone();
        return clone.TryApplyPlacement(UseHold, TargetX, TargetRotation, awardHardDrop: false);
    }
}

public sealed class NeuralNetwork
{
    private const int InputCount = 6;
    private const int HiddenCount = 4;

    private readonly double[,] _inputHidden;
    private readonly double[] _hiddenBias;
    private readonly double[] _hiddenOutput;
    private readonly double _outputBias;

    public NeuralNetwork(double[,] inputHidden, double[] hiddenBias, double[] hiddenOutput, double outputBias)
    {
        if (inputHidden.GetLength(0) != HiddenCount || inputHidden.GetLength(1) != InputCount)
        {
            throw new ArgumentException("The input-hidden matrix must be 4x6.", nameof(inputHidden));
        }

        if (hiddenBias.Length != HiddenCount)
        {
            throw new ArgumentException("The hidden bias vector must contain four values.", nameof(hiddenBias));
        }

        if (hiddenOutput.Length != HiddenCount)
        {
            throw new ArgumentException("The hidden-output vector must contain four values.", nameof(hiddenOutput));
        }

        _inputHidden = inputHidden;
        _hiddenBias = hiddenBias;
        _hiddenOutput = hiddenOutput;
        _outputBias = outputBias;
    }

    public double Evaluate(IReadOnlyList<double> inputs)
    {
        if (inputs.Count != InputCount)
        {
            throw new ArgumentException("The neural network expects six input features.", nameof(inputs));
        }

        var output = _outputBias;
        for (var hidden = 0; hidden < HiddenCount; hidden++)
        {
            var activation = _hiddenBias[hidden];
            for (var input = 0; input < InputCount; input++)
            {
                activation += _inputHidden[hidden, input] * inputs[input];
            }

            output += Math.Tanh(activation) * _hiddenOutput[hidden];
        }

        return output;
    }

    public static NeuralNetwork CreateBeginner() => new(
        new double[,]
        {
            { 1.8, -0.3, -0.5, -0.2, -0.8, 0.2 },
            { 0.2, -1.1, -1.5, -0.4, -1.0, 0.1 },
            { 1.0, 0.1, -0.2, 0.1, -0.2, 0.6 },
            { 0.0, -0.4, -0.2, -0.8, -0.4, 0.0 }
        },
        new[] { 0.0, 0.2, -0.1, 0.1 },
        new[] { 1.0, 1.1, 0.4, 0.5 },
        0.0);

    public static NeuralNetwork CreateIntermediate() => new(
        new double[,]
        {
            { 2.4, -0.4, -0.8, -0.2, -1.0, 0.6 },
            { 0.1, -1.9, -3.3, -0.8, -1.9, 0.3 },
            { 1.8, -0.2, -0.8, -0.4, -0.5, 1.7 },
            { 0.2, -1.0, -0.8, -1.7, -1.0, 0.0 }
        },
        new[] { 0.0, 0.3, -0.2, 0.2 },
        new[] { 1.3, 1.5, 0.8, 0.9 },
        0.0);

    public static NeuralNetwork CreateAdvanced() => new(
        new double[,]
        {
            { 3.0, -0.5, -0.8, -0.3, -1.3, 1.0 },
            { 0.0, -2.6, -4.4, -1.1, -2.4, 0.4 },
            { 2.3, -0.2, -1.2, -0.5, -0.7, 2.4 },
            { 0.1, -1.3, -1.0, -2.3, -1.2, 0.1 }
        },
        new[] { 0.0, 0.35, -0.25, 0.15 },
        new[] { 1.4, 1.8, 1.1, 1.1 },
        0.0);
}

public sealed class AiMovePlanner
{
    public IReadOnlyList<PlannedMove> RankMoves(GameState state, AiDifficultyPreset difficulty)
    {
        if (state.IsGameOver)
        {
            return Array.Empty<PlannedMove>();
        }

        var moves = new List<ScoredMove>();
        moves.AddRange(EnumerateMoves(state.Clone(), useHold: false, difficulty, difficulty.LookaheadDepth));

        if (difficulty.AllowHold && !state.HoldUsed)
        {
            moves.AddRange(EnumerateMoves(state.Clone(), useHold: true, difficulty, difficulty.LookaheadDepth));
        }

        return moves
            .OrderByDescending(move => move.TotalScore)
            .Select(move => move.Move)
            .ToArray();
    }

    private static IEnumerable<ScoredMove> EnumerateMoves(
        GameState source,
        bool useHold,
        AiDifficultyPreset difficulty,
        int lookaheadDepth)
    {
        var state = source.Clone();
        if (useHold && !state.Hold())
        {
            yield break;
        }

        var rotations = GetRotations(state.ActivePiece.Kind);
        foreach (var rotation in rotations)
        {
            for (var x = -2; x <= Board.Width; x++)
            {
                var spawnCandidate = state.ActivePiece with { X = x, Rotation = rotation };
                if (!state.Board.CanPlace(spawnCandidate))
                {
                    continue;
                }

                var dropped = Drop(state.Board, spawnCandidate);
                var simulated = state.CloneAfterLocking(dropped);
                var linesCleared = simulated.Lines - state.Lines;
                var immediateScore = EvaluateBoard(simulated.Board, linesCleared, difficulty.Network);
                var totalScore = immediateScore;

                if (lookaheadDepth > 0 && !simulated.IsGameOver)
                {
                    var next = EnumerateMoves(simulated, useHold: false, difficulty, lookaheadDepth - 1)
                        .OrderByDescending(move => move.TotalScore)
                        .Take(Math.Max(1, difficulty.BeamWidth))
                        .ToArray();

                    if (next.Length > 0)
                    {
                        totalScore += next[0].TotalScore * 0.45;
                    }
                }

                yield return new ScoredMove(
                    new PlannedMove(useHold, x, rotation, totalScore, state.ActivePiece.Kind, linesCleared),
                    totalScore);
            }
        }
    }

    private static IReadOnlyList<Rotation> GetRotations(PieceKind kind) => kind switch
    {
        PieceKind.O => new[] { Rotation.North },
        PieceKind.I or PieceKind.S or PieceKind.Z => new[] { Rotation.North, Rotation.East },
        _ => new[] { Rotation.North, Rotation.East, Rotation.South, Rotation.West }
    };

    private static ActivePiece Drop(Board board, ActivePiece piece)
    {
        while (board.CanPlace(piece.Move(0, 1)))
        {
            piece = piece.Move(0, 1);
        }

        return piece;
    }

    private static double EvaluateBoard(Board board, int linesCleared, NeuralNetwork network)
    {
        var metrics = board.GetMetrics();
        var features = new[]
        {
            linesCleared / 4.0,
            metrics.AggregateHeight / (double)(Board.Width * Board.TotalHeight),
            metrics.Holes / (double)(Board.Width * Board.TotalHeight),
            metrics.Bumpiness / (double)(Board.Width * Board.TotalHeight),
            metrics.MaxHeight / (double)Board.TotalHeight,
            metrics.WellDepth / (double)(Board.Width * Board.TotalHeight)
        };

        var lineReward = linesCleared switch
        {
            0 => 0.0,
            1 => 7.0,
            2 => 18.0,
            3 => 36.0,
            4 => 80.0,
            _ => 0.0
        };

        var dangerHeight = Math.Max(0, metrics.MaxHeight - 16);
        var visibleOverflow = Math.Max(0, metrics.MaxHeight - Board.VisibleHeight);

        var strategicScore =
            lineReward
            + metrics.WellDepth * 0.20
            - metrics.AggregateHeight * 0.38
            - metrics.Holes * 9.50
            - metrics.Bumpiness * 0.72
            - metrics.MaxHeight * 1.25
            - dangerHeight * dangerHeight * 2.25
            - visibleOverflow * 50.0;

        return strategicScore + network.Evaluate(features) * 6.0;
    }

    private readonly record struct ScoredMove(PlannedMove Move, double TotalScore);
}

public sealed class NeuralNetworkAiPlayer : IAiPlayer
{
    private readonly AiMovePlanner _planner;
    private XorShift32 _random;

    public NeuralNetworkAiPlayer(AiDifficultyPreset difficulty, int seed)
        : this(difficulty, seed, new AiMovePlanner())
    {
    }

    public NeuralNetworkAiPlayer(AiDifficultyPreset difficulty, int seed, AiMovePlanner planner)
    {
        Difficulty = difficulty;
        _random = new XorShift32((uint)seed ^ 0xA17E71u);
        _planner = planner;
    }

    public AiDifficultyPreset Difficulty { get; }

    public PlannedMove ChooseMove(GameState state)
    {
        var ranked = _planner.RankMoves(state, Difficulty);
        if (ranked.Count == 0)
        {
            return new PlannedMove(
                UseHold: false,
                TargetX: state.ActivePiece.X,
                TargetRotation: state.ActivePiece.Rotation,
                Score: double.NegativeInfinity,
                PieceKind: state.ActivePiece.Kind,
                LinesCleared: 0);
        }

        if (Difficulty.MistakeRate > 0 && ranked.Count > 1 && _random.NextDouble() < Difficulty.MistakeRate)
        {
            var mistakePool = Math.Min(ranked.Count, Math.Max(2, ranked.Count / 3));
            var index = 1 + _random.Next(mistakePool - 1);
            return ranked[index];
        }

        return ranked[0];
    }
}

public static class AiCommandSequenceBuilder
{
    public static IReadOnlyList<MoveCommand> BuildCommands(
        GameState state,
        PlannedMove move,
        AiDifficultyPreset difficulty)
    {
        var commands = new List<MoveCommand>();
        var simulated = state.Clone();

        if (move.UseHold)
        {
            commands.Add(MoveCommand.Hold);
            if (!simulated.ApplyCommand(MoveCommand.Hold))
            {
                return BuildFallbackCommands(simulated, difficulty);
            }
        }

        foreach (var rotationCommand in GetRotationCommands(simulated.ActivePiece.Rotation, move.TargetRotation))
        {
            commands.Add(rotationCommand);
            simulated.ApplyCommand(rotationCommand);
        }

        var horizontalDelta = move.TargetX - simulated.ActivePiece.X;
        var horizontalCommand = horizontalDelta < 0 ? MoveCommand.Left : MoveCommand.Right;
        for (var i = 0; i < Math.Abs(horizontalDelta); i++)
        {
            commands.Add(horizontalCommand);
            simulated.ApplyCommand(horizontalCommand);
        }

        var dropDistance = simulated.GetDropDistance(simulated.ActivePiece);
        if (difficulty.UseHardDrop)
        {
            var softDrops = Math.Min(dropDistance, difficulty.SoftDropsBeforeHardDrop);
            for (var i = 0; i < softDrops; i++)
            {
                commands.Add(MoveCommand.SoftDrop);
                simulated.ApplyCommand(MoveCommand.SoftDrop);
            }

            commands.Add(MoveCommand.HardDrop);
        }
        else
        {
            for (var i = 0; i <= dropDistance; i++)
            {
                commands.Add(MoveCommand.SoftDrop);
            }
        }

        return commands;
    }

    private static IReadOnlyList<MoveCommand> BuildFallbackCommands(GameState state, AiDifficultyPreset difficulty)
    {
        if (difficulty.UseHardDrop)
        {
            return new[] { MoveCommand.HardDrop };
        }

        var dropDistance = state.GetDropDistance(state.ActivePiece);
        return Enumerable.Repeat(MoveCommand.SoftDrop, dropDistance + 1).ToArray();
    }

    private static IReadOnlyList<MoveCommand> GetRotationCommands(Rotation from, Rotation to)
    {
        var clockwiseSteps = ((int)to - (int)from + 4) % 4;
        return clockwiseSteps switch
        {
            0 => Array.Empty<MoveCommand>(),
            1 => new[] { MoveCommand.RotateClockwise },
            2 => new[] { MoveCommand.RotateClockwise, MoveCommand.RotateClockwise },
            3 => new[] { MoveCommand.RotateCounterClockwise },
            _ => Array.Empty<MoveCommand>()
        };
    }
}
