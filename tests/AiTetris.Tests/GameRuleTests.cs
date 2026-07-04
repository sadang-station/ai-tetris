using AiTetris.Game;

namespace AiTetris.Tests;

public sealed class GameRuleTests
{
    [Fact]
    public void BoardRejectsPieceOutsideBounds()
    {
        var board = new Board();
        var piece = new ActivePiece(PieceKind.I, -2, 4, Rotation.North);

        Assert.False(board.CanPlace(piece));
    }

    [Fact]
    public void ClearFullLinesCompactsRows()
    {
        var board = new Board();
        var bottom = Board.TotalHeight - 1;
        for (var x = 0; x < Board.Width; x++)
        {
            board.SetCell(x, bottom, PieceKind.I);
        }

        board.SetCell(0, bottom - 1, PieceKind.T);

        var cleared = board.ClearFullLines();

        Assert.Equal(1, cleared);
        Assert.Null(board.GetCell(0, bottom - 1));
        Assert.Equal(PieceKind.T, board.GetCell(0, bottom));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 100)]
    [InlineData(2, 300)]
    [InlineData(3, 500)]
    [InlineData(4, 800)]
    public void LineClearScoringMatchesPlan(int lines, int expectedScore)
    {
        Assert.Equal(expectedScore, GameRules.GetLineClearScore(lines));
    }

    [Fact]
    public void HoldCanOnlyBeUsedOncePerFallingPiece()
    {
        var state = GameState.Create(1234);

        Assert.True(state.ApplyCommand(MoveCommand.Hold));
        Assert.True(state.HoldUsed);
        Assert.False(state.ApplyCommand(MoveCommand.Hold));
    }

    [Fact]
    public void SevenBagSequenceIsSeedReproducible()
    {
        var first = new PieceBag(9876);
        var second = new PieceBag(9876);

        var firstPieces = Enumerable.Range(0, 21).Select(_ => first.Next()).ToArray();
        var secondPieces = Enumerable.Range(0, 21).Select(_ => second.Next()).ToArray();

        Assert.Equal(firstPieces, secondPieces);
        foreach (var bag in firstPieces.Chunk(7))
        {
            Assert.Equal(7, bag.Distinct().Count());
        }
    }

    [Fact]
    public void SrsKicksIPieceAwayFromLeftWall()
    {
        var board = new Board();
        var piece = new ActivePiece(PieceKind.I, -1, 5, Rotation.East);

        Assert.True(board.CanPlace(piece));
        Assert.True(GameRules.TryRotate(board, piece, clockwise: false, out var rotated));
        Assert.Equal(Rotation.North, rotated.Rotation);
        Assert.True(board.CanPlace(rotated));
    }
}

