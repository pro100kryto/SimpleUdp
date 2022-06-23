using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using SimpleUdp;

namespace Node
{
    class Program
    {
        static UdpEndpoint _udpEndpoint;

        static void Main(string[] args)
        {
            if (args != null && args.Length>0)
            {
                if ("--help".Equals(args[0]))
                {
                    Usage();
                    return;
                }
                int port = args.Length >= 2 ? int.Parse(args[1]) : 0;
                InitEndPoint(args[0], port);
            }
            
            Console.WriteLine("[" + (_udpEndpoint!= null ? _udpEndpoint.Socket.LocalEndPoint.ToString() : "") + " Command/? for help ]: ");
            Console.WriteLine();

            while (true)
            {
                try {
                    Console.Write("> ");
                    string userInput = Console.ReadLine();
                    if (String.IsNullOrEmpty(userInput)) continue;

                    if (userInput.Equals("?"))
                    {
                        Menu();
                    }
                    else if (userInput.Equals("q"))
                    {
                        Environment.Exit(0);
                    }
                    else if (userInput.Equals("cls"))
                    {
                        Console.Clear();
                    }
                    else if (userInput.StartsWith("bind"))
                    {
                        if (_udpEndpoint != null && !_udpEndpoint.Disposed) throw new Exception("Already bind");
                        var split = userInput.Split(' ');
                        int port = split.Length < 2 ? 0 : int.Parse(split[1]);
                        string ip = split.Length < 3 ? "127.0.0.1" : split[2];
                        InitEndPoint(ip, port);
                        Console.WriteLine(_udpEndpoint.Socket.LocalEndPoint.ToString());
                    }
                    else if (userInput.Equals("list"))
                    {
                        List<EndPoint> recents = _udpEndpoint.EndPoints;
                        if (recents != null)
                        {
                            Console.WriteLine("Recent endpoints");
                            foreach (var endpoint in recents) Console.WriteLine("  " + endpoint.ToString());
                        }
                        else
                        {
                            Console.WriteLine("None");
                        }
                    }
                    else if (userInput.Equals("start"))
                    {
                        _udpEndpoint.Start();
                    }
                    else if (userInput.Equals("stop"))
                    {
                        _udpEndpoint.Stop();
                    }
                    else if (userInput.Equals("dispose"))
                    {
                        _udpEndpoint.Dispose();
                    }
                    else if (userInput.Equals("status"))
                    {
                        if (_udpEndpoint == null)
                        {
                            Console.WriteLine("not bind");
                        }
                        else
                        if (_udpEndpoint.Disposed)
                        {
                            Console.WriteLine("disposed");
                        }
                        else
                        {
                            Console.WriteLine(_udpEndpoint.Started ? "started" : "stopped");
                        }
                    }
                    else if (userInput.StartsWith("echo"))
                    {
                        var split = userInput.Split(' ');
                        var msg = split[1];
                        _udpEndpoint.Send(_udpEndpoint.Socket.LocalEndPoint, msg);
                    }
                    else if (userInput.StartsWith("sendL"))
                    {
                        var split = userInput.Split(' ');

                        var port = int.Parse(split[1]);
                        var msg = split[2];
                        _udpEndpoint.Send(_udpEndpoint.Socket.LocalEndPoint.ToString().Split(':')[0], port, msg);
                    }
                    else if (userInput.StartsWith("send"))
                    {
                        var split = userInput.Split(' ');

                        var ip = split[1];
                        var port = int.Parse(split[2]);
                        var msg = split[3];
                        _udpEndpoint.Send(ip, port, msg);
                    }
                    else if (userInput.Length != 0)
                    {
                        Console.WriteLine("Unknown command!");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error: "+ex.Message);
                }
            }

            Console.ReadLine();
        }

        static void InitEndPoint(string ip, int port)
        {
            _udpEndpoint = new UdpEndpoint(ip, port);
            _udpEndpoint.EndpointDetected += EndpointDetected;
            _udpEndpoint.DatagramReceived += DatagramReceived;
            _udpEndpoint.Events.Started += NodeStarted;
            _udpEndpoint.Events.Stopped += NodeStopped;
            Console.Title = "SimpleUdp " + _udpEndpoint.Socket.LocalEndPoint.ToString();
        }

        static void Usage()
        {
            Console.WriteLine("");
            Console.WriteLine("Usage:");
            Console.WriteLine("# node");
            Console.WriteLine("# node 127.0.0.1 0");
        }

        static void Menu()
        {
            Console.WriteLine("");
            Console.WriteLine("Available commands");
            Console.WriteLine("  q              quit");
            Console.WriteLine("  ?              help, this menu");
            Console.WriteLine("  cls            clear the screen");
            Console.WriteLine("  bind           create socket and bind 127.0.0.1");
            Console.WriteLine("  bind [port]        create socket and bind an spicific ip");
            Console.WriteLine("  bind [port] [ip]   create socket and bind an specifics ip and port number");
            Console.WriteLine("  list           list recent endpoints");
            Console.WriteLine("  start          start the endpoint");
            Console.WriteLine("  stop           stop the endpoint");
            Console.WriteLine("  dispose        close socket");
            Console.WriteLine("  status         show endpoint listener status");
            Console.WriteLine("  echo [msg]     send msg to yourself");
            Console.WriteLine("  sendL [port] [msg]     send msg over localhost");
            Console.WriteLine("  send [ip] [port] [msg] send msg to cpecific endpoint");
            Console.WriteLine("");
        }

        static void EndpointDetected(object sender, EndPoint ep)
        {
            Console.WriteLine("Endpoint detected: " + ep.ToString());
        }

        static void DatagramReceived(object sender, Datagram dg)
        {
            Console.WriteLine("[" + dg.endPoint.ToString() + "]: " + Encoding.UTF8.GetString(dg.data));
        }

        private static void NodeStarted(object sender, EventArgs e)
        {
            Console.WriteLine("*** Node started");
        }

        private static void NodeStopped(object sender, EventArgs e)
        {
            Console.WriteLine("*** Node stopped");
        }
    }
}
