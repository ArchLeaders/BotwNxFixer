using BotwNxFixer;
using SevenZip;
using Yaz0Library;

Console.Title = $"{nameof(BotwNxFixer)} - v{typeof(Program).Assembly.GetName().Version?.ToString(3)}";

if (!(args.Length > 0 && File.Exists(args[0]) && Path.GetExtension(args[0]) == ".bnp")) {
    Console.WriteLine("Invalid input mod, please specify a path to a BNP");
    Console.ReadLine();
    Environment.Exit(1);
}

Util.SetupDependencies();

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

    await File.WriteAllBytesAsync(file, isYaz0 ? Yaz0.Compress(convertedData).ToArray() : convertedData, cancellationToken);
});

SevenZipCompressor compressor = new() {
    CompressionMethod = CompressionMethod.Lzma2,
    CompressionLevel = CompressionLevel.Ultra
};

compressor.CompressDirectory(path, args[0]);
Directory.Delete(path, true);