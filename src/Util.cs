using HKX2;
using SarcLibrary;
using SevenZip;
using System.Reflection;
using System.Text.Json;
using Yaz0Library;

namespace BotwNxFixer
{
    public class Util
    {
        private static Dictionary<string, JsonElement>? _config;
        private static Dictionary<string, JsonElement> Config {
            get {
                if (_config == null) {
                    string configFile = Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? "", "bcml", "settings.json");
                    _config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllBytes(configFile))!;
                }

                return _config;
            }
        }

        public static string[] Args { get; set; } = Array.Empty<string>();

        public static readonly string[] HavokExts = { ".shknm2", ".shksc", ".shktmrb", ".hkrb", ".hkcl", "hkrg" };
        public static readonly string[] SarcExts = {
            ".sarc", ".pack", ".bactorpack", ".bmodelsh", ".beventpack", ".stats", ".ssarc", ".sbactorpack",
            ".sbmodelsh", ".sbeventpack", ".sstats", ".sblarc", ".blarc", // ".stera", ".sstera",
        };

        public static byte[] ConvertSarc(byte[] data, string path)
        {
            List<string> tex2Keys = new();
            List<string> mdlKeys = new();

            SarcFile sarc = SarcFile.FromBinary(data);
            Parallel.ForEach(sarc, (sarcFile) => {
                string ext = Path.GetExtension(sarcFile.Key);
                bool isYaz0 = UnyazIfNeedBe(sarcFile.Key, out byte[] data, sarcFile.Value);
                byte[] convertedData;
                if (HavokExts.Contains(ext)) {
                    convertedData = HavokToSwitch(data, ext);
                }
                else if (ext == ".sbfres" && Path.GetFileName(path).StartsWith("Dungeon")) {
                    if (HasFlag("replace-tex2") && Path.GetFileName(sarcFile.Key).EndsWith(".Tex2.sbfres")) {
                        tex2Keys.Add(sarcFile.Key);
                    }
                    else if (HasFlag("replace-bfres")) {
                        mdlKeys.Add(sarcFile.Key);
                    }
                    return;
                }
                else if (ext == ".sbfres") {
                    convertedData = BfresToSwitch(data);
                }
                else if (SarcExts.Contains(ext)) {
                    convertedData = ConvertSarc(data, Path.Combine(path, "", sarcFile.Key));
                }
                else {
                    return;
                }

                _ = Task.Run(() => {
                    if (!SarcExts.Contains(ext)) {
                        Console.WriteLine($"Fixed '{sarcFile.Key}'");
                    }
                });
                sarc[sarcFile.Key] = isYaz0 ? Yaz0.Compress(convertedData, out Yaz0SafeHandle handle).ToArray() : convertedData;
            });

            foreach (var key in tex2Keys) {
                string file = key.Replace(".Tex2.sbfres", ".Tex.sbfres");
                sarc[file] = GetVanillaBytes(path, file);
                sarc.Remove(key);
            }

            foreach (var key in mdlKeys) {
                sarc[key] = GetVanillaBytes(path, key);
            }

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

        public static byte[] GetVanillaBytes(string path, string? fileName = null)
        {
            string vanillaFile = path.Contains("01007EF00011E000") ?
                Path.Combine(GetNxGameDir(), Path.GetRelativePath(Path.Combine("01007EF00011E000", "romfs"), path)) :
                Path.Combine(GetNxDlcDir(), Path.GetRelativePath(Path.Combine("01007EF00011F001", "romfs"), path));

            if (fileName != null) {
                UnyazIfNeedBe(vanillaFile, out byte[] decompressed);
                SarcFile sarc = SarcFile.FromBinary(decompressed);
                return sarc[fileName];
            }
            else {
                return File.ReadAllBytes(vanillaFile);
            }
        }

        public static bool HasFlag(string flag)
        {
            return Args.Where(x => x.StartsWith('-'))
                .Select(x => x.Replace("-", string.Empty))
                .Where(x => x == flag || x.StartsWith(flag[0]))
                .Any();
        }

        public static string GetNxGameDir()
        {
            return Config["game_dir_nx"].GetString()!;
        }

        public static string GetNxDlcDir()
        {
            return Config["dlc_dir_nx"].GetString()!;
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

            if (!File.Exists(_7z)) {
                ExtractResource("7z64.dll", _7z);
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
