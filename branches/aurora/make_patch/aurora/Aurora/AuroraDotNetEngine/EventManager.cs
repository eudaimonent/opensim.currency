/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.CoreModules;
using OpenSim.Region;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using Aurora.ScriptEngine.AuroraDotNetEngine.APIs.Interfaces;
using Aurora.ScriptEngine.AuroraDotNetEngine.Plugins;
using Aurora.ScriptEngine.AuroraDotNetEngine.Runtime;
using log4net;

namespace Aurora.ScriptEngine.AuroraDotNetEngine
{
    /// <summary>
    /// Prepares events so they can be directly executed upon a script by EventQueueManager, then queues it.
    /// </summary>
    [Serializable]
    public class EventManager
    {
        //
        // This class it the link between an event inside OpenSim and
        // the corresponding event in a user script being executed.
        //
        // For example when an user touches an object then the
        // "scene.EventManager.OnObjectGrab" event is fired
        // inside OpenSim.
        // We hook up to this event and queue a touch_start in
        // the event queue with the proper LSL parameters.
        //
        // You can check debug C# dump of an LSL script if you need to
        // verify what exact parameters are needed.
        //

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private ScriptEngine m_scriptEngine;

        private List<Scene> m_Scenes = new List<Scene>();

        private Dictionary<uint, Dictionary<UUID, DetectParams>> CoalescedTouchEvents = new Dictionary<uint, Dictionary<UUID, DetectParams>>();

        public EventManager(ScriptEngine _ScriptEngine)
        {
            m_scriptEngine = _ScriptEngine;
        }

        public void HookUpRegionEvents(Scene Scene)
        {
            //m_log.Info("[" + myScriptEngine.ScriptEngineName +
            //           "]: Hooking up to server events");

            m_Scenes.Add(Scene);

            Scene.EventManager.OnObjectGrab +=
                    touch_start;
            Scene.EventManager.OnObjectGrabbing += 
                    touch;
            Scene.EventManager.OnObjectDeGrab +=
                    touch_end;
            Scene.EventManager.OnScriptChangedEvent +=
                    changed;
            Scene.EventManager.OnScriptAtTargetEvent +=
                    at_target;
            Scene.EventManager.OnScriptNotAtTargetEvent +=
                    not_at_target;
            Scene.EventManager.OnScriptAtRotTargetEvent +=
                    at_rot_target;
            Scene.EventManager.OnScriptNotAtRotTargetEvent +=
                    not_at_rot_target;
            Scene.EventManager.OnScriptControlEvent +=
                    control;
            Scene.EventManager.OnScriptColliderStart +=
                    collision_start;
            Scene.EventManager.OnScriptColliding +=
                    collision;
            Scene.EventManager.OnScriptCollidingEnd +=
                    collision_end;
            Scene.EventManager.OnScriptLandColliderStart += 
                    land_collision_start;
            Scene.EventManager.OnScriptLandColliding += 
                    land_collision;
            Scene.EventManager.OnScriptLandColliderEnd +=
                    land_collision_end;
            Scene.EventManager.OnAttach += attach;
            Scene.EventManager.OnScriptMovingStartEvent += moving_start;
            Scene.EventManager.OnScriptMovingEndEvent += moving_end;

            Scene.EventManager.OnRezScript += rez_script;
            Scene.EventManager.OnRezScripts += rez_scripts;


            IMoneyModule money =
                    Scene.RequestModuleInterface<IMoneyModule>();
            if (money != null) {
                money.OnObjectPaid+=HandleObjectPaid;
                money.OnPostObjectPaid += HandlePostObjectPaid;
            }
        }

        //private void HandleObjectPaid(UUID objectID, UUID agentID, int amount)
        private bool HandleObjectPaid(UUID objectID, UUID agentID, int amount)
        {
            bool ret = false;
            SceneObjectPart part = m_scriptEngine.findPrim(objectID);

            //if (part == null)
            //    return;

            //m_log.Debug("Paid: " + objectID + " from " + agentID + ", amount " + amount);
            //if (part.ParentGroup != null)
            //    part = part.ParentGroup.RootPart;

            //if (part != null)
            //{
            //    money(part.LocalId, agentID, amount);
            //}
            if (part != null) 
            {
                m_log.Debug("Paid: " + objectID + " from " + agentID + ", amount " + amount);
                if (part.ParentGroup != null) part = part.ParentGroup.RootPart;
                if (part != null)
                {
                    ret = money(part.LocalId, agentID, amount);
                }
            }
            return ret;
        }


        private bool HandlePostObjectPaid(uint localID, ulong regionHandle, UUID agentID, int amount)
        {
            bool ret = true;

            foreach (Scene scene in m_Scenes)
            {
                if (scene.RegionInfo.RegionHandle == regionHandle)
                {
                    ret = money(localID, agentID, amount);
                    break;
                }
            }

            return ret;
        }


