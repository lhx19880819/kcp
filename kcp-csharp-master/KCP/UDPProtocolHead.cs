using System;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
public class UDPProtocolHead
{
    public int p_type = 0;
}
