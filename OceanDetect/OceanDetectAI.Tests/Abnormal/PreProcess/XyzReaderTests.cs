using AbnormalDetection.Pcd;
using Xunit;

namespace OceanDetect.OceanDetectAI.Tests.Abnormal.PreProcess;

public class XyzReaderTests
{
    [Fact]
    public void Read_SkipsNonDataLines_AndMakesLocalCoordinates()
    {
        using var dir = new TempDir();
        var path = dir.Write("scan.xyz",
            "X Y Z\n" +
            "# comment\n" +
            "\n" +
            "385403.472 3848908.310 3.381\n" +
            "385403.771,3848908.319,3.447\n" +
            "385403.861\t3848908.394\t3.304\textra\n");

        var (xyz, ox, oy) = XyzReader.Read(path);

        Assert.Equal(3, xyz.Count);
        Assert.Equal(3, XyzReader.CountPoints(path));
        Assert.Equal(385403.472, ox);
        Assert.Equal(3848908.310, oy);
        Assert.Equal(0.0, xyz.X[0]);
        Assert.Equal(0.0, xyz.Y[0]);
        Assert.Equal((double)(float)(385403.771 - 385403.472), xyz.X[1]);
        Assert.Equal((double)(float)(3848908.394 - 3848908.310), xyz.Y[2]);
        Assert.Equal((double)(float)-3.447, xyz.Z[1]);
    }

    [Fact]
    public void Read_NoDataLines_ReturnsEmpty()
    {
        using var dir = new TempDir();
        var path = dir.Write("empty.xyz", "X Y Z\n# only header\n");

        var (xyz, ox, oy) = XyzReader.Read(path);

        Assert.Equal(0, xyz.Count);
        Assert.Equal((0.0, 0.0), (ox, oy));
    }
}
