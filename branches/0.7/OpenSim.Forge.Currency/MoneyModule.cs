// * Copyright (c) Contributors, http://opensimulator.org/
// * See CONTRIBUTORS.TXT for a full list of copyright holders.
// *
// * Redistribution and use in source and binary forms, with or without
// * modification, are permitted provided that the following conditions are met:
// *     * Redistributions of source code must retain the above copyright
// *       notice, this list of conditions and the following disclaimer.
// *     * Redistributions in binary form must reproduce the above copyright
// *       notice, this list of conditions and the following disclaimer in the
// *       documentation and/or other materials provided with the distribution.
// *     * Neither the name of the OpenSim Project nor the
// *       names of its contributors may be used to endorse or promote products
// *       derived from this software without specific prior written permission.
// *
// * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
// * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
// * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Net;

using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenMetaverse;

using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
//using OpenSim.Framework.Communications.Cache;
using OpenSim.Services.Interfaces;
using OpenSim.Services.UserAccountService;

using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework;


namespace OpenSim.Forge.Currency
{
    public class MoneyModule : IMoneyModule, IRegionModule
    {
        /*Memebers*************************************************************/
        #region Constant numbers and members.

        // Constant memebers   
        private const int MONEYMODULE_REQUEST_TIMEOUT = 30000;
        private const int MONEYMODULE_INITIAL_BALANCE = 2000;

        public enum TransactionType : int
        {
            MONEY_TRANS_SYSTEMGENERATED = 0,
            MONEY_TRANS_REGIONMONEYREQUEST,
            MONEY_TRANS_GIFT,
            MONEY_TRANS_PURCHASE,
        }

        // Private data members.   
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IConfigSource m_config;
        private string m_moneyServURL = string.Empty;
        private string m_userServIP = string.Empty;

        /// <summary>   
        /// Scene dictionary indexed by Region Handle   
        /// </summary>   
        private Dictionary<ulong, Scene> m_sceneList = new Dictionary<ulong, Scene>();

        /// <summary>   
        /// To cache the balance data while the money server is not available.   
        /// </summary>   
        private Dictionary<UUID, int> m_moneyServer = new Dictionary<UUID, int>();

        // Events  
        public event ObjectPaid OnObjectPaid;
        public event PostObjectPaid OnPostObjectPaid;

        public BaseHttpServer HttpServer;

        #endregion


        /*Public***************************************************************/
        #region IRegionModule interface

        public void Initialise(Scene scene, IConfigSource source)
        {
            // Handle the parameters errors.
            if (scene == null || source == null) return;

            try
            {
                m_config = source;
                // Refer to the [Startup] secion and check if current is grid mode or standalone.
                IConfig networkConfig = m_config.Configs["Network"];
                m_userServIP = "";
                 if (networkConfig.Contains("user_server_url")) {
                    m_userServIP = Util.GetHostFromURL(networkConfig.GetString("user_server_url")).ToString();
                }

                IConfig economyConfig = m_config.Configs["Economy"];
                if (m_userServIP=="") {
                    m_userServIP = Util.GetHostFromURL(economyConfig.GetString("UserServer")).ToString();
                }
                m_moneyServURL = economyConfig.GetString("CurrencyServer").ToString();
                // Check if the DTLMoneyModule is configured to load.
                if (economyConfig.GetString("EconomyModule").ToString() != "DTLMoneyModule")
                {
                    return;
                }
            }
            catch
            {
                m_log.ErrorFormat("[MONEY]: Faile to read configuration file.");
            }
            scene.RegisterModuleInterface<IMoneyModule>(this);

            lock (m_sceneList)
            {
                if (m_sceneList.Count == 0)
                {
                    if (!string.IsNullOrEmpty(m_moneyServURL))
                    {
                        HttpServer = new BaseHttpServer(9000);
                        HttpServer.AddStreamHandler(new Region.Framework.Scenes.RegionStatsHandler(scene.RegionInfo));

                        HttpServer.AddXmlRPCHandler("UpdateBalance", BalanceUpdateHandler);
                        HttpServer.AddXmlRPCHandler("UserAlert", UserAlertHandler);
                        HttpServer.AddXmlRPCHandler("SendConfirmLink", SendConfirmLinkHandler);
                        HttpServer.AddXmlRPCHandler("OnMoneyTransfered", OnMoneyTransferedHandler);

                        //scene.CommsManager.HttpServer.AddXmlRPCHandler("UpdateBalance", BalanceUpdateHandler);
                        //scene.CommsManager.HttpServer.AddXmlRPCHandler("UserAlert", UserAlertHandler);
                        //scene.CommsManager.HttpServer.AddXmlRPCHandler("SendConfirmLink", SendConfirmLinkHandler);
                        //scene.CommsManager.HttpServer.AddXmlRPCHandler("OnMoneyTransfered", OnMoneyTransferedHandler);
                        MainServer.Instance.AddXmlRPCHandler("UpdateBalance", BalanceUpdateHandler);
                        MainServer.Instance.AddXmlRPCHandler("UserAlert", UserAlertHandler);
                        MainServer.Instance.AddXmlRPCHandler("SendConfirmLink", SendConfirmLinkHandler);
                        MainServer.Instance.AddXmlRPCHandler("OnMoneyTransfered", OnMoneyTransferedHandler);
                    }
                }

                if (m_sceneList.ContainsKey(scene.RegionInfo.RegionHandle))
                {
                    m_sceneList[scene.RegionInfo.RegionHandle] = scene;
                }
                else
                {
                    m_sceneList.Add(scene.RegionInfo.RegionHandle, scene);
                }

            }

             scene.EventManager.OnMakeRootAgent += OnMakeRootAgent;
            //scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnMoneyTransfer += MoneyTransferAction;
            scene.EventManager.OnClientClosed += ClientClosed;
            scene.EventManager.OnAvatarEnteringNewParcel += AvatarEnteringParcel;
            scene.EventManager.OnMakeChildAgent += MakeChildAgent;
            scene.EventManager.OnValidateLandBuy += ValidateLandBuy;
            scene.EventManager.OnLandBuy += processLandBuy;
        }


