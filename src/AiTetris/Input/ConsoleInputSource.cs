using AiTetris.Game;

namespace AiTetris.Input;

public interface IInputSource
{
    bool TryReadCommand(out MoveCommand command);
}

public sealed class ConsoleInputSource : IInputSource
{
    public bool TryReadCommand(out MoveCommand command)
    {
        command = MoveCommand.None;

        try
        {
            if (!Console.KeyAvailable)
            {
                return false;
            }

            var key = Console.ReadKey(intercept: true);
            command = key.Key switch
            {
                ConsoleKey.LeftArrow => MoveCommand.Left,
                ConsoleKey.RightArrow => MoveCommand.Right,
                ConsoleKey.DownArrow => MoveCommand.SoftDrop,
                ConsoleKey.UpArrow => MoveCommand.RotateClockwise,
                ConsoleKey.Z => MoveCommand.RotateCounterClockwise,
                ConsoleKey.Spacebar => MoveCommand.HardDrop,
                ConsoleKey.C => MoveCommand.Hold,
                ConsoleKey.P => MoveCommand.Pause,
                ConsoleKey.Escape => MoveCommand.Quit,
                _ => MoveCommand.None
            };

            return command != MoveCommand.None;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}

