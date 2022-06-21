using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleUdp
{
    /// <summary>
    /// UDP endpoint, both client and server.
    /// </summary>
    public class UdpEndpoint : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Event to fire when a new endpoint is detected.
        /// </summary>
        public event EventHandler<EndPoint> EndpointDetected;

        /// <summary>
        /// Event to fire when a datagram is received.
        /// </summary>
        public event EventHandler<Datagram> DatagramReceived;

        /// <summary>
        /// Retrieve a list of remote endpoints.
        /// </summary>
        public List<EndPoint> Endpoints { get; } = new List<EndPoint>();

        /// <summary>
        /// Maximum datagram size, must be greater than zero and less than or equal to 65507.
        /// </summary>
        public int MaxDatagramSize
        {
            get => _maxDatagramSize;
            set
            {
                if (value < 1 || value > 65507) throw new ArgumentException("MaxDatagramSize must be greater than zero and less than or equal to 65507.");
                _maxDatagramSize = value;
            }
        }

        /// <summary>
        /// Events.
        /// </summary>
        public SimpleUdpEvents Events
        {
            get => _events;
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Events));
                _events = value;
            }
        }
        
        public EndPoint LocalEndPoint => _socket.LocalEndPoint;

        public bool Disposed { get; private set; }
        public bool Started { get; private set; }

        /// <summary>
        /// Time to live, the maximum number of routers the packet is allowed to traverse.
        /// </summary>
        public short Ttl
        {
            get => _socket.Ttl;
            set => _socket.Ttl = value;
        }

        #endregion

        #region Private-Members
        
        private Socket _socket = null;
        private int _maxDatagramSize = 65507;
        private AsyncCallback _receiveCallback = null;

        private SimpleUdpEvents _events = new SimpleUdpEvents();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the UDP endpoint with default Ttl = 64.
        /// If you wish to also receive datagrams, set the 'DatagramReceived' event and call 'Start()'.
        /// </summary>
        /// <param name="hostname">Local hostname.</param>
        /// <param name="port">Local port number.</param>
        public UdpEndpoint(string hostname, int port = 0) : this(new UdpClient(hostname, port))
        {
            Ttl = 64;
        }

        /// <summary>
        /// Instantiate the UDP endpoint with default Ttl = 64.
        /// If you wish to also receive datagrams, set the 'DatagramReceived' event and call 'Start()'.
        /// </summary>
        /// <param name="port">Local port number.</param>
        public UdpEndpoint(int port = 0) : this(new UdpClient(port))
        {
            Ttl = 64;
        }

        /// <summary>
        /// Use existing Socket from UdpClient.
        /// If you wish to also receive datagrams, set the 'DatagramReceived' event and call 'Start()'.
        /// </summary>
        public UdpEndpoint(UdpClient udpClient) : this(udpClient.Client)
        {
            _socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress, true);
        }

        /// <summary>
        /// Use existing Socket.
        /// If you wish to also receive datagrams, set the 'DatagramReceived' event and call 'Start()'.
        /// </summary>
        public UdpEndpoint(Socket socket)
        {
            _socket = socket;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        /// <param name="disposing">Disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (Disposed) return;

            if (disposing)
            {
                Disposed = true;
                try
                {
                    _socket.Dispose();
                } finally
                {
                    Started = false;
                }
            }
        }


        /// <summary>
        /// Start the UDP listener to receive datagrams.  Before calling this method, set the 'DatagramReceived' event.
        /// </summary>
        /// <exception cref="InvalidOperationException">Already started</exception>
        public void Start()
        {
            if (Started) throw new InvalidOperationException("Already started");

            Started = true;
            _events.HandleStarted(this);

            BeginReceive();

            new Thread(() =>
            {
                try
                {
                    while (Started)
                    {
                        BeginReceive();
                    }
                } catch(Exception)
                {
                    _events.HandleStopped(this);

                }
            }).Start();

        }

        private void BeginReceive()
        {
            var state = new Datagram(_maxDatagramSize);
            _socket.BeginReceiveFrom(state.data, 0, _maxDatagramSize, SocketFlags.None, ref state.endPoint, EndReceive, state);
        }

        private void EndReceive(IAsyncResult result)
        {
            var state = (Datagram)result.AsyncState;

            state.dataSize = _socket.EndReceiveFrom(result, ref state.endPoint);

            if (!Endpoints.Contains(state.endPoint))
            {
                Endpoints.Add(state.endPoint);
                EndpointDetected?.Invoke(this, state.endPoint);
            }

            DatagramReceived?.Invoke(this, state);
        }

        /// <summary>
        /// Stop the UDP listener.
        /// </summary>
        public void Stop()
        {
            try
            {
                _socket.Close();
            } finally
            {
                Started = false;
            }
        }

        /// <summary>
        /// Send a datagram to the specific IP address and UDP port.
        /// This will throw a SocketException if the report UDP port is unreachable.
        /// </summary>
        /// <param name="ip">IP address.</param>
        /// <param name="port">Port.</param>
        /// <param name="text">Text to send.</param>
        public void Send(string ip, int port, string text)
        {
            byte[] data = Encoding.UTF8.GetBytes(text);
            SendInternal(
                new IPEndPoint(IPAddress.Parse(ip), port),
                data,
                0,
                data.Length); 
        }

        /// <summary>
        /// Send a datagram to the specific IP address and UDP port.
        /// This will throw a SocketException if the report UDP port is unreachable.
        /// </summary>
        /// <param name="ipe">Remote end point.</param>
        /// <param name="text">Text to send.</param>
        public void Send(EndPoint ipe, string text)
        {
            byte[] data = Encoding.UTF8.GetBytes(text);
            SendInternal(
                ipe,
                data,
                0,
                data.Length);
        }

        /// <summary>
        /// Send a datagram to the specific IP address and UDP port.
        /// This will throw a SocketException if the report UDP port is unreachable.
        /// </summary>
        /// <param name="ip">IP address.</param>
        /// <param name="port">Port.</param>
        /// <param name="data">Bytes.</param>
        public void Send(string ip, int port, byte[] data)
        {
            SendInternal(
                new IPEndPoint(IPAddress.Parse(ip), port),
                data,
                0,
                data.Length);
        }

        /// <summary>
        /// Send a datagram to the specific IP address and UDP port.
        /// This will throw a SocketException if the report UDP port is unreachable.
        /// </summary>
        /// <param name="ipe">Remote end point.</param>
        /// <param name="data">Bytes.</param>
        public void Send(EndPoint ipe, byte[] data)
        {
            SendInternal(
                ipe,
                data,
                0,
                data.Length);
        }

        /// <summary>
        /// Send a datagram to the specific IP address and UDP port with specific offset and size.
        /// This will throw a SocketException if the report UDP port is unreachable.
        /// </summary>
        /// <param name="ip">IP address.</param>
        /// <param name="port">Port.</param>
        /// <param name="data">Bytes.</param>
        public void SendOffset(string ip, int port, byte[] data, int offset, int size)
        {
            SendInternal(
                new IPEndPoint(IPAddress.Parse(ip), port),
                data,
                offset,
                size);
        }

        /// <summary>
        /// Send a datagram to the specific IP address and UDP port with specific offset and size.
        /// This will throw a SocketException if the report UDP port is unreachable.
        /// </summary>
        /// <param name="ipe">Remote end point.</param>
        /// <param name="data">Bytes.</param>
        public void SendOffset(EndPoint ipe, byte[] data, int offset, int size)
        {
            SendInternal(
                ipe,
                data,
                offset,
                size);
        }

        /// <summary>
        /// Send a datagram asynchronously to the specific IP address and UDP port.
        /// This will throw a SocketException if the report UDP port is unreachable.
        /// </summary>
        /// <param name="ip">IP address.</param>
        /// <param name="port">Port.</param>
        /// <param name="text">Text to send.</param>
        public async Task SendAsync(string ip, int port, string text)
        {
            byte[] data = Encoding.UTF8.GetBytes(text);
            await SendInternalAsync(
                new IPEndPoint(IPAddress.Parse(ip), port),
                data,
                0,
                data.Length)
            .ConfigureAwait(false);
        }

        /// <summary>
        /// Send a datagram asynchronously to the specific IP address and UDP port.
        /// This will throw a SocketException if the report UDP port is unreachable.
        /// </summary>
        /// <param name="ipe">Remote end point.</param>
        /// <param name="text">Text to send.</param>
        public async Task SendAsync(EndPoint ipe, string text)
        {
            byte[] data = Encoding.UTF8.GetBytes(text);
            await SendInternalAsync(
                ipe,
                data,
                0,
                data.Length)
            .ConfigureAwait(false);
        }

        /// <summary>
        /// Send a datagram asynchronously to the specific IP address and UDP port.
        /// This will throw a SocketException if the report UDP port is unreachable.
        /// </summary>
        /// <param name="ip">IP address.</param>
        /// <param name="port">Port.</param>
        /// <param name="data">Bytes.</param> 
        public async Task SendAsync(string ip, int port, byte[] data)
        {
            await SendInternalAsync(
                new IPEndPoint(IPAddress.Parse(ip), port),
                data,
                0,
                data.Length)
            .ConfigureAwait(false);
        }

        /// <summary>
        /// Send a datagram asynchronously to the specific IP address and UDP port.
        /// This will throw a SocketException if the report UDP port is unreachable.
        /// </summary>
        /// <param name="ipe">Remote end point.</param>
        /// <param name="data">Bytes.</param> 
        public async Task SendAsync(EndPoint ipe, byte[] data)
        {
            await SendInternalAsync(
                ipe,
                data,
                0,
                data.Length)
            .ConfigureAwait(false);
        }

        /// <summary>
        /// Send a datagram asynchronously to the specific IP address and UDP port with specific offset and size.
        /// This will throw a SocketException if the report UDP port is unreachable.
        /// </summary>
        /// <param name="ip">IP address.</param>
        /// <param name="port">Port.</param>
        /// <param name="data">Bytes.</param> 
        public async Task SendOffsetAsync(string ip, int port, byte[] data, int offset, int size)
        {
            await SendInternalAsync(
                new IPEndPoint(IPAddress.Parse(ip), port),
                data,
                offset,
                size)
            .ConfigureAwait(false);
        }

        /// <summary>
        /// Send a datagram asynchronously to the specific IP address and UDP port with specific offset and size.
        /// This will throw a SocketException if the report UDP port is unreachable.
        /// </summary>
        /// <param name="ipe">Remote end point.</param>
        /// <param name="data">Bytes.</param> 
        public async Task SendOffsetAsync(EndPoint ipe, byte[] data, int offset, int size)
        {
            await SendInternalAsync(
                ipe,
                data,
                offset,
                size)
            .ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        private void SendInternal(EndPoint ipe, byte[] data, int offset, int size)
        {
            if (data == null || data.Length == 0) throw new ArgumentNullException(nameof(data));
            if (size > _maxDatagramSize) throw new ArgumentException("Data exceed maximum datagram size (" + data.Length + " data bytes, " + _maxDatagramSize + " bytes).");

            _socket.SendTo(data, offset, size, SocketFlags.None, ipe);
        }

        private async Task SendInternalAsync(EndPoint ipe, byte[] data, int offset, int size)
        {
            if (data == null || data.Length == 0) throw new ArgumentNullException(nameof(data));
            if (size > _maxDatagramSize) throw new ArgumentException("Data exceed maximum datagram size (" + data.Length + " data bytes, " + _maxDatagramSize + " bytes).");

            await _socket.SendToAsync(new ArraySegment<byte>(data, offset, size), SocketFlags.None, ipe).ConfigureAwait(false);
        }

        #endregion
    }
}