#nullable enable

namespace NetProxy
{


    internal class UdpProxy 
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
            string? localIp = null
        )
        {
            System.Collections.Concurrent.ConcurrentDictionary<
                System.Net.IPEndPoint, UdpConnection
                >? connections = 
                new System.Collections.Concurrent.ConcurrentDictionary<
                    System.Net.IPEndPoint, UdpConnection
                    >();

            // TCP will lookup every time while this is only once.
            System.Net.IPAddress[]? ips = await System.Net.Dns.GetHostAddressesAsync(remoteServerHostNameOrAddress).ConfigureAwait(false);
            System.Net.IPEndPoint? remoteServerEndPoint = 
                new System.Net.IPEndPoint(ips[0], remoteServerPort);

            System.Net.Sockets.UdpClient? localServer = 
                new System.Net.Sockets.UdpClient(System.Net.Sockets.AddressFamily.InterNetworkV6);

            localServer.Client.SetSocketOption(
                System.Net.Sockets.SocketOptionLevel.IPv6, 
                System.Net.Sockets.SocketOptionName.IPv6Only, 
                false
            );

            System.Net.IPAddress localIpAddress = 
                string.IsNullOrEmpty(localIp) ? 
                System.Net.IPAddress.IPv6Any : System.Net.IPAddress.Parse(localIp);

            localServer.Client.Bind(new System.Net.IPEndPoint(localIpAddress, localPort));

            System.Console.WriteLine($"UDP proxy started [{localIpAddress}]:{localPort} -> [{remoteServerHostNameOrAddress}]:{remoteServerPort}");

            _ = System.Threading.Tasks.Task.Run(
                async () =>
                {
                    while (true)
                    {
                        await System.Threading.Tasks.Task.Delay(System.TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                        foreach (System.Collections.Generic.KeyValuePair<System.Net.IPEndPoint, UdpConnection>
                            connection in connections.ToArray()
                        )
                        {
                            if (connection.Value.LastActivity + ConnectionTimeout 
                                    < System.Environment.TickCount64
                            )
                            {
                                connections.TryRemove(connection.Key, out UdpConnection? c);
                                connection.Value.Stop();
                            }
                        } // Next connection 
                    } // Whend 
                }
            );

            while (true)
            {
                try
                {
                    System.Net.Sockets.UdpReceiveResult message = await localServer.ReceiveAsync().ConfigureAwait(false);
                    System.Net.IPEndPoint? sourceEndPoint = message.RemoteEndPoint;
                    UdpConnection? client = connections.GetOrAdd(sourceEndPoint,
                        ep =>
                        {
                            UdpConnection? udpConnection = new UdpConnection(localServer, sourceEndPoint, remoteServerEndPoint);
                            udpConnection.Run();
                            return udpConnection;
                        });
                    await client.SendToServerAsync(message.Buffer).ConfigureAwait(false);
                }
                catch (System.Exception ex)
                {
                    System.Console.WriteLine($"an exception occurred on receiving a client datagram: {ex}");
                }
            } // Whend 

        } // End Task Start 


    } // End Class UdpProxy 


    internal class UdpConnection
    {
        private readonly System.Net.Sockets.UdpClient _localServer;
        private readonly System.Net.Sockets.UdpClient _forwardClient;

        public long LastActivity { get; private set; } = System.Environment.TickCount64;
        private readonly System.Net.IPEndPoint _sourceEndpoint;
        private readonly System.Net.IPEndPoint _remoteEndpoint;
        private readonly System.Net.EndPoint? _serverLocalEndpoint;
        private System.Net.EndPoint? _forwardLocalEndpoint;
        private bool _isRunning;
        private long _totalBytesForwarded;
        private long _totalBytesResponded;

        private readonly System.Threading.Tasks.TaskCompletionSource<bool> 
            _forwardConnectionBindCompleted = 
            new System.Threading.Tasks.TaskCompletionSource<bool>();


        public UdpConnection(
            System.Net.Sockets.UdpClient localServer, 
            System.Net.IPEndPoint sourceEndpoint,
            System.Net.IPEndPoint remoteEndpoint)
        {
            _localServer = localServer;
            _serverLocalEndpoint = _localServer.Client.LocalEndPoint;

            _isRunning = true;
            _remoteEndpoint = remoteEndpoint;
            _sourceEndpoint = sourceEndpoint;

            _forwardClient = new System.Net.Sockets.UdpClient(
                System.Net.Sockets.AddressFamily.InterNetworkV6
            );

            _forwardClient.Client.SetSocketOption(
                System.Net.Sockets.SocketOptionLevel.IPv6, 
                System.Net.Sockets.SocketOptionName.IPv6Only, 
                false
            );
        } // End Constructor 


        public async System.Threading.Tasks.Task SendToServerAsync(byte[] message)
        {
            LastActivity = System.Environment.TickCount64;

            await _forwardConnectionBindCompleted.Task.ConfigureAwait(false);
            int sent = await _forwardClient.SendAsync(message, message.Length, _remoteEndpoint)
                .ConfigureAwait(false);

            System.Threading.Interlocked.Add(ref _totalBytesForwarded, sent);
        } // End Task SendToServerAsync 


        public void Run()
        {
            System.Threading.Tasks.Task.Run(async () =>
            {
                using (_forwardClient)
                {
                    _forwardClient.Client.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0));
                    _forwardLocalEndpoint = _forwardClient.Client.LocalEndPoint;
                    _forwardConnectionBindCompleted.SetResult(true);
                    System.Console.WriteLine($"Established UDP {_sourceEndpoint} => {_serverLocalEndpoint} => {_forwardLocalEndpoint} => {_remoteEndpoint}");

                    while (_isRunning)
                    {
                        try
                        {
                            System.Net.Sockets.UdpReceiveResult result = await _forwardClient.ReceiveAsync().ConfigureAwait(false);
                            LastActivity = System.Environment.TickCount64;
                            int sent = await _localServer.SendAsync(result.Buffer, result.Buffer.Length, _sourceEndpoint).ConfigureAwait(false);
                            System.Threading.Interlocked.Add(ref _totalBytesResponded, sent);
                        }
                        catch (System.Exception ex)
                        {
                            if (_isRunning)
                            {
                                System.Console.WriteLine($"An exception occurred while receiving a server datagram : {ex}");
                            }
                        }
                    } // Whend 
                } // End Using _forwardClient 
            });

        } // End Sub Run 


        public void Stop()
        {
            try
            {
                System.Console.WriteLine($"Closed UDP {_sourceEndpoint} => {_serverLocalEndpoint} => {_forwardLocalEndpoint} => {_remoteEndpoint}. {_totalBytesForwarded} bytes forwarded, {_totalBytesResponded} bytes responded.");
                _isRunning = false;
                _forwardClient.Close();
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"An exception occurred while closing UdpConnection : {ex}");
            }
            
        } // End Sub Stop 


    } // End Class UdpConnection 


} // End Namespace 
