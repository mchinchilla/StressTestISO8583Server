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
        private static ParallelLoopResult _result;
        private static CancellationTokenSource _cts = new CancellationTokenSource();

        private static MessageFactory<IsoMessage> _mf;
        private static Stopwatch _sw = new Stopwatch();
        private static int _successMessages = 0;
        private static int _failedMessages = 0;
        private static string _success = "FAILED";

        private static Options _cliOptions = new Options();
        private static bool _cliOptionsHasErrors = false;
        private static ProgressBar _pbar = null;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            try
            {
                Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(opts =>
                {
                    Console.WriteLine($@"Example: StressTestISO8583Server -s tekiumlabs.com -p 5005 -q 1000 -b 10 -t -v");
                    _cliOptions = opts;

                }).WithNotParsed(errs =>
                {

                    if (errs.ToList().Count > 0)
                    {
                        _cliOptionsHasErrors = true;
                    }
                    else
                    {
                        _cliOptionsHasErrors = false;
                    }
                });

                if (_cliOptionsHasErrors)
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
                var token = _cts.Token;
                ParallelOptions po = new ParallelOptions();
                po.CancellationToken = token;

                Console.Clear();

                Console.WriteLine($"{String.Empty.PadRight(70, '=')}");
                Console.WriteLine($"Server Address: {_cliOptions.serverAddress}, Port: {_cliOptions.serverPort}\nVerbose: {_cliOptions.Verbose}, Use TLS: {_cliOptions.UseTLS}\nQuantity: {_cliOptions.Quantity}, Batch: {_cliOptions.Batch}, Total Messages to Send: {_cliOptions.Quantity * _cliOptions.Batch}");
                Console.WriteLine($"{String.Empty.PadRight(70, '=')}");

                // Load the MessageFactory from Config
                _mf = new MessageFactory<IsoMessage>
                {
                    Encoding = Encoding.UTF8
                };
                _mf.SetConfigPath(@"Resources/config.xml");

                // Create an ISO Message
                var iso = _mf.NewMessage(0x200);
                sbyte[] streamToSend = iso.WriteData();
                byte[] msg = Encoding.ASCII.GetBytes(streamToSend.ToString(Encoding.ASCII).Replace("ISO0150000500200", "0200"));


                int totalTicks = _cliOptions.Quantity;
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

                _sw.Start();
                _successMessages = 0;
                _failedMessages = 0;
                int k = 0;

                if (!_cliOptions.Verbose)
                {
                    _pbar = new ProgressBar((_cliOptions.Quantity * _cliOptions.Batch), $@"Proccessing Messages...", opts);
                    _pbar.Tick(0, $" [Proccessing Messages.... ]");
                }

                
                for (int i = 0; i < _cliOptions.Quantity; i++)
                {
                    bool haveLock = mutex.WaitOne();
                    try
                    {
                        tasks.Add(Task.Factory.StartNew(() =>
                        {
                            // (cliOptions.Batch + 1): +1 because is not inclusive [0,11[ => 10 messages
                            _result = Parallel.For(1, (_cliOptions.Batch + 1), po, async (int x, ParallelLoopState state) =>
                              {
                                  int? TaskId = Task.CurrentId.Value;
                                  k++;
                                  try
                                  {
                                      //Console.WriteLine($"Task: {x} --> Task Id:{Task.CurrentId}");
                                      string response = await SendISOMessageAsync(_cliOptions.serverAddress, _cliOptions.serverPort, msg);

                                      if (!String.IsNullOrEmpty(response))
                                      {
                                          //Console.Write($"Yay!!! Transaction success!\n");
                                          _successMessages++;
                                          _success = "OK";
                                      }
                                      else
                                      {
                                          //Console.WriteLine($"Opps!, :( No luck this time!\n");
                                          _failedMessages++;
                                          _success = "FAILED";
                                      }

                                      if (_cts.IsCancellationRequested)
                                      {
                                          _cts.Cancel();
                                      }
                                      
                                      if(!_cliOptions.Verbose)
                                      {
                                          _pbar.Tick(k, $" [Proccessing Message: {k} ]");
                                      }
                                  }
                                  catch (Exception)
                                  {
                                      Console.WriteLine($"Error on Task: {x}-> Task Id:{TaskId}");
                                      _failedMessages++;
                                  }
                                  
                                  if (_cliOptions.Verbose)
                                    Console.WriteLine($"Task: {x}-> Task Id:{TaskId}.....[{_success}]");
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

                _cts.Cancel();

                _sw.Stop();

                if (!_cliOptions.Verbose)
                {
                    _pbar.Tick(_pbar.MaxTicks);
                    _pbar.WriteLine("Tasks Completed!!!");
                    _pbar.Dispose();
                }

                Console.WriteLine($"{String.Empty.PadRight(70, '=')}");
                Console.WriteLine($"Success Messages: {_successMessages} and Failed Messages: {_failedMessages} in {_sw.Elapsed}");
                Console.WriteLine($"{String.Empty.PadRight(70, '=')}");
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serverAddress"></param>
        /// <param name="remotePort"></param>
        /// <param name="isoMessage"></param>
        /// <returns></returns>
        private static async Task<string> SendISOMessageAsync(string serverAddress, int remotePort, byte[] isoMessage)
        {
            string returndata = string.Empty;
            try
            {
                TcpClient clientSocket = new TcpClient();
                if (_cliOptions.UseTLS)
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="certificate"></param>
        /// <param name="chain"></param>
        /// <param name="sslPolicyErrors"></param>
        /// <returns></returns>
        public static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }
}
