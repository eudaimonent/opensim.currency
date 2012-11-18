/*
 *
 */


/*

Aurora/Simulation/Base/BaseApplication.cs:  

BaseApplication.BaseMain()
{
	BaseApplication.Startup()
}

BaseApplication.Startup()
{
	simBase.Initialize(); 		// simBase => AuroraMoneyBase
	simBase.Startup();
	simBase.Run();
}
*/


using Aurora.Simulation.Base;

namespace Aurora.Server.MoneyServer
{
    public class Application
    {
        public static void Main(string[] args)
        {
            BaseApplication.BaseMain(args, "MoneyServer.ini", new AuroraMoneyBase());
        }
    }

}
