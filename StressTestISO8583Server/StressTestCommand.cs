using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetCore8583;
using NetCore8583.Extensions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace StressTestISO8583Server;

public sealed class StressTestCommand : AsyncCommand<StressTestSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, StressTestSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            DisplayConfiguration(settings);

            if (!await CheckConnectivityAsync(settings.ServerAddress, settings.ServerPort, cancellationToken))
                return 1;

            byte[] msg = BuildIsoMessage();

            using var sender = new IsoMessageSender(settings.ServerAddress, settings.ServerPort, settings.UseTLS);
            var runner = new StressTestRunner(sender, msg, settings.Quantity, settings.Batch);

            var sw = Stopwatch.StartNew();

            if (settings.Verbose)
                await RunVerboseAsync(runner, cancellationToken);
            else
                await RunWithProgressAsync(runner, cancellationToken);

            sw.Stop();

            DisplayResults(runner.SuccessCount, runner.FailedCount, sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]The operation has been cancelled by the user.[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }

        return 0;
    }

    private static void DisplayConfiguration(StressTestSettings settings)
    {
        AnsiConsole.Write(new Rule("[yellow]ISO 8583 Stress Test[/]").RuleStyle("grey"));

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Setting")
            .AddColumn("Value");

        table.AddRow("Server Address", settings.ServerAddress);
        table.AddRow("Port", settings.ServerPort.ToString());
        table.AddRow("Verbose", settings.Verbose.ToString());
        table.AddRow("Use TLS", settings.UseTLS.ToString());
        table.AddRow("Quantity (batches)", settings.Quantity.ToString());
        table.AddRow("Batch (concurrent/batch)", settings.Batch.ToString());
        table.AddRow("[bold]Total Messages[/]", $"[bold]{settings.Quantity * settings.Batch}[/]");

        AnsiConsole.Write(table);
        AnsiConsole.Write(new Rule().RuleStyle("grey"));
    }

    private static byte[] BuildIsoMessage()
    {
        var mf = new MessageFactory<IsoMessage> { Encoding = Encoding.UTF8 };
        mf.SetConfigPath(@"Resources/config.xml");

        var iso = mf.NewMessage(0x200);
        sbyte[] streamToSend = iso.WriteData();
        return Encoding.ASCII.GetBytes(streamToSend.ToString(Encoding.ASCII).Replace("ISO0150000500200", "0200"));
    }

    private static async Task RunWithProgressAsync(StressTestRunner runner, CancellationToken cancellationToken)
    {
        await AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var progressTask = ctx.AddTask("[yellow]Sending messages...[/]", maxValue: runner.TotalMessages);

                await runner.RunAsync(result =>
                {
                    progressTask.Increment(1);
                }, cancellationToken);

                progressTask.Description = "[green]Messages sent![/]";
            });
    }

    private static async Task RunVerboseAsync(StressTestRunner runner, CancellationToken cancellationToken)
    {
        await runner.RunAsync(result =>
        {
            string status = result.Success ? "[green]OK[/]" : "[red]FAILED[/]";
            AnsiConsole.MarkupLine($"Batch: {result.BatchIndex + 1} | Message: {result.MessageIndex + 1}.....{status}");
        }, cancellationToken);
    }

    private static async Task<bool> CheckConnectivityAsync(string serverAddress, int port, CancellationToken cancellationToken)
    {
        return await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("yellow"))
            .StartAsync($"Checking connectivity to {serverAddress}:{port}...", async _ =>
            {
                try
                {
                    using var client = new TcpClient();
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

                    await client.ConnectAsync(serverAddress, port, timeoutCts.Token);
                    AnsiConsole.MarkupLine($"[green]Connection OK[/] — {serverAddress}:{port} is reachable.");
                    return true;
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    AnsiConsole.MarkupLine($"[red]Connection timed out[/] — {serverAddress}:{port} did not respond within 5 seconds.");
                    return false;
                }
                catch (SocketException ex)
                {
                    AnsiConsole.MarkupLine($"[red]Connection failed[/] — {serverAddress}:{port}: {ex.Message}");
                    return false;
                }
            });
    }

    private static void DisplayResults(int success, int failed, TimeSpan elapsed)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[green]Results[/]").RuleStyle("grey"));

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Metric")
            .AddColumn("Value");

        table.AddRow("[green]Success[/]", $"[green]{success}[/]");
        table.AddRow("[red]Failed[/]", $"[red]{failed}[/]");
        table.AddRow("Elapsed Time", elapsed.ToString());

        AnsiConsole.Write(table);
    }
}
