/*
 *
 */


/*
Memo

Aurora/Simulation/Base/SimulationBase.cs: SimulationBase

BaseApplication.Startup()
{
    simBase.Initialize();       // simBase => SimulationBase
    simBase.Startup();
    simBase.Run();
}
*/


using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Reflection;
using System.Threading;
using System.Timers;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
//using System.Net;
//using System.Net.Sockets;
//using Timer=System.Timers.Timer;
//using CoolHTTPListener = HttpServer.HttpListener;

using Nini.Config;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Repository;

using Aurora.Framework;
using Aurora.Framework.Servers.HttpServer;
using Aurora.Simulation.Base;



namespace Aurora.Server.MoneyServer
{
    public class AuroraMoneyBase : SimulationBase, IMoneyServiceCore
    {
		private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private string m_VersionNum = "0.1";

        protected CommandConsole  m_console;
        protected OpenSimAppender m_consoleAppender;
        protected IAppender m_logFileAppender = null;

        protected string m_startupDirectory = Environment.CurrentDirectory;

		private string connectionString = string.Empty;
		private uint m_moneyServerPort = 8008;
		private string m_hostName = "localhost";
		private BaseHttpServer  m_httpServer;
		private bool   m_useHTTPS = true;

		private string m_certFilename	 = "";
		private string m_certPassword	 = "";
		private bool   m_checkClientCert = false;
		private X509Certificate2 m_cert  = null;

		private int DEAD_TIME;
		private int MAX_DB_CONNECTION;

		private MoneyXmlRpcModule m_moneyXmlRpcModule;
		private MoneyDBService m_moneyDBService;

		private Dictionary<string, string> m_sessionDic = new Dictionary<string, string>();
		private Dictionary<string, string> m_secureSessionDic = new Dictionary<string, string>();
		private Dictionary<string, string> m_webSessionDic = new Dictionary<string, string>();

		protected IStatsCollector m_stats;

		IConfig m_moneyConfig;
		IConfig m_startConfig;
		IConfig m_DBConfig;



		//
        public override ISimulationBase Copy()
        {
            return new AuroraMoneyBase();
        }



		//
		public override void Initialize(IConfigSource originalConfig, IConfigSource configSource, string[] cmdParams, ConfigurationLoader configLoader)
		{
			m_log.Info("[MONEY SERVER]: Initialize.");

            m_StartupTime = DateTime.Now;
            m_version = VersionInfo.Version + " (" + Util.GetRuntimeInformation() + ")";
            m_config = configSource;

            //m_commandLineParameters = cmdParams;
            //m_version = m_VersionNum;
            //m_original_config = originalConfig;
            //m_configurationLoader = configLoader;

			//
            string iniFile = "MoneyServer.ini";
            if (configLoader.defaultIniFile!="") iniFile = configLoader.defaultIniFile;
            ReadIniConfig(iniFile);
		}



        public override void Startup()
        {
			m_log.Info("[MONEY SERVER]: Startup.");

			SetUpConsole();

            SetUpHTTPServer();

            SetupMoneyServices();
        }




        public override void SetUpHTTPServer()
        {
            try {
                if (m_certFilename!="" && m_certPassword!="") {
					m_useHTTPS = true;
                    m_httpServer = new BaseHttpServer(m_moneyServerPort, m_hostName, m_useHTTPS);
                    m_httpServer.SetSecureParams(m_certFilename, m_certPassword, SslProtocols.Tls);
                	m_log.Info("[MONEY SERVER]: HTTPS: Secure TLS ");
                }
                else {
					m_useHTTPS = false;
                    m_httpServer = new BaseHttpServer(m_moneyServerPort, m_hostName, m_useHTTPS);
                	m_log.Info("[MONEY SERVER]: HTTP: Non Secure");
                }

                m_httpServer.Start();
            	MainServer.Instance = m_httpServer;
            }
            //
            catch (Exception e) {
                m_log.ErrorFormat("[MONEY SERVER]: SetUpHTTPServer: Fail to start HTTP/HTTPS process");
                m_log.ErrorFormat("[MONEY SERVER]: SetUpHTTPServer: {0}", e);
                Environment.Exit(1);
            }
        }




        protected void SetUpConsole()
        {
            m_console = new LocalConsole();
            m_console.DefaultPrompt = "Money ";
			MainConsole.Instance = m_console;

            if (m_console!=null)
            {
                ILoggerRepository repository = LogManager.GetRepository();
                IAppender[] appenders = repository.GetAppenders();

				// Console
                foreach (IAppender appender in appenders) {
                    if (appender.Name == "Console") {
                        m_consoleAppender = (OpenSimAppender)appender;
                        break;
                    }
                }

                if (m_consoleAppender!=null) {
                    m_consoleAppender.Console = m_console;
                    
                    if (null == m_consoleAppender.Threshold) m_consoleAppender.Threshold = Level.All;
                    repository.Threshold = m_consoleAppender.Threshold;

                    foreach (ILogger log in repository.GetCurrentLoggers()) {
                        log.Level = m_consoleAppender.Threshold;
                    }
                    MainConsole.Instance.MaxLogLevel = m_consoleAppender.Threshold;
                }
               
				// Log
            	IAppender logFileAppender = null;
	            foreach (IAppender appender in appenders) {
                	if (appender.Name == "LogFileAppender") {
	                    logFileAppender = appender;
            	    }
           	 	}

            	if (logFileAppender!=null) {
                	if (logFileAppender is FileAppender) {
                    	FileAppender appender = (FileAppender)logFileAppender;
                    	string fileName = m_startConfig.GetString("LogFile", String.Empty);
                    	if (fileName != String.Empty) {
                        	appender.File = fileName;
                        	appender.ActivateOptions();
                    	}
                	}
           	 	}
            	if (MainConsole.Instance==null)
            	{
                	m_log.Info("[Console]: No Console located");
                	return;
            	}

				//
        		RegisterConsoleCommands();
			}
		}





