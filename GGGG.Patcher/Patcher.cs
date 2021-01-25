using System;
using System.Collections.Generic;
using System.IO;

namespace GGGG
{
    public class Patcher
    {
        public delegate void Log(string text);
        public event Log OnLog;
        public enum RomType
        {
            GameBoy = 1,
            GameGear = 1,
            MasterSystem = 1,
            GenesisMd = 2,
            Nintendo = 3,
            SuperNintendo = 4,
            PcEngine = 5
        }

        #region Helper Functions
        private static string CleanCode(string code) => code?.Replace("-", "").Replace("-", "").Trim().ToUpper();
        private static Dictionary<string, string> GetLookup(bool hex = false, params char[] keys)
        {
            var padLength = (hex ? Convert.ToString(keys.Length - 1, 16) : Convert.ToString(keys.Length - 1, 2)).Length;
            var output = new Dictionary<string, string>();

            for (var i = 0; i < keys.Length; i++)
            {
                output.Add(keys[i].ToString(), (hex ? Convert.ToString(i, 16).ToUpper() : Convert.ToString(i, 2)).PadLeft(padLength, '0').Replace(" ", ""));
            }

            return output;
        }
        private static Dictionary<string, string> GetLookup(bool hex = false, string keys = "") => GetLookup(hex, keys.ToCharArray());
        private static string Left(string text, int length) => text.Substring(0, length);
        private static string Mid(string text, int startIndex, int length) => text.Substring(startIndex - 1, length);
        private static string Right(string text, int length) => text.Substring(text.Length - length, length);
        private static int Hex2Dec(string hex) => Convert.ToInt32(hex, 16);
        private static string Hex(int dec) => Convert.ToString(dec, 16).Replace(" ", "");
        private static int Bin2Dec(string bin) => Convert.ToInt32(bin, 2);
        private static string Bin(int dec) => Convert.ToString(dec, 2).Replace(" ", "");
        private static string Rpad(string text, int totalWidth, char paddingChar = ' ') => text.PadRight(totalWidth, paddingChar);
        private static string Lpad(string text, int totalWidth, char paddingChar = ' ') => text.PadLeft(totalWidth, paddingChar);
        private static void Put(Stream inputStream, byte data, int offset)
        {
            inputStream.Seek(offset, SeekOrigin.Begin);
            inputStream.Write(new byte[] { data }, 0, 1);
        }
        private static byte[] Get(Stream inputStream, int offset, int count)
        {
            var buffer = new byte[count];
            inputStream.Seek(offset, SeekOrigin.Begin);
            inputStream.Read(buffer, 0, count);
            return buffer;
        }
        private void Print(string text) => OnLog?.Invoke(text);
        #endregion

