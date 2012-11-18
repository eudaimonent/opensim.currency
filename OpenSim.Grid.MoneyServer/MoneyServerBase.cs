/*
 *
 */


/*
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
using System.Text;
using System.Reflection;
using System.Timers;
using System.Security.Authentication;

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
    public class AuroraMoneyBase : MoneySimulationBase, IMoneyServiceCore
    {
		private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected CommandConsole  m_console;
        protected OpenSimAppender m_consoleAppender;
        protected IAppender m_logFileAppender = null;

		private string connectionString = string.Empty;
		private uint m_moneyServerPort = 8008;
		private string m_hostName = "localhost";
		private BaseHttpServer  m_httpServer;

		private string m_certFilename	 = "";
		private string m_certPassword	 = "";
		private bool   m_checkClientCert = false;
		//private X509Certificate2 m_cert  = null;

		private int DEAD_TIME;
		private int MAX_DB_CONNECTION;

		private MoneyXmlRpcModule m_moneyXmlRpcModule;
		private MoneyDBService m_moneyDBService;

		private Dictionary<string, string> m_sessionDic = new Dictionary<string, string>();
		private Dictionary<string, string> m_secureSessionDic = new Dictionary<string, string>();
		private Dictionary<string, string> m_webSessionDic = new Dictionary<string, string>();


		IConfig m_config;


		//
        public override ISimulationBase Copy()
        {
            return new AuroraMoneyBase();
        }



		//
		public override void Initialize(IConfigSource originalConfig, IConfigSource configSource, string[] cmdParams, ConfigurationLoader configLoader)
		{
			m_log.Info("[MONEY SERVER]: Initialize.");

            string iniFile = "MoneyServer.ini";
            if (configLoader.defaultIniFile!="") iniFile = configLoader.defaultIniFile;

            ReadIniConfig(iniFile);
		}



        /// <summary>
        ///   Performs initialisation of the scene, such as loading configuration from disk.
        /// </summary>
        public override void Startup()
        {
			m_log.Info("[MONEY SERVER]: Startup.");

            //Fix the default prompt
            m_console = new LocalConsole();
            m_console.DefaultPrompt = "Money ";

			MainConsole.Instance = m_console;

        	StartupSpecific();




            //SetUpHTTPServer();

            //StartModules();

            //Has to be after Scene Manager startup
            //AddPluginCommands();



//            if (MainConsole.Instance != null)
//                MainConsole.Instance.DefaultPrompt = "Money ";

            SetUpHTTPServer();
            //MainConsole.Instance.Info("[MONEY SERVER]: Startup completed in " + (DateTime.Now - this.StartupTime).TotalSeconds);

        }








        /// <summary>
        /// Set up the base HTTP server
        /// </summary>
        public override void SetUpHTTPServer()
        {
            try {
                if (m_certFilename!="" && m_certPassword!="")
                {
                    m_httpServer = new BaseHttpServer(m_moneyServerPort, m_hostName, true);
                    m_httpServer.SetSecureParams(m_certFilename, m_certPassword, SslProtocols.Tls);
                	m_log.Info("[MONEY SERVER]: HTTPS: Secure TLS");
                }
                else
                {
                    m_httpServer = new BaseHttpServer(m_moneyServerPort, m_hostName, false);
                	m_log.Info("[MONEY SERVER]: HTTP: Non Secure");
                }

                m_httpServer.Start();

            	//MainServer.Instance = m_httpServer;

                SetupMoneyServices();
                //base.StartupSpecific();
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[MONEY SERVER]: StartupSpecific: Fail to start HTTP/HTTPS process");
                m_log.ErrorFormat("[MONEY SERVER]: StartupSpecific: {0}", e);
                Environment.Exit(1);
            }
        }



        protected virtual void StartupSpecific()
        {
            m_log.ErrorFormat("XXXXXXXXXXXXXXXXXXXXXzzzzzzzzzzzzxxx");

			if (m_console!=null)
			{
                m_log.ErrorFormat("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzxxx");
/*
                m_console.AddCommand("quit", "quit", "Quit the application", HandleQuit);
                m_console.AddCommand("shutdown", "shutdown", "Quit the application", HandleQuit);
                m_console.Commands.AddCommand("set log level", "set log level <level>", "Set the console logging level", HandleLogLevel);
                m_console.Commands.AddCommand("show info", "show info", "Show general information about the server", HandleShow);
                m_console.Commands.AddCommand("show stats", "show stats", "Show statistics", HandleShow);
                m_console.Commands.AddCommand("show threads", "show threads", "Show thread status", HandleShow);
                m_console.Commands.AddCommand("show uptime", "show uptime", "Show server uptime", HandleShow);
                m_console.Commands.AddCommand("show version", "show version", "Show server version", HandleShow);
*/
            }
        }
       


        public override void RegisterConsoleCommands()
        {
            if (MainConsole.Instance == null)
                return;

                m_console.Commands.AddCommand("set log level", "set log level <level>", "Set the console logging level", HandleLogLevel);
                m_console.Commands.AddCommand("show info", "show info", "Show general information about the server", HandleShow);
                m_console.Commands.AddCommand("show stats", "show stats", "Show statistics", HandleShow);
                m_console.Commands.AddCommand("show threads", "show threads", "Show thread status", HandleShow);
                m_console.Commands.AddCommand("show uptime", "show uptime", "Show server uptime", HandleShow);
                m_console.Commands.AddCommand("show version", "show version", "Show server version", HandleShow);

        }



		// read MoneyServer.ini
		protected void ReadIniConfig(string inifile)
		{
			MoneyServerConfigSource moneyConfig = new MoneyServerConfigSource(inifile);

			try {
				// [Startup]
				IConfig st_config = moneyConfig.m_config.Configs["Startup"];
				string PIDFile = st_config.GetString("PIDFile", "");
				if (PIDFile!="") Create_PIDFile(PIDFile);

				// [MySql]
				IConfig db_config = moneyConfig.m_config.Configs["MySql"];
				string sqlserver  = db_config.GetString("hostname", "localhost");
				string database   = db_config.GetString("database", "OpenSim");
				string username   = db_config.GetString("username", "root");
				string password   = db_config.GetString("password", "password");
				string pooling 	  = db_config.GetString("pooling",  "false");
				string port 	  = db_config.GetString("port", 	"3306");
				MAX_DB_CONNECTION = db_config.GetInt   ("MaxConnection", 10);

				connectionString  = "Server=" + sqlserver + ";Port=" + port + ";Database=" + database + ";User ID=" +
												username + ";Password=" + password + ";Pooling=" + pooling + ";";

				// [MoneyServer]
				m_config   = moneyConfig.m_config.Configs["MoneyServer"];
				DEAD_TIME  = m_config.GetInt   ("ExpiredTime", 120);
				m_hostName = m_config.GetString("HostName", "localhost");

				string checkcert = m_config.GetString("CheckClientCert", "false");
				if (checkcert.ToLower()=="true") m_checkClientCert = true;

				m_certFilename = m_config.GetString("ServerCertFilename", "SineWaveCert.pfx");
				m_certPassword = m_config.GetString("ServerCertPassword", "123");
				//
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
		// added by skidz
		//
		protected void Create_PIDFile(string path)
		{
			try
			{
				string pidstring = System.Diagnostics.Process.GetCurrentProcess().Id.ToString();
				FileStream fs = File.Create(path);
				System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
				Byte[] buf = enc.GetBytes(pidstring);
				fs.Write(buf, 0, buf.Length);
				fs.Close();
				m_pidFile = path;
			}
			catch (Exception)
			{
			}
		}


        //
        protected void RemovePIDFile()
        {
            if (m_pidFile != String.Empty)
            {
               try 
               {
                    File.Delete(m_pidFile);
                    m_pidFile = String.Empty;
               }
               catch (Exception)
               {
               }
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
			m_moneyXmlRpcModule.Initialise(m_version, m_config, m_moneyDBService, this);
			m_moneyXmlRpcModule.PostInitialise();
		}



		//



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
        public virtual void ShutdownSpecific() {}


        /// <summary>
        /// Should be overriden and referenced by descendents if they need to perform extra shutdown processing
        /// </summary>
        public virtual void Shutdown()
        {
            ShutdownSpecific();
            m_log.Info("[SHUTDOWN]: Shutdown processing on main thread complete.  Exiting...");

            RemovePIDFile();
            Environment.Exit(0);
        }


        //private void HandleQuit(string module, string[] args)
        public void HandleQuit(string[] args)
        {
            Shutdown();
        }

    }





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
				//m_config = DefaultConfig();
			}
		}

		public void Save(string path)
		{
			m_config.Save(path);
		}

	}



}