		// read MoneyServer.ini
		protected void ReadIniConfig(string inifile)
		{
			MoneyServerConfigSource configSource = new MoneyServerConfigSource(inifile);

			try {
				// [Startup]
				m_startConfig = configSource.m_config.Configs["Startup"];
				string PIDFile = m_startConfig.GetString("PIDFile", "");
				if (PIDFile!="") CreatePIDFile(PIDFile);

				// [MySql]
				m_DBConfig = configSource.m_config.Configs["MySql"];
				string sqlserver  = m_DBConfig.GetString("hostname", "localhost");
				string database   = m_DBConfig.GetString("database", "Aurora");
				string username   = m_DBConfig.GetString("username", "root");
				string password   = m_DBConfig.GetString("password", "password");
				string pooling 	  = m_DBConfig.GetString("pooling",  "false");
				string port 	  = m_DBConfig.GetString("port", 	"3306");
				MAX_DB_CONNECTION = m_DBConfig.GetInt   ("MaxConnection", 10);

				connectionString  = "Server=" + sqlserver + ";Port=" + port + ";Database=" + database + ";User ID=" +
												username + ";Password=" + password + ";Pooling=" + pooling + ";";

				// [MoneyServer]
				m_moneyConfig   = configSource.m_config.Configs["MoneyServer"];
				DEAD_TIME  = m_moneyConfig.GetInt   ("ExpiredTime", 120);
				m_hostName = m_moneyConfig.GetString("HostName", "localhost");

				string checkcert = m_moneyConfig.GetString("CheckClientCert", "false");
				if (checkcert.ToLower()=="true") m_checkClientCert = true;

				m_certFilename = m_moneyConfig.GetString("ServerCertFilename", "SineWaveCert.pfx");
				m_certPassword = m_moneyConfig.GetString("ServerCertPassword", "123");
				
				//if (m_certFilename!="" && m_certPassword!="")
				//{
				//	m_cert = new X509Certificate2(m_certFilename, m_certPassword);
				//}
			}
			catch (Exception)
			{
				m_log.Error("[MONEY SERVER]: ReadIniConfig: Fail to setup configure. Please check MoneyServer.ini. Exit");
				Environment.Exit(1);
			}
		}



		//
		// connect to MySQL server
		//
		protected virtual void SetupMoneyServices()
		{
			m_log.Info("[DATA]: Connecting to Money Storage Server");

			m_moneyDBService = new MoneyDBService();
			m_moneyDBService.Initialise(connectionString, MAX_DB_CONNECTION);
			m_moneyXmlRpcModule = new MoneyXmlRpcModule();
			m_moneyXmlRpcModule.Initialise(m_version, m_moneyConfig, m_moneyDBService, this);
			m_moneyXmlRpcModule.PostInitialise();
		}


       //
        public void Notice(string format, params string[] components)
        {
            if (m_console!=null) m_console.Output(string.Format(format, components));
        }



		//
		// for IMoneyServiceCore
		//
        //public IHttpServer GetHttpServer()
        public BaseHttpServer GetHttpServer()
        {
			return m_httpServer;
        }


        public Dictionary<string, string> GetSessionDic()
        {
            return m_sessionDic;
        }


        public Dictionary<string, string> GetSecureSessionDic()
        {
            return m_secureSessionDic;
        }


        public Dictionary<string, string> GetWebSessionDic()
        {
            return m_webSessionDic;
        }




		//
		// Command Handler
		//

        public override void RegisterConsoleCommands()
        {
            if (MainConsole.Instance!=null) {
                m_console.Commands.AddCommand("quit", "quit", "Quit the application", HandleQuit);
                m_console.Commands.AddCommand("shutdown", "shutdown", "Quit the application", HandleQuit);
                m_console.Commands.AddCommand("set log level", "set log level <level>", "Set the console logging level", HandleLogLevel);
                m_console.Commands.AddCommand("show info", "show info", "Show general information about the server", HandleShow);
                m_console.Commands.AddCommand("show stats", "show stats", "Show statistics", HandleShow);
                m_console.Commands.AddCommand("show threads", "show threads", "Show thread status", HandleShow);
                m_console.Commands.AddCommand("show uptime", "show uptime", "Show server uptime", HandleShow);
                m_console.Commands.AddCommand("show version", "show version", "Show server version", HandleShow);
                m_console.Commands.AddCommand("help", "help", "show help menu", ShowHelp);
			}

        }


