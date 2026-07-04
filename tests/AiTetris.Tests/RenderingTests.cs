using AiTetris.Ai;
using AiTetris.Game;
using AiTetris.Rendering;

namespace AiTetris.Tests;

public sealed class RenderingTests
{
    [Fact]
    public void RendererShowsSmallTerminalWarning()
    {
        var state = GameState.Create(1);

        var frame = ConsoleRenderer.BuildFrame(
            state,
            state.Clone(),
            AiDifficultyPreset.Beginner,
            new RenderStatus(false, false, "Playing", TimeSpan.Zero),
            terminalWidth: 40,
            terminalHeight: 10);

        Assert.Contains("터미널 크기", frame);
        Assert.Contains("화면이 너무 작습니다", frame);
        Assert.Contains("필요", frame);
        Assert.Contains("╭", frame);
    }

    [Fact]
    public void RendererIncludesStatusAndBothBoardsWithoutSeed()
    {
        var player = GameState.Create(123);
        var ai = GameState.Create(123);
        player.Board.SetCell(0, Board.TotalHeight - 1, PieceKind.T);

        var frame = ConsoleRenderer.BuildFrame(
            player,
            ai,
            AiDifficultyPreset.Intermediate,
            new RenderStatus(false, false, "Playing", TimeSpan.FromSeconds(3)),
            terminalWidth: ConsoleRenderer.MinWidth,
            terminalHeight: ConsoleRenderer.MinHeight);

        Assert.Contains("C#으로 AI 테트리스를 만들어 보자꾸나", frame);
        Assert.Contains("PLAYER", frame);
        Assert.Contains("AI", frame);
        Assert.DoesNotContain("시드", frame);
        Assert.DoesNotContain("123", frame);
        Assert.Contains("상태      Playing", frame);
        Assert.Contains("다음", frame);
        Assert.Contains("조작", frame);
        Assert.Contains("╔", frame);
        Assert.Contains("╭", frame);
        Assert.Contains("▄", frame);
        Assert.DoesNotContain("====", frame);
    }

    [Fact]
    public void RendererShowsFrozenGameOverState()
    {
        var player = GameState.Create(123);
        var ai = GameState.Create(123);

        while (!player.IsGameOver)
        {
            player.HardDrop();
        }

        var frame = ConsoleRenderer.BuildFrame(
            player,
            ai,
            AiDifficultyPreset.Beginner,
            new RenderStatus(false, true, "AI wins by survival", TimeSpan.FromSeconds(30)),
            terminalWidth: ConsoleRenderer.MinWidth,
            terminalHeight: ConsoleRenderer.MinHeight);

        Assert.Contains("종료", frame);
        Assert.Contains("Esc로 종료", frame);
    }

    [Fact]
    public void RendererKeepsEveryMainFrameLineAligned()
    {
        var player = GameState.Create(123);
        var ai = GameState.Create(123);
        player.ApplyCommand(MoveCommand.Hold);

        var frame = ConsoleRenderer.BuildFrame(
            player,
            ai,
            AiDifficultyPreset.Advanced,
            new RenderStatus(false, false, "Playing", TimeSpan.FromSeconds(12)),
            terminalWidth: ConsoleRenderer.MinWidth,
            terminalHeight: ConsoleRenderer.MinHeight);

        var widths = frame
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(GetVisibleLength)
            .Distinct()
            .ToArray();

        Assert.Single(widths);
        Assert.DoesNotContain('\r', frame);
        Assert.False(frame.EndsWith('\n'));
        Assert.True(frame.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length <= ConsoleRenderer.MinHeight);
    }