        public void Patch(Stream romStream, RomType romType, params string[] codes)
        {
            string patchLog = "";
            if (romStream.CanSeek == false)
                throw new ArgumentException("Stream must be seekable");

            if (romStream.CanWrite == false)
                throw new ArgumentException("Stream must be writable");

            for (var c = 0; c < codes.Length; c++)
            {
                var code = CleanCode(codes[c]);

                if (String.IsNullOrEmpty(code))
                    continue;

                int offset;
                byte replacement;
                string Dec = "";
                byte comparison = 0;

                Print($"Parsing code: {code}");

                #region SMS/PCE/RAW
                if (code.Contains(":"))
                {
                    offset = Hex2Dec(Left(code, code.Length - 3));
                    if (romType == RomType.PcEngine || (romType == RomType.MasterSystem && romStream.Length % 1024 == 0))
                        offset += 512;

                    replacement = (byte)Hex2Dec(Right(code, 2));

                    if (offset < romStream.Length)
                    {
                        Put(romStream, replacement, offset);
                        patchLog = $"  {codes[c]}\r\n{patchLog}";
                        Print(patchLog);
                    }
                }
                #endregion
                #region GB/GG
                if (romType == RomType.GameBoy || romType == RomType.GameGear || romType == RomType.MasterSystem)
                {
                    #region GB/GG SHORT
                    if (code.Length == 6)
                    {
                        code = $"{Hex(Hex2Dec(Mid(code, 6, 1)) ^ 15)}{Mid(code, 3, 3)}:{Left(code, 2)}";
                        offset = Hex2Dec(Left(code, 4));
                        replacement = (byte)Hex2Dec(Right(code, 2));

                        for (var Num = 0; Num <= romStream.Length / 8192; Num++)
                        {
                            if (offset < romStream.Length)
                            {
                                Put(romStream, replacement, offset);
                                patchLog = $"  {codes[c]} - {Hex(offset).Trim()}:{Hex(comparison).Trim()}:{Hex(replacement).Trim()}\r\n{patchLog}";
                            }
                            offset += 8192;
                        }

                    }
                    #endregion
                    #region GB/GG LONG
                    if (code.Length == 9)
                    {
                        code = $"{Hex(Hex2Dec(Mid(code, 6, 1)) ^ 15)}{Mid(code, 3, 3)}:{Left(code, 2)}:{Mid(code, 7, 1)}{Mid(code, 9, 1)}";
                        Dec = Lpad(Bin(Hex2Dec(Mid(code, 9, 2))), 8, '0');
                        code = $"{Left(code, 8)}{Hex(Bin2Dec($"{Mid(Dec, 7, 2)}{Left(Dec, 6)}") ^ 186)}";
                        offset = Hex2Dec(Left(code, 4));
                        comparison = (byte)Hex2Dec(Right(code, 2));
                        replacement = (byte)Hex2Dec(Mid(code, 6, 2));

                        for (var Num = 0; Num <= romStream.Length / 8192; Num++)
                        {
                            if (offset < romStream.Length)
                            {
                                if (Get(romStream, offset, 1)[0] == comparison)
                                {
                                    Put(romStream, replacement, offset);
                                    patchLog = $"  {codes[c]} - {Hex(offset).Trim()}:{Hex(comparison).Trim()}:{Hex(replacement).Trim()}\r\n{patchLog}";
                                }
                            }
                            offset += 8192;
                        }
                    }
                    #endregion
                }
                #endregion
                #region MD
                if (romType == RomType.GenesisMd)
                {
                    var Lookup = GetLookup(false, "ABCDEFGHJKLMNPRSTVWXYZ0123456789");
                    foreach (var character in code)
                        Dec += Lookup[character.ToString()];

                    code = $"{Mid(Dec, 17, 8)}{Mid(Dec, 9, 8)}{Right(Dec, 8)}{Mid(Dec, 30, 3)}{Mid(Dec, 25, 5)}{Left(Dec, 8)}";
                    Dec = $"{Lpad(Hex(Bin2Dec(Left(code, 24))), 6, '0')}:{Lpad(Hex(Bin2Dec(Right(code, 16))), 4, '0')}";
                    offset = Hex2Dec(Left(Dec, 6));
                    comparison = (byte)Hex2Dec(Mid(Dec, 8, 2));
                    replacement = (byte)Hex2Dec(Right(Dec, 2));

                    if (offset < romStream.Length)
                    {
                        Put(romStream, comparison, offset);
                        Put(romStream, replacement, offset + 1);
                        patchLog = $"  {codes[c]} - {Hex(offset).Trim()}:{Hex(comparison).Trim()}{Hex(replacement).Trim()}\r\n{patchLog}";
                    }
                }
                #endregion
                #region NES
                if (romType == RomType.Nintendo)
                {
                    #region NES SHORT
                    var Lookup = GetLookup(false, "APZLGITYEOXUKSVN");
                    foreach (var character in code)
                        Dec += Lookup[character.ToString()];

                    if (code.Length == 6)
                    {
                        code = $"{Mid(Dec, 9, 1)}{Mid(Dec, 14, 4)}{Mid(Dec, 22, 3)}{Mid(Dec, 5, 1)}{Mid(Dec, 10, 4)}{Mid(Dec, 18, 3)}{Mid(Dec, 1, 1)}{Mid(Dec, 6, 3)}{Mid(Dec, 21, 1)}{Mid(Dec, 2, 3)}";
                        Dec = $"{Lpad(Hex(Bin2Dec(Left(code, 16))), 4, '0')}:{Lpad(Hex(Bin2Dec(Right(code, 8))), 2, '0')}";
                        offset = Hex2Dec(Left(Dec, 4));
                        replacement = (byte)Hex2Dec(Right(Dec, 2));
                        if (romStream.Length % 1024 != 0)
                            offset += 16;

                        if (romStream.Length >= 49169)
                        {
                            for (var Num = 0; Num <= romStream.Length / 8192; Num++)
                            {
                                if (offset < romStream.Length)
                                {
                                    Put(romStream, replacement, offset);
                                    patchLog = $"  {codes[c]} - {Hex(offset).Trim()}:{Hex(comparison).Trim()}:{Hex(replacement).Trim()}\r\n{patchLog}";
                                }
                                offset += 8192;
                            }
                        }
                        else
                        {
                            Put(romStream, replacement, offset);
                            patchLog = $"  {codes[c]} - {Hex(offset).Trim()}:{Hex(comparison).Trim()}:{Hex(replacement).Trim()}\r\n{patchLog}";
                        }
                    }
                    #endregion
                    #region NES LONG
                    if (code.Length == 8)
                    {
                        code = $"{Mid(Dec, 9, 1)}{Mid(Dec, 14,4)}{Mid(Dec, 22, 3)}{Mid(Dec, 5, 1)}{Mid(Dec, 10, 4)}{Mid(Dec, 18, 3)}{Mid(Dec, 1, 1)}{Mid(Dec, 6, 3)}{Mid(Dec, 29, 1)}{Mid(Dec, 2, 3)}{Mid(Dec, 25, 1)}{Mid(Dec, 30, 3)}{Mid(Dec, 21, 1)}{Mid(Dec, 26, 3)}";
                        Dec = $"{Lpad(Hex(Bin2Dec(Left(code, 16))), 4, '0')}:{Lpad(Hex(Bin2Dec(Mid(code, 17, 8))), 2, '0')}:{Lpad(Hex(Bin2Dec(Right(code, 8))), 2, '0')}";
                        offset = Hex2Dec(Left(Dec, 4)) - 49152;
                        comparison = (byte)Hex2Dec(Right(Dec, 2));
                        replacement = (byte)Hex2Dec(Right(Dec, 2));
                        replacement = (byte)Hex2Dec(Mid(Dec, 6, 2));

                        if (romStream.Length % 1024 != 0)
                            offset += 16;

                        for (var Num = 0; Num <= romStream.Length / 8192; Num++)
                        {
                            if (offset < romStream.Length)
                            {
                                if (Get(romStream, offset, 1)[0] == comparison)
                                {
                                    Put(romStream, replacement, offset);
                                    patchLog = $"  {codes[c]} - {Hex(offset).Trim()}:{Hex(comparison).Trim()}:{Hex(replacement).Trim()}\r\n{patchLog}";
                                }
                            }
                            offset += 8192;
                        }
                    }
                    #endregion
                }
                #endregion
                #region SNES
                if (romType == RomType.SuperNintendo)
                {
                    var Lookup = GetLookup(true, "DF4709156BC8A23E");

                    foreach (var character in code)
                        Dec += Lookup[character.ToString()];

                    replacement = (byte)Hex2Dec(Left(Dec, 2));
                    code = $"{Lpad(Bin(Hex2Dec(Mid(Dec, 3, 2))), 8, '0')}{Lpad(Bin(Hex2Dec(Mid(Dec, 5, 2))), 8, '0')}{Lpad(Bin(Hex2Dec(Mid(Dec, 7, 2))), 8, '0')}";
                    Dec = $"{Mid(code, 11, 4)}{Mid(code, 19, 4)}{Left(code, 4)}{Mid(code, 23, 2)}{Mid(code, 9, 2)}{Mid(code, 5, 4)}{Mid(code, 15, 4)}";
                    code = $"{Lpad(Hex(Bin2Dec(Left(Dec, 8))), 2, '0')}{Lpad(Hex(Bin2Dec(Mid(Dec, 9, 8))), 2, '0')}{Lpad(Hex(Bin2Dec(Right(Dec, 8))), 2, '0')}:{Hex(replacement)}";
                    offset = Hex2Dec(Left(code, 6));

                    if (romStream.Length % 1024 != 0)
                        offset += 512;

                    Dec = Hex(offset);

                    #region SNES HIROM
                    var Num = 65493;

                    if (romStream.Length % 1024 != 0)
                        Num += 512;

                    var Bit = Get(romStream, Num, 1)[0];
                    if (Bit != 33 && Bit != 49)
                        offset = Bin2Dec($"0{Left(Lpad(Bin(Hex2Dec(Dec)), 24, '0'), 8)}{Right(Lpad(Bin(Hex2Dec(Dec)), 24, '0'), 15)}");

                    if (offset >= 4194304 && offset <= 8388607)
                        offset -= 4194304;

                    if (offset >= 8388608 && offset <= 12582911)
                        offset -= 8388608;

                    if (offset >= 12582912 && offset <= 16777215)
                        offset = offset - 12582912;
                    #endregion

                    if (offset < romStream.Length)
                    {
                        Put(romStream, replacement, offset);
                        patchLog = $"  {codes[c]} - {Hex(offset).Trim()}:{Hex(replacement).Trim()}\r\n{patchLog}";
                    }
                    
                }
                #endregion
            }

            Print($"Final Changes:\r\n{patchLog.Trim('\t', '\r', '\n')}");
        }
    }
}
