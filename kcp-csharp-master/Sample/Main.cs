using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;


enum UDPStatus
{
    eUDPStatus_UNKNOWN = 0,

    eUDPStatus_CONNECT_REQ = 1,
    eUDPStatus_CONNECTING = 6,

    eUDPStatus_CONNECT_RSP = 2,
    eUDPStatus_CONNECTED = 7,

    eUDPStatus_DISCONNECT = 3,
    eUDPStatus_TRANSMIT = 4,
    eUDPStatus_KEEP_LIVE = 5,
    eUDPStatus_DEAD = 8,
};

namespace KcpProject.Sample
{
    class Program
    {
        static bool s_IsConnected = false;
        static bool s_IsConnecting = false;
        static Int64 keepLiveCount = 0;
        private static int liveCount = 0;

        static void Main(string[] args)
        {
            var connection = new UDPSession();
            connection.Connect("10.0.2.156", 7788);// 服务器
                                                   //            connection.Connect("10.0.18.57", 7788);// 吴宇

            var firstSend = true;
            var buffer = new byte[1024 * 32];
            var counter = 0;

            UDPStatus lUDPStatus = UDPStatus.eUDPStatus_CONNECT_REQ;

            bool waitTransmit = false;

            while (true)
            {
                connection.Update();

                switch (lUDPStatus)
                {
                    case UDPStatus.eUDPStatus_CONNECT_REQ:
                        {
                            if (connection.ConnectUDP() < 0)
                            {
                                Console.WriteLine("Write message failed.");
                                break;
                            }
                            lUDPStatus = UDPStatus.eUDPStatus_CONNECTING;
                        }
                        break;
                    case UDPStatus.eUDPStatus_CONNECTING:
                        {
                            var n = connection.RecvUDP(buffer, 0, buffer.Length);

                            if (n == 0)
                            {
                                Thread.Sleep(10);
                                continue;
                            }
                            else if (n < 0)
                            {
                                Console.WriteLine("Receive Message failed.");
                                break;
                            }
                            else
                            {
                                UDPProtocolConnectRsp rsp = UDPSession.ByteToStructure<UDPProtocolConnectRsp>(buffer);
                                if (rsp != null)
                                {
                                    if (connection.CheckConv(rsp.conv))
                                    {
                                        if (rsp.p_type == (int)UDPStatus.eUDPStatus_CONNECT_RSP)
                                        {

                                            lUDPStatus = UDPStatus.eUDPStatus_CONNECTED;
                                            Console.WriteLine("Connect Success");
                                        }
                                    }
                                    else
                                    {
                                        lUDPStatus = UDPStatus.eUDPStatus_UNKNOWN;
                                        Console.WriteLine("Session Conv Error");
                                        break;
                                    }
                                }
                            }
                        }
                        break;
                    case UDPStatus.eUDPStatus_CONNECTED:
                    case UDPStatus.eUDPStatus_KEEP_LIVE:
                        {
                            bool live = false;

                            connection.SendKeepLive();
                            connection.TestTransimitKCP();

                            UDPStatus n = connection.RecvKcp(buffer, 0, buffer.Length);
                            switch (n)
                            {
                                case UDPStatus.eUDPStatus_KEEP_LIVE:
                                    {
                                        keepLiveCount++;
                                        Console.WriteLine("KeepLive:" + keepLiveCount);
                                        lUDPStatus = UDPStatus.eUDPStatus_KEEP_LIVE;
                                        live = true;
                                    }
                                    break;

                                case UDPStatus.eUDPStatus_TRANSMIT:
                                    {
                                        GameProtocol data = UDPSession.ByteToStructure<GameProtocol>(buffer);
                                        if (data != null)
                                        {
                                            Console.WriteLine("TestTransimit............ receive data : " + data.data);
                                            live = true;
                                        }
                                        waitTransmit = false;
                                    }
                                    break;
                            }

                            if (live)
                            {
                                liveCount = 0;
                            }
                            else
                            {
                                liveCount++;
//                                Thread.Sleep(1000);
                            }

                            if (liveCount > 5)
                            {
//                                lUDPStatus = UDPStatus.eUDPStatus_DISCONNECT;
                            }

                            //                            int rn = 0;
                            //                            if (lUDPStatus == UDPStatus.eUDPStatus_KEEP_LIVE)
                            //                            {
                            //                                if (!waitTransmit)
                            //                                {
                            //                                    rn = connection.TestTransimitKCP();
                            //                                    waitTransmit = true;
                            //                                    if (rn < 0)
                            //                                    {
                            //                                        Console.WriteLine("TestTransimit............failed !!!!!.");
                            //                                    }
                            //                                    else if (rn > 0)
                            //                                    {
                            //                                        var n = connection.RecvUDP(buffer, 0, buffer.Length);
                            //                                        if (n > 0)
                            //                                        {
                            //                                            UDPProtocolTransmit transmit = UDPSession.ByteToStructure<UDPProtocolTransmit>(buffer);
                            //                                            if (transmit != null &&
                            //                                                connection.CheckConv(transmit.conv) &&
                            //                                                transmit.p_type == (int)UDPStatus.eUDPStatus_TRANSMIT)
                            //                                            {
                            //                                                Console.WriteLine("TestTransimit............ receive data : " + transmit);
                            //                                            }
                            //                                        }
                            //                                    }
                            //                                }
                            //                                else
                            //                                {
                            //                                    var n = connection.RecvUDP(buffer, 0, buffer.Length);
                            //                                    if (n > 0)
                            //                                    {
                            //                                        UDPProtocolTransmit transmit = UDPSession.ByteToStructure<UDPProtocolTransmit>(buffer);
                            //                                        if (transmit != null &&
                            //                                            connection.CheckConv(transmit.conv) &&
                            //                                            transmit.p_type == (int)UDPStatus.eUDPStatus_TRANSMIT)
                            //                                        {
                            //                                            Console.WriteLine("TestTransimit............ receive.");
                            //                                            waitTransmit = false;
                            //                                        }
                            //                                    }
                            //                                }
                            //                            }

                            /////////////////////////////////////////////////////////////////
                            //                            Thread.Sleep(1100);
                            //                            rn = connection.SendKeepLive();
                            //                            if (rn < 0)
                            //                            {
                            //                                Console.WriteLine("Write message failed.");
                            //                                break;
                            //                            }
                            //                            else if (rn > 0)
                            //                            {
                            //                                Thread.Sleep(10);
                            //                                var n = connection.RecvUDP(buffer, 0, buffer.Length);
                            //                                if (n > 0)
                            //                                {
                            //                                    UDPProtocolKeepLive keepLive =
                            //                                        UDPSession.ByteToStructure<UDPProtocolKeepLive>(buffer);
                            //                                    if (keepLive != null &&
                            //                                        connection.CheckConv(keepLive.conv) &&
                            //                                        keepLive.p_type == (int)UDPStatus.eUDPStatus_KEEP_LIVE)
                            //                                    {
                            //                                        lUDPStatus = UDPStatus.eUDPStatus_KEEP_LIVE;
                            //                                        keepLiveCount++;
                            //                                        Console.WriteLine("KeepLive:" + keepLiveCount);
                            //                                        break;
                            //                                    }
                            //                                }
                            //                            }

                            //                            int checkNum = 0;
                            //                            for (int i = 0; i < 5; i++)
                            //                            {
                            //                                Thread.Sleep(1000);
                            //                                rn = connection.SendKeepLive();
                            //                                if (rn < 0)
                            //                                {
                            //                                    Console.WriteLine("Write message failed.");
                            //                                    checkNum++;
                            //                                    continue;
                            //                                }
                            //                                else if (rn > 0)
                            //                                {
                            //                                    Thread.Sleep(10);
                            //                                    var n = connection.RecvUDP(buffer, 0, buffer.Length);
                            //                                    if (n > 0)
                            //                                    {
                            //                                        UDPProtocolKeepLive keepLive =
                            //                                            UDPSession.ByteToStructure<UDPProtocolKeepLive>(buffer);
                            //                                        if (keepLive != null &&
                            //                                            connection.CheckConv(keepLive.conv) &&
                            //                                            keepLive.p_type == (int)UDPStatus.eUDPStatus_KEEP_LIVE)
                            //                                        {
                            //                                            keepLiveCount++;
                            //                                            Console.WriteLine("KeepLive:" + keepLiveCount);
                            //                                            break;
                            //                                        }
                            //                                    }
                            //                                    checkNum++;
                            //                                    continue;
                            //                                }
                            //                                checkNum++;
                            //                            }
                            //
                            //                            if (checkNum > 4)
                            //                            {
                            //                                                                lUDPStatus = UDPStatus.eUDPStatus_DISCONNECT;
                            //                            }
                        }
                        break;
                    case UDPStatus.eUDPStatus_DISCONNECT:
                        {
                            Console.WriteLine("DISCONNECT");
                            lUDPStatus = UDPStatus.eUDPStatus_DEAD;
                        }
                        break;
                    case UDPStatus.eUDPStatus_DEAD:
                        break;
                }

                //
                //                if (firstSend)  {
                //                    //firstSend = false;
                //                    // Console.WriteLine("Write Message...");
                //                    var text = Encoding.UTF8.GetBytes(string.Format("Hello KCP: {0}", ++counter));
                //                    if (connection.Send(text, 0, text.Length) < 0) {
                //                        Console.WriteLine("Write message failed.");
                //                        break;
                //                    }
                //                }
                //
                //                var n = connection.Recv(buffer, 0, buffer.Length);
                //                if (n == 0) {
                //                    Thread.Sleep(10);
                //                    continue;
                //                } else if (n < 0) {
                //                    Console.WriteLine("Receive Message failed.");
                //                    break;
                //                }
                //
                //                var resp = Encoding.UTF8.GetString(buffer, 0, n);
                //                Console.WriteLine("Received Message: " + resp);
            }
        }
    }
}