        public void PostInitialise()
        {

        }


        public bool IsSharedModule
        {
            get { return true; }
        }


        public string Name
        {
            get { return "DTLMoneyModule"; }
        }


        public void Close()
        {

        }

        #endregion



        // Since the economy data won't be used anymore,    
        // removed the related legacy code from interface implement.   
        #region IMoneyModule interface.

        public bool ObjectGiveMoney(UUID objectID, UUID fromID, UUID toID, int amount)
        {
            string objName = string.Empty;
            string avatarName = string.Empty;
            SceneObjectPart sceneObj = FindPrim(objectID);
            if (sceneObj != null)
            {
                objName = sceneObj.Name;
            }

            Scene scene = null;
            if (m_sceneList.Count > 0)
            {
                //scene = m_sceneList[0];
                foreach (Scene _scene in m_sceneList.Values)
                {
                    scene = _scene;
                    break;
                }
            }

            if (scene != null)
            {
                UserAccount account = scene.UserAccountService.GetUserAccount(scene.RegionInfo.ScopeID, toID);
                if (account != null)
                {
                    avatarName = account.FirstName + " " + account.LastName;
                }

                //CachedUserInfo profile = scene.CommsManager.UserProfileCacheService.GetUserDetails(toID);
                //if (profile != null && profile.UserProfile != null)
                //{
                //    avatarName = profile.UserProfile.FirstName + " " + profile.UserProfile.SurName;
                //}
            }

            string description = String.Format("Object {0} pays {1}", objName, avatarName);
            bool ret = TransferMoney(fromID, toID, amount, 2, 0, 0, description);

            return ret;
        }


        public int GetBalance(IClientAPI client)
        {
            return QueryBalanceFromMoneyServer(client);
        }


        public void ApplyUploadCharge(UUID agentID)
        {
            // Empty!
        }


        public bool UploadCovered(IClientAPI client)
        {
            return true;
        }


        public void ApplyGroupCreationCharge(UUID agentID)
        {
            // Empty!
        }


        public bool GroupCreationCovered(IClientAPI client)
        {
            return true;
        }


        public bool AmountCovered(IClientAPI client, int amount)
        {
            return true;
        }


        public void ApplyCharge(UUID agentID, int amount, string text)
        {
            // Empty!
        }


        public EconomyData GetEconomyData()
        {
            return new EconomyData();
        }

        #endregion


        #region MoneyModule event handlers

/*
        // deleted by Fumi.Iseki
        private void OnNewClient(IClientAPI client)
        {
            int balance = 0;
            LoginMoneyServer(client, out balance);
            client.SendMoneyBalance(UUID.Zero, true, new byte[0], balance);

            // Subscribe to Money messages   
            client.OnEconomyDataRequest += OnEconomyDataRequest;
            client.OnMoneyBalanceRequest += OnMoneyBalanceRequest;
            client.OnRequestPayPrice += OnRequestPayPrice;
            client.OnObjectBuy += OnObjectBuy;
            client.OnLogout += ClientClosed;
        }
*/

