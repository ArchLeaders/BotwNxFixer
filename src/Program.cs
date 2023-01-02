using BotwNxFixer;
using Nintendo.Yaz0;
using SevenZip;
using System.Reflection;

if (!(args.Length > 0 && File.Exists(args[0]) && Path.GetExtension(args[0]) == ".bnp")) {
    Console.WriteLine("Invalid input mod, please specify a path to a BNP");
    Console.ReadLine();
}

SevenZipBase.SetLibraryPath(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "7z64.dll"));

string path = Path.Combine(Path.GetDirectoryName(args[0]) ?? "", Path.GetFileNameWithoutExtension(args[0]));
using (SevenZipExtractor extractor = new(args[0])) {
    extractor.ExtractArchive(path);
}

await Parallel.ForEachAsync(Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories), async (file, cancellationToken) => {
    string ext = Path.GetExtension(file);
    bool isYaz0 = Util.UnyazIfNeedBe(file, out byte[] data);
    byte[] convertedData;
    if (data.Length < 4) {
        return;
    }
    if (Util.HavokExts.Contains(ext)) {
        convertedData = Util.HavokToSwitch(data, ext);
    }
    else if (ext == ".sbfres") {
        convertedData = Util.BfresToSwitch(data);
    }
    else if (Util.SarcExts.Contains(ext)) {
        convertedData = Util.ConvertSarc(data);
    }
    else {
        return;
    }

    _ = Task.Run(() => {
        if (!Util.SarcExts.Contains(ext)) {
            Console.WriteLine($"Fixed '{Path.GetRelativePath(path, file)}'");
        }
    }, cancellationToken);
    await File.WriteAllBytesAsync(file, isYaz0 ? Yaz0.CompressFast(convertedData) : convertedData, cancellationToken);
});

SevenZipCompressor compressor = new() {
    CompressionMethod = CompressionMethod.Lzma2,
    CompressionLevel = CompressionLevel.Ultra
};

compressor.CompressDirectory(path, args[0]);
Directory.Delete(path, true);