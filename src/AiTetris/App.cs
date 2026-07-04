using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using AiTetris.Ai;
using AiTetris.Game;
using AiTetris.Input;
using AiTetris.Rendering;

namespace AiTetris;

public static class App
{
    private static readonly TimeSpan FrameTime = TimeSpan.FromMilliseconds(33);
    private static readonly TimeSpan GravityInterval = TimeSpan.FromMilliseconds(700);
    private const int StartMenuWidth = 44;
    private const string EnterAlternateScreen = "\u001b[?1049h";
    private const string ExitAlternateScreen = "\u001b[?1049l";
    private const string HideCursor = "\u001b[?25l";
    private const string ShowCursor = "\u001b[?25h";
    private const string ClearScreen = "\u001b[H\u001b[2J";
    private const string Reset = "\u001b[0m";

    public static int Run()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        var ownsTerminal = !Console.IsOutputRedirected;

        try
        {
            if (ownsTerminal)
            {
                Console.Write(EnterAlternateScreen + HideCursor + ClearScreen);
            }

            TrySetCursorVisible(false);
            var options = ShowStartMenu();
            RunMatch(options, new ConsoleInputSource(), new ConsoleRenderer());
            return 0;
        }
        finally
        {
            if (ownsTerminal)
            {
                Console.Write(Reset + ShowCursor + ExitAlternateScreen);
            }

            TrySetCursorVisible(true);
            TryResetColor();
        }
    }

    private static void TrySetCursorVisible(bool visible)
    {
        try
        {
            Console.CursorVisible = visible;
        }
        catch (IOException)
        {
        }
    }

    private static void TryResetColor()
    {
        try
        {
            Console.ResetColor();
        }
        catch (IOException)
        {
        }
    }

    private static GameOptions ShowStartMenu()
    {
        Console.Clear();
        Console.WriteLine("╔" + new string('═', StartMenuWidth - 2) + "╗");
        Console.WriteLine(MenuLine("C#으로 AI 테트리스를 만들어 보자꾸나"));
        Console.WriteLine(MenuDivider("AI 난이도 선택"));
        Console.WriteLine(MenuLine("1  초급"));
        Console.WriteLine(MenuLine("2  중급"));
        Console.WriteLine(MenuLine("3  고급"));
        Console.WriteLine("╚" + new string('═', StartMenuWidth - 2) + "╝");
        Console.Write("선택 [1-3] › ");

        AiDifficultyPreset difficulty;
        while (true)
        {
            var key = Console.ReadKey(intercept: true).KeyChar;
            difficulty = key switch
            {
                '1' => AiDifficultyPreset.Beginner,
                '2' => AiDifficultyPreset.Intermediate,
                '3' => AiDifficultyPreset.Advanced,
                _ => null!
            };

            if (difficulty is not null)
            {
                Console.WriteLine(difficulty.DisplayName);
                break;
            }
        }

        Console.Clear();
        return new GameOptions(difficulty, GenerateRandomSeed());
    }

    private static int GenerateRandomSeed() => RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue);

    private static string MenuLine(string value)
    {
        return "║ " + PadVisible(value, StartMenuWidth - 4) + " ║";
    }

    private static string MenuDivider(string title)
    {
        var innerWidth = StartMenuWidth - 2;
        var titleSegment = $" {title} ";
        var remaining = Math.Max(0, innerWidth - GetVisibleLength(titleSegment));
        var leftFill = remaining / 2;
        var rightFill = remaining - leftFill;
        return "╠" + new string('═', leftFill) + titleSegment + new string('═', rightFill) + "╣";
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

    private static int GetVisibleLength(string value)
    {
        var length = 0;
        foreach (var character in value)
        {
            length += GetCellWidth(character);
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

    private static void RunMatch(GameOptions options, IInputSource input, IRenderer renderer)
    {
        var player = GameState.Create(options.Seed);
        var ai = GameState.Create(options.Seed);
        var aiPlayer = new NeuralNetworkAiPlayer(options.Difficulty, options.Seed);
        var stopwatch = Stopwatch.StartNew();
        var lastPlayerFall = TimeSpan.Zero;
        var lastAiFall = TimeSpan.Zero;
        var aiThinkReadyAt = TimeSpan.Zero;
        var nextAiCommandAt = TimeSpan.Zero;
        var aiCommandSerial = -1;
        var aiCommands = new Queue<MoveCommand>();
        var paused = false;
        var quit = false;
        var matchOver = false;
        var message = "Playing";

        while (!quit)
        {
            var now = stopwatch.Elapsed;
            while (input.TryReadCommand(out var command))
            {
                if (command == MoveCommand.Quit)
                {
                    quit = true;
                    break;
                }

                if (command == MoveCommand.Pause)
                {
                    paused = !paused;
                    message = paused ? "Paused" : "Playing";
                    continue;
                }

                if (!paused && !matchOver)
                {
                    player.ApplyCommand(command);
                }
            }

            if (!paused && !matchOver)
            {
                if (now - lastPlayerFall >= GravityInterval)
                {
                    player.StepGravity();
                    lastPlayerFall = now;
                }

                if (now - lastAiFall >= GravityInterval)
                {
                    ai.StepGravity();
                    lastAiFall = now;
                }

                if (!ai.IsGameOver)
                {
                    if (ai.PieceSerial != aiCommandSerial)
                    {
                        aiCommands.Clear();
                        aiCommandSerial = ai.PieceSerial;
                        aiThinkReadyAt = now + TimeSpan.FromMilliseconds(options.Difficulty.ThinkDelayMilliseconds);
                        nextAiCommandAt = aiThinkReadyAt;
                    }

                    if (now >= aiThinkReadyAt)
                    {
                        if (aiCommands.Count == 0)
                        {
                            var move = aiPlayer.ChooseMove(ai);
                            foreach (var command in AiCommandSequenceBuilder.BuildCommands(ai, move, options.Difficulty))
                            {
                                aiCommands.Enqueue(command);
                            }

                            if (aiCommands.Count == 0)
                            {
                                aiCommands.Enqueue(MoveCommand.SoftDrop);
                            }

                            nextAiCommandAt = now;
                        }

                        if (aiCommands.Count > 0 && now >= nextAiCommandAt)
                        {
                            var command = aiCommands.Dequeue();
                            ai.ApplyCommand(command);

                            if (ai.IsGameOver || ai.PieceSerial != aiCommandSerial)
                            {
                                aiCommands.Clear();
                                aiCommandSerial = -1;
                                lastAiFall = now;
                            }
                            else
                            {
                                nextAiCommandAt = now + TimeSpan.FromMilliseconds(options.Difficulty.CommandDelayMilliseconds);
                            }
                        }
                    }
                }

                if (player.IsGameOver || ai.IsGameOver)
                {
                    matchOver = true;
                    message = DetermineWinner(player, ai);
                }
            }

            renderer.Render(player, ai, options.Difficulty, new RenderStatus(paused, matchOver, message, now));
            Thread.Sleep(FrameTime);
        }
    }

    private static string DetermineWinner(GameState player, GameState ai)
    {
        if (player.IsGameOver && !ai.IsGameOver)
        {
            return "AI wins by survival";
        }

        if (!player.IsGameOver && ai.IsGameOver)
        {
            return "Player wins by survival";
        }

        if (player.Score != ai.Score)
        {
            return player.Score > ai.Score ? "Player wins by score" : "AI wins by score";
        }

        if (player.Lines != ai.Lines)
        {
            return player.Lines > ai.Lines ? "Player wins by lines" : "AI wins by lines";
        }

        return "Draw";
    }

    private readonly record struct GameOptions(AiDifficultyPreset Difficulty, int Seed);
}
