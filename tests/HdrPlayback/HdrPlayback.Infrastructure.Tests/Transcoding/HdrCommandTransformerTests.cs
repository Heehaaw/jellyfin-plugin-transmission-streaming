namespace HdrPlayback.Infrastructure.Tests.Transcoding;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using Xunit;
using HdrPlayback.Infrastructure.Transcoding;

public sealed class HdrCommandTransformerTests
{
    [Theory, AutoMoqData]
    internal void Should_ExtendOneEncodeAndPreserveInputAndQuality(string input, int bitrate, HdrCommandTransformer sut)
    {
        // Arrange
        var native = FfmpegArguments.Format(["-i", "http://source/" + input, "-codec:v:0", "hevc_vaapi", "-vf", "scale_vaapi=w=1920:h=1080:format=nv12",
            "-b:v", bitrate.ToString(), "-codec:a", "copy", "-ss", "25.5", "output.m3u8"]);

        // Act
        var result = FfmpegArguments.Parse(sut.Transform(native));

        // Assert
        result[result.IndexOf("-i") + 1].Should().Be("http://source/" + input);
        result[result.IndexOf("-b:v") + 1].Should().Be(bitrate.ToString());
        result[result.IndexOf("-codec:a") + 1].Should().Be("copy");
        result[result.IndexOf("-ss") + 1].Should().Be("25.5");
        result.Count(value => value == "-codec:v:0").Should().Be(1);
        result[result.IndexOf("-vf") + 1].Should().StartWith("scale_vaapi=w=1920:h=1080:format=nv12,").And.Contain(HdrCommandTransformer.ConversionFilter);
        result[result.IndexOf("-profile:v:0") + 1].Should().Be("main10");
        result[result.IndexOf("-color_trc") + 1].Should().Be("smpte2084");
    }

    [Theory, AutoMoqData]
    internal void Should_ApplyConversionAfterNativeSubtitleComposition(string input, HdrCommandTransformer sut)
    {
        // Arrange
        var native = FfmpegArguments.Format(["-i", input, "-codec:v:0", "hevc_vaapi", "-filter_complex", "[0:v][0:s]overlay[vout]",
            "-map", "[vout]", "-map", "0:a", "output.m3u8"]);

        // Act
        var result = FfmpegArguments.Parse(sut.Transform(native));

        // Assert
        result[result.IndexOf("-filter_complex") + 1].Should().StartWith("[0:v][0:s]overlay[vout];[vout]").And.EndWith("[hdr_playback]");
        result[result.IndexOf("-map") + 1].Should().Be("[hdr_playback]");
        result.Should().Contain("0:a");
    }

    [Theory, AutoMoqData]
    internal void Should_ConvertUnlabelledNativeSubtitleOutput(string input, HdrCommandTransformer sut)
    {
        // Arrange
        var graph = $"alphasrc=s=1280x720,subtitles=f='{input}.srt',hwupload=derive_device=vulkan[sub];" +
                    "[0:0]hwmap=derive_device=drm,format=drm_prime,libplacebo=w=1280:h=720,format=vulkan[main];" +
                    "[main][sub]overlay_vulkan=eof_action=pass:repeatlast=0,hwmap=derive_device=vaapi,format=vaapi,scale_vaapi=format=nv12:out_range=tv";
        var native = FfmpegArguments.Format(["-i", input, "-map", "0:0", "-map", "0:1", "-map", "-0:0", "-codec:v:0", "hevc_vaapi",
            "-filter_complex", graph, "-codec:a:0", "aac", "output.m3u8"]);

        // Act
        var result = FfmpegArguments.Parse(sut.Transform(native));

        // Assert
        result[result.IndexOf("-filter_complex") + 1].Should().Be(graph + "," + HdrCommandTransformer.ConversionFilter);
        result.Where((_, index) => index > 0 && result[index - 1] == "-map").Should().Equal("0:0", "0:1", "-0:0");
        result[result.IndexOf("-codec:a:0") + 1].Should().Be("aac");
    }

    [Theory, AutoMoqData]
    internal void Should_RejectUnlabelledNonVideoOutput(string input, HdrCommandTransformer sut)
    {
        // Arrange
        var native = FfmpegArguments.Format(["-i", input, "-codec:v:0", "hevc_vaapi", "-filter_complex", "[0:a]anull", "output.m3u8"]);

        // Act
        var action = () => sut.Transform(native);

        // Assert
        action.Should().Throw<NotSupportedException>();
    }

    [Theory, AutoMoqData]
    internal void Should_RejectUnsupportedEncoderWithoutPretendingToProduceHdr(string input, HdrCommandTransformer sut)
    {
        // Arrange
        var native = FfmpegArguments.Format(["-i", input, "-codec:v:0", "h264_vaapi", "-vf", "scale_vaapi=1920:1080", "output.m3u8"]);

        // Act
        var action = () => sut.Transform(native);

        // Assert
        action.Should().Throw<NotSupportedException>();
    }

    [Theory, AutoMoqData]
    internal void Should_RejectAmbiguousFilterGraph(string input, HdrCommandTransformer sut)
    {
        // Arrange
        var native = FfmpegArguments.Format(["-i", input, "-codec:v:0", "hevc_vaapi", "-filter_complex", "[0:v]split[v1][v2]", "-map", "[v1]", "-map", "[v2]", "output.m3u8"]);

        // Act
        var action = () => sut.Transform(native);

        // Assert
        action.Should().Throw<NotSupportedException>();
    }

    [Theory, AutoMoqData]
    internal void Should_PreserveQuotedPathsAndFilterEscapes(string input, HdrCommandTransformer sut)
    {
        // Arrange
        var path = "media folder/" + input + " \"quoted\".mkv";
        var filter = @"scale=trunc(min(iw\,1920)/2)*2:1080,format=nv12,hwupload=derive_device=vaapi";
        var native = FfmpegArguments.Format(["-i", path, "-codec:v:0", "hevc_vaapi", "-vf", filter, "output.m3u8"]);

        // Act
        var result = FfmpegArguments.Parse(sut.Transform(native));

        // Assert
        result[result.IndexOf("-i") + 1].Should().Be(path);
        result[result.IndexOf("-vf") + 1].Should().StartWith(filter + ",");
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture
        {
            RepeatCount = 1
        };
        fixture.Customize(new AutoMoqCustomization
        {
            ConfigureMembers = true,
            GenerateDelegates = true
        });
        return fixture;
    }
    );
}
