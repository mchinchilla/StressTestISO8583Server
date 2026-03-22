using Spectre.Console.Cli;

namespace StressTestISO8583Server;

public class Program
{
    public static int Main(string[] args)
    {
        var app = new CommandApp<StressTestCommand>();
        app.Configure(config =>
        {
            config.SetApplicationName("StressTestISO8583Server");
            config.AddExample("-s", "tekiumlabs.com");
            config.AddExample("-s", "tekiumlabs.com", "-p", "5005", "-q", "1000", "-b", "10", "-t", "-v");
        });

        return app.Run(args);
    }
}
