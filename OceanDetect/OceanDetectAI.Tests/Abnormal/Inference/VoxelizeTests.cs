using AbnormalDetection.SampleSupport;
using Xunit;

namespace OceanDetect.OceanDetectAI.Tests.Abnormal.Inference;

public class VoxelizeTests
{
    [Fact]
    public void Voxelize_GroupsPointsByVoxel_InInputOrder()
    {
        float[] pts =
        {
            0.1f, 0.1f, 0.1f, 0f,
            0.5f, 0.1f, 0.1f, 0f,
            0.15f, 0.1f, 0.1f, 1f,
        };

        var v = SampleRunner.Voxelize(pts);

        Assert.Equal(new[] { 2, 1 }, v.Counts);
        Assert.Equal(new[] { 0, 10, 125, 125, 0, 10, 125, 127 }, v.Coordinates);
        Assert.Equal(80, v.Voxels.Length);
        Assert.Equal(pts[0..4], v.Voxels[0..4]);
        Assert.Equal(pts[8..12], v.Voxels[4..8]);
        Assert.Equal(pts[4..8], v.Voxels[40..44]);
        Assert.All(v.Voxels[8..40], f => Assert.Equal(0f, f));
    }

    [Theory]
    [InlineData(25f, 0f, 0f)]
    [InlineData(0f, -25.1f, 0f)]
    [InlineData(0f, 0f, -2.1f)]
    [InlineData(0f, 0f, 30f)]
    public void Voxelize_DropsPointsOutsideModelRange(float x, float y, float z)
    {
        var v = SampleRunner.Voxelize(new[] { x, y, z, 0f });

        Assert.Empty(v.Counts);
        Assert.Empty(v.Voxels);
    }

    [Fact]
    public void Voxelize_KeepsAtMostTenPointsPerVoxel()
    {
        var pts = Enumerable.Repeat(new[] { 0.1f, 0.1f, 0.1f, 0f }, 12).SelectMany(p => p).ToArray();

        Assert.Equal(new[] { 10 }, SampleRunner.Voxelize(pts).Counts);
    }

    [Fact]
    public void Voxelize_RejectsBadInput()
    {
        Assert.Throws<ArgumentException>(() => SampleRunner.Voxelize(new float[3]));
        Assert.Throws<ArgumentException>(() => SampleRunner.Voxelize(new[] { float.NaN, 0f, 0f, 0f }));
    }
}
