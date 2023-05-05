#nullable enable
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetProxy
{
    internal class TcpProxy : IProxy
    {
        /// <summary>
        /// Milliseconds
        /// </summary>
        public int ConnectionTimeout { get; set; } = (4 * 60 * 1000);

        public async Task Start(string remoteServerHostNameOrAddress, ushort remoteServerPort, ushort localPort, string? localIp)
        {
            var connections = new ConcurrentBag<TcpConnection>();

            IPAddress localIpAddress = string.IsNullOrEmpty(localIp) ? IPAddress.IPv6Any : IPAddress.Parse(localIp);
            var localServer = new TcpListener(new IPEndPoint(localIpAddress, localPort));
            localServer.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
            localServer.Start();

            Console.WriteLine($"TCP proxy started [{localIpAddress}]:{localPort} -> [{remoteServerHostNameOrAddress}]:{remoteServerPort}");

            var _ = Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

                    var tempConnections = new List<TcpConnection>(connections.Count);
                    while (connections.TryTake(out var connection))
                    {
                        tempConnections.Add(connection);
                    }

                    foreach (var tcpConnection in tempConnections)
                    {
                        if (tcpConnection.LastActivity + ConnectionTimeout < Environment.TickCount64)
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
                    var ips = await Dns.GetHostAddressesAsync(remoteServerHostNameOrAddress).ConfigureAwait(false);

                    var tcpConnection = await TcpConnection.AcceptTcpClientAsync(localServer,
                            new IPEndPoint(ips[0], remoteServerPort))
                        .ConfigureAwait(false);
                    tcpConnection.Run();
                    connections.Add(tcpConnection);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(ex);
                    Console.ResetColor();
                }
            }
        }
    }

    internal class TcpConnection
    {
        private readonly TcpClient _localServerConnection;
        private readonly EndPoint? _sourceEndpoint;
        private readonly IPEndPoint _remoteEndpoint;
        private readonly TcpClient _forwardClient;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly EndPoint? _serverLocalEndpoint;
        private EndPoint? _forwardLocalEndpoint;
        private long _totalBytesForwarded;
        private long _totalBytesResponded;
        public long LastActivity { get; private set; } = Environment.TickCount64;

        public static async Task<TcpConnection> AcceptTcpClientAsync(TcpListener tcpListener, IPEndPoint remoteEndpoint)
        {
            var localServerConnection = await tcpListener.AcceptTcpClientAsync().ConfigureAwait(false);
            localServerConnection.NoDelay = true;
            return new TcpConnection(localServerConnection, remoteEndpoint);
        }

        private TcpConnection(TcpClient localServerConnection, IPEndPoint remoteEndpoint)
        {
            _localServerConnection = localServerConnection;
            _remoteEndpoint = remoteEndpoint;

            _forwardClient = new TcpClient {NoDelay = true};

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
            catch (Exception ex)
            {
                Console.WriteLine($"An exception occurred while closing TcpConnection : {ex}");
            }
        }

        private void RunInternal(CancellationToken cancellationToken)
        {
            Task.Run(async () =>
            {
                try
                {
                    using (_localServerConnection)
                    using (_forwardClient)
                    {
                        await _forwardClient.ConnectAsync(_remoteEndpoint.Address, _remoteEndpoint.Port, cancellationToken).ConfigureAwait(false);
                        _forwardLocalEndpoint = _forwardClient.Client.LocalEndPoint;

                        Console.WriteLine($"Established TCP {_sourceEndpoint} => {_serverLocalEndpoint} => {_forwardLocalEndpoint} => {_remoteEndpoint}");

                        using (var serverStream = _forwardClient.GetStream())
                        using (var clientStream = _localServerConnection.GetStream())
                        using (cancellationToken.Register(() =>
                        {
                            serverStream.Close();
                            clientStream.Close();
                        }, true))
                        {
                            await Task.WhenAny(
                                CopyToAsync(clientStream, serverStream, 81920, Direction.Forward, cancellationToken),
                                CopyToAsync(serverStream, clientStream, 81920, Direction.Responding, cancellationToken)
                            ).ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An exception occurred during TCP stream : {ex}");
                }
                finally
                {
                    Console.WriteLine($"Closed TCP {_sourceEndpoint} => {_serverLocalEndpoint} => {_forwardLocalEndpoint} => {_remoteEndpoint}. {_totalBytesForwarded} bytes forwarded, {_totalBytesResponded} bytes responded.");
                }
            });
        }

        private async Task CopyToAsync(Stream source, Stream destination, int bufferSize = 81920, Direction direction = Direction.Unknown, CancellationToken cancellationToken = default)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                while (true)
                {
                    int bytesRead = await source.ReadAsync(new Memory<byte>(buffer), cancellationToken).ConfigureAwait(false);
                    if (bytesRead == 0) break;
                    LastActivity = Environment.TickCount64;

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
                    }


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
                    }




                    await destination.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, bytesRead), cancellationToken).ConfigureAwait(false);

                    switch (direction)
                    {
                        case Direction.Forward:
                            Interlocked.Add(ref _totalBytesForwarded, bytesRead);
                            break;
                        case Direction.Responding:
                            Interlocked.Add(ref _totalBytesResponded, bytesRead);
                            break;
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    internal enum Direction
    {
        Unknown = 0,
        Forward,
        Responding,
    }
}