/*


namespace OpenSim.Grid.MoneyServer
{
	class MoneyServerBase : BaseOpenSimServer, IMoneyServiceCore
	{
		private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private string connectionString = string.Empty;
		private uint m_moneyServerPort = 8008;
		private string m_hostName = "localhost";

		private string m_certFilename	 = "";
		private string m_certPassword	 = "";
		private bool   m_checkClientCert = false;
		//private X509Certificate2 m_cert  = null;

		private int DEAD_TIME;
		private int MAX_DB_CONNECTION;

		private MoneyXmlRpcModule m_moneyXmlRpcModule;
		private MoneyDBService m_moneyDBService;

		private Dictionary<string, string> m_sessionDic = new Dictionary<string, string>();
		private Dictionary<string, string> m_secureSessionDic = new Dictionary<string, string>();
		private Dictionary<string, string> m_webSessionDic = new Dictionary<string, string>();

		IConfig m_config;


		public MoneyServerBase()
		{
			m_console = new LocalConsole();
			m_console.DefaultPrompt = "Money ";
			MainConsole.Instance = m_console;
		}


		public void Work()
		{
			//m_console.Notice("Enter help for a list of commands\n");

			//The timer checks the transactions table every 60 seconds
			Timer checkTimer = new Timer();
			checkTimer.Interval = 60*1000;
			checkTimer.Enabled = true;
			checkTimer.Elapsed += new ElapsedEventHandler(CheckTransaction);
			checkTimer.Start();
			while (true)
			{
				m_console.Prompt();
			}
		}


		/// <summary>
		/// Check the transactions table, set expired transaction state to failed
		/// </summary>
		private void CheckTransaction(object sender, ElapsedEventArgs e)
		{
			long ticksToEpoch = new DateTime(1970, 1, 1).Ticks;
			int unixEpochTime =(int) ((DateTime.Now.Ticks - ticksToEpoch )/10000000);
			int deadTime = unixEpochTime - DEAD_TIME;
			m_moneyDBService.SetTransExpired(deadTime);
		}


		protected override void StartupSpecific()
		{
			m_log.Info("[MONEY SERVER]: Starting HTTPS process");

			ReadIniConfig();

			try {
				if (m_certFilename!="" && m_certPassword!="")
				{
					m_httpServer = new BaseHttpServer(m_moneyServerPort, m_hostName, true);
					m_httpServer.SetSecureParams(m_certFilename, m_certPassword, SslProtocols.Tls);
				}
				else
				{
					m_httpServer = new BaseHttpServer(m_moneyServerPort, m_hostName, false);
				}

				SetupMoneyServices();
				m_httpServer.Start();
				base.StartupSpecific();
			}
			catch (Exception e)
			{
				m_log.ErrorFormat("[MONEY SERVER] StartupSpecific: Fail to start HTTPS process");
				m_log.ErrorFormat("[MONEY SERVER] StartupSpecific: Please Check Certificate File or Password. Exit");
				m_log.ErrorFormat("[MONEY SERVER] StartupSpecific: {0}", e);
				Environment.Exit(1);
			}

			//TODO : Add some console commands here
		}



		protected void ReadIniConfig()
		{
			MoneyServerConfigSource moneyConfig = new MoneyServerConfigSource();

			try {
				// [Startup]
				IConfig st_config = moneyConfig.m_config.Configs["Startup"];
				string PIDFile = st_config.GetString("PIDFile", "");
				if (PIDFile!="") Create_PIDFile(PIDFile);

				// [MySql]
				IConfig db_config = moneyConfig.m_config.Configs["MySql"];
				string sqlserver  = db_config.GetString("hostname", "localhost");
				string database   = db_config.GetString("database", "OpenSim");
				string username   = db_config.GetString("username", "root");
				string password   = db_config.GetString("password", "password");
				string pooling 	  = db_config.GetString("pooling",  "false");
				string port 	  = db_config.GetString("port", 	"3306");
				MAX_DB_CONNECTION = db_config.GetInt   ("MaxConnection", 10);

				connectionString  = "Server=" + sqlserver + ";Port=" + port + ";Database=" + database + ";User ID=" +
												username + ";Password=" + password + ";Pooling=" + pooling + ";";

				// [MoneyServer]
				m_config   = moneyConfig.m_config.Configs["MoneyServer"];
				DEAD_TIME  = m_config.GetInt   ("ExpiredTime", 120);
				m_hostName = m_config.GetString("HostName", "localhost");	// be not used

				string checkcert = m_config.GetString("CheckClientCert", "false");
				if (checkcert.ToLower()=="true") m_checkClientCert = true;
				m_certFilename = m_config.GetString("ServerCertFilename", "SineWaveCert.pfx");
				m_certPassword = m_config.GetString("ServerCertPassword", "123");
				//if (m_certFilename!="" && m_certPassword!="")
				//{
				//	m_cert = new X509Certificate2(m_certFilename, m_certPassword);
				//}
			}
			catch (Exception)
			{
				m_log.Error("[MONEY SERVER] ReadIniConfig: Fail to setup configure. Please check MoneyServer.ini. Exit");
				Environment.Exit(1);
			}
		}


		// added by skidz
		protected void Create_PIDFile(string path)
		{
			try
			{
				string pidstring = System.Diagnostics.Process.GetCurrentProcess().Id.ToString();
				FileStream fs = File.Create(path);
				System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
				Byte[] buf = enc.GetBytes(pidstring);
				fs.Write(buf, 0, buf.Length);
				fs.Close();
				m_pidFile = path;
			}
			catch (Exception)
			{
			}
		}



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

	}



	class MoneyServerConfigSource
	{
		public IniConfigSource m_config;

		public MoneyServerConfigSource()
		{
			string configPath = Path.Combine(Directory.GetCurrentDirectory(), "MoneyServer.ini");
			if (File.Exists(configPath))
			{
				m_config = new IniConfigSource(configPath, Nini.Ini.IniFileType.AuroraStyle);
			}
			else
			{
				//TODO: create default configuration.
				//m_config = DefaultConfig();
			}
		}

		public void Save(string path)
		{
			m_config.Save(path);
		}

	}
}
*/

