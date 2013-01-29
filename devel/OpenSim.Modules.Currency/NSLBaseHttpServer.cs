/*



 */




using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Reflection;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Xml;
using HttpServer;
using log4net;
using Nwc.XmlRpc;

using OpenMetaverse.StructuredData;
using CoolHTTPListener = HttpServer.HttpListener;
using HttpListener = System.Net.HttpListener;
using LogPrio = HttpServer.LogPrio;

using OpenSim.Framework;
using OpenSim.Framework.Monitoring;
using OpenSim.Framework.Servers.HttpServer;



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

        public NSLBaseHttpServer(uint port, bool ssl, uint sslport, string CN) : base(port, ssl, sslport, CN)
        {
            if (m_ssl)
            {
                m_sslport = sslport;
                m_cert = new X509Certificate2(CN);
            }
        }

        public NSLBaseHttpServer(uint port, bool ssl, string CPath, string CPass) : base(port, ssl, CPath, CPass)
        {
        }



        protected override void StartHTTP()
        {
            m_log.InfoFormat(
                "[NSL BASE HTTP SERVER]: Starting {0} server on port {1}", UseSSL ? "HTTPS" : "HTTP", Port);

            try
            {
                //m_httpListener = new HttpListener();

                NotSocketErrors = 0;
                if (!m_ssl)
                {
                    //m_httpListener.Prefixes.Add("http://+:" + m_port + "/");
                    //m_httpListener.Prefixes.Add("http://10.1.1.5:" + m_port + "/");
                    m_httpListener2 = CoolHTTPListener.Create(m_listenIPAddress, (int)m_port);
                    m_httpListener2.ExceptionThrown += httpServerException;
                    m_httpListener2.LogWriter = httpserverlog;

                    // Uncomment this line in addition to those in HttpServerLogWriter
                    // if you want more detailed trace information from the HttpServer
                    //m_httpListener2.UseTraceLogs = true;

                    //m_httpListener2.DisconnectHandler = httpServerDisconnectMonitor;
                }
                else
                {
                    //m_httpListener.Prefixes.Add("https://+:" + (m_sslport) + "/");
                    //m_httpListener.Prefixes.Add("http://+:" + m_port + "/");
                    m_httpListener2 = CoolHTTPListener.Create(IPAddress.Any, (int)m_port, m_cert);
                    m_httpListener2.ExceptionThrown += httpServerException;
                    m_httpListener2.LogWriter = httpserverlog;
                }

                m_httpListener2.RequestReceived += OnRequest;
                //m_httpListener.Start();
                m_httpListener2.Start(64);

                // Long Poll Service Manager with 3 worker threads a 25 second timeout for no events
                m_PollServiceManager = new PollServiceRequestManager(this, 3, 25000);
                HTTPDRunning = true;

                //HttpListenerContext context;
                //while (true)
                //{
                //    context = m_httpListener.GetContext();
                //    ThreadPool.UnsafeQueueUserWorkItem(new WaitCallback(HandleRequest), context);
               // }
            }
            catch (Exception e)
            {
                m_log.Error("[NSL BASE HTTP SERVER]: Error - " + e.Message);
                m_log.Error("[NSL BASE HTTP SERVER]: Tip: Do you have permission to listen on port " + m_port + ", " + m_sslport + "?");

                // We want this exception to halt the entire server since in current configurations we aren't too
                // useful without inbound HTTP.
                throw e;
            }
        }

    }




}
