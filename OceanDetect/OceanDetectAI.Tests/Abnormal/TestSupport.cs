using AbnormalDetection.Pcd;
using AbnormalDetection.Scans;

namespace OceanDetect.OceanDetectAI.Tests.Abnormal;

internal sealed class TempDir : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "OceanDetectAITests", Guid.NewGuid().ToString("N"));

    public TempDir() => Directory.CreateDirectory(Root);

    public string Write(string name, string content)
    {
        var path = Path.GetFullPath(Path.Combine(Root, name));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, true); }
        catch (IOException) { }
    }
}

internal static class TestPaths
{
    public static string RepoRoot { get; } = FindRepoRoot();
    public static string SampleXyz => Path.Combine(RepoRoot, "samples", "test01.xyz");

    static string FindRepoRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "OceanDetectAITest.sln")))
                return d.FullName;
        throw new DirectoryNotFoundException("OceanDetectAITest.sln 을 찾지 못했습니다.");
    }
}

internal static class ScanFactory
{
    // 1 m 간격 width×height 격자. x, y 는 0 부터 정수.
    public static Scan Grid(string stem, int width, int height, double z = 0, double sfZ = 0)
    {
        var p = new PointXyz(width * height);
        int k = 0;
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++, k++)
            {
                p.X[k] = x;
                p.Y[k] = y;
                p.Z[k] = z;
            }
        return new Scan { Stem = stem, Path = stem + ".xyz", Xyz = p, SfZ = sfZ, Frame = "local:" + stem };
    }
}
