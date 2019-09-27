using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
public class UDPProtocolConnectReq : UDPProtocolHead
{
    public int conv = 0;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
public class UDPProtocolConnectRsp : UDPProtocolHead
{
    public int conv = 0;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
public class UDPProtocolDisconnect : UDPProtocolHead
{
    public int conv = 0;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
public class UDPProtocolTransmit : UDPProtocolHead
{
    public int conv = 0;
    //    public byte[] data1 = { 48, 49, 50 };
    //    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
//    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4)]
//    public string data;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
public class UDPProtocolKeepLive : UDPProtocolHead
{
    public int conv = 0;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
public class GameProtocol
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1800)]
    public string data = "abcdefghijklmnopqrstuvwxyz abcdefghijklmnopqrstuvwxyz";
}