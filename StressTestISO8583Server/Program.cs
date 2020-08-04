using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Runtime.CompilerServices;
using NetCore8583;
using System.Text;
using NetCore8583.Util;
using System.Net.Sockets;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using CommandLine;
using ShellProgressBar;

namespace StressTestISO8583Server
{
    class Program
    {
        private static ParallelLoopResult result;
        private static CancellationTokenSource cts = new CancellationTokenSource();

        private static MessageFactory<IsoMessage> mf;
        private static Stopwatch sw = new Stopwatch();
        private static int successMessages = 0;
        private static int failedMessages = 0;
        private static string success = "FAILED";

        private static Options cliOptions = new Options();
        private static bool cliOptionsHasErrors = false;
        private static ProgressBar pbar = null;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            Console.WriteLine($"{String.Empty.PadRight(80, '*')}");
            Console.WriteLine($@"
        ████████╗███████╗██╗  ██╗██╗██╗   ██╗███╗   ███╗
        ╚══██╔══╝██╔════╝██║ ██╔╝██║██║   ██║████╗ ████║
           ██║   █████╗  █████╔╝ ██║██║   ██║██╔████╔██║
           ██║   ██╔══╝  ██╔═██╗ ██║██║   ██║██║╚██╔╝██║
           ██║   ███████╗██║  ██╗██║╚██████╔╝██║ ╚═╝ ██║
           ╚═╝   ╚══════╝╚═╝  ╚═╝╚═╝ ╚═════╝ ╚═╝     ╚═╝
");
            Console.WriteLine($"{String.Empty.PadRight(80, '*')}");

            try
            {
                Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(opts =>
                {
                    Console.WriteLine($@"Example: StressTestISO8583Server -s tekiumlabs.com -p 5005 -q 1000 -b 10 -t -v");
                    cliOptions = opts;

                }).WithNotParsed(errs =>
                {

                    if (errs.ToList().Count > 0)
                    {
                        cliOptionsHasErrors = true;
                    }
                    else
                    {
                        cliOptionsHasErrors = false;
                    }
                });

                if (cliOptionsHasErrors)
                {
                    return;
                }

            }
            catch (Exception)
            {
                Console.WriteLine($"Error parsing CLI options");
            }

            try
            {
                List<Task> tasks = new List<Task>();
                Mutex mutex = new Mutex();
                var token = cts.Token;
                ParallelOptions po = new ParallelOptions();
                po.CancellationToken = token;

                // Load the MessageFactory from Config
                mf = new MessageFactory<IsoMessage>
                {
                    Encoding = Encoding.UTF8
                };
                mf.SetConfigPath(@"Resources/config.xml");

                // Create an ISO Message
                var iso = mf.NewMessage(0x200);
                sbyte[] streamToSend = iso.WriteData();
                byte[] msg = Encoding.ASCII.GetBytes(streamToSend.BytesToString(Encoding.ASCII).Replace("ISO0150000500200", "0200"));


                int totalTicks = cliOptions.Quantity;
                var opts = new ProgressBarOptions
                {
                    ForegroundColor = ConsoleColor.Yellow,
                    ForegroundColorDone = ConsoleColor.DarkGreen,
                    BackgroundColor = ConsoleColor.DarkGray,
                    BackgroundCharacter = '\u2593',
                    ProgressBarOnBottom = true,
                    DisplayTimeInRealTime = false,
                    CollapseWhenFinished = true
                };

                sw.Start();
                successMessages = 0;
                failedMessages = 0;
                int k = 0;

                if (!cliOptions.Verbose)
                {
                    pbar = new ProgressBar((cliOptions.Quantity * cliOptions.Batch), $@"Proccessing Messages...", opts);
                    pbar.Tick(0, $" [Proccessing Messages.... ]");
                }

                
                for (int i = 0; i < cliOptions.Quantity; i++)
                {
                    bool haveLock = mutex.WaitOne();
                    try
                    {
                        tasks.Add(Task.Factory.StartNew(() =>
                        {
                            // (cliOptions.Batch + 1): +1 because is not inclusive [0,11[ => 10 messages
                            result = Parallel.For(1, (cliOptions.Batch + 1), po, async (int x, ParallelLoopState state) =>
                              {
                                  int? TaskId = Task.CurrentId.Value;
                                  k++;
                                  try
                                  {
                                      //Console.WriteLine($"Task: {x} --> Task Id:{Task.CurrentId}");
                                      string response = await SendISOMessage(cliOptions.serverAddress, cliOptions.serverPort, msg);

                                      if (!String.IsNullOrEmpty(response))
                                      {
                                          //Console.Write($"Yay!!! Transaction success!\n");
                                          successMessages++;
                                          success = "OK";
                                      }
                                      else
                                      {
                                          //Console.WriteLine($"Opps!, :( No luck this time!\n");
                                          failedMessages++;
                                          success = "FAILED";
                                      }

                                      if (cts.IsCancellationRequested)
                                      {
                                          cts.Cancel();
                                      }
                                      
                                      if(!cliOptions.Verbose)
                                      {
                                          pbar.Tick(k, $" [Proccessing Message: {k} ]");
                                      }
                                  }
                                  catch (Exception)
                                  {
                                      Console.WriteLine($"Error on Task: {x}-> Task Id:{TaskId}");
                                      failedMessages++;
                                  }
                                  
                                  if (cliOptions.Verbose)
                                    Console.WriteLine($"Task: {x}-> Task Id:{TaskId}.....[{success}]");
                              });

                            token.ThrowIfCancellationRequested();

                        }, token));
                    }
                    finally
                    {
                        if (haveLock)
                            mutex.ReleaseMutex();
                    }
                }

                Task.WaitAll(tasks.ToArray());

                cts.Cancel();

                sw.Stop();

                if (!cliOptions.Verbose)
                {
                    pbar.Tick(pbar.MaxTicks);
                    pbar.WriteLine("Tasks Completed!!!");
                    pbar.Dispose();
                }

                Console.WriteLine($"===========================================================================================");
                Console.WriteLine($"Success Messages: {successMessages} and Failed Messages: {failedMessages} in {sw.Elapsed}");
                Console.WriteLine($"===========================================================================================");
                Console.WriteLine($"Press any key to continue...");
                Console.ReadKey();

            }
            catch (AggregateException ae)
            {
                ae.Handle(e =>
                {
                    return true;
                });
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"The operation has been cancelled by the user...");
            }
        }

