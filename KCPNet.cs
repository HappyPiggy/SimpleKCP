using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleKCP {
    [Serializable]
    public abstract class KCPMsg { }

    public class KCPNet<T, K>
        where T : KCPSession<K>, new()
        where K : KCPMsg, new() {
        UdpClient udp;
        IPEndPoint remotePoint;

        private CancellationTokenSource cts;
        private CancellationToken ct;

        public KCPNet() {
            cts = new CancellationTokenSource();
            ct = cts.Token;
        }

        #region Server
        private Dictionary<uint, T> sessionDic = null;
        public void StartAsServer(string ip, int port) {
            sessionDic = new Dictionary<uint, T>();

            udp = new UdpClient(new IPEndPoint(IPAddress.Parse(ip), port));
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                udp.Client.IOControl((IOControlCode)(-1744830452), new byte[] { 0, 0, 0, 0 }, null);
            }
            remotePoint = new IPEndPoint(IPAddress.Parse(ip), port);
            Tools.ColorLog(KCPLogColor.Green, "Server Start...");
            Task.Run(ServerRecive, ct);
        }
        async void ServerRecive() {
            UdpReceiveResult result;
            while(true) {
                try {
                    if(ct.IsCancellationRequested) {
                        Tools.ColorLog(KCPLogColor.Cyan, "SeverRecive Task is Cancelled.");
                        break;
                    }
                    result = await udp.ReceiveAsync();
                    uint sid = BitConverter.ToUInt32(result.Buffer, 0);
                    if(sid == 0) {
                        sid = GenerateUniqueSessionID();
                        byte[] sid_bytes = BitConverter.GetBytes(sid);
                        byte[] conv_bytes = new byte[8];
                        Array.Copy(sid_bytes, 0, conv_bytes, 4, 4);
                        SendUDPMsg(conv_bytes, result.RemoteEndPoint);
                    }
                    else {
                        if(!sessionDic.TryGetValue(sid, out T session)) {
                            session = new T();
                            session.InitSession(sid, SendUDPMsg, result.RemoteEndPoint);
                            session.OnSessionClose = OnServerSessionClose;
                            lock(sessionDic) {
                                sessionDic.Add(sid, session);
                            }
                        }
                        else {
                            session = sessionDic[sid];
                        }
                        session.ReciveData(result.Buffer);
                    }
                }
                catch(Exception e) {
                    Tools.Warn("Server Udp Recive Data Exception:{0}", e.ToString());
                }
            }
        }
        void OnServerSessionClose(uint sid) {
            if(sessionDic.ContainsKey(sid)) {
                lock(sessionDic) {
                    sessionDic.Remove(sid);
                    Tools.Warn("Session:{0} remove from sessionDic.", sid);
                }
            }
            else {
                Tools.Error("Session:{0} cannot find in sessionDic", sid);
            }
        }
        public void CloseServer() {
            foreach(var item in sessionDic) {
                item.Value.CloseSession();
            }
            sessionDic = null;

            if(udp != null) {
                udp.Close();
                udp = null;
                cts.Cancel();
            }
        }
        #endregion

        #region Client
        public T clientSession;
        public void StartAsClient(string ip, int port) {
            udp = new UdpClient(0);
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                udp.Client.IOControl((IOControlCode)(-1744830452), new byte[] { 0, 0, 0, 0 }, null);
            }
            remotePoint = new IPEndPoint(IPAddress.Parse(ip), port);
            Tools.ColorLog(KCPLogColor.Green, "Client Start...");

            Task.Run(ClientRecive, ct);
        }
        public Task<bool> ConnectServer(int interval, int maxintervalSum = 5000) {
            SendUDPMsg(new byte[4], remotePoint);
            int checkTimes = 0;
            Task<bool> task = Task.Run(async () => {
                while(true) {
                    await Task.Delay(interval);
                    checkTimes += interval;
                    if(clientSession != null && clientSession.IsConnected()) {
                        return true;
                    }
                    else {
                        if(checkTimes > maxintervalSum) {
                            return false;
                        }
                    }
                }
            });
            return task;
        }

        async void ClientRecive() {
            UdpReceiveResult result;
            while(true) {
                try {
                    if(ct.IsCancellationRequested) {
                        Tools.ColorLog(KCPLogColor.Cyan, "ClientRecive Task is Cancelled.");
                        break;
                    }
                    result = await udp.ReceiveAsync();

                    if(Equals(remotePoint, result.RemoteEndPoint)) {
                        uint sid = BitConverter.ToUInt32(result.Buffer, 0);
                        if(sid == 0) {
                            //sid 数据
                            if(clientSession != null && clientSession.IsConnected()) {
                                //已经建立连接，初始化完成了，收到了多的sid,直接丢弃。
                                Tools.Warn("Client is Init Done,Sid Surplus.");
                            }
                            else {
                                //未初始化，收到服务器分配的sid数据，初始化一个客户端session
                                sid = BitConverter.ToUInt32(result.Buffer, 4);
                                Tools.ColorLog(KCPLogColor.Green, "UDP Request Conv Sid:{0}", sid);

                                //会话处理
                                clientSession = new T();
                                clientSession.InitSession(sid, SendUDPMsg, remotePoint);
                                clientSession.OnSessionClose = OnClientSessionClose;
                            }
                        }
                        else {
                            //处理业务逻辑
                            if(clientSession != null && clientSession.IsConnected()) {
                                clientSession.ReciveData(result.Buffer);
                            }
                            else {
                                //没初始化且sid!=0，数据消息提前到了，直接丢弃消息，直到初
                                //始化完成，kcp重传再开始处理。
                                Tools.Warn("Client is Initing...");
                            }
                        }
                    }
                    else {
                        Tools.Warn("Client Udp Recive illegal target Data.");
                    }
                }
                catch(Exception e) {
                    Tools.Warn("Client Udp Recive Data Exception:{0}", e.ToString());
                }
            }
        }
        void OnClientSessionClose(uint sid) {
            cts.Cancel();
            if(udp != null) {
                udp.Close();
                udp = null;
            }
            Tools.Warn("Client Session Close,sid:{0}", sid);
        }
        public void CloseClient() {
            if(clientSession != null) {
                clientSession.CloseSession();
                clientSession = null;
            }
        }
        #endregion

        void SendUDPMsg(byte[] bytes, IPEndPoint remotePoint) {
            if(udp != null) {
                udp.SendAsync(bytes, bytes.Length, remotePoint);
            }
        }
        public void BroadCastMsg(K msg) {
            byte[] bytes = Tools.Serialize<K>(msg);
            foreach(var item in sessionDic) {
                item.Value.SendMsg(bytes);
            }
        }
        private uint sid = 0;
        public uint GenerateUniqueSessionID() {
            lock(sessionDic) {
                while(true) {
                    ++sid;
                    if(sid == uint.MaxValue) {
                        sid = 1;
                    }
                    if(!sessionDic.ContainsKey(sid)) {
                        break;
                    }
                }
            }
            return sid;
        }
    }
}