        public void changed(SceneObjectPart part, uint change)
        {
            ScriptData[] datas = ScriptEngine.ScriptProtection.GetScripts(part.UUID);

            if (datas == null || datas.Length == 0)
            {
                datas = ScriptEngine.ScriptProtection.GetScripts(part.ParentGroup.RootPart.UUID);
                if (datas == null || datas.Length == 0)
                    return;
            }
            string functionName = "changed";
            object[] param = new Object[] { new LSL_Types.LSLInteger(change) };

            foreach (ScriptData ID in datas)
            {
                if (CheckIfEventShouldFire(ID, functionName, param))
                    m_scriptEngine.AddToScriptQueue(ID, functionName, new DetectParams[0], ID.VersionID, EventPriority.FirstStart, param);
            }
        }

        /// <summary>
        /// Handles piping the proper stuff to The script engine for touching
        /// Including DetectedParams
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="originalID"></param>
        /// <param name="offsetPos"></param>
        /// <param name="remoteClient"></param>
        /// <param name="surfaceArgs"></param>
        public void touch_start(SceneObjectPart part, Vector3 offsetPos,
                IClientAPI remoteClient, SurfaceTouchEventArgs surfaceArgs)
        {
            // Add to queue for all scripts in ObjectID object
            Dictionary<UUID, DetectParams> det = new Dictionary<UUID, DetectParams>();
            if (!CoalescedTouchEvents.TryGetValue(part.LocalId, out det))
                det = new Dictionary<UUID, DetectParams>();

            DetectParams detparam = new DetectParams();
            detparam.Key = remoteClient.AgentId;

            detparam.Populate(part.ParentGroup.Scene);
            detparam.LinkNum = part.LinkNum;
            
            if (surfaceArgs != null)
            {
                detparam.SurfaceTouchArgs = surfaceArgs;
            }

            det[remoteClient.AgentId] = detparam;
            CoalescedTouchEvents[part.LocalId] = det;

            ScriptData[] datas = ScriptEngine.ScriptProtection.GetScripts(part.UUID);

            if (datas == null || datas.Length == 0)
                return;

            string functionName = "touch_start";
            object[] param = new Object[] { new LSL_Types.LSLInteger(1) };

            foreach (ScriptData ID in datas)
            {
                if (CheckIfEventShouldFire(ID, functionName, param))
                    m_scriptEngine.AddToScriptQueue(ID, functionName, new List<DetectParams>(det.Values).ToArray(), ID.VersionID, EventPriority.FirstStart, param);
            }
        }

        public void touch(SceneObjectPart part, Vector3 offsetPos,
                IClientAPI remoteClient, SurfaceTouchEventArgs surfaceArgs)
        {
            Dictionary<UUID, DetectParams> det = new Dictionary<UUID, DetectParams>();
            if (!CoalescedTouchEvents.TryGetValue(part.LocalId, out det))
                det = new Dictionary<UUID, DetectParams>();

            // Add to queue for all scripts in ObjectID object
            DetectParams detparam = new DetectParams();
            detparam = new DetectParams();
            detparam.Key = remoteClient.AgentId;
            detparam.OffsetPos = new LSL_Types.Vector3(offsetPos.X,
                                                     offsetPos.Y,
                                                     offsetPos.Z);

            detparam.Populate(part.ParentGroup.Scene);
            detparam.LinkNum = part.LinkNum;

            if (surfaceArgs != null)
                detparam.SurfaceTouchArgs = surfaceArgs;

            det[remoteClient.AgentId] = detparam;
            CoalescedTouchEvents[part.LocalId] = det;

            ScriptData[] datas = ScriptEngine.ScriptProtection.GetScripts(part.UUID);

            if (datas == null || datas.Length == 0)
                return;

            string functionName = "touch";
            object[] param = new Object[] { new LSL_Types.LSLInteger(1) };

            foreach (ScriptData ID in datas)
            {
                if (CheckIfEventShouldFire(ID, functionName, param))
                    m_scriptEngine.AddToScriptQueue(ID, functionName, new List<DetectParams>(det.Values).ToArray(), ID.VersionID, EventPriority.FirstStart, param);
            }
        }

