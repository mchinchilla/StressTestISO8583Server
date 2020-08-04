using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.Text;

namespace StressTestISO8583Server
{

    class Options
    {
        [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
        public bool Verbose { get; set; }

        [Option('s', "server", Required = true, HelpText = "This can be Ip Address or FQDN")]
        public string serverAddress { get; set; }

        [Option('p', "port", Default = 5005, Required = false, HelpText = "Server port (Default port 5005)")]
        public int serverPort { get; set; }

        [Option('t', "usetls", Default = false, Required = false, HelpText = "The transport is TLS or not")]
        public bool UseTLS { get; set; }

        [Option('b', "batch", Default = 10, Required = false, HelpText = "The messages parallel batch that will be send to the server")]
        public int Batch { get; set; }

        [Option('q', "quantity", Default = 100, Required = false, HelpText = "The total messages that will be send to the server")]
        public int Quantity { get; set; }

        [Usage(ApplicationAlias = "StressTestISO8583Server")]
        public static IEnumerable<Example> Examples
        {
            get
            {
                return new List<Example>()
                {
                    new Example("ISO 8583 Server Stress Test", new Options {serverAddress = "127.0.0.1"})
                };
            }

        }
    }
}