        // added by Fumi.Iseki
        public void OnMakeRootAgent(ScenePresence agent)
        {
            int balance = 0;
            IClientAPI client = agent.ControllingClient;

            m_log.Debug("[MONEY]: OnMakeRootAgent.");

            LoginMoneyServer(client, out balance);
            client.SendMoneyBalance(UUID.Zero, true, new byte[0], balance);

            // Subscribe to Money messages   
            client.OnEconomyDataRequest += OnEconomyDataRequest;
            client.OnMoneyBalanceRequest += OnMoneyBalanceRequest;
            client.OnRequestPayPrice += OnRequestPayPrice;
            client.OnObjectBuy += OnObjectBuy;
            client.OnLogout += ClientClosed;
        }       



        //private void ClientClosed(UUID clientID)
         private void ClientClosed(UUID clientID, Scene scene)
        {
            IClientAPI client = LocateClientObject(clientID);
            if (client != null)
            {
                // If the User is just transferred to another region. No need to logoff from money server.
                // LogoffMoneyServer(client);
            }
        }



        private void ClientClosed(IClientAPI client)
        {
            if (client != null)
            {
                LogoffMoneyServer(client);
            }
        }



        private void MoneyTransferAction(Object sender, EventManager.MoneyTransferArgs moneyEvent)
        {
            // Check the money transaction is necessary.   
            if (moneyEvent.sender == moneyEvent.receiver)
            {
                return;
            }

            string description = string.Empty;
            UUID receiver = moneyEvent.receiver;
            if (moneyEvent.transactiontype == 5008)// Pay for the object.   
            {
                SceneObjectPart sceneObj = FindPrim(moneyEvent.receiver);
                if (sceneObj != null)
                {
                    receiver = sceneObj.OwnerID;
                }
                else
                {
                    return;
                }
            }

            // Before paying for the object, save the object local ID for current transaction.
            uint objLocalID = 0;
            ulong regionHandle = 0;
            if (moneyEvent.transactiontype == 5008)
            {
                // Notify the client.   
                if (sender is Scene)
                {
                    Scene scene = (Scene)sender;
                    regionHandle = scene.RegionInfo.RegionHandle;
                    objLocalID = scene.GetSceneObjectPart(moneyEvent.receiver).LocalId;
                    m_log.Debug("Paying for object " + objLocalID);
                }
            }

            bool ret = TransferMoney(moneyEvent.sender, receiver, moneyEvent.amount, moneyEvent.transactiontype, objLocalID, regionHandle, description);
        }



        private void AvatarEnteringParcel(ScenePresence avatar, int localLandID, UUID regionID)
        {
            ILandObject obj = avatar.Scene.LandChannel.GetLandObject(avatar.AbsolutePosition.X, avatar.AbsolutePosition.Y);
            //if ((obj.landData.Flags & (uint)Parcel.ParcelFlags.AllowDamage) != 0)  for r10084
            if ((obj.LandData.Flags & (uint)RegionFlags.AllowDamage) != 0)
            {
                avatar.Invulnerable = false;
            }
            else
            {
                avatar.Invulnerable = true;
            }
        }



        private void MakeChildAgent(ScenePresence avatar)
        {

        }



        private void ValidateLandBuy(Object sender, EventManager.LandBuyArgs landBuyEvent)
        {
            IClientAPI senderClient = LocateClientObject(landBuyEvent.agentId);
            if (senderClient != null)
            {
                int balance = QueryBalanceFromMoneyServer(senderClient);
                if (balance >= landBuyEvent.parcelPrice)
                {
                    lock (landBuyEvent)
                    {
                        landBuyEvent.economyValidated = true;
                    }
                }
            }
        }



        private void processLandBuy(Object sender, EventManager.LandBuyArgs landBuyEvent)
        {
            lock (landBuyEvent)
            {
                if (landBuyEvent.economyValidated == true &&
                    landBuyEvent.transactionID == 0)
                {
                    landBuyEvent.transactionID = Util.UnixTimeSinceEpoch();

                    if (TransferMoney(landBuyEvent.agentId, landBuyEvent.parcelOwnerID, landBuyEvent.parcelPrice, 0, 0, 0, "Land purchase"))
                    {
                        lock (landBuyEvent)
                        {
                            landBuyEvent.amountDebited = landBuyEvent.parcelPrice;
                        }
                    }
                }
            }
        }

        #endregion


        #region MoneyModule XML-RPC Handler

