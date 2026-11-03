using AbnormalDetection;
using Xunit;

namespace OceanDetect.OceanDetectAI.Tests.Abnormal.PreProcess;

public class NumpyCompatTests
{
    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(2, 1.06)]
    [InlineData(50, 2.5)]
    [InlineData(100, 4.0)]
    public void Percentile_MatchesNumpyLinear(double q, double expected)
    {
        var values = new double[] { 4, 1, 3, 2 };

        Assert.Equal(expected, NumpyCompat.Percentile(values, q), 12);
        Assert.Equal(new double[] { 4, 1, 3, 2 }, values);
    }

    [Fact]
    public void Percentile_Empty_Throws() =>
        Assert.Throws<ArgumentException>(() => NumpyCompat.Percentile(Array.Empty<double>(), 2));

    [Theory]
    [InlineData(0, 100, 40, new double[] { 0, 40, 80 })]
    [InlineData(0, 80, 40, new double[] { 0, 40 })]
    [InlineData(5, 5, 40, new double[0])]
    public void Arange_MatchesNumpy(double lo, double hi, double step, double[] expected) =>
        Assert.Equal(expected, NumpyCompat.Arange(lo, hi, step));
}