        public void touch_end(SceneObjectPart part, IClientAPI remoteClient,
                              SurfaceTouchEventArgs surfaceArgs)
        {
            Dictionary<UUID, DetectParams> det = new Dictionary<UUID, DetectParams>();
            if (!CoalescedTouchEvents.TryGetValue(part.LocalId, out det))
                det = new Dictionary<UUID, DetectParams>();

            // Add to queue for all scripts in ObjectID object
            DetectParams detparam = new DetectParams();
            detparam = new DetectParams();
            detparam.Key = remoteClient.AgentId;

            detparam.Populate(m_scriptEngine.findPrimsScene(part.LocalId));
            detparam.LinkNum = part.LinkNum;

            if (surfaceArgs != null)
                detparam.SurfaceTouchArgs = surfaceArgs;

            det[remoteClient.AgentId] = detparam;
            CoalescedTouchEvents[part.LocalId] = det;

            ScriptData[] datas = ScriptEngine.ScriptProtection.GetScripts(part.UUID);

            if (datas == null || datas.Length == 0)
                return; 
            
            string functionName = "touch_end";
            object[] param = new Object[] { new LSL_Types.LSLInteger(1) };

            foreach (ScriptData ID in datas)
            {
                if (CheckIfEventShouldFire(ID, functionName, param))
                    m_scriptEngine.AddToScriptQueue(ID, functionName, new List<DetectParams>(det.Values).ToArray(), ID.VersionID, EventPriority.FirstStart, param);
            }
            //Remove us from the det param list
            det.Remove(remoteClient.AgentId);
            CoalescedTouchEvents[part.LocalId] = det;
        }

        //public void money(uint localID, UUID agentID, int amount)
        public bool money(uint localID, UUID agentID, int amount)
        {
            bool ret = false;

            SceneObjectPart part = m_scriptEngine.findPrim(localID);
            if (part == null) return ret;
            ScriptData[] datas = ScriptEngine.ScriptProtection.GetScripts(part.UUID);

            if (datas == null || datas.Length == 0)
            {
                datas = ScriptEngine.ScriptProtection.GetScripts(part.ParentGroup.RootPart.UUID);
                if (datas == null || datas.Length == 0) return ret;
            }
            string functionName = "money";
            object[] param = new object[] {
                    new LSL_Types.LSLString(agentID.ToString()),
                    new LSL_Types.LSLInteger(amount) };

            foreach (ScriptData ID in datas)
            {
                if (CheckIfEventShouldFire(ID, functionName, param)) {
                    m_scriptEngine.AddToScriptQueue(ID, functionName, new DetectParams[0], ID.VersionID, EventPriority.FirstStart, param);
                    ret = true;
                }
            }

            return ret;
        }

        public void collision_start(SceneObjectPart part, ColliderArgs col)
        {
            // Add to queue for all scripts in ObjectID object
            List<DetectParams> det = new List<DetectParams>();

            foreach (DetectedObject detobj in col.Colliders)
            {
                DetectParams d = new DetectParams();
                d.Key = detobj.keyUUID;
                d.Populate(part.ParentGroup.Scene);
                det.Add(d);
            }

            if (det.Count > 0)
            {
                ScriptData[] datas = ScriptEngine.ScriptProtection.GetScripts(part.UUID);

                if (datas == null || datas.Length == 0)
                {
                    //datas = ScriptEngine.ScriptProtection.GetScripts(part.ParentGroup.RootPart.UUID);
                    //if (datas == null || datas.Length == 0)
                        return;
                }
                string functionName = "collision_start";
                object[] param = new Object[] { new LSL_Types.LSLInteger(det.Count) };

                foreach (ScriptData ID in datas)
                {
                    if (CheckIfEventShouldFire(ID, functionName, param))
                        m_scriptEngine.AddToScriptQueue(ID, functionName, det.ToArray(), ID.VersionID, EventPriority.FirstStart, param);
                }
            }
        }

        public void collision(SceneObjectPart part, ColliderArgs col)
        {
            // Add to queue for all scripts in ObjectID object
            List<DetectParams> det = new List<DetectParams>();

            foreach (DetectedObject detobj in col.Colliders)
            {
                DetectParams d = new DetectParams();
                d.Key = detobj.keyUUID;
                d.Populate(part.ParentGroup.Scene);
                det.Add(d);
            }

            if (det.Count > 0)
            {
                ScriptData[] datas = ScriptEngine.ScriptProtection.GetScripts(part.UUID);

                if (datas == null || datas.Length == 0)
                {
                    //datas = ScriptEngine.ScriptProtection.GetScripts(part.ParentGroup.RootPart.UUID);
                    //if (datas == null || datas.Length == 0)
                        return;
                }
                string functionName = "collision";
                object[] param = new Object[] { new LSL_Types.LSLInteger(det.Count) };

                foreach (ScriptData ID in datas)
                {
                    if (CheckIfEventShouldFire(ID, functionName, param))
                        m_scriptEngine.AddToScriptQueue(ID, functionName, det.ToArray(), ID.VersionID, EventPriority.FirstStart, param);
                }
            }
        }