        private static async Task<string> SendISOMessage(string serverAddress, int remotePort, byte[] isoMessage)
        {
            string returndata = string.Empty;
            try
            {
                TcpClient clientSocket = new TcpClient();
                if (cliOptions.UseTLS)
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                    clientSocket.Connect(serverAddress, remotePort);

                    using (SslStream serverStream = new SslStream(clientSocket.GetStream(), false, new RemoteCertificateValidationCallback(ValidateServerCertificate), null))
                    {
                        byte[] outStream = isoMessage;
                        serverStream.Write(outStream, 0, outStream.Length);
                        serverStream.Flush();

                        byte[] inStream = new byte[1024000];
                        serverStream.Read(inStream, 0, (int)clientSocket.ReceiveBufferSize);
                        returndata = Encoding.ASCII.GetString(inStream);
                    }
                }
                else
                {
                    clientSocket.Connect(serverAddress, remotePort);
                    NetworkStream serverStream = clientSocket.GetStream();
                    byte[] outStream = isoMessage;
                    serverStream.Write(outStream, 0, outStream.Length);
                    serverStream.Flush();

                    byte[] inStream = new byte[1024000];
                    serverStream.Read(inStream, 0, (int)clientSocket.ReceiveBufferSize);
                    returndata = Encoding.ASCII.GetString(inStream);
                }
            }
            catch (Exception)
            {
                returndata = string.Empty;
            }

            return await Task.Run(() => returndata);
        }

        public static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }
}
