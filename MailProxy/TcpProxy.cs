#nullable enable

namespace NetProxy
{

    using System;


    internal class TcpProxy 
        : IProxy
    {


        /// <summary>
        /// Milliseconds
        /// </summary>
        public int ConnectionTimeout { get; set; } = (4 * 60 * 1000);

        public async System.Threading.Tasks.Task Start(
            string remoteServerHostNameOrAddress, 
            ushort remoteServerPort, 
            ushort localPort, 
            string? localIp
        )
        {
            System.Collections.Concurrent.ConcurrentBag<TcpConnection>? connections = 
                new System.Collections.Concurrent.ConcurrentBag<TcpConnection>();

            MailProxy.LogHelper.StartLog();

            System.Net.IPAddress localIpAddress = string.IsNullOrEmpty(localIp) ? 
                System.Net.IPAddress.IPv6Any : System.Net.IPAddress.Parse(localIp);

            System.Net.Sockets.TcpListener? localServer = 
                new System.Net.Sockets.TcpListener(
                    new System.Net.IPEndPoint(localIpAddress, localPort)
            );

            localServer.Server.SetSocketOption(
                System.Net.Sockets.SocketOptionLevel.IPv6,
                System.Net.Sockets.SocketOptionName.IPv6Only, false
            );

            localServer.Start();

            System.Console.WriteLine($"TCP proxy started [{localIpAddress}]:{localPort} -> [{remoteServerHostNameOrAddress}]:{remoteServerPort}");

            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                while (true)
                {
                    await System.Threading.Tasks.Task.Delay(System.TimeSpan.FromSeconds(10)).ConfigureAwait(false);

                    System.Collections.Generic.List<TcpConnection>? tempConnections = 
                    new System.Collections.Generic.List<TcpConnection>(connections.Count);

                    while (connections.TryTake(out TcpConnection? connection))
                    {
                        tempConnections.Add(connection);
                    }

                    foreach (TcpConnection? tcpConnection in tempConnections)
                    {
                        if (tcpConnection.LastActivity + ConnectionTimeout < System.Environment.TickCount64)
                        {
                            tcpConnection.Stop();
                        }
                        else
                        {
                            connections.Add(tcpConnection);
                        }
                    }
                }
            });

