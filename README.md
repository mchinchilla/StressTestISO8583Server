# StressTestISO8583Server

### GOAL
This project has been created with the intention of testing the performance of ISO 8583 servers for financial transactions.

### How to Use

USAGE:

  -v, --verbose     Set output to verbose messages.

  -s, --server      Required. This can be Ip Address or FQDN

  -p, --port        (Default: 5005) Server port (Default port 5005)

  -t, --usetls      (Default: false) The transport is TLS or not

  -b, --batch       (Default: 10) The messages parallel batch that will be send to the
                    server

  -q, --quantity    (Default: 100) The total messages that will be send to the server

  --help            Display this help screen.

  --version         Display version information.

If the option -v or --verbose is set the progress bar won't be appear.


```shell
Example: dotnet StressTestISO8583Server.dll -s tekiumlabs.com -p 5005 -q 1000 -b 10 -t -v
```

=========================================================================
### NOTICE: The total quantity to send will be the multiply of quantity and batch.
=========================================================================

### Output samples

![example with ProgressBar](https://raw.githubusercontent.com/mchinchilla/StressTestISO8583Server/blob/master/Resources/prgbar.gif)

![example with verbose option](https://raw.githubusercontent.com/mchinchilla/StressTestISO8583Server/blob/master/Resources/no-prgbar.gif)



