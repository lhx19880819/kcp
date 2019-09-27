using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

enum UDPProtocolType
{
    eUDPPT_UNKNOWN = 0,

    eUDPPT_CONNECT_REQ = 1,
    eUDPPT_CONNECT_RSP = 2,
    eUDPPT_DISCONNECT = 3,
    eUDPPT_TRANSMIT = 4,
    eUDPPT_KEEP_LIVE = 5,
};

namespace KcpProject
{
    class UDPSession
    {
        private Socket mSocket = null;
        private KCP mKCP = null;

        private ByteBuffer mRecvBuffer = ByteBuffer.Allocate(1024 * 32);
        private UInt32 mNextUpdateTime = 0;

        public bool IsConnected { get { return mSocket != null && mSocket.Connected; } }
        public bool WriteDelay { get; set; }

        private int mConv = 0;

        public UDPSession()
        {
            mConv = 1;//new Random().Next(1, Int32.MaxValue);
        }

        public void Connect(string host, int port)
        {
            var endpoint = IPAddress.Parse(host);
            mSocket = new Socket(endpoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            mSocket.Connect(endpoint, port);
            mKCP = new KCP((uint)mConv, rawSend);
            //            mKCP = new KCP((uint)UDPProtocolType.eUDPPT_CONNECT_REQ, rawSend);
            // normal:  0, 40, 2, 1
            // fast:    0, 30, 2, 1
            // fast2:   1, 20, 2, 1
            // fast3:   1, 10, 2, 1
            mKCP.NoDelay(0, 30, 2, 1);
            mRecvBuffer.Clear();
        }

        public void Close()
        {
            if (mSocket != null)
            {
                mSocket.Close();
                mSocket = null;
                mRecvBuffer.Clear();
            }
        }

        private void rawSend(byte[] data, int length)
        {
            if (mSocket != null)
            {
                UDPProtocolTransmit req = new UDPProtocolTransmit();
                req.p_type = (int)UDPProtocolType.eUDPPT_TRANSMIT;
                req.conv = mConv;
                //                req.data = "abc";

                byte[] udpHead = StructureToByte<UDPProtocolTransmit>(req);

                byte[] total = new byte[udpHead.Length + length];
                Buffer.BlockCopy(udpHead, 0, total, 0, udpHead.Length);
                Buffer.BlockCopy(data, 0, total, udpHead.Length, length);

                int n = mSocket.Send(total, total.Length, SocketFlags.None);
                Console.WriteLine("rawSend:" + n + ",length:" + total.Length);
            }
        }

        public int Send(byte[] data, int index, int length)
        {
            if (mSocket == null)
                return -1;

            if (mKCP.WaitSnd >= mKCP.SndWnd)
            {
                return 0;
            }

            mNextUpdateTime = 0;

            var n = mKCP.Send(data, index, length);

            if (mKCP.WaitSnd >= mKCP.SndWnd || !WriteDelay)
            {
                mKCP.Flush(false);
            }
            return n;
        }

        public int ConnectUDP()
        {
            if (mSocket != null)
            {
                UDPProtocolConnectReq req = new UDPProtocolConnectReq();
                req.p_type = (int)UDPProtocolType.eUDPPT_CONNECT_REQ;
                req.conv = mConv;

                byte[] data = StructureToByte<UDPProtocolConnectReq>(req);
                int length = data.Length;
                mSocket.Send(data, length, SocketFlags.None);
            }

            return 0;
        }

        /// <summary>
        /// 由结构体转换为byte数组
        /// </summary>
        public static byte[] StructureToByte<T>(T structure)
        {
            int size = Marshal.SizeOf(typeof(T));
            byte[] buffer = new byte[size];
            IntPtr bufferIntPtr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(structure, bufferIntPtr, true);
                Marshal.Copy(bufferIntPtr, buffer, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(bufferIntPtr);
            }
            return buffer;
        }

        /// <summary>
        /// 由byte数组转换为结构体
        /// </summary>
        public static T ByteToStructure<T>(byte[] dataBuffer)
        {
            object structure = null;
            int size = Marshal.SizeOf(typeof(T));
            IntPtr allocIntPtr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.Copy(dataBuffer, 0, allocIntPtr, size);
                structure = Marshal.PtrToStructure(allocIntPtr, typeof(T));
            }
            finally
            {
                Marshal.FreeHGlobal(allocIntPtr);
            }
            return (T)structure;
        }

