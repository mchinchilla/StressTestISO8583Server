using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace StressTestISO8583Server;

public sealed class StressTestSettings : CommandSettings
{
    [CommandOption("-v|--verbose")]
    [Description("Set output to verbose messages")]
    [DefaultValue(false)]
    public bool Verbose { get; set; }

    [CommandOption("-s|--server")]
    [Description("IP address or FQDN of the target server (required)")]
    public string ServerAddress { get; set; } = string.Empty;

    public override ValidationResult Validate()
    {
        return string.IsNullOrWhiteSpace(ServerAddress)
            ? ValidationResult.Error("The --server (-s) option is required.")
            : ValidationResult.Success();
    }

    [CommandOption("-p|--port")]
    [Description("Server port (default 5005)")]
    [DefaultValue(5005)]
    public int ServerPort { get; set; } = 5005;

    [CommandOption("-t|--usetls")]
    [Description("Use TLS/SSL transport")]
    [DefaultValue(false)]
    public bool UseTLS { get; set; }

    [CommandOption("-b|--batch")]
    [Description("Number of concurrent messages per batch")]
    [DefaultValue(10)]
    public int Batch { get; set; } = 10;

    [CommandOption("-q|--quantity")]
    [Description("Total number of batches to send")]
    [DefaultValue(100)]
    public int Quantity { get; set; } = 100;
}
