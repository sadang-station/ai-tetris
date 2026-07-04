using System.Text;
using AiTetris.Ai;
using AiTetris.Game;

namespace AiTetris.Rendering;

public interface IRenderer
{
    void Render(GameState player, GameState ai, AiDifficultyPreset difficulty, RenderStatus status);
}

public readonly record struct RenderStatus(bool IsPaused, bool IsMatchOver, string Message, TimeSpan Elapsed);

public enum RenderStyle
{
    UnicodeColor,
    UnicodeSafe,
    UnicodeMono
}

public sealed class ConsoleRenderer : IRenderer
{
    public const int MinWidth = 83;
    public const int MinHeight = 24;
    private const int CompactBoardPanelWidth = 24;
    private const int FullBoardPanelWidth = 24;
    private const int MinSidePanelWidth = 28;
    private const int MaxSidePanelWidth = 56;

    private const string Reset = "\u001b[0m";
    private const string StartSynchronizedUpdate = "\u001b[?2026h";
    private const string EndSynchronizedUpdate = "\u001b[?2026l";
    private const string ClearViewport = "\u001b[H\u001b[0J";
    private const string TetroPastelCyan = "\u001b[38;2;0;199;198m";
    private const string TetroPastelYellow = "\u001b[38;2;239;175;50m";
    private const string TetroPastelMagenta = "\u001b[38;2;164;130;255m";
    private const string TetroPastelGreen = "\u001b[38;2;108;189;70m";
    private const string TetroPastelRed = "\u001b[38;2;255;87;126m";
    private const string TetroPastelBlue = "\u001b[38;2;49;159;253m";
    private const string TetroPastelOrange = "\u001b[38;2;245;122;62m";
    private const string TetroPastelGray = "\u001b[38;2;143;143;143m";
    private const string GhostGray = "\u001b[38;2;77;84;96m";

    private readonly RenderStyle _style;

    public ConsoleRenderer()
        : this(ResolveDefaultStyle())
    {
    }

    public ConsoleRenderer(RenderStyle style)
    {
        _style = style;
    }

    public void Render(GameState player, GameState ai, AiDifficultyPreset difficulty, RenderStatus status)
    {
        var width = GetWindowWidth();
        var height = GetWindowHeight();
        var frame = BuildFrame(player, ai, difficulty, status, width, height, _style);

        try
        {
            Console.Write(StartSynchronizedUpdate);
            Console.Write(ClearViewport);
            Console.Write(frame);
            Console.Write(EndSynchronizedUpdate);
        }
        catch (IOException)
        {
            return;
        }
    }

    public static string BuildFrame(
        GameState player,
        GameState ai,
        AiDifficultyPreset difficulty,
        RenderStatus status,
        int terminalWidth,
        int terminalHeight,
        RenderStyle style = RenderStyle.UnicodeColor)
    {
        var layout = ResolveLayout(terminalWidth, terminalHeight);
        if (layout is null)
        {
            return BuildTooSmallFrame(terminalWidth, terminalHeight);
        }

        var metrics = layout.Value;
        var playerBlock = BuildBoardBlock("PLAYER", player, style, metrics);
        var aiBlock = BuildBoardBlock("AI", ai, style, metrics);
        var side = BuildSidePanel(player, ai, difficulty, status, style, metrics);
        var maxLines = Math.Max(Math.Max(playerBlock.Count, aiBlock.Count), side.Count);
        var builder = new StringBuilder();

        AppendFrameLine(builder, "╔" + new string('═', metrics.OuterInnerWidth) + "╗");
        AppendFrameLine(builder, "║ " + CenterVisible("C#으로 AI 테트리스를 만들어 보자꾸나", metrics.OuterInnerWidth - 2) + " ║");
        AppendFrameLine(builder, "╠" + new string('═', metrics.OuterInnerWidth) + "╣");

        for (var i = 0; i < maxLines; i++)
        {
            var left = i < playerBlock.Count ? playerBlock[i] : string.Empty;
            var middle = i < aiBlock.Count ? aiBlock[i] : string.Empty;
            var right = i < side.Count ? side[i] : string.Empty;
            builder.Append('║');
            builder.Append(PadVisible(left, metrics.BoardColumnWidth));
            builder.Append(PadVisible(middle, metrics.BoardColumnWidth));
            builder.Append(PadVisible(right, metrics.SidePanelWidth));
            AppendFrameLine(builder, "║");
        }

        builder.Append("╚" + new string('═', metrics.OuterInnerWidth) + "╝");
        return builder.ToString();
    }