/*
        //private void HandleLogLevel(string module, string[] cmd)
        private void HandleLogLevel(string[] cmd)
        {
            if (null == m_consoleAppender)
            {
                Notice("No appender named Console found (see the log4net config file for this executable)!");
                return;
            }
      
            if (cmd.Length > 3)
            {
                string rawLevel = cmd[3];
                
                ILoggerRepository repository = LogManager.GetRepository();
                Level consoleLevel = repository.LevelMap[rawLevel];
                
                if (consoleLevel != null)
                    m_consoleAppender.Threshold = consoleLevel;
                else
                    Notice(
                        String.Format(
                            "{0} is not a valid logging level.  Valid logging levels are ALL, DEBUG, INFO, WARN, ERROR, FATAL, OFF",
                            rawLevel));
            }

            Notice(String.Format("Console log level is {0}", m_consoleAppender.Threshold));
        }


        /// <summary>
        /// Show help information
        /// </summary>
        /// <param name="helpArgs"></param>
        protected virtual void ShowHelp(string[] helpArgs)
        {
            Notice("");
            
            if (helpArgs.Length == 0)
            {
                Notice("set log level [level] - change the console logging level only.  For example, off or debug.");
                Notice("show info - show server information (e.g. startup path).");

                if (m_stats != null)
                    Notice("show stats - show statistical information for this server");

                Notice("show threads - list tracked threads");
                Notice("show uptime - show server startup time and uptime.");
                Notice("show version - show server version.");
                Notice("");

                return;
            }
        }


        //public virtual void HandleShow(string module, string[] cmd)
        public virtual void HandleShow(string[] cmd)
        {
            List<string> args = new List<string>(cmd);

            args.RemoveAt(0);

            string[] showParams = args.ToArray();

            switch (showParams[0])
            {
                case "info":
                    ShowInfo();
                    break;

                case "stats":
                    if (m_stats != null)
                        Notice(m_stats.Report());
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
            return String.Format("Version: {0} (interface version {1})", m_version, VersionInfo.MajorInterfaceVersion);
        }
*/
