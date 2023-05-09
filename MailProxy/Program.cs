#nullable enable

namespace NetProxy
{


    internal static class Program
    {


        // 143 IMAP, 110 POP3
        private static async System.Threading.Tasks.Task Main(string[] args)
        {
            try
            {
                string configJson = System.IO.File.ReadAllText("config.json");

                System.Collections.Generic.Dictionary<string, ProxyConfig>? configs = 
                    System.Text.Json.JsonSerializer.Deserialize<
                        System.Collections.Generic.Dictionary<string, ProxyConfig>
                        >(configJson);

                if (configs == null)
                {
                    throw new System.Exception("configs is null");
                }


                System.Collections.Generic.List<System.Threading.Tasks.Task> tasks = 
                    new System.Collections.Generic.List<System.Threading.Tasks.Task>();

                foreach (System.Collections.Generic.KeyValuePair<string, ProxyConfig>
                    c in configs)
                {
                    tasks.AddRange(ProxyFromConfig(c.Key, c.Value));
                }

                await System.Threading.Tasks.Task.WhenAll(tasks);
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"An error occurred : {ex}");
            }

        } // End Sub Main 


        private static System.Collections.Generic.IEnumerable<System.Threading.Tasks.Task> 
            ProxyFromConfig(string proxyName, ProxyConfig proxyConfig)
        {
            ushort? forwardPort = proxyConfig.forwardPort;
            ushort? localPort = proxyConfig.localPort;
            string? forwardIp = proxyConfig.forwardIp;
            string? localIp = proxyConfig.localIp;
            string? protocol = proxyConfig.protocol;
            try
            {
                if (forwardIp == null)
                {
                    throw new System.Exception("forwardIp is null");
                }
                if (!forwardPort.HasValue)
                {
                    throw new System.Exception("forwardPort is null");
                }
                if (!localPort.HasValue)
                {
                    throw new System.Exception("localPort is null");
                }
                if (protocol != "udp" && protocol != "tcp" && protocol != "any")
                {
                    throw new System.Exception($"protocol is not supported {protocol}");
                }
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Failed to start {proxyName} : {ex.Message}");
                throw;
            }

            bool protocolHandled = false;
            if (protocol == "udp" || protocol == "any")
            {
                protocolHandled = true;
                System.Threading.Tasks.Task task;
                try
                {
                    UdpProxy? proxy = new UdpProxy();
                    task = proxy.Start(forwardIp, forwardPort.Value, localPort.Value, localIp);
                }
                catch (System.Exception ex)
                {
                    System.Console.WriteLine($"Failed to start {proxyName} : {ex.Message}");
                    throw;
                }

                yield return task;
            } // End if (protocol == "udp" || protocol == "any") 

            if (protocol == "tcp" || protocol == "any")
            {
                protocolHandled = true;
                System.Threading.Tasks.Task task;
                try
                {
                    TcpProxy? proxy = new TcpProxy();
                    task = proxy.Start(forwardIp, forwardPort.Value, localPort.Value, localIp);
                }
                catch (System.Exception ex)
                {
                    System.Console.WriteLine($"Failed to start {proxyName} : {ex.Message}");
                    throw;
                }

                yield return task;
            } // End if (protocol == "tcp" || protocol == "any") 

            if (!protocolHandled)
            {
                throw new System.InvalidOperationException($"protocol not supported {protocol}");
            } // End if (!protocolHandled) 

        } // End Task ProxyFromConfig 


    } // End Class Program 


    public class ProxyConfig
    {
        public string? protocol { get; set; }
        public ushort? localPort { get; set; }
        public string? localIp { get; set; }
        public string? forwardIp { get; set; }
        public ushort? forwardPort { get; set; }
    } // End Class ProxyConfig 


    internal interface IProxy
    {
        System.Threading.Tasks.Task Start(string remoteServerHostNameOrAddress, ushort remoteServerPort, ushort localPort, string? localIp = null);
    } // End Interface IProxy 


} // End Namespace 