        public void collision_end(SceneObjectPart part, ColliderArgs col)
        {
            // Add to queue for all scripts in ObjectID object
            List<DetectParams> det = new List<DetectParams>();

            foreach (DetectedObject detobj in col.Colliders)
            {
                DetectParams d = new DetectParams();
                d.Key = detobj.keyUUID;
                d.Populate(part.ParentGroup.Scene);
                det.Add(d);
            }

            if (det.Count > 0)
            {
                ScriptData[] datas = ScriptEngine.ScriptProtection.GetScripts(part.UUID);

                if (datas == null || datas.Length == 0)
                {
                    //datas = ScriptEngine.ScriptProtection.GetScripts(part.ParentGroup.RootPart.UUID);
                    //if (datas == null || datas.Length == 0)
                        return;
                }
                string functionName = "collision_end";
                object[] param = new Object[] { new LSL_Types.LSLInteger(det.Count) };

                foreach (ScriptData ID in datas)
                {
                    if (CheckIfEventShouldFire(ID, functionName, param))
                        m_scriptEngine.AddToScriptQueue(ID, functionName, det.ToArray(), ID.VersionID, EventPriority.FirstStart, param);
                }
            }
        }

        public void land_collision_start(SceneObjectPart part, ColliderArgs col)
        {
            List<DetectParams> det = new List<DetectParams>();

            foreach (DetectedObject detobj in col.Colliders)
            {
                DetectParams d = new DetectParams();
                d.Position = new LSL_Types.Vector3(detobj.posVector.X,
                    detobj.posVector.Y,
                    detobj.posVector.Z);
                d.Key = detobj.keyUUID;
                d.Populate(part.ParentGroup.Scene);
                det.Add(d);
            }
            if (det.Count != 0)
            {
                ScriptData[] datas = ScriptEngine.ScriptProtection.GetScripts(part.UUID);

                if (datas == null || datas.Length == 0)
                {
                    //datas = ScriptEngine.ScriptProtection.GetScripts(part.ParentGroup.RootPart.UUID);
                    //if (datas == null || datas.Length == 0)
                        return;
                }
                string functionName = "land_collision_start";
                object[] param = new Object[] { new LSL_Types.Vector3(det[0].Position) };

                foreach (ScriptData ID in datas)
                {
                    if (CheckIfEventShouldFire(ID, functionName, param))
                        m_scriptEngine.AddToScriptQueue(ID, functionName, det.ToArray(), ID.VersionID, EventPriority.FirstStart, param);
                }
            }
        }

        public void land_collision(SceneObjectPart part, ColliderArgs col)
        {
            List<DetectParams> det = new List<DetectParams>();

            foreach (DetectedObject detobj in col.Colliders)
            {
                DetectParams d = new DetectParams();
                d.Position = new LSL_Types.Vector3(detobj.posVector.X,
                    detobj.posVector.Y,
                    detobj.posVector.Z);
                d.Key = detobj.keyUUID;
                d.Populate(part.ParentGroup.Scene);
                det.Add(d);
            }
            if (det.Count != 0)
            {
                ScriptData[] datas = ScriptEngine.ScriptProtection.GetScripts(part.UUID);

                if (datas == null || datas.Length == 0)
                {
                    //datas = ScriptEngine.ScriptProtection.GetScripts(part.ParentGroup.RootPart.UUID);
                    //if (datas == null || datas.Length == 0)
                        return;
                }
                string functionName = "land_collision";
                object[] param = new Object[] { new LSL_Types.Vector3(det[0].Position) };

                foreach (ScriptData ID in datas)
                {
                    if (CheckIfEventShouldFire(ID, functionName, param))
                        m_scriptEngine.AddToScriptQueue(ID, functionName, det.ToArray(), ID.VersionID, EventPriority.FirstStart, param);
                }
            }
        }

        public void land_collision_end(SceneObjectPart part, ColliderArgs col)
        {
            List<DetectParams> det = new List<DetectParams>();

            foreach (DetectedObject detobj in col.Colliders)
            {
                DetectParams d = new DetectParams();
                d.Position = new LSL_Types.Vector3(detobj.posVector.X,
                    detobj.posVector.Y,
                    detobj.posVector.Z);
                d.Key = detobj.keyUUID;
                d.Populate(part.ParentGroup.Scene);
                det.Add(d);
            }
            if (det.Count != 0)
            {
                ScriptData[] datas = ScriptEngine.ScriptProtection.GetScripts(part.UUID);

                if (datas == null || datas.Length == 0)
                {
                    //datas = ScriptEngine.ScriptProtection.GetScripts(part.ParentGroup.RootPart.UUID);
                    //if (datas == null || datas.Length == 0)
                        return;
                }
                string functionName = "land_collision_end";
                object[] param = new Object[] { new LSL_Types.Vector3(det[0].Position) };

                foreach (ScriptData ID in datas)
                {
                    if (CheckIfEventShouldFire(ID, functionName, param))
                        m_scriptEngine.AddToScriptQueue(ID, functionName, det.ToArray(), ID.VersionID, EventPriority.FirstStart, param);
                }
            }
        }