        public int Recv(byte[] data, int index, int length)
        {
            // 上次剩下的部分
            if (mRecvBuffer.ReadableBytes > 0)
            {
                var recvBytes = Math.Min(mRecvBuffer.ReadableBytes, length);
                Buffer.BlockCopy(mRecvBuffer.RawBuffer, mRecvBuffer.ReaderIndex, data, index, recvBytes);
                mRecvBuffer.ReaderIndex += recvBytes;
                // 读完重置读写指针
                if (mRecvBuffer.ReaderIndex == mRecvBuffer.WriterIndex)
                {
                    mRecvBuffer.Clear();
                }
                return recvBytes;
            }

            if (mSocket == null)
                return -1;

            if (!mSocket.Poll(0, SelectMode.SelectRead))
            {
                return 0;
            }

            var rn = 0;
            try
            {
                rn = mSocket.Receive(mRecvBuffer.RawBuffer, mRecvBuffer.WriterIndex, mRecvBuffer.WritableBytes, SocketFlags.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                rn = -1;
            }

            if (rn <= 0)
            {
                return rn;
            }
            mRecvBuffer.WriterIndex += rn;

            var inputN = mKCP.Input(mRecvBuffer.RawBuffer, mRecvBuffer.ReaderIndex, mRecvBuffer.ReadableBytes, true, true);
            if (inputN < 0)
            {
                mRecvBuffer.Clear();
                return inputN;
            }
            mRecvBuffer.Clear();

            // 读完所有完整的消息
            for (; ; )
            {
                var size = mKCP.PeekSize();
                if (size <= 0) break;

                mRecvBuffer.EnsureWritableBytes(size);

                var n = mKCP.Recv(mRecvBuffer.RawBuffer, mRecvBuffer.WriterIndex, size);
                if (n > 0) mRecvBuffer.WriterIndex += n;
            }

            // 有数据待接收
            if (mRecvBuffer.ReadableBytes > 0)
            {
                return Recv(data, index, length);
            }

            return 0;
        }

        public UDPStatus RecvKcp(byte[] data, int index, int length)
        {
            // 上次剩下的部分
            if (mRecvBuffer.ReadableBytes > 0)
            {
                var recvBytes = Math.Min(mRecvBuffer.ReadableBytes, length);
                Buffer.BlockCopy(mRecvBuffer.RawBuffer, mRecvBuffer.ReaderIndex, data, index, recvBytes);
                mRecvBuffer.ReaderIndex += recvBytes;
                // 读完重置读写指针
                if (mRecvBuffer.ReaderIndex == mRecvBuffer.WriterIndex)
                {
                    mRecvBuffer.Clear();
                }
                return UDPStatus.eUDPStatus_TRANSMIT;
            }

            if (mSocket == null)
                return UDPStatus.eUDPStatus_UNKNOWN;

            if (!mSocket.Poll(0, SelectMode.SelectRead))
            {
                return 0;
            }

            var rn = 0;
            try
            {
                rn = mSocket.Receive(mRecvBuffer.RawBuffer, mRecvBuffer.WriterIndex, mRecvBuffer.WritableBytes, SocketFlags.None);

                byte[] headBytes = new[]
                {
                    mRecvBuffer.RawBuffer[0],
                    mRecvBuffer.RawBuffer[1],
                    mRecvBuffer.RawBuffer[2],
                    mRecvBuffer.RawBuffer[3]
                };
                UDPProtocolHead head = UDPSession.ByteToStructure<UDPProtocolHead>(headBytes);
                switch ((UDPStatus)head.p_type)
                {
                    case UDPStatus.eUDPStatus_KEEP_LIVE:
                        return UDPStatus.eUDPStatus_KEEP_LIVE;
                    case UDPStatus.eUDPStatus_TRANSMIT:
                        {
                            Buffer.BlockCopy(mRecvBuffer.RawBuffer, 8, mRecvBuffer.RawBuffer, 0, rn - 8);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                rn = -1;
            }

            if (rn <= 0)
            {
                return UDPStatus.eUDPStatus_UNKNOWN;
            }
            mRecvBuffer.WriterIndex += rn;

            var inputN = mKCP.Input(mRecvBuffer.RawBuffer, mRecvBuffer.ReaderIndex, mRecvBuffer.ReadableBytes, true, true);
            if (inputN < 0)
            {
                mRecvBuffer.Clear();
                return UDPStatus.eUDPStatus_UNKNOWN;
            }
            mRecvBuffer.Clear();

            // 读完所有完整的消息
            for (; ; )
            {
                var size = mKCP.PeekSize();
                if (size <= 0) break;

                mRecvBuffer.EnsureWritableBytes(size);

                var n = mKCP.Recv(mRecvBuffer.RawBuffer, mRecvBuffer.WriterIndex, size);
                if (n > 0) mRecvBuffer.WriterIndex += n;
            }

            // 有数据待接收
            if (mRecvBuffer.ReadableBytes > 0)
            {
                return RecvKcp(data, index, length);
            }

            return 0;
        }

        public int RecvUDP(byte[] data, int index, int length)
        {
            // 上次剩下的部分
            if (mRecvBuffer.ReadableBytes > 0)
            {
                var recvBytes = Math.Min(mRecvBuffer.ReadableBytes, length);
                Buffer.BlockCopy(mRecvBuffer.RawBuffer, mRecvBuffer.ReaderIndex, data, index, recvBytes);
                mRecvBuffer.ReaderIndex += recvBytes;
                // 读完重置读写指针
                if (mRecvBuffer.ReaderIndex == mRecvBuffer.WriterIndex)
                {
                    mRecvBuffer.Clear();
                }
                return recvBytes;
            }

            if (mSocket == null)
                return -1;

            if (!mSocket.Poll(0, SelectMode.SelectRead))
            {
                return 0;
            }

            var rn = 0;
            try
            {
                rn = mSocket.Receive(mRecvBuffer.RawBuffer, mRecvBuffer.WriterIndex, mRecvBuffer.WritableBytes, SocketFlags.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                rn = -1;
            }

            if (rn <= 0)
            {
                return rn;
            }
            mRecvBuffer.WriterIndex += rn;

            // 有数据待接收
            if (mRecvBuffer.ReadableBytes > 0)
            {
                return Recv(data, index, length);
            }

            return 0;
        }

        public void Update()
        {
            if (mSocket == null)
                return;

            if (0 == mNextUpdateTime || mKCP.CurrentMS >= mNextUpdateTime)
            {
                mKCP.Update();
                mNextUpdateTime = mKCP.Check();
            }
        }

        public bool CheckConv(int rspConv)
        {
            return rspConv == mConv;
        }

        static long lastTime = 0;
        static double GetDeltaTime()
        {
            long now = DateTime.Now.Ticks;
            double dT = (now - lastTime) / 10000;
            lastTime = now;
            return dT;
        }

        private double mKeepLiveTime = 1000;

        public int SendKeepLive()
        {
            double deltaTime = GetDeltaTime();
            if (mKeepLiveTime - deltaTime <= 0)
            {
                mKeepLiveTime = 1000;

                if (mSocket != null)
                {
                    UDPProtocolKeepLive req = new UDPProtocolKeepLive();
                    req.p_type = (int)UDPProtocolType.eUDPPT_KEEP_LIVE;
                    req.conv = mConv;

                    byte[] data = StructureToByte<UDPProtocolKeepLive>(req);
                    int length = data.Length;
                    return mSocket.Send(data, length, SocketFlags.None);
                }
            }
            mKeepLiveTime -= deltaTime;
            return 0;
        }

        //        private double mTestTime = new Random().Next(1000, 2000);
        //
        //        public int TestTransimit()
        //        {
        //            double deltaTime = GetDeltaTime();
        //            if (mTestTime - deltaTime <= 0)
        //            {
        //                double time = mTestTime;
        //                mTestTime = new Random().Next(1000, 2000);
        //
        //                if (mSocket != null)
        //                {
        //                    UDPProtocolTransmit req = new UDPProtocolTransmit();
        //                    req.p_type = (int)UDPProtocolType.eUDPPT_TRANSMIT;
        //                    req.conv = mConv;
        //                    //                    req.data = "abc";
        //
        //                    byte[] data = StructureToByte<UDPProtocolTransmit>(req);
        //
        //                    int length = data.Length;
        //                    Console.WriteLine("TestTransimit............Sending, time : " + time);
        //                    return mSocket.Send(data, length, SocketFlags.None);
        //                }
        //            }
        //            mTestTime -= deltaTime;
        //            return 0;
        //        }


        static long lastTime1 = 0;
        static double GetDeltaTime1()
        {
            long now = DateTime.Now.Ticks;
            double dT = (now - lastTime1) / 10000;
            lastTime1 = now;
            return dT;
        }

        const  double c_DataTime = 2000;
        private double mDataTime = c_DataTime;
        private double mDataTimeStamp = 0;
        public int TestTransimitKCP()
        {
            double deltaTime = GetDeltaTime1();
            if (mDataTime - deltaTime <= 0)
            {
                if (mSocket == null)
                    return -1;
                
                mNextUpdateTime = 0;

                GameProtocol gp = new GameProtocol();
                int time = new Random().Next(3, 8);
                for (int i = 0; i < time; i++)
                {
                    gp.data += gp.data;
                }

                gp.data = Math.Pow(2, time) * 53 + ":" + gp.data;

                byte[] data = StructureToByte<GameProtocol>(gp);

                Console.WriteLine("TestTransimit............." + mDataTimeStamp);
                int length = data.Length;
                var n = mKCP.Send(data, 0, length);

                if (mKCP.WaitSnd >= mKCP.SndWnd || !WriteDelay)
                {
                    mKCP.Flush(false);
                }
                mKeepLiveTime = 1000;
                mDataTime = new Random().Next(500, 5000);
                mDataTimeStamp = mDataTime;
                return n;
            }
            mDataTime -= deltaTime;
            return 0;
        }
    }
}
