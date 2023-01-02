using HKX2;
using Nintendo.Yaz0;
using SarcLibrary;
using System.IO;

namespace ArchFixTool
{
    public class Util
    {
        public static readonly string[] HavokExts = { ".shknm2", ".shksc", ".shktmrb", ".hkrb", ".hkcl", "hkrg" };
        public static readonly string[] SarcExts = {
            ".sarc", ".pack", ".bactorpack", ".bmodelsh", ".beventpack", ".stats", ".ssarc", ".sbactorpack",
            ".sbmodelsh", ".sbeventpack", ".sstats", ".sblarc", ".blarc", // ".stera", ".sstera",
        };

        public static byte[] ConvertSarc(byte[] data)
        {
            SarcFile sarc = SarcFile.FromBinary(data);
            Parallel.ForEach(sarc, (sarcFile) => {
                string ext = Path.GetExtension(sarcFile.Key);
                bool isYaz0 = UnyazIfNeedBe(sarcFile.Key, out byte[] data, sarcFile.Value);
                byte[] convertedData;
                if (HavokExts.Contains(ext)) {
                    convertedData = HavokToSwitch(data, ext);
                }
                else if (ext == ".sbfres") {
                    convertedData = BfresToSwitch(data);
                }
                else if (SarcExts.Contains(ext)) {
                    convertedData = ConvertSarc(data);
                }
                else {
                    return;
                }

                _ = Task.Run(() => {
                    if (!SarcExts.Contains(ext)) {
                        Console.WriteLine($"Fixed '{sarcFile.Key}'");
                    }
                });
                sarc[sarcFile.Key] = isYaz0 ? Yaz0.CompressFast(convertedData) : convertedData;
            });

            return sarc.ToBinary();
        }

        internal static IHavokObject ReadHkx(byte[] data)
        {
            var des = new PackFileDeserializer();
            var br = new BinaryReaderEx(data);
            return des.Deserialize(br);
        }

        internal static byte[] WriteHkx(IHavokObject root, HKXHeader header)
        {
            using MemoryStream ms = new();
            new PackFileSerializer().Serialize(root, new(ms), header);
            return ms.ToArray();
        }

        public static byte[] HavokToSwitch(byte[] data, string extension)
        {
            HKXHeader header = HKXHeader.BotwNx();

            if (extension == ".shksc") {
                StaticCompoundInfo hksc = (StaticCompoundInfo)ReadHkx(data);
                hkRootLevelContainer scRoot = (hkRootLevelContainer)ReadHkx(data[(int)hksc.m_Offset..]);

                header.SectionOffset = 0;
                byte[] hkscData = WriteHkx(hksc, header);
                hksc.m_Offset = (uint)hkscData.Length;
                hkscData = WriteHkx(hksc, header);

                header.SectionOffset = 16;
                byte[] rootData = WriteHkx(scRoot, header);

                byte[] buffer = new byte[hkscData.Length + rootData.Length];
                Buffer.BlockCopy(hkscData, 0, buffer, 0, hkscData.Length);
                Buffer.BlockCopy(rootData, 0, buffer, hkscData.Length, rootData.Length);
                return buffer;
            }

            header.SectionOffset = (extension == ".shknm2" || extension == ".shktmrb") ? (short)16 : (short)0;
            return WriteHkx(ReadHkx(data), header);
        }

        public static byte[] BfresToSwitch(byte[] data)
        {
            return data;
        }

        public static bool UnyazIfNeedBe(string file, out byte[] data, byte[]? inData = null)
        {
            data = inData ?? File.ReadAllBytes(file);
            if (data.Length > 4 && data.AsSpan()[0..4].SequenceEqual("Yaz0"u8)) {
                data = Yaz0.Decompress(data);
                return true;
            }

            return false;
        }
    }
}
