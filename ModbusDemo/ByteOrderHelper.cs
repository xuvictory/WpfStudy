using System;
using System.Collections.Generic;
using System.Text;

namespace ModbusDemo
{
    public static class ByteOrderHelper
    {
        /// 16位寄存器：大端转小端（最常用）
        public static ushort SwapBytes(ushort value)
            => (ushort)((value >> 8) | (value << 8));

        /// 32位浮点数：ABCD模式（大端）
        public static float RegistersToFloat_ABCD(ushort reg0, ushort reg1)
        {
            byte[] bytes = new byte[4];
            bytes[0] = (byte)(reg0 >> 8);   // 高字节
            bytes[1] = (byte)(reg0 & 0xFF); // 低字节
            bytes[2] = (byte)(reg1 >> 8);
            bytes[3] = (byte)(reg1 & 0xFF);
            return BitConverter.ToSingle(bytes, 0);
        }

        /// 32位浮点数：CDAB模式（双字交换，国产设备常见）
        public static float RegistersToFloat_CDAB(ushort reg0, ushort reg1)
            => RegistersToFloat_ABCD(SwapBytes(reg1), SwapBytes(reg0));

        /// 32位浮点数：BADC模式（字节交换）
        public static float RegistersToFloat_BADC(ushort reg0, ushort reg1)
            => RegistersToFloat_ABCD(SwapBytes(reg0), SwapBytes(reg1));
    }
}