            while (true)
            {
                try
                {
                    System.Net.IPAddress[]? ips = 
                        await System.Net.Dns.GetHostAddressesAsync(remoteServerHostNameOrAddress)
                        .ConfigureAwait(false);

                    TcpConnection? tcpConnection = 
                        await TcpConnection.AcceptTcpClientAsync(localServer,
                            new System.Net.IPEndPoint(ips[0], remoteServerPort))
                        .ConfigureAwait(false);

                    tcpConnection.Run();
                    connections.Add(tcpConnection);
                }
                catch (System.Exception ex)
                {
                    System.Console.ForegroundColor = System.ConsoleColor.Red;
                    System.Console.WriteLine(ex);
                    System.Console.ResetColor();
                }
            } // Whend 

        } // End Task Start 


    } // End Class TcpProxy 


    internal class TcpConnection
    {
        private readonly System.Net.Sockets.TcpClient _localServerConnection;
        private readonly System.Net.EndPoint? _sourceEndpoint;
        private readonly System.Net.IPEndPoint _remoteEndpoint;
        private readonly System.Net.Sockets.TcpClient _forwardClient;
        private readonly System.Threading.CancellationTokenSource _cancellationTokenSource = 
            new System.Threading.CancellationTokenSource();
        private readonly System.Net.EndPoint? _serverLocalEndpoint;
        private System.Net.EndPoint? _forwardLocalEndpoint;
        private long _totalBytesForwarded;
        private long _totalBytesResponded;
        public long LastActivity { get; private set; } = System.Environment.TickCount64;


        public static async System.Threading.Tasks.Task<TcpConnection> AcceptTcpClientAsync(
            System.Net.Sockets.TcpListener tcpListener, 
            System.Net.IPEndPoint remoteEndpoint
        )
        {
            System.Net.Sockets.TcpClient? localServerConnection = 
                await tcpListener.AcceptTcpClientAsync()
                .ConfigureAwait(false);

            localServerConnection.NoDelay = true;
            return new TcpConnection(localServerConnection, remoteEndpoint);
        }


        private TcpConnection(
           System.Net.Sockets.TcpClient localServerConnection, 
            System.Net.IPEndPoint remoteEndpoint
        )
        {
            _localServerConnection = localServerConnection;
            _remoteEndpoint = remoteEndpoint;

            _forwardClient = new System.Net.Sockets.TcpClient {NoDelay = true};

            _sourceEndpoint = _localServerConnection.Client.RemoteEndPoint;
            _serverLocalEndpoint = _localServerConnection.Client.LocalEndPoint;
        }


        public void Run()
        {
            RunInternal(_cancellationTokenSource.Token);
        }


        public void Stop()
        {
            try
            {
                _cancellationTokenSource.Cancel();
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"An exception occurred while closing TcpConnection : {ex}");
            }
        }


        private void RunInternal(System.Threading.CancellationToken cancellationToken)
        {
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    using (_localServerConnection)
                    using (_forwardClient)
                    {
                        await _forwardClient.ConnectAsync(_remoteEndpoint.Address, _remoteEndpoint.Port, cancellationToken).ConfigureAwait(false);
                        _forwardLocalEndpoint = _forwardClient.Client.LocalEndPoint;

                        System.Console.WriteLine($"Established TCP {_sourceEndpoint} => {_serverLocalEndpoint} => {_forwardLocalEndpoint} => {_remoteEndpoint}");

                        using (System.Net.Sockets.NetworkStream? serverStream = 
                            _forwardClient.GetStream())
                        using (System.Net.Sockets.NetworkStream? clientStream = 
                            _localServerConnection.GetStream())
                        using (cancellationToken.Register(() =>
                        {
                            serverStream.Close();
                            clientStream.Close();
                        }, true))
                        {
                            await System.Threading.Tasks.Task.WhenAny(
                                CopyToAsync(clientStream, serverStream, 81920, Direction.Forward, cancellationToken),
                                CopyToAsync(serverStream, clientStream, 81920, Direction.Responding, cancellationToken)
                            ).ConfigureAwait(false);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    System.Console.WriteLine($"An exception occurred during TCP stream : {ex}");
                }
                finally
                {
                    System.Console.WriteLine($"Closed TCP {_sourceEndpoint} => {_serverLocalEndpoint} => {_forwardLocalEndpoint} => {_remoteEndpoint}. {_totalBytesForwarded} bytes forwarded, {_totalBytesResponded} bytes responded.");
                }
            });
        } // End Sub RunInternal 


        private async System.Threading.Tasks.Task CopyToAsync(
            System.IO.Stream source,
            System.IO.Stream destination, 
            int bufferSize = 81920, 
            Direction direction = Direction.Unknown,
            System.Threading.CancellationToken cancellationToken = default
        )
        {
            byte[] buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                while (true)
                {
                    int bytesRead = await source.ReadAsync(new System.Memory<byte>(buffer), cancellationToken).ConfigureAwait(false);
                    if (bytesRead == 0) 
                        break;

                    LastActivity = System.Environment.TickCount64;

                    bool s_isWindows = true;
                    string text = System.Text.Encoding.UTF8.GetString(buffer.AsSpan(0, bytesRead));
                    

                    string eventCaption = "Server";
                    System.Drawing.Color backgroundColor = System.Drawing.Color.Green;
                    System.Drawing.Color foregroundColor = System.Drawing.Color.Black;
                    if (direction == Direction.Forward)
                    {
                        eventCaption = "Client";
                        backgroundColor = System.Drawing.Color.Blue;
                        foregroundColor = System.Drawing.Color.White;
                    } // End if (direction == Direction.Forward) 


                    System.Console.ResetColor();
                    System.Console.Write(eventCaption + ":");
                    System.Console.Write(new string(' ', System.Console.BufferWidth - System.Console.CursorLeft));


                    if (!s_isWindows)
                        System.Console.Write(System.Environment.NewLine);

                    ExpressProfiler.ConsoleOutputWriter cw = new ExpressProfiler.ConsoleOutputWriter()
                    {
                        BackColor = System.Drawing.Color.White, ForeColor = foregroundColor
                    };

                    if (!string.IsNullOrEmpty(text))
                    {
                        cw.BackColor = backgroundColor;
                        cw.Append(text + System.Environment.NewLine);
                        MailProxy.LogHelper.LogLine(eventCaption, buffer.AsSpan(0, bytesRead), text);
                    }

                    await destination.WriteAsync(new System.ReadOnlyMemory<byte>(buffer, 0, bytesRead), cancellationToken).ConfigureAwait(false);

                    switch (direction)
                    {
                        case Direction.Forward:
                            System.Threading.Interlocked.Add(ref _totalBytesForwarded, bytesRead);
                            break;
                        case Direction.Responding:
                            System.Threading.Interlocked.Add(ref _totalBytesResponded, bytesRead);
                            break;
                    }
                }
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
            }

        } // End Task CopyAsync 

    } // End Class 


    internal enum Direction
    {
        Unknown = 0,
        Forward,
        Responding,
    } // End Enum 


} // End Namespace 
