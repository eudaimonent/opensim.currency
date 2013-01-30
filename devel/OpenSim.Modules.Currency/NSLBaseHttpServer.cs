/*



 */




using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Reflection;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Xml;

using log4net;
using Nwc.XmlRpc;

using OpenMetaverse.StructuredData;

using HttpServer;
using CoolHTTPListener = HttpServer.HttpListener;
//using HttpListener = System.Net.HttpListener;
using LogPrio = HttpServer.LogPrio;

using OpenSim.Framework;
using OpenSim.Framework.Monitoring;
using OpenSim.Framework.Servers.HttpServer;

using NSL.Certificate.Tools;



namespace NSL.Network.HttpServer
{
    public class NSLBaseHttpServer : BaseHttpServer
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);



        public NSLBaseHttpServer(uint port) : base(port)
        {
            //m_port = port;
        }

        public NSLBaseHttpServer(uint port, bool ssl) : base(port, ssl)
        {
            //m_ssl = ssl;
        }

/*
        public NSLBaseHttpServer(uint port, bool ssl, uint sslport, string CN) : base(port, ssl, sslport, CN)
        {
            if (m_ssl)
            {
                m_sslport = sslport;
            }
        }
*/

        public NSLBaseHttpServer(uint port, bool ssl, string CPath, string CPass) : base(port, ssl, CPath, CPass)
        {
        }

    }




	///
	///
	///
	public class NSLHttpContextFactory : HttpContextFactory
	{

		public NSLHttpContextFactory(ILogWriter writer, int bufferSize, IRequestParserFactory factory) : base(writer, bufferSize, factory)
		{
		}



        public new IHttpClientContext CreateSecureContext(Socket socket, X509Certificate certificate, SslProtocols protocol)
        {
			var networkStream = new ReusableSocketNetworkStream(socket, true);
            var remoteEndPoint = (IPEndPoint) socket.RemoteEndPoint;

            var sslStream = new SslStream(networkStream, false);
            try {
                sslStream.AuthenticateAsServer(certificate, false, protocol, false);
                return CreateContext(true, remoteEndPoint, sslStream, socket);
            }
            catch (IOException err) {
                //if (UseTraceLogs)
                //    _logWriter.Write(this, LogPrio.Trace, err.Message);
            }
            catch (ObjectDisposedException err) {
                //if (UseTraceLogs)
                //    _logWriter.Write(this, LogPrio.Trace, err.Message);
            }

            return null;
        }

	}



	///
	///
	///
	internal class ReusableSocketNetworkStream : NetworkStream
	{
	    private bool disposed = false;

		//
		public ReusableSocketNetworkStream(Socket socket) : base(socket)
		{
		}

		//
		public ReusableSocketNetworkStream(Socket socket, bool ownsSocket) : base(socket, ownsSocket)
		{
		}

		//
		public ReusableSocketNetworkStream(Socket socket, FileAccess access) : base(socket, access)
		{
		}

		//
		public ReusableSocketNetworkStream(Socket socket, FileAccess access, bool ownsSocket) : base(socket, access, ownsSocket)
		{
		}


		//
		public override void Close()
		{
			if (Socket != null && Socket.Connected) Socket.Close(); 
			base.Close();
		}


		//
		protected override void Dispose(bool disposing)
		{
            try {
            	if (!disposed) {
                	disposed = true;
                	if (Socket != null && Socket.Connected) Socket.Disconnect(true);
            	}
            	base.Dispose(disposing);
			}
            catch { } // Best effort, ignore fails
		}

	}


}