        public void control(SceneObjectPart part, UUID itemID, UUID agentID, uint held, uint change)
        {
            ScriptData ID = ScriptEngine.ScriptProtection.GetScript(part.UUID, itemID);

            if (ID == null)
                return;

            string functionName = "control";
            object[] param = new object[] {
                    new LSL_Types.LSLString(agentID.ToString()),
                    new LSL_Types.LSLInteger(held),
                    new LSL_Types.LSLInteger(change)};

            if (CheckIfEventShouldFire(ID, functionName, param))
                m_scriptEngine.AddToScriptQueue(ID, functionName, new DetectParams[0], ID.VersionID, EventPriority.FirstStart, param);
        }

        public void email(uint localID, UUID itemID, string timeSent,
                string address, string subject, string message, int numLeft)
        {
            SceneObjectPart part = m_scriptEngine.findPrim(localID);
            if (part == null)
                return;
            ScriptData[] datas = ScriptEngine.ScriptProtection.GetScripts(part.UUID);

            if (datas == null || datas.Length == 0)
            {
                datas = ScriptEngine.ScriptProtection.GetScripts(part.ParentGroup.RootPart.UUID);
                if (datas == null || datas.Length == 0)
                    return;
            }
            string functionName = "email";
            object[] param = new object[] {
                    new LSL_Types.LSLString(timeSent),
                    new LSL_Types.LSLString(address),
                    new LSL_Types.LSLString(subject),
                    new LSL_Types.LSLString(message),
                    new LSL_Types.LSLInteger(numLeft)};

            foreach (ScriptData ID in datas)
            {
                if (CheckIfEventShouldFire(ID, functionName, param))
                    m_scriptEngine.AddToScriptQueue(ID, functionName, new DetectParams[0], ID.VersionID, EventPriority.FirstStart, param);
            }
        }

        public void at_target(uint localID, uint handle, Vector3 targetpos,
                Vector3 atpos)
        {
            SceneObjectPart part = m_scriptEngine.findPrim(localID);
            if (part == null)
                return;
            ScriptData[] datas = ScriptEngine.ScriptProtection.GetScripts(part.UUID);

            if (datas == null || datas.Length == 0)
            {
                datas = ScriptEngine.ScriptProtection.GetScripts(part.ParentGroup.RootPart.UUID);
                if (datas == null || datas.Length == 0)
                    return;
            }
            string functionName = "at_target";
            object[] param = new object[] {
                    new LSL_Types.LSLInteger(handle),
                    new LSL_Types.Vector3(targetpos.X,targetpos.Y,targetpos.Z),
                    new LSL_Types.Vector3(atpos.X,atpos.Y,atpos.Z) };

            foreach (ScriptData ID in datas)
            {
                if (CheckIfEventShouldFire(ID, functionName, param))
                    m_scriptEngine.AddToScriptQueue(ID, functionName, new DetectParams[0], ID.VersionID, EventPriority.FirstStart, param);
            }
        }

        public void not_at_target(uint localID)
        {
            SceneObjectPart part = m_scriptEngine.findPrim(localID);
            if (part == null)
                return;
            ScriptData[] datas = ScriptEngine.ScriptProtection.GetScripts(part.UUID);

            if (datas == null || datas.Length == 0)
            {
                datas = ScriptEngine.ScriptProtection.GetScripts(part.ParentGroup.RootPart.UUID);
                if (datas == null || datas.Length == 0)
                    return;
            }
            string functionName = "not_at_target";
            object[] param = new object[0];

            foreach (ScriptData ID in datas)
            {
                if (CheckIfEventShouldFire(ID, functionName, param))
                    m_scriptEngine.AddToScriptQueue(ID, functionName, new DetectParams[0], ID.VersionID, EventPriority.FirstStart, param);
            }
        }

        public void at_rot_target(uint localID, uint handle, Quaternion targetrot,
                Quaternion atrot)
        {
            SceneObjectPart part = m_scriptEngine.findPrim(localID);
            if (part == null)
                return;
            ScriptData[] datas = ScriptEngine.ScriptProtection.GetScripts(part.UUID);

            if (datas == null || datas.Length == 0)
            {
                datas = ScriptEngine.ScriptProtection.GetScripts(part.ParentGroup.RootPart.UUID);
                if (datas == null || datas.Length == 0)
                    return;
            }
            string functionName = "at_rot_target";
            object[] param = new object[] {
                    new LSL_Types.LSLInteger(handle),
                    new LSL_Types.Quaternion(targetrot.X,targetrot.Y,targetrot.Z,targetrot.W),
                    new LSL_Types.Quaternion(atrot.X,atrot.Y,atrot.Z,atrot.W) };

            foreach (ScriptData ID in datas)
            {
                if (CheckIfEventShouldFire(ID, functionName, param))
                    m_scriptEngine.AddToScriptQueue(ID, functionName, new DetectParams[0], ID.VersionID, EventPriority.FirstStart, param);
            }
        }