    private static LayoutMetrics? ResolveLayout(int terminalWidth, int terminalHeight)
    {
        if (terminalWidth < MinWidth || terminalHeight < MinHeight)
        {
            return null;
        }

        var scale = terminalHeight >= 32 && terminalWidth >= MinWidth
            ? BoardRenderScale.Full
            : BoardRenderScale.Compact;
        var boardPanelWidth = scale == BoardRenderScale.Full ? FullBoardPanelWidth : CompactBoardPanelWidth;
        var boardColumnsWidth = (boardPanelWidth + 2) * 2;
        var availableSideWidth = terminalWidth - 3 - boardColumnsWidth;

        if (availableSideWidth < MinSidePanelWidth)
        {
            scale = BoardRenderScale.Compact;
            boardPanelWidth = CompactBoardPanelWidth;
            boardColumnsWidth = (boardPanelWidth + 2) * 2;
            availableSideWidth = terminalWidth - 3 - boardColumnsWidth;
        }

        if (availableSideWidth < MinSidePanelWidth)
        {
            return null;
        }

        var sidePanelWidth = Math.Min(MaxSidePanelWidth, availableSideWidth);
        return new LayoutMetrics(boardPanelWidth, sidePanelWidth, scale);
    }

    private static string BuildTooSmallFrame(int terminalWidth, int terminalHeight)
    {
        var builder = new StringBuilder();
        const int width = 48;
        AppendFrameLine(builder, BorderWithTitle("터미널 크기", width, '╭', '╮', '─'));
        AppendFrameLine(builder, BoxLine("화면이 너무 작습니다.", width));
        AppendFrameLine(builder, BoxLine($"현재 {terminalWidth}×{terminalHeight}", width));
        AppendFrameLine(builder, BoxLine($"필요 {MinWidth}×{MinHeight}", width));
        AppendFrameLine(builder, BoxLine("창 크기를 키우면 게임이 계속됩니다.", width));
        builder.Append("╰" + new string('─', width - 2) + "╯");
        return builder.ToString();
    }

    private static void AppendFrameLine(StringBuilder builder, string value)
    {
        builder.Append(value);
        builder.Append('\n');
    }

    private static List<string> BuildBoardBlock(
        string label,
        GameState state,
        RenderStyle style,
        LayoutMetrics metrics)
    {
        var lines = new List<string>
        {
            BorderWithTitle(label, metrics.BoardPanelWidth, '╭', '╮', '─'),
            BoxLine($"점수 {state.Score,7}", metrics.BoardPanelWidth),
            BoxLine($"라인 {state.Lines,7}", metrics.BoardPanelWidth),
            SectionBorder("홀드", metrics.BoardPanelWidth)
        };

        foreach (var holdLine in BuildHoldPreviewRows(state.HoldPiece, style))
        {
            lines.Add(BoxLine(CenterVisible(holdLine, metrics.BoardPanelWidth - 4), metrics.BoardPanelWidth));
        }

        lines.Add("├" + new string('─', metrics.BoardPanelWidth - 2) + "┤");

        var activeCells = new Dictionary<(int X, int Y), PieceKind>();
        var ghostCells = new HashSet<(int X, int Y)>();

        if (!state.IsGameOver)
        {
            foreach (var cell in state.GetGhostPiece().Cells)
            {
                if (cell.Y >= Board.BufferHeight && cell.Y < Board.TotalHeight)
                {
                    ghostCells.Add((cell.X, cell.Y));
                }
            }

            foreach (var cell in state.ActivePiece.Cells)
            {
                if (cell.Y >= Board.BufferHeight && cell.Y < Board.TotalHeight)
                {
                    activeCells[(cell.X, cell.Y)] = state.ActivePiece.Kind;
                }
            }
        }

        if (metrics.Scale == BoardRenderScale.Full)
        {
            for (var visibleY = 0; visibleY < Board.VisibleHeight; visibleY++)
            {
                var boardY = visibleY + Board.BufferHeight;
                var row = new StringBuilder();
                for (var x = 0; x < Board.Width; x++)
                {
                    var cell = GetCellVisual(state, activeCells, ghostCells, x, boardY);
                    row.Append(FullBlockToken(cell, style));
                }

                lines.Add(BoxLine(CenterVisible(row.ToString(), metrics.BoardPanelWidth - 4), metrics.BoardPanelWidth));
            }
        }
        else
        {
            for (var visibleY = 0; visibleY < Board.VisibleHeight; visibleY += 2)
            {
                var topY = visibleY + Board.BufferHeight;
                var bottomY = topY + 1;
                var cells = new StringBuilder();
                for (var x = 0; x < Board.Width; x++)
                {
                    var top = GetCellVisual(state, activeCells, ghostCells, x, topY);
                    var bottom = GetCellVisual(state, activeCells, ghostCells, x, bottomY);
                    cells.Append(HalfBlockToken(top, bottom, style));
                }

                lines.Add(BoxLine(CenterVisible(cells.ToString(), metrics.BoardPanelWidth - 4), metrics.BoardPanelWidth));
            }
        }

        lines.Add("╰" + new string('─', metrics.BoardPanelWidth - 2) + "╯");
        return lines;
    }

