using FluentAssertions;
using SysDiag.Collectors.Linux;

namespace SysDiag.Tests;

/// <summary>
/// Tests for the /proc parsing. The fixtures are excerpts of real kernel files,
/// which is what makes this suite meaningful: the parser is verified against the
/// formats it will actually meet, on a machine that has no /proc at all.
/// </summary>
public sealed class ProcParserTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public void ParseCpuModelName_X86_ReadsModelNameField()
    {
        string cpuInfo = Fixture("cpuinfo-x86-dual-socket.txt");

        ProcParser.ParseCpuModelName(cpuInfo).Should().Be("Intel(R) Xeon(R) Silver 4210 CPU @ 2.20GHz");
    }

    [Fact]
    public void ParseCpuModelName_Arm_FallsBackToModelField()
    {
        // ARM kernels have no "model name" line; the board name lives in "Model".
        string cpuInfo = Fixture("cpuinfo-arm-raspberry-pi.txt");

        ProcParser.ParseCpuModelName(cpuInfo).Should().Be("Raspberry Pi 4 Model B Rev 1.4");
    }

    [Fact]
    public void ParseLogicalCoreCount_CountsProcessorBlocks()
    {
        Fixture("cpuinfo-x86-dual-socket.txt").Pipe(ProcParser.ParseLogicalCoreCount).Should().Be(4);
        Fixture("cpuinfo-arm-raspberry-pi.txt").Pipe(ProcParser.ParseLogicalCoreCount).Should().Be(2);
    }

    [Fact]
    public void ParsePhysicalCoreCount_SumsCoresPerSocket()
    {
        // Two sockets with ten cores each: reading "cpu cores" once would report
        // half the truth on every server.
        string cpuInfo = Fixture("cpuinfo-x86-dual-socket.txt");

        ProcParser.ParsePhysicalCoreCount(cpuInfo).Should().Be(20);
    }

    [Fact]
    public void ParsePhysicalCoreCount_WithoutCoreInformation_ReturnsNull()
    {
        string cpuInfo = Fixture("cpuinfo-arm-raspberry-pi.txt");

        ProcParser.ParsePhysicalCoreCount(cpuInfo).Should().BeNull();
    }

    [Fact]
    public void ParseMemoryBytes_ConvertsKibibytesToBytes()
    {
        string memInfo = Fixture("meminfo-ubuntu.txt");

        ProcParser.ParseMemoryBytes(memInfo, "MemTotal").Should().Be(16_316_456L * 1024);
        ProcParser.ParseMemoryBytes(memInfo, "MemAvailable").Should().Be(9_873_452L * 1024);
    }

    [Fact]
    public void ParseMemoryBytes_UnknownKey_ReturnsNull()
    {
        ProcParser.ParseMemoryBytes(Fixture("meminfo-ubuntu.txt"), "MemNotThere").Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage without a colon")]
    [InlineData("MemTotal:       not-a-number kB")]
    public void ParseMemoryBytes_BrokenInput_ReturnsNullInsteadOfThrowing(string content)
    {
        // Anything can end up in these files on a damaged system; the collector
        // must degrade to "unknown", never crash.
        ProcParser.ParseMemoryBytes(content, "MemTotal").Should().BeNull();
    }

    [Fact]
    public void ParseOsRelease_StripsQuotesAndSkipsComments()
    {
        IReadOnlyDictionary<string, string> values = ProcParser.ParseOsRelease(Fixture("os-release-ubuntu.txt"));

        values["PRETTY_NAME"].Should().Be("Ubuntu 24.04.1 LTS");
        values["VERSION_ID"].Should().Be("24.04");
        values["ID"].Should().Be("ubuntu");
        values.Should().NotContainKey("#");
    }

    [Theory]
    [InlineData("4700000\n", 4700)]
    [InlineData("2200000", 2200)]
    [InlineData("0", null)]
    [InlineData("not a number", null)]
    public void ParseMaxClockMhz_ConvertsKilohertz(string content, int? expected)
    {
        ProcParser.ParseMaxClockMhz(content).Should().Be(expected);
    }

    [Fact]
    public void ParseCpuModelName_EmptyContent_ReturnsNull()
    {
        ProcParser.ParseCpuModelName(string.Empty).Should().BeNull();
    }
}

/// <summary>Small helper that keeps the fixture assertions on one line.</summary>
internal static class PipeExtensions
{
    public static TResult Pipe<TSource, TResult>(this TSource source, Func<TSource, TResult> projection) =>
        projection(source);
}