    [Fact]
    public void RendererExpandsBoardWhenTerminalIsLargeEnough()
    {
        var player = GameState.Create(123);
        var ai = GameState.Create(123);
        player.SetActivePieceForTesting(new ActivePiece(PieceKind.T, 3, Board.BufferHeight, Rotation.North));

        var compact = ConsoleRenderer.BuildFrame(
            player,
            ai,
            AiDifficultyPreset.Advanced,
            new RenderStatus(false, false, "Playing", TimeSpan.FromSeconds(12)),
            terminalWidth: ConsoleRenderer.MinWidth,
            terminalHeight: ConsoleRenderer.MinHeight);

        var expanded = ConsoleRenderer.BuildFrame(
            player,
            ai,
            AiDifficultyPreset.Advanced,
            new RenderStatus(false, false, "Playing", TimeSpan.FromSeconds(12)),
            terminalWidth: 120,
            terminalHeight: 40);

        Assert.True(expanded.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length >
            compact.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Contains("▓▓", expanded);
    }

    [Fact]
    public void RendererDoesNotShowSeedOrNextPieceLetters()
    {
        var player = GameState.Create(123);
        var ai = GameState.Create(123);

        var frame = ConsoleRenderer.BuildFrame(
            player,
            ai,
            AiDifficultyPreset.Intermediate,
            new RenderStatus(false, false, "Playing", TimeSpan.FromSeconds(3)),
            terminalWidth: ConsoleRenderer.MinWidth,
            terminalHeight: ConsoleRenderer.MinHeight);

        Assert.DoesNotContain("시드", frame);
        Assert.DoesNotContain("Seed", frame);
        foreach (var piece in player.NextPieces(3))
        {
            Assert.DoesNotContain($"{piece}  ", frame);
        }
    }

    [Fact]
    public void RendererSeparatesNextPiecePreviewsWithoutStretchingSection()
    {
        var player = GameState.Create(123);
        var ai = GameState.Create(123);

        var frame = ConsoleRenderer.BuildFrame(
            player,
            ai,
            AiDifficultyPreset.Intermediate,
            new RenderStatus(false, false, "Playing", TimeSpan.FromSeconds(3)),
            terminalWidth: ConsoleRenderer.MinWidth,
            terminalHeight: ConsoleRenderer.MinHeight);

        var lines = frame.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var nextIndex = Array.FindIndex(lines, line => line.Contains("다음", StringComparison.Ordinal));

        Assert.True(nextIndex >= 0);
        Assert.Contains(lines.Skip(nextIndex + 1).Take(8), IsMostlyEmptyPanelLine);
        Assert.DoesNotContain(lines.Skip(nextIndex + 1).Take(8), line => StripAnsi(line).Contains("███", StringComparison.Ordinal));
    }

    [Fact]
    public void RendererUsesCompactUnbrokenHoldPreview()
    {
        var player = GameState.Create(123);
        var ai = GameState.Create(123);
        player.SetActivePieceForTesting(new ActivePiece(PieceKind.S, 3, Board.BufferHeight, Rotation.North));
        player.ApplyCommand(MoveCommand.Hold);

        var frame = ConsoleRenderer.BuildFrame(
            player,
            ai,
            AiDifficultyPreset.Intermediate,
            new RenderStatus(false, false, "Playing", TimeSpan.FromSeconds(3)),
            terminalWidth: ConsoleRenderer.MinWidth,
            terminalHeight: ConsoleRenderer.MinHeight);

        var lines = frame.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var holdIndex = Array.FindIndex(lines, line => line.Contains("홀드", StringComparison.Ordinal));

        Assert.True(holdIndex >= 0);

        var holdRows = lines
            .Skip(holdIndex + 1)
            .Take(2)
            .Select(StripAnsi)
            .Select(FirstPanelSegment)
            .ToArray();

        Assert.Contains(holdRows, line => line.Contains("████", StringComparison.Ordinal));
        Assert.DoesNotContain(holdRows, line => line.Contains('▄') || line.Contains('▀'));
    }

    [Fact]
    public void RendererUsesTetroPastelPieceColors()
    {
        var player = GameState.Create(123);
        var ai = GameState.Create(123);
        player.SetActivePieceForTesting(new ActivePiece(PieceKind.I, 3, Board.BufferHeight, Rotation.North));
        player.Board.SetCell(0, Board.TotalHeight - 1, PieceKind.O);
        player.Board.SetCell(1, Board.TotalHeight - 1, PieceKind.T);

        var frame = ConsoleRenderer.BuildFrame(
            player,
            ai,
            AiDifficultyPreset.Intermediate,
            new RenderStatus(false, false, "Playing", TimeSpan.FromSeconds(3)),
            terminalWidth: 120,
            terminalHeight: 40);

        Assert.Contains("\u001b[38;2;0;199;198m", frame);
        Assert.Contains("\u001b[38;2;239;175;50m", frame);
        Assert.Contains("\u001b[38;2;164;130;255m", frame);
    }

    [Fact]
    public void RendererUsesTetroSmallSymbolsForJAndLPreviews()
    {
        var seed = Enumerable
            .Range(0, 10_000)
            .First(value =>
            {
                var nextPieces = GameState.Create(value).NextPieces(3);
                return nextPieces.Contains(PieceKind.J) && nextPieces.Contains(PieceKind.L);
            });
        var player = GameState.Create(seed);
        var ai = GameState.Create(seed);

        var frame = ConsoleRenderer.BuildFrame(
            player,
            ai,
            AiDifficultyPreset.Intermediate,
            new RenderStatus(false, false, "Playing", TimeSpan.FromSeconds(3)),
            terminalWidth: ConsoleRenderer.MinWidth,
            terminalHeight: ConsoleRenderer.MinHeight);
        var plain = StripAnsi(frame);

        Assert.Contains("█▄▄", plain);
        Assert.Contains("▄▄█", plain);
    }

    [Fact]
    public void RendererSafeStyleAvoidsBackgroundColorMixing()
    {
        var player = GameState.Create(123);
        var ai = GameState.Create(123);
        player.Board.SetCell(0, Board.BufferHeight, PieceKind.I);
        player.Board.SetCell(0, Board.BufferHeight + 1, PieceKind.O);

        var frame = ConsoleRenderer.BuildFrame(
            player,
            ai,
            AiDifficultyPreset.Intermediate,
            new RenderStatus(false, false, "Playing", TimeSpan.FromSeconds(3)),
            terminalWidth: ConsoleRenderer.MinWidth,
            terminalHeight: ConsoleRenderer.MinHeight,
            style: RenderStyle.UnicodeSafe);

        Assert.Contains("\u001b[38;2;0;199;198m", frame);
        Assert.DoesNotContain("\u001b[48;2;", frame);
    }

    private static bool IsMostlyEmptyPanelLine(string line)
    {
        var plain = StripAnsi(line);
        if (!plain.EndsWith('║'))
        {
            return false;
        }

        var withoutOuterBorder = plain[..^1];
        var lastPanelBorder = withoutOuterBorder.LastIndexOf('│');
        if (lastPanelBorder <= 0)
        {
            return false;
        }

        var previousPanelBorder = withoutOuterBorder.LastIndexOf('│', lastPanelBorder - 1);
        if (previousPanelBorder < 0)
        {
            return false;
        }

        return withoutOuterBorder[(previousPanelBorder + 1)..lastPanelBorder].All(character => character == ' ');
    }

    private static string FirstPanelSegment(string line)
    {
        var start = line.IndexOf('│');
        if (start < 0)
        {
            return line;
        }

        var end = line.IndexOf('│', start + 1);
        return end < 0 ? line[start..] : line[start..(end + 1)];
    }

    private static string StripAnsi(string value)
    {
        var builder = new System.Text.StringBuilder();
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\u001b' && i + 1 < value.Length && value[i + 1] == '[')
            {
                i += 2;
                while (i < value.Length && value[i] != 'm')
                {
                    i++;
                }

                continue;
            }

            builder.Append(value[i]);
        }

        return builder.ToString();
    }

    private static int GetVisibleLength(string value)
    {
        var length = 0;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\u001b' && i + 1 < value.Length && value[i + 1] == '[')
            {
                i += 2;
                while (i < value.Length && value[i] != 'm')
                {
                    i++;
                }

                continue;
            }

            length += GetCellWidth(value[i]);
        }

        return length;
    }

    private static int GetCellWidth(char value)
    {
        return value is >= '\u1100' and <= '\u115F'
            or >= '\u2E80' and <= '\uA4CF'
            or >= '\uAC00' and <= '\uD7A3'
            or >= '\uF900' and <= '\uFAFF'
            or >= '\uFE10' and <= '\uFE19'
            or >= '\uFE30' and <= '\uFE6F'
            or >= '\uFF00' and <= '\uFF60'
            or >= '\uFFE0' and <= '\uFFE6'
            ? 2
            : 1;
    }
}