    private static List<string> BuildSidePanel(
        GameState player,
        GameState ai,
        AiDifficultyPreset difficulty,
        RenderStatus status,
        RenderStyle style,
        LayoutMetrics metrics)
    {
        var message = status.Message;
        if (status.IsPaused)
        {
            message = "Paused";
        }

        var lines = new List<string>
        {
            BorderWithTitle("상태", metrics.SidePanelWidth, '╭', '╮', '─'),
            BoxLine($"AI        {difficulty.DisplayName}", metrics.SidePanelWidth),
            BoxLine($"시간      {status.Elapsed:mm\\:ss}", metrics.SidePanelWidth),
            BoxLine($"상태      {message}", metrics.SidePanelWidth),
            SectionBorder("다음", metrics.SidePanelWidth),
        };

        var nextPieces = player.NextPieces(3);
        for (var index = 0; index < nextPieces.Count; index++)
        {
            lines.Add(BoxLine("  " + BuildPiecePreview(nextPieces[index], style), metrics.SidePanelWidth));

            if (index < nextPieces.Count - 1)
            {
                lines.Add(BoxLine(string.Empty, metrics.SidePanelWidth));
            }
        }

        lines.Add(SectionBorder("조작", metrics.SidePanelWidth));
        lines.Add(BoxLine("←/→ 이동      ↓ 소프트", metrics.SidePanelWidth));
        lines.Add(BoxLine("↑ 회전        Z 반시계", metrics.SidePanelWidth));
        lines.Add(BoxLine("Space 하드    C 보관", metrics.SidePanelWidth));
        lines.Add(BoxLine("P 일시정지    Esc 종료", metrics.SidePanelWidth));
        lines.Add(SectionBorder("판정", metrics.SidePanelWidth));
        lines.Add(BoxLine($"플레이어  {PlayerState(player)}", metrics.SidePanelWidth));
        lines.Add(BoxLine($"AI        {PlayerState(ai)}", metrics.SidePanelWidth));
        if (status.IsMatchOver)
        {
            lines.Add(BoxLine("Esc로 종료", metrics.SidePanelWidth));
        }

        lines.Add("╰" + new string('─', metrics.SidePanelWidth - 2) + "╯");
        return lines;
    }

    private static string BuildPiecePreview(PieceKind piece, RenderStyle style)
    {
        var symbol = piece switch
        {
            PieceKind.I => "▄▄▄▄",
            PieceKind.O => "██",
            PieceKind.T => "▄█▄",
            PieceKind.S => "▄█▀",
            PieceKind.Z => "▀█▄",
            PieceKind.J => "█▄▄",
            PieceKind.L => "▄▄█",
            _ => "████"
        };

        return UsesColor(style) ? Foreground(piece) + symbol + Reset : symbol;
    }

    private static IReadOnlyList<string> BuildHoldPreviewRows(PieceKind? piece, RenderStyle style)
    {
        if (piece is { } held)
        {
            return BuildMiniPieceRows(held, style);
        }

        return ["        ", "        "];
    }

