using System;
using System.Net;

namespace SimpleUdp
{
    /// <summary>
    /// Datagram received by this endpoint.
    /// </summary>
    public class Datagram
    {
        public static readonly IPEndPoint anyIPEndPoint = new IPEndPoint(IPAddress.Any, 0);

        /// <summary>
        /// remote endpoint.
        /// </summary>
        public EndPoint endPoint = anyIPEndPoint;

        /// <summary>
        /// Data received from the remote endpoint.
        /// </summary>
        public byte[] data;

        public int dataSize = 0;
        public int dataOffset = 0;

        public Datagram(int bufferSize)
        {
            data = new byte[bufferSize];
        }

        public Datagram(byte[] data)
        {
            this.data = data;
        }
    }
}