        public void not_at_rot_target(uint localID)
        {
            SceneObjectPart part = m_scriptEngine.findPrim(localID);
            if (part == null)
                return;
            ScriptData[] datas = ScriptEngine.ScriptProtection.GetScripts(part.UUID);

            if (datas == null || datas.Length == 0)
            {
                datas = ScriptEngine.ScriptProtection.GetScripts(part.ParentGroup.RootPart.UUID);
                if (datas == null || datas.Length == 0)
                    return;
            }
            string functionName = "not_at_rot_target";
            object[] param = new object[0];

            foreach (ScriptData ID in datas)
            {
                if (CheckIfEventShouldFire(ID, functionName, param))
                    m_scriptEngine.AddToScriptQueue(ID, functionName, new DetectParams[0], ID.VersionID, EventPriority.FirstStart, param);
            }
        }

        public void attach(uint localID, UUID itemID, UUID avatar)
        {
            SceneObjectPart part = m_scriptEngine.findPrim(localID);
            if (part == null)
                return;
            ScriptData[] datas = ScriptEngine.ScriptProtection.GetScripts(part.UUID);

            if (datas == null || datas.Length == 0)
            {
                datas = ScriptEngine.ScriptProtection.GetScripts(part.ParentGroup.RootPart.UUID);
                if (datas == null || datas.Length == 0)
                    return;
            }
            string functionName = "attach";
            object[] param = new object[] {
                    new LSL_Types.LSLString(avatar.ToString()) };

            foreach (ScriptData ID in datas)
            {
                if (CheckIfEventShouldFire(ID, functionName, param))
                    m_scriptEngine.AddToScriptQueue(ID, functionName, new DetectParams[0], ID.VersionID, EventPriority.FirstStart, param);
            }
        }

        public void moving_start(SceneObjectPart part)
        {
            ScriptData[] datas = ScriptEngine.ScriptProtection.GetScripts(part.UUID);

            if (datas == null || datas.Length == 0)
            {
                datas = ScriptEngine.ScriptProtection.GetScripts(part.ParentGroup.RootPart.UUID);
                if (datas == null || datas.Length == 0)
                    return;
            }
            string functionName = "moving_start";
            object[] param = new object[0];

            foreach (ScriptData ID in datas)
            {
                if (CheckIfEventShouldFire(ID, functionName, param))
                    m_scriptEngine.AddToScriptQueue(ID, functionName, new DetectParams[0], ID.VersionID, EventPriority.FirstStart, param);
            }
        }

        public void moving_end(SceneObjectPart part)
        {
            ScriptData[] datas = ScriptEngine.ScriptProtection.GetScripts(part.UUID);

            if (datas == null || datas.Length == 0)
            {
                datas = ScriptEngine.ScriptProtection.GetScripts(part.ParentGroup.RootPart.UUID);
                if (datas == null || datas.Length == 0)
                    return;
            }
            string functionName = "moving_end";
            object[] param = new object[0];

            foreach (ScriptData ID in datas)
            {
                if (CheckIfEventShouldFire(ID, functionName, param))
                    m_scriptEngine.AddToScriptQueue(ID, functionName, new DetectParams[0], ID.VersionID, EventPriority.FirstStart, param);
            }
        }

        /// <summary>
        /// Start one script in an object
        /// </summary>
        /// <param name="part"></param>
        /// <param name="itemID"></param>
        /// <param name="script"></param>
        /// <param name="startParam"></param>
        /// <param name="postOnRez"></param>
        /// <param name="engine"></param>
        /// <param name="stateSource"></param>
        public void rez_script(SceneObjectPart part, UUID itemID, string script,
                int startParam, bool postOnRez, string engine, int stateSource)
        {
            if (script.StartsWith("//MRM:"))
                return;

            List<IScriptModule> engines =
                new List<IScriptModule>(
                m_Scenes[0].RequestModuleInterfaces<IScriptModule>());

            List<string> names = new List<string>();
            foreach (IScriptModule m in engines)
                names.Add(m.ScriptEngineName);

            int lineEnd = script.IndexOf('\n');

            if (lineEnd > 1)
            {
                string firstline = script.Substring(0, lineEnd).Trim();

                int colon = firstline.IndexOf(':');
                if (firstline.Length > 2 &&
                    firstline.Substring(0, 2) == "//" && colon != -1)
                {
                    string engineName = firstline.Substring(2, colon - 2);

                    if (names.Contains(engineName))
                    {
                        engine = engineName;
                        script = "//" + script.Substring(script.IndexOf(':') + 1);
                    }
                    else
                    {
                        if (engine == m_scriptEngine.ScriptEngineName)
                        {
                            TaskInventoryItem item =
                                    part.Inventory.GetInventoryItem(itemID);

                            ScenePresence presence =
                                    part.ParentGroup.Scene.GetScenePresence(
                                    item.OwnerID);

                            if (presence != null)
                            {
                                presence.ControllingClient.SendAgentAlertMessage(
                                         "Selected engine unavailable. " +
                                         "Running script on " +
                                         m_scriptEngine.ScriptEngineName,
                                         false);
                            }
                        }
                    }
                }
            }

            if (engine != m_scriptEngine.ScriptEngineName)
                return;

            LUStruct itemToQueue = m_scriptEngine.StartScript(part, itemID, script,
                    startParam, postOnRez, (StateSource)stateSource, UUID.Zero);
            if (itemToQueue.Action != LUType.Unknown)
                m_scriptEngine.MaintenanceThread.AddScriptChange(new LUStruct[] { itemToQueue }, LoadPriority.FirstStart);
        }