    private static IReadOnlyList<string> BuildMiniPieceRows(PieceKind piece, RenderStyle style)
    {
        var pattern = piece switch
        {
            PieceKind.I => new[] { "....", "####" },
            PieceKind.O => new[] { ".##.", ".##." },
            PieceKind.T => new[] { ".#..", "###." },
            PieceKind.S => new[] { ".##.", "##.." },
            PieceKind.Z => new[] { "##..", ".##." },
            PieceKind.J => new[] { "#...", "###." },
            PieceKind.L => new[] { "..#.", "###." },
            _ => new[] { "####", "...." }
        };

        var filled = MiniBlockToken(piece, style);
        return pattern
            .Select(row => string.Concat(row.Select(cell => cell == '#' ? filled : "  ")))
            .ToArray();
    }

    private static string MiniBlockToken(PieceKind piece, RenderStyle style)
    {
        return UsesColor(style) ? Foreground(piece) + "██" + Reset : "██";
    }

    private static string FullBlockToken(CellVisual? cell, RenderStyle style)
    {
        if (cell is null)
        {
            return "  ";
        }

        if (cell.Value.Role == CellRole.Ghost)
        {
            return UsesColor(style) ? GhostGray + GhostTile(style) + Reset : "░░";
        }

        var texture = cell.Value.Role == CellRole.Active ? "▓▓" : "██";
        return UsesColor(style)
            ? Foreground(cell.Value.Kind!.Value) + texture + Reset
            : texture;
    }

    private static string GhostHalfToken(bool top, bool bottom, RenderStyle style)
    {
        var token = (top, bottom) switch
        {
            (true, true) => "█",
            (true, false) => "▀",
            (false, true) => "▄",
            _ => " "
        };

        return UsesColor(style) ? GhostGray + token + Reset : token;
    }

    private static CellVisual? GetCellVisual(
        GameState state,
        IReadOnlyDictionary<(int X, int Y), PieceKind> activeCells,
        ISet<(int X, int Y)> ghostCells,
        int x,
        int y)
    {
        if (activeCells.TryGetValue((x, y), out var active))
        {
            return new CellVisual(active, CellRole.Active);
        }

        if (state.Board.GetCell(x, y) is { } locked)
        {
            return new CellVisual(locked, CellRole.Locked);
        }

        if (ghostCells.Contains((x, y)))
        {
            return new CellVisual(null, CellRole.Ghost);
        }

        return null;
    }

    private static string HalfBlockToken(CellVisual? top, CellVisual? bottom, RenderStyle style)
    {
        if (top is null && bottom is null)
        {
            return " ";
        }

        if ((top?.Role == CellRole.Ghost || top is null) && (bottom?.Role == CellRole.Ghost || bottom is null))
        {
            return GhostHalfToken(top?.Role == CellRole.Ghost, bottom?.Role == CellRole.Ghost, style);
        }

        if (top is { Role: not CellRole.Ghost } topCell && bottom is { Role: not CellRole.Ghost } bottomCell)
        {
            if (!UsesColor(style))
            {
                return "█";
            }

            if (topCell.Kind == bottomCell.Kind)
            {
                return Foreground(topCell.Kind!.Value) + "█" + Reset;
            }

            if (style == RenderStyle.UnicodeSafe)
            {
                return Foreground(topCell.Kind!.Value) + "█" + Reset;
            }

            return Foreground(topCell.Kind!.Value) + Background(bottomCell.Kind!.Value) + "▀" + Reset;
        }

        if (top is { Role: not CellRole.Ghost } onlyTop)
        {
            return UsesColor(style) ? Foreground(onlyTop.Kind!.Value) + "▀" + Reset : "▀";
        }

        if (bottom is { Role: not CellRole.Ghost } onlyBottom)
        {
            return UsesColor(style) ? Foreground(onlyBottom.Kind!.Value) + "▄" + Reset : "▄";
        }

        return GhostHalfToken(top?.Role == CellRole.Ghost, bottom?.Role == CellRole.Ghost, style);
    }

    private static string Foreground(PieceKind piece) => piece switch
    {
        PieceKind.I => TetroPastelCyan,
        PieceKind.O => TetroPastelYellow,
        PieceKind.T => TetroPastelMagenta,
        PieceKind.S => TetroPastelGreen,
        PieceKind.Z => TetroPastelRed,
        PieceKind.J => TetroPastelBlue,
        PieceKind.L => TetroPastelOrange,
        _ => TetroPastelGray
    };

