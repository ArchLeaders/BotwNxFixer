using HKX2;
using SarcLibrary;
using SevenZip;
using System.Reflection;
using Yaz0Library;

namespace BotwNxFixer
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
                sarc[sarcFile.Key] = isYaz0 ? Yaz0.Compress(convertedData).ToArray() : convertedData;
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
                data = Yaz0.Decompress(data).ToArray();
                return true;
            }

            return false;
        }

        public static void SetupDependencies()
        {
            string _7z = Path.Combine(AppContext.BaseDirectory, "7z.dll");
            string _yaz0 = Path.Combine(AppContext.BaseDirectory, "Lib", "Yaz0.dll");

            if (!File.Exists(_7z)) {
                ExtractResource("7z64.dll", _7z);
            }

            if (!File.Exists(_yaz0)) {
                ExtractResource("Lib.Yaz0.dll", _yaz0);
            }

            SevenZipBase.SetLibraryPath(_7z);
        }

        public static void ExtractResource(string name, string output)
        {
            using Stream stream = Assembly.GetCallingAssembly().GetManifestResourceStream($"{nameof(BotwNxFixer)}.{name}") ?? throw new FileNotFoundException($"Could not find the embedded file '{name}'");
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? "");
            using FileStream fs = File.Create(output);
            stream.CopyTo(fs);
        }
    }
}