        // Money Server -> Region Server -> Viewer
        public XmlRpcResponse BalanceUpdateHandler(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            bool ret = false;

            #region Update the balance from money server.

            if (request.Params.Count > 0)
            {
                Hashtable requestParam = (Hashtable)request.Params[0];
                if (requestParam.Contains("clientUUID") &&
                    requestParam.Contains("clientSessionID") &&
                    requestParam.Contains("clientSecureSessionID"))
                {
                    UUID clientUUID = UUID.Zero;
                    UUID.TryParse((string)requestParam["clientUUID"], out clientUUID);
                    if (clientUUID != UUID.Zero)
                    {
                        IClientAPI client = LocateClientObject(clientUUID);
                        if (client != null &&
                            client.SessionId.ToString() == (string)requestParam["clientSessionID"] &&
                            client.SecureSessionId.ToString() == (string)requestParam["clientSecureSessionID"])
                        {
                            if (requestParam.Contains("Balance"))
                            {
                                client.SendMoneyBalance(UUID.Random(),
                                                        true,
                                                        Utils.StringToBytes("Balance update event from money server"),
                                                        (int)requestParam["Balance"]);
                                ret = true;
                            }
                        }
                    }
                }
            }

            #endregion

            // Send the response to money server.
            XmlRpcResponse resp = new XmlRpcResponse();
            Hashtable paramTable = new Hashtable();
            paramTable["success"] = ret;
            if (!ret)
            {
                m_log.ErrorFormat("[MONEY]: Cannot update client balance from MoneyServer.");
            }
            resp.Value = paramTable;

            return resp;
        }



        public XmlRpcResponse UserAlertHandler(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            bool ret = false;

            #region confirm the request and show the notice from money server.

            if (request.Params.Count > 0)
            {
                Hashtable requestParam = (Hashtable)request.Params[0];
                if (requestParam.Contains("clientUUID") &&
                    requestParam.Contains("clientSessionID") &&
                    requestParam.Contains("clientSecureSessionID"))
                {
                    UUID clientUUID = UUID.Zero;
                    UUID.TryParse((string)requestParam["clientUUID"], out clientUUID);
                    if (clientUUID != UUID.Zero)
                    {
                        IClientAPI client = LocateClientObject(clientUUID);
                        if (client != null &&
                            client.SessionId.ToString() == (string)requestParam["clientSessionID"] &&
                            client.SecureSessionId.ToString() == (string)requestParam["clientSecureSessionID"])
                        {
                            if (requestParam.Contains("Description"))
                            {
                                // Show the notice dialog with money server message.
                               GridInstantMessage gridMsg = new GridInstantMessage(null, UUID.Zero, "MonyServer", new UUID(clientUUID.ToString()),
                                                                                    (byte)InstantMessageDialog.MessageFromAgent,
                                                                                    "Please clink the URI in IM window to confirm your purchase.",
                                                                                    false, new Vector3());

                                client.SendInstantMessage(gridMsg);
                                ret = true; 
                            }
                        }
                    }
                }
            }

            #endregion

            // Send the response to money server.
            XmlRpcResponse resp = new XmlRpcResponse();
            Hashtable paramTable = new Hashtable();
            paramTable["success"] = ret;

            resp.Value = paramTable;
            return resp;
        }



        public XmlRpcResponse SendConfirmLinkHandler(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            bool ret = false;

            #region confirm the request and send out confirm link.

            if (request.Params.Count > 0)
            {
                Hashtable requestParam = (Hashtable)request.Params[0];
                if (requestParam.Contains("clientUUID") &&
                    requestParam.Contains("clientSessionID") &&
                    requestParam.Contains("clientSecureSessionID"))
                {
                    UUID clientUUID = UUID.Zero;
                    UUID.TryParse((string)requestParam["clientUUID"], out clientUUID);
                    if (clientUUID != UUID.Zero)
                    {
                        IClientAPI client = LocateClientObject(clientUUID);
                        if (client != null &&
                            client.SessionId.ToString() == (string)requestParam["clientSessionID"] &&
                            client.SecureSessionId.ToString() == (string)requestParam["clientSecureSessionID"])
                        {
                            if (requestParam.Contains("URI"))
                            {
                                // Show the notice for user to confirm the link in IM.
                                GridInstantMessage gridMsg_notice = new GridInstantMessage(null, UUID.Zero, "MonyServer", new UUID(clientUUID.ToString()),
                                                                                           (byte)InstantMessageDialog.MessageBox,
                                                                                           "Please clink the URI in IM window to confirm your purchase.",
                                                                                           false, new Vector3());
                                client.SendInstantMessage(gridMsg_notice);

                                // Show the confirm link in IM window.
                                GridInstantMessage gridMsg_link = new GridInstantMessage(null, UUID.Zero, "MonyServer", new UUID(clientUUID.ToString()),
                                                                                         (byte)InstantMessageDialog.MessageFromAgent,
                                                                                         (string)requestParam["URI"],
                                                                                         false, new Vector3());
                                client.SendInstantMessage(gridMsg_link);

                                ret = true;
                            }
                        }
                    }
                }
            }

            #endregion

            // Send the response to money server.
            XmlRpcResponse resp = new XmlRpcResponse();
            Hashtable paramTable = new Hashtable();
            paramTable["success"] = ret;
            if (!ret)
            {
                m_log.ErrorFormat("[MONEY]: Cannot get or deliver the confirm link from MoneyServer.");
            }
            resp.Value = paramTable;

            return resp;
        }