    private static string Background(PieceKind piece) => piece switch
    {
        PieceKind.I => "\u001b[48;2;0;199;198m",
        PieceKind.O => "\u001b[48;2;239;175;50m",
        PieceKind.T => "\u001b[48;2;164;130;255m",
        PieceKind.S => "\u001b[48;2;108;189;70m",
        PieceKind.Z => "\u001b[48;2;255;87;126m",
        PieceKind.J => "\u001b[48;2;49;159;253m",
        PieceKind.L => "\u001b[48;2;245;122;62m",
        _ => "\u001b[48;2;143;143;143m"
    };

    private static RenderStyle ResolveDefaultStyle()
    {
        var requestedStyle = Environment.GetEnvironmentVariable("AI_TETRIS_RENDER_STYLE");
        if (requestedStyle is not null)
        {
            return requestedStyle.Trim().ToLowerInvariant() switch
            {
                "mono" or "monochrome" or "ascii" => RenderStyle.UnicodeMono,
                "safe" or "compatible" or "compat" => RenderStyle.UnicodeSafe,
                "color" or "unicode" => RenderStyle.UnicodeColor,
                _ => RenderStyle.UnicodeSafe
            };
        }

        if (Environment.GetEnvironmentVariable("NO_COLOR") is not null)
        {
            return RenderStyle.UnicodeMono;
        }

        if (IsAppleTerminal())
        {
            return RenderStyle.UnicodeSafe;
        }

        return RenderStyle.UnicodeColor;
    }

    private static bool UsesColor(RenderStyle style) => style is RenderStyle.UnicodeColor or RenderStyle.UnicodeSafe;

    private static string GhostTile(RenderStyle style) => style == RenderStyle.UnicodeSafe ? "██" : "░░";

    private static bool IsAppleTerminal()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable("TERM_PROGRAM"),
            "Apple_Terminal",
            StringComparison.OrdinalIgnoreCase);
    }

    private static string PlayerState(GameState state) => state.IsGameOver ? "종료" : "진행";

    private static string BorderWithTitle(string title, int width, char left, char right, char horizontal)
    {
        var innerWidth = width - 2;
        var titleSegment = $" {title} ";
        var remaining = Math.Max(0, innerWidth - GetVisibleLength(titleSegment));
        var leftFill = remaining / 2;
        var rightFill = remaining - leftFill;
        return left + new string(horizontal, leftFill) + titleSegment + new string(horizontal, rightFill) + right;
    }

    private static string SectionBorder(string title, int width)
    {
        var innerWidth = width - 2;
        var titleSegment = $" {title} ";
        var remaining = Math.Max(0, innerWidth - GetVisibleLength(titleSegment));
        var leftFill = remaining / 2;
        var rightFill = remaining - leftFill;
        return "├" + new string('─', leftFill) + titleSegment + new string('─', rightFill) + "┤";
    }

    private static string BoxLine(string value, int width)
    {
        return "│ " + PadVisible(value, width - 4) + " │";
    }

    private static string PadVisible(string value, int width)
    {
        var visibleLength = GetVisibleLength(value);
        if (visibleLength >= width)
        {
            return value;
        }

        return value + new string(' ', width - visibleLength);
    }

    private static string CenterVisible(string value, int width)
    {
        var visibleLength = GetVisibleLength(value);
        if (visibleLength >= width)
        {
            return value;
        }

        var remaining = width - visibleLength;
        var left = remaining / 2;
        var right = remaining - left;
        return new string(' ', left) + value + new string(' ', right);
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

    private static int GetWindowWidth()
    {
        try
        {
            return Console.WindowWidth;
        }
        catch (IOException)
        {
            return MinWidth;
        }
    }

    private static int GetWindowHeight()
    {
        try
        {
            return Console.WindowHeight;
        }
        catch (IOException)
        {
            return MinHeight;
        }
    }

    private enum BoardRenderScale
    {
        Compact,
        Full
    }

    private enum CellRole
    {
        Active,
        Locked,
        Ghost
    }

    private readonly record struct LayoutMetrics(int BoardPanelWidth, int SidePanelWidth, BoardRenderScale Scale)
    {
        public int BoardColumnWidth => BoardPanelWidth + 2;

        public int OuterInnerWidth => (BoardColumnWidth * 2) + SidePanelWidth;
    }

    private readonly record struct CellVisual(PieceKind? Kind, CellRole Role);
}
