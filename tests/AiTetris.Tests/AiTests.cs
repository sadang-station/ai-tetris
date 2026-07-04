using AiTetris.Ai;
using AiTetris.Game;

namespace AiTetris.Tests;

public sealed class AiTests
{
    [Fact]
    public void AiAlwaysReturnsLegalMoveOnEmptyBoard()
    {
        var state = GameState.Create(42);
        var ai = new NeuralNetworkAiPlayer(AiDifficultyPreset.Advanced, 42);

        var move = ai.ChooseMove(state);

        Assert.True(move.IsLegalFor(state));
    }

    [Fact]
    public void AiDoesNotMutateOriginalStateWhenPlanning()
    {
        var state = GameState.Create(42);
        var fingerprint = state.Board.Fingerprint();
        var active = state.ActivePiece;
        var score = state.Score;
        var lines = state.Lines;
        var ai = new NeuralNetworkAiPlayer(AiDifficultyPreset.Advanced, 42);

        _ = ai.ChooseMove(state);

        Assert.Equal(fingerprint, state.Board.Fingerprint());
        Assert.Equal(active, state.ActivePiece);
        Assert.Equal(score, state.Score);
        Assert.Equal(lines, state.Lines);
    }

    [Fact]
    public void AiReturnsLegalMoveOnDangerousBoard()
    {
        var state = GameState.Create(99);
        for (var y = Board.BufferHeight + 4; y < Board.TotalHeight; y++)
        {
            for (var x = 0; x < Board.Width; x++)
            {
                if (x != 4)
                {
                    state.Board.SetCell(x, y, PieceKind.Z);
                }
            }
        }

        var ai = new NeuralNetworkAiPlayer(AiDifficultyPreset.Intermediate, 99);
        var move = ai.ChooseMove(state);

        Assert.True(move.IsLegalFor(state));
    }

    [Fact]
    public void PlannerPrefersAvailableLineClearCandidate()
    {
        var state = GameState.Create(7);
        state.SetActivePieceForTesting(new ActivePiece(PieceKind.I, 3, 1, Rotation.North));

        var bottom = Board.TotalHeight - 1;
        for (var x = 0; x < Board.Width; x++)
        {
            if (x is < 3 or > 6)
            {
                state.Board.SetCell(x, bottom, PieceKind.O);
            }
        }

        var planner = new AiMovePlanner();
        var ranked = planner.RankMoves(state, AiDifficultyPreset.Advanced);

        Assert.NotEmpty(ranked);
        Assert.True(ranked[0].LinesCleared > 0);
    }

    [Fact]
    public void BeginnerAiUsesVisibleSoftDropCommandsInsteadOfInstantHardDrop()
    {
        var state = GameState.Create(42);
        var ai = new NeuralNetworkAiPlayer(AiDifficultyPreset.Beginner, 42);

        var move = ai.ChooseMove(state);
        var commands = AiCommandSequenceBuilder.BuildCommands(state, move, AiDifficultyPreset.Beginner);

        Assert.NotEmpty(commands);
        Assert.DoesNotContain(MoveCommand.HardDrop, commands);
        Assert.Contains(MoveCommand.SoftDrop, commands);
    }

    [Fact]
    public void AdvancedAiStillMovesBeforeHardDrop()
    {
        var state = GameState.Create(42);
        var ai = new NeuralNetworkAiPlayer(AiDifficultyPreset.Advanced, 42);

        var move = ai.ChooseMove(state);
        var commands = AiCommandSequenceBuilder.BuildCommands(state, move, AiDifficultyPreset.Advanced);

        Assert.NotEmpty(commands);
        Assert.Equal(MoveCommand.HardDrop, commands[^1]);
        Assert.True(commands.Count > 1);
    }
}