        protected void ShowHelp(string[] args)
        {
            Notice("");

            Notice("set log level <level> - change the console logging level only.  For example, OFF or DEBUG.");
            Notice("show info    - show server information (e.g. startup path).");
            Notice("show stats   - show server information (e.g. startup path).");
            if (m_stats!=null) Notice("show stats   - show statistical information for this server");
            Notice("show threads - list tracked threads");
            Notice("show uptime  - show server startup time and uptime.");
            Notice("show version - show server version.");
            Notice("");

            return;
        }



        public void HandleQuit(string[] args)
        {
            Shutdown(true);
        }



		//
        public void HandleShow(string[] cmd)
        {
            List<string> args = new List<string>(cmd);
            args.RemoveAt(0);
            string[] showParams = args.ToArray();

            switch (showParams[0]) {
                case "info":
                    ShowInfo();
                    break;
                case "stats":
                    if (m_stats != null) Notice(m_stats.Report());
                    break;

                case "threads":
                    Notice(GetThreadsReport());
                    break;

                case "uptime":
                    Notice(GetUptimeReport());
                    break;

                case "version":
            		Notice(GetVersionText());
                    break;
            }
        }



        protected void ShowInfo()
        {
            Notice(GetVersionText());
            Notice("Startup directory: " + m_startupDirectory);
            if (null != m_consoleAppender) Notice(String.Format("Console log level: {0}", m_consoleAppender.Threshold));
        }
       

        protected string GetVersionText()
        {
            return String.Format("Version: {0}", m_version);
        }


        protected string GetThreadsReport()
        {
            StringBuilder sb = new StringBuilder();

            ProcessThreadCollection threads = GetThreads();
            if (threads == null) {
                sb.Append("OpenSim thread tracking is only enabled in DEBUG mode.");
            }
            else {
                sb.Append(threads.Count + " threads are being tracked:" + Environment.NewLine);
                foreach (ProcessThread t in threads)
                {
                    sb.Append("ID: " + t.Id + ", TotalProcessorTime: " + t.TotalProcessorTime + ", TimeRunning: " +
                        		(DateTime.Now - t.StartTime) + ", Pri: " + t.CurrentPriority + ", State: " + t.ThreadState);
                    if (t.ThreadState == System.Diagnostics.ThreadState.Wait) {
                        sb.Append(", Reason: " + t.WaitReason + Environment.NewLine);
					}
                    else {
                        sb.Append(Environment.NewLine);
					}
                }
            }
 
            int workers = 0, ports = 0, maxWorkers = 0, maxPorts = 0;
            ThreadPool.GetAvailableThreads(out workers, out ports);
            ThreadPool.GetMaxThreads(out maxWorkers, out maxPorts);

            sb.Append(Environment.NewLine + "*** ThreadPool threads ***"  + Environment.NewLine);
            sb.Append("workers: " + (maxWorkers - workers) + " (" + maxWorkers + "); ports: " + (maxPorts - ports) + " (" + maxPorts + ")" + Environment.NewLine);

            return sb.ToString();
        }


        public ProcessThreadCollection GetThreads()
        {
            Process thisProc = Process.GetCurrentProcess();
            return thisProc.Threads;
        }


        protected string GetUptimeReport()
        {
            StringBuilder sb = new StringBuilder(String.Format("Time now is {0}\n", DateTime.Now));
            sb.Append(String.Format("Server has been running since {0}, {1}\n", m_StartupTime.DayOfWeek, m_StartupTime));
            sb.Append(String.Format("That is an elapsed time of {0}\n", DateTime.Now - m_StartupTime));

            return sb.ToString();
        }



        private void HandleLogLevel(string[] cmd)
        {
            if (null == m_consoleAppender) {
                Notice("No appender named Console found (see the log4net config file for this executable)!");
                return;
            }
     
            if (cmd.Length > 3) {
                string rawLevel = cmd[3];
               
                ILoggerRepository repository = LogManager.GetRepository();
                Level consoleLevel = repository.LevelMap[rawLevel];
               
                if (consoleLevel != null) {
                    m_consoleAppender.Threshold = consoleLevel;
				}
                else {
                    Notice(String.Format("{0} is not a valid logging level.  Valid logging levels are ALL, DEBUG, INFO, WARN, ERROR, FATAL, OFF", rawLevel));
				}
            }

            Notice(String.Format("Console log level is {0}", m_consoleAppender.Threshold));
        }

    }





	//
	//
	//
	class MoneyServerConfigSource
	{
		public IniConfigSource m_config;

		public MoneyServerConfigSource(string filename)
		{
			string configPath = Path.Combine(Directory.GetCurrentDirectory(), filename);
			if (File.Exists(configPath))
			{
				m_config = new IniConfigSource(configPath, Nini.Ini.IniFileType.AuroraStyle);
			}
			else
			{
				//TODO: create default configuration.
				//m_moneyConfig = DefaultConfig();
			}
		}

		public void Save(string path)
		{
			m_config.Save(path);
		}

	}



}