        /// <summary>
        /// Start multiple scripts in the object
        /// </summary>
        /// <param name="part"></param>
        /// <param name="items"></param>
        /// <param name="startParam"></param>
        /// <param name="postOnRez"></param>
        /// <param name="engine"></param>
        /// <param name="stateSource"></param>
        /// <param name="RezzedFrom"></param>
        public void rez_scripts(SceneObjectPart part, TaskInventoryItem[] items,
                int startParam, bool postOnRez, string engine, int stateSource, UUID RezzedFrom)
        {
            List<TaskInventoryItem> ItemsToStart = new List<TaskInventoryItem>();
            foreach (TaskInventoryItem item in items)
            {
                AssetBase asset = m_Scenes[0].AssetService.Get(item.AssetID.ToString());
                if (null == asset)
                {
                    m_log.ErrorFormat(
                        "[PRIM INVENTORY]: " +
                        "Couldn't start script {0}, {1} since asset ID {4} could not be found",
                        item.Name, item.ItemID, item.AssetID);
                    continue;
                }
                string script = OpenMetaverse.Utils.BytesToString(asset.Data);

                if (script.StartsWith("//MRM:"))
                    return;

                List<IScriptModule> engines =
                    new List<IScriptModule>(
                    m_Scenes[0].RequestModuleInterfaces<IScriptModule>());

                List<string> names = new List<string>();
                foreach (IScriptModule m in engines)
                    names.Add(m.ScriptEngineName);

                int lineEnd = script.IndexOf('\n');

                if (lineEnd > 1)
                {
                    string firstline = script.Substring(0, lineEnd).Trim();

                    int colon = firstline.IndexOf(':');
                    if (firstline.Length > 2 &&
                        firstline.Substring(0, 2) == "//" && colon != -1)
                    {
                        string engineName = firstline.Substring(2, colon - 2);

                        if (names.Contains(engineName))
                        {
                            engine = engineName;
                            script = "//" + script.Substring(script.IndexOf(':') + 1);
                        }
                        else
                        {
                            if (engine == m_scriptEngine.ScriptEngineName)
                            {
                                ScenePresence presence =
                                        part.ParentGroup.Scene.GetScenePresence(
                                        item.OwnerID);

                                if (presence != null)
                                {
                                    presence.ControllingClient.SendAgentAlertMessage(
                                             "Selected engine '" + engineName +"' is unavailable. " +
                                             "Running script on " +
                                             m_scriptEngine.ScriptEngineName,
                                             false);
                                }
                            }
                        }
                    }
                }

                if (engine != m_scriptEngine.ScriptEngineName)
                    return;
                ItemsToStart.Add(item);
            }

            List<LUStruct> ItemsToQueue = new List<LUStruct>();
            foreach (TaskInventoryItem item in ItemsToStart)
            {
                AssetBase asset = m_Scenes[0].AssetService.Get(item.AssetID.ToString());
                if (null == asset)
                {
                    m_log.ErrorFormat(
                        "[PRIM INVENTORY]: " +
                        "Couldn't start script {0}, {1} since asset ID {4} could not be found",
                        item.Name, item.ItemID, item.AssetID);
                    continue;
                }
                string script = OpenMetaverse.Utils.BytesToString(asset.Data);

                LUStruct itemToQueue = m_scriptEngine.StartScript(part, item.ItemID, script,
                        startParam, postOnRez, (StateSource)stateSource, RezzedFrom);
                if (itemToQueue.Action != LUType.Unknown)
                    ItemsToQueue.Add(itemToQueue);
            }
            if (ItemsToQueue.Count != 0)
                m_scriptEngine.MaintenanceThread.AddScriptChange(ItemsToQueue.ToArray(), LoadPriority.FirstStart);

        }
        
