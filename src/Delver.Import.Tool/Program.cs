namespace Delver.Import.Tool;

/// <summary>Thrown for bad command lines; maps to exit code 2.</summary>
internal sealed class UsageException : Exception
{
    public UsageException(string message)
        : base(message)
    {
    }
}

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            return ImportCommands.Run(args);
        }
        catch (UsageException usage)
        {
            Console.Error.WriteLine(usage.Message);
            return 2;
        }
        catch (Exception failure)
        {
            Console.Error.WriteLine($"delverimport: {failure.Message}");
            return 1;
        }
    }
}