        public XmlRpcResponse OnMoneyTransferedHandler(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            bool ret = false;

            #region Confirm the transaction type and send out object paid event.

            if (request.Params.Count > 0)
            {
                Hashtable requestParam = (Hashtable)request.Params[0];
                if (requestParam.Contains("senderID") &&
                    requestParam.Contains("receiverID") &&
                    requestParam.Contains("senderSessionID") &&
                    requestParam.Contains("senderSecureSessionID"))
                {
                    UUID senderID = UUID.Zero;
                    UUID receiverID = UUID.Zero;
                    UUID.TryParse((string)requestParam["senderID"], out senderID);
                    UUID.TryParse((string)requestParam["receiverID"], out receiverID);
                    if (senderID != UUID.Zero)
                    {
                        IClientAPI client = LocateClientObject(senderID);
                        if (client != null &&
                            client.SessionId.ToString() == (string)requestParam["senderSessionID"] &&
                            client.SecureSessionId.ToString() == (string)requestParam["senderSecureSessionID"])
                        {
                            if (requestParam.Contains("transactionType") &&
                                requestParam.Contains("localID") &&
                                requestParam.Contains("amount"))
                            {
                                if ((int)requestParam["transactionType"] == 5008)// Pay for the object.
                                {
                                    // Notify the client.   
                                    PostObjectPaid handlerOnObjectPaid = OnPostObjectPaid;
                                    if (handlerOnObjectPaid != null)
                                    {
                                        ret = handlerOnObjectPaid(UInt32.Parse((string)requestParam["localID"]),
                                                                  ulong.Parse((string)requestParam["regionHandle"]),
                                                                  senderID, (int)requestParam["amount"]);
                                        ret = true;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            #endregion

            // Send the response to money server.
            XmlRpcResponse resp = new XmlRpcResponse();
            Hashtable paramTable = new Hashtable();
            paramTable["success"] = ret;
            if (!ret)
            {
                m_log.ErrorFormat("[MONEY]: Transaction is failed. MoneyServer will rollback.");
            }
            resp.Value = paramTable;

            return resp;
        }

        #endregion


        #region MoneyModule private help functions

        /// <summary>   
        /// Transfer the money from one user to another. Need to notify money server to update.   
        /// </summary>   
        /// <param name="amount">   
        /// The amount of money.   
        /// </param>   
        /// <returns>   
        /// return true, if successfully.   
        /// </returns>   
        private bool TransferMoney(UUID sender, UUID receiver, int amount, int transactiontype, uint localID, ulong regionHandle, string description)
        {
            bool ret = false;
            IClientAPI senderClient = LocateClientObject(sender);
            IClientAPI receiverClient = LocateClientObject(receiver);
            int senderBalance = -1;
            int receiverBalance = -1;

            // Handle the illegal transaction.   
            if (senderClient == null) // receiverClient could be null.
            {
                m_log.ErrorFormat("[MONEY]: Client {0} not found", ((senderClient == null) ? sender : receiver).ToString());
                return false;
            }

            if (QueryBalanceFromMoneyServer(senderClient) < amount)
            {
                m_log.ErrorFormat("[MONEY]: No insufficient balance in client [{0}].", sender.ToString());
                return false;
            }

            #region Send transaction request to money server and parse the resultes.

            if (!string.IsNullOrEmpty(m_moneyServURL))
            {
                // Fill parameters for money transfer XML-RPC.   
                Hashtable paramTable = new Hashtable();
                paramTable["senderUserServIP"] = m_userServIP;
                paramTable["senderID"] = sender.ToString();
                paramTable["receiverUserServIP"] = m_userServIP;
                paramTable["receiverID"] = receiver.ToString();
                paramTable["senderSessionID"] = senderClient.SessionId.ToString();
                paramTable["senderSecureSessionID"] = senderClient.SecureSessionId.ToString();
                paramTable["transactionType"] = transactiontype;
                paramTable["localID"] = localID.ToString();
                paramTable["regionHandle"] = regionHandle.ToString();
                paramTable["amount"] = amount;
                paramTable["description"] = description;

                // Generate the request for transfer.   
                Hashtable resultTable = genericCurrencyXMLRPCRequest(paramTable, "TransferMoney");

                // Handle the return values from Money Server.  
                if (resultTable != null && resultTable.Contains("success"))
                {
                    if ((bool)resultTable["success"] == true)
                    {
                        m_log.DebugFormat("[MONEY]: Money transfer from client [{0}] to client [{1}] is done.", sender.ToString(), receiver.ToString());
                        ret = true;
                    }
                }
                else
                {
                    m_log.ErrorFormat("[MONEY]: Can not send money server transaction request from client [{0}].", sender.ToString(), receiver.ToString());
                    ret = false;
                }
            }
            else // If the money server is not available, save the balance into local.
            {
                if (m_moneyServer.ContainsKey(sender))
                {
                    if (!m_moneyServer.ContainsKey(receiver))
                    {
                        m_moneyServer.Add(receiver, MONEYMODULE_INITIAL_BALANCE);
                    }
                    m_moneyServer[sender] -= amount;
                    m_moneyServer[receiver] += amount;

                    senderBalance = m_moneyServer[sender];
                    receiverBalance = m_moneyServer[receiver];

                    ret = true;
                }
                else
                {
                    ret = false;
                }
            }

            #endregion

            return ret;
        }



        /// <summary>   
        /// Login the money server when the new client login.   
        /// </summary>   
        /// <param name="userID">   
        /// Indicate user ID of the new client.   
        /// </param>   
        /// <returns>   
        /// return true, if successfully.   
        /// </returns>   
        private bool LoginMoneyServer(IClientAPI client, out int balance)
        {
            bool ret = false;
            balance = 0;

            #region Send money server the client info for login.

            Scene scene = (Scene)client.Scene;
            string userName = string.Empty;
            if (!string.IsNullOrEmpty(m_moneyServURL))
            {
                // Get the username for the login user.
                if (client.Scene is Scene)
                {
                    if (scene != null)
                    {
                        UserAccount account = scene.UserAccountService.GetUserAccount(scene.RegionInfo.ScopeID, client.AgentId);
                        if (account != null)
                        {
                            userName = account.FirstName + " " + account.LastName;
                        }

                        //CachedUserInfo profile = scene.CommsManager.UserProfileCacheService.GetUserDetails(client.AgentId);
                        //if (profile != null && profile.UserProfile != null)
                        //{
                        //    userName = profile.UserProfile.FirstName + " " + profile.UserProfile.SurName;
                        //}
                    }
                }

                // Login the Money Server.   
                Hashtable paramTable = new Hashtable();
                paramTable["userServIP"] = m_userServIP;
                paramTable["openSimServIP"] = scene.RegionInfo.ServerURI.Replace(scene.RegionInfo.InternalEndPoint.Port.ToString(), 
                                                                                 scene.RegionInfo.HttpPort.ToString());
                paramTable["userName"] = userName;
                paramTable["clientUUID"] = client.AgentId.ToString();
                paramTable["clientSessionID"] = client.SessionId.ToString();
                paramTable["clientSecureSessionID"] = client.SecureSessionId.ToString();

                // Generate the request for transfer.   
                Hashtable resultTable = genericCurrencyXMLRPCRequest(paramTable, "ClientLogin");

                // Handle the return result 
                if (resultTable != null && resultTable.Contains("success"))
                {
                    if ((bool)resultTable["success"] == true)
                    {
                        balance = (int)resultTable["clientBalance"];
                        m_log.InfoFormat("[MONEY]: Client [{0}] login Money Server {1}.",
                                         client.AgentId.ToString(), m_moneyServURL);
                        ret = true;
                    }
                }
                else
                {
                    m_log.ErrorFormat("[MONEY]: Unable to login Money Server {0} for client [{1}].",
                                      m_moneyServURL, client.AgentId.ToString());
                }
            }
            else // login to the local money server.
            {
                if (!m_moneyServer.ContainsKey(client.AgentId))
                {
                    m_moneyServer.Add(client.AgentId, MONEYMODULE_INITIAL_BALANCE);
                }
                balance = m_moneyServer[client.AgentId];

                ret = true;
            }

            #endregion

            return ret;
        }



        /// <summary>   
        /// Log off from the money server.   
        /// </summary>   
        /// <param name="userID">   
        /// Indicate user ID of the new client.   
        /// </param>   
        /// <returns>   
        /// return true, if successfully.   
        /// </returns>   
        private bool LogoffMoneyServer(IClientAPI client)
        {
            bool ret = false;

            if (!string.IsNullOrEmpty(m_moneyServURL))
            {
                // Log off from the Money Server.   
                Hashtable paramTable = new Hashtable();
                paramTable["userServIP"] = m_userServIP;
                paramTable["clientUUID"] = client.AgentId.ToString();
                paramTable["clientSessionID"] = client.SessionId.ToString();
                paramTable["clientSecureSessionID"] = client.SecureSessionId.ToString();

                // Generate the request for transfer.   
                Hashtable resultTable = genericCurrencyXMLRPCRequest(paramTable, "ClientLogout");
                // Handle the return result
                if (resultTable != null && resultTable.Contains("success"))
                {
                    if ((bool)resultTable["success"] == true)
                    {
                        ret = true;
                    }
                }
            }

            return ret;
        }



        /// <summary>   
        /// Generic XMLRPC client abstraction   
        /// </summary>   
        /// <param name="ReqParams">Hashtable containing parameters to the method</param>   
        /// <param name="method">Method to invoke</param>   
        /// <returns>Hashtable with success=>bool and other values</returns>   
        private Hashtable genericCurrencyXMLRPCRequest(Hashtable reqParams, string method)
        {
            // Handle the error in parameter list.   
            if (reqParams.Count <= 0 || string.IsNullOrEmpty(method) || string.IsNullOrEmpty(m_moneyServURL))
            {
                return null;
            }

            ArrayList arrayParams = new ArrayList();
            arrayParams.Add(reqParams);
            XmlRpcResponse moneyServResp = null;
            try
            {
                XmlRpcRequest moneyModuleReq = new XmlRpcRequest(method, arrayParams);
                moneyServResp = moneyModuleReq.Send(m_moneyServURL, MONEYMODULE_REQUEST_TIMEOUT);
            }
            catch (Exception ex)
            {
                m_log.ErrorFormat( "[MONEY]: Unable to connect to Money Server {0}.  Exception {1}", m_moneyServURL, ex);

                Hashtable ErrorHash = new Hashtable();
                ErrorHash["success"] = false;
                ErrorHash["errorMessage"] = "Unable to manage your money at this time. Purchases may be unavailable";
                ErrorHash["errorURI"] = "";

                return ErrorHash;
            }

            if (moneyServResp.IsFault)
            {
                Hashtable ErrorHash = new Hashtable();
                ErrorHash["success"] = false;
                ErrorHash["errorMessage"] = "Unable to manage your money at this time. Purchases may be unavailable";
                ErrorHash["errorURI"] = "";

                return ErrorHash;
            }
            Hashtable moneyRespData = (Hashtable)moneyServResp.Value;

            return moneyRespData;
        }



        /// <summary>   
        /// Locates a IClientAPI for the client specified   
        /// </summary>   
        /// <param name="AgentID"></param>   
        /// <returns></returns>   
        private IClientAPI LocateClientObject(UUID AgentID)
        {
            ScenePresence tPresence = null;
            IClientAPI rclient = null;

            lock (m_sceneList)
            {
                foreach (Scene _scene in m_sceneList.Values)
                {
                    tPresence = _scene.GetScenePresence(AgentID);
                    if (tPresence != null)
                    {
                        if (!tPresence.IsChildAgent)
                        {
                            rclient = tPresence.ControllingClient;
                        }
                    }
                    if (rclient != null)
                    {
                        return rclient;
                    }
                }
            }

            return null;
        }



        private Scene LocateSceneClientIn(UUID AgentId)
        {
            lock (m_sceneList)
            {
                foreach (Scene _scene in m_sceneList.Values)
                {
                    ScenePresence tPresence = _scene.GetScenePresence(AgentId);
                    if (tPresence != null)
                    {
                        if (!tPresence.IsChildAgent)
                        {
                            return _scene;
                        }
                    }
                }
            }

            return null;
        }



        private SceneObjectPart FindPrim(UUID objectID)
        {
            lock (m_sceneList)
            {
                foreach (Scene scene in m_sceneList.Values)
                {
                    SceneObjectPart part = scene.GetSceneObjectPart(objectID);
                    if (part != null)
                    {
                        return part;
                    }
                }
            }

            return null;
        }



        private int QueryBalanceFromMoneyServer(IClientAPI client)
        {
            int ret = -1;

            #region Send the request to get the balance from money server for cilent.

            if (client != null)
            {
                if (!string.IsNullOrEmpty(m_moneyServURL))
                {
                    Hashtable paramTable = new Hashtable();
                    paramTable["userServIP"] = m_userServIP;
                    paramTable["clientUUID"] = client.AgentId.ToString();
                    paramTable["clientSessionID"] = client.SessionId.ToString();
                    paramTable["clientSecureSessionID"] = client.SecureSessionId.ToString();

                    // Generate the request for transfer.   
                    Hashtable resultTable = genericCurrencyXMLRPCRequest(paramTable, "GetBalance");

                    // Handle the return result
                    if (resultTable != null && resultTable.Contains("success"))
                    {
                        if ((bool)resultTable["success"] == true)
                        {
                            ret = (int)resultTable["clientBalance"];
                        }
                    }
                }
                else
                {
                    if (m_moneyServer.ContainsKey(client.AgentId))
                    {
                        ret = m_moneyServer[client.AgentId];
                    }
                }

                if (ret < 0)
                {
                    m_log.ErrorFormat("[MONEY]: Unable to query balance from Money Server {0} for client [{1}].", 
                                                                                    m_moneyServURL, client.AgentId.ToString());
                }
            }

            #endregion

            return ret;
        }



        private void OnEconomyDataRequest(UUID agentId)
        {
            // Empty!
        }



        /// <summary>   
        /// Sends the the stored money balance to the client   
        /// </summary>   
        /// <param name="client"></param>   
        /// <param name="agentID"></param>   
        /// <param name="SessionID"></param>   
        /// <param name="TransactionID"></param>   
        private void OnMoneyBalanceRequest(IClientAPI client, UUID agentID, UUID SessionID, UUID TransactionID)
        {
            if (client.AgentId == agentID && client.SessionId == SessionID)
            {
                int balance = -1;
                if (!string.IsNullOrEmpty(m_moneyServURL))
                {
                    balance = QueryBalanceFromMoneyServer(client);
                }
                else if (m_moneyServer.ContainsKey(agentID))
                {

                    balance = m_moneyServer[agentID];
                }

                if (balance < 0)
                {
                    client.SendAlertMessage("Fail to query the balance.");
                }
                else
                {
                    client.SendMoneyBalance(TransactionID, true, new byte[0], balance);
                }
            }
            else
            {
                client.SendAlertMessage("Unable to send your money balance.");
            }
        }



        private void OnRequestPayPrice(IClientAPI client, UUID objectID)
        {
            Scene scene = LocateSceneClientIn(client.AgentId);
            if (scene == null) return;
            SceneObjectPart sceneObj = scene.GetSceneObjectPart(objectID);
            if (sceneObj == null) return;
            SceneObjectGroup group = sceneObj.ParentGroup;
            SceneObjectPart root = group.RootPart;

            client.SendPayPrice(objectID, root.PayPrice);
        }



        public void OnObjectBuy(IClientAPI remoteClient, UUID agentID,
                                UUID sessionID, UUID groupID, UUID categoryID,
                                uint localID, byte saleType, int salePrice)
        {
            // Handle the parameters error.   
            //if (remoteClient == null || salePrice <= 0) return;
            if (remoteClient == null || salePrice < 0) return;        // for L$0 Sell  by Fumi.Iseki

            // Get the balance from money server.   
            int balance = QueryBalanceFromMoneyServer(remoteClient);
            if (balance < salePrice)
            {
                remoteClient.SendAgentAlertMessage("Unable to buy now. You don't have sufficient funds.", false);
                return;
            }

            Scene scene = LocateSceneClientIn(remoteClient.AgentId);
            if (scene != null)
            {
                SceneObjectPart sceneObj = scene.GetSceneObjectPart(localID);
                if (sceneObj != null)
                {
                    if (scene.PerformObjectBuy(remoteClient, categoryID, localID, saleType))
                    {
                        TransferMoney(remoteClient.AgentId, sceneObj.OwnerID, salePrice, 5000, 0, 0, "Object Buy");
                    }
                }
                else
                {
                    remoteClient.SendAgentAlertMessage("Unable to buy now. The object was not found.", false);
                    return;
                }
            }
        }

        #endregion

        /*Private**************************************************************/

    }
}