        /// <summary>
        /// This checks the minimum amount of time between script firings as well as control events, making sure that events do NOT fire after scripts reset, close or restart, etc
        /// </summary>
        /// <param name="ID"></param>
        /// <param name="FunctionName"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        private bool CheckIfEventShouldFire(ScriptData ID, string FunctionName, object[] param)
        {
            //This will happen if the script doesn't compile correctly
            if (ID.Script == null)
            {
                m_log.Info("[AuroraDotNetEngine]: Could not load script from item '" + ID.InventoryItem.Name + "' to fire event " + FunctionName);
                return false;
            }
            scriptEvents eventType = (scriptEvents)Enum.Parse(typeof(scriptEvents), FunctionName);
            if ((ID.Script.GetStateEventFlags(ID.State) & (long)eventType) == 0)
                return false; //If the script doesn't contain the state, don't even bother queueing it

            if (eventType == scriptEvents.timer)
            {
                if (ID.TimerQueued)
                    return false;
                ID.TimerQueued = true;
            }
            else if (eventType == scriptEvents.control)
            {
                int held = ((LSL_Types.LSLInteger)param[1]).value;
                // int changed = ((LSL_Types.LSLInteger)data.Params[2]).value;

                // If the last message was a 0 (nothing held)
                // and this one is also nothing held, drop it
                //
                if (ID.LastControlLevel == held && held == 0)
                    return true;

                // If there is one or more queued, then queue
                // only changed ones, else queue unconditionally
                //
                if (ID.ControlEventsInQueue > 0)
                {
                    if (ID.LastControlLevel == held)
                        return false;
                }
            }
            else if (eventType == scriptEvents.collision)
            {
                if (ID.CollisionInQueue || ID.RemoveCollisionEvents)
                    return false;

                ID.CollisionInQueue = true;
            }
            else if (eventType == scriptEvents.moving_start)
            {
                if (ID.MovingInQueue) //Block all other moving_starts until moving_end is called
                    return false;
                ID.MovingInQueue = true;
            }
            else if (eventType == scriptEvents.moving_end)
            {
                if (!ID.MovingInQueue) //If we get a moving_end after we have sent one event, don't fire another
                    return false;
            }
            else if (eventType == scriptEvents.collision_start)
            {
                ID.RemoveCollisionEvents = false;
            }
            else if (eventType == scriptEvents.collision_end)
            {
                if (ID.RemoveCollisionEvents)
                    return false;
            }
            else if (eventType == scriptEvents.touch)
            {
                if (ID.TouchInQueue || ID.RemoveTouchEvents)
                    return false;

                ID.TouchInQueue = true;
            }
            else if (eventType == scriptEvents.touch_start)
            {
                ID.RemoveTouchEvents = false;
            }
            else if (eventType == scriptEvents.touch_end)
            {
                if (ID.RemoveTouchEvents)
                    return false;
            }
            else if (eventType == scriptEvents.land_collision)
            {
                if (ID.LandCollisionInQueue || ID.RemoveLandCollisionEvents)
                    return false;

                ID.LandCollisionInQueue = true;
            }
            else if (eventType == scriptEvents.land_collision_start)
            {
                ID.RemoveLandCollisionEvents = false;
            }
            else if (eventType == scriptEvents.land_collision_end)
            {
                if (ID.RemoveLandCollisionEvents)
                    return false;
            }
            else if (eventType == scriptEvents.changed)
            {
                Changed changed = (Changed)(((LSL_Types.LSLInteger)param[0]).value);
                if (ID.ChangedInQueue.Contains(changed))
                    return false;
                ID.ChangedInQueue.Add(changed);
            }

            if (FunctionName == "state_entry")
            {
                ID.ResetEvents();
            }
            return true;
        }
        
        /// <summary>
        /// This removes the event from the queue and allows it to be fired again
        /// </summary>
        /// <param name="QIS"></param>
        public void EventComplete(QueueItemStruct QIS)
        {
            if (QIS.functionName == "timer")
                QIS.ID.TimerQueued = false;
            else if (QIS.functionName == "control")
            {
                if (QIS.ID.ControlEventsInQueue > 0)
                    QIS.ID.ControlEventsInQueue--;
            }
            else if (QIS.functionName == "collision")
                QIS.ID.CollisionInQueue = false;
            else if (QIS.functionName == "moving_end")
                QIS.ID.MovingInQueue = false;
            else if (QIS.functionName == "touch")
                QIS.ID.TouchInQueue = false;
            else if (QIS.functionName == "land_collision")
                QIS.ID.LandCollisionInQueue = false;
            else if (QIS.functionName == "changed")
            {
                Changed changed = (Changed)Enum.Parse(typeof(Changed),QIS.param[0].ToString());
                QIS.ID.ChangedInQueue.Remove(changed);
            }
        }
    }
}
