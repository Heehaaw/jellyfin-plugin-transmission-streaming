namespace Jellyfin.Plugin.HdrPlayback.Tests.Playback;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Kernel;
using AutoFixture.Xunit2;
using FluentAssertions;
using Xunit;
using Jellyfin.Plugin.HdrPlayback.Playback;
using Microsoft.AspNetCore.WebUtilities;
using global::HdrPlayback.Core.Playback;
using global::HdrPlayback.Logic.Playback;
using Mediator;
using MediaBrowser.Model.MediaInfo;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Dto;
using Jellyfin.Database.Implementations.Entities;

public sealed class HdrPlaybackFilterTests
{
    [Theory, AutoMoqData]
    internal void Should_KeepNativeQualityTracksAndPosition(string source, int bitrate, int width)
    {
        // Arrange
        var native = QueryHelpers.AddQueryString(
            uri: "Videos/" + source + "/master.m3u8",
            queryString: new Dictionary<string, string?>
            {
                ["VideoBitrate"] = bitrate.ToString(),
                ["MaxWidth"] = width.ToString(),
                ["StartTimeTicks"] = "123456789",
                ["AudioStreamIndex"] = "2",
                ["SubtitleStreamIndex"] = "3",
                ["MediaSourceId"] = source
            }
        );

        // Act
        var result = HdrPlaybackFilter.HdrTranscodingUrl(native);
        var query = QueryHelpers.ParseQuery(result[(result.IndexOf('?') + 1)..]);

        // Assert
        query["VideoBitrate"].ToString().Should().Be(bitrate.ToString());
        query["MaxWidth"].ToString().Should().Be(width.ToString());
        query["MediaSourceId"].ToString().Should().Be(source);
        query["StartTimeTicks"].ToString().Should().Be("123456789");
        query["AudioStreamIndex"].ToString().Should().Be("2");
        query["SubtitleStreamIndex"].ToString().Should().Be("3");
        query["VideoCodec"].ToString().Should().Be("hevc");
        query["Profile"].ToString().Should().Be("main10");
        query["VideoRangeType"].ToString().Should().Be("HDR10");
    }

    [Theory, AutoMoqData]
    internal async Task Should_RegisterNegotiatedNativeSessionWithoutAddingSources(
        PlaybackInfoResponse source,
        HdrSessionKey key,
        ResultExecutingContext context,
        [Frozen] Mock<IHdrSessionStore> sessions,
        HdrPlaybackFilter sut)
    {
        // Arrange
        var originalIds = source.MediaSources.Select(media => media.Id).ToArray();
        source.ErrorCode = null;
        foreach (var media in source.MediaSources)
        {
            media.TranscodingUrl = "Videos/" + media.Id + "/master.m3u8?VideoCodec=h264&VideoBitrate=4000000";
        }

        Authenticate(context.HttpContext, key);
        context.HttpContext.Items[HdrRequest.NegotiationKey] = new HdrPlaybackDecision(true, true, null);
        context.Result = new ObjectResult(source);

        // Act
        await sut.OnResultExecutionAsync(context, () => Task.FromResult(new ResultExecutedContext(context, context.Filters, context.Result, context.Controller)));

        // Assert
        source.MediaSources.Select(media => media.Id).Should().Equal(originalIds);
        source.MediaSources.Should().OnlyContain(media => !media.SupportsDirectPlay && !media.SupportsDirectStream && media.SupportsTranscoding);
        source.MediaSources.Should().OnlyContain(media => media.TranscodingUrl!.Contains("Profile=main10") && media.TranscodingUrl.Contains("VideoBitrate=4000000"));
        sessions.Verify(store => store.Enable(new HdrSessionKey(key.UserId, key.DeviceId, source.PlaySessionId ?? "")), Times.Once);
        context.HttpContext.Response.Headers[HdrRequest.AvailableHeader].ToString().Should().Be("true");
    }

    [Theory, AutoMoqData]
    internal async Task Should_LabelOnlySelectedHlsOutputAsPq(
        HdrSessionKey source,
        FileContentResult playlist,
        ResultExecutingContext context,
        [Frozen] Mock<IHdrSessionStore> sessions,
        HdrPlaybackFilter sut)
    {
        // Arrange
        const string nativePlaylist = "#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=4000000,VIDEO-RANGE=SDR\nmain.m3u8";
        Authenticate(context.HttpContext, source);
        context.HttpContext.Request.QueryString = new QueryString("?PlaySessionId=" + source.PlaySessionId);
        playlist.FileContents = Encoding.UTF8.GetBytes(nativePlaylist);
        context.Result = playlist;
        sessions.Setup(store => store.Contains(source)).Returns(true);

        // Act
        await sut.OnResultExecutionAsync(context, () => Task.FromResult(new ResultExecutedContext(context, context.Filters, context.Result, context.Controller)));

        // Assert
        Encoding.UTF8.GetString(playlist.FileContents).Should().Be(nativePlaylist.Replace("VIDEO-RANGE=SDR", "VIDEO-RANGE=PQ"));
    }

    [Theory, AutoMoqData]
    internal async Task Should_KeepUnselectedHlsOutputUnchanged(
        HdrSessionKey source,
        FileContentResult playlist,
        ResultExecutingContext context,
        [Frozen] Mock<IHdrSessionStore> sessions,
        HdrPlaybackFilter sut)
    {
        // Arrange
        const string nativePlaylist = "#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=4000000,VIDEO-RANGE=SDR\nmain.m3u8";
        Authenticate(context.HttpContext, source);
        context.HttpContext.Request.QueryString = new QueryString("?PlaySessionId=" + source.PlaySessionId);
        playlist.FileContents = Encoding.UTF8.GetBytes(nativePlaylist);
        context.Result = playlist;
        sessions.Setup(store => store.Contains(source)).Returns(false);

        // Act
        await sut.OnResultExecutionAsync(context, () => Task.FromResult(new ResultExecutedContext(context, context.Filters, context.Result, context.Controller)));

        // Assert
        Encoding.UTF8.GetString(playlist.FileContents).Should().Be(nativePlaylist);
    }

    [Theory, AutoMoqData]
    internal async Task Should_LeaveMalformedPlaybackRequestsToNativeValidation(
        Guid source,
        ResourceExecutingContext context,
        HdrPlaybackFilter sut)
    {
        // Arrange
        context.HttpContext.Request.Method = "POST";
        context.HttpContext.Request.Path = "/Items/" + source + "/PlaybackInfo";
        context.RouteData.Values["itemId"] = source;
        const string malformed = "{broken json";
        context.HttpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(malformed));
        var nativeCalls = 0;

        // Act
        await sut.OnResourceExecutionAsync(
            context: context,
            next: () =>
            {
                nativeCalls++;
                return Task.FromResult(new ResourceExecutedContext(context, context.Filters));
            }
        );

        // Assert
        nativeCalls.Should().Be(1);
        context.HttpContext.Request.Body.Position.Should().Be(0);
        context.Result.Should().BeNull();
    }

    [Theory, AutoMoqData]
    internal async Task Should_RemoveOnlyTheStoppedPlaybackSelection(
        HdrSessionKey source,
        ResourceExecutingContext context,
        [Frozen] Mock<IHdrSessionStore> sessions,
        HdrPlaybackFilter sut)
    {
        // Arrange
        Authenticate(context.HttpContext, source);
        context.HttpContext.Request.Method = "POST";
        context.HttpContext.Request.Path = "/Sessions/Playing/Stopped";
        context.HttpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"PlaySessionId\":\"" + source.PlaySessionId + "\"}"));

        // Act
        await sut.OnResourceExecutionAsync(context, () => Task.FromResult(new ResourceExecutedContext(context, context.Filters)));

        // Assert
        sessions.Verify(store => store.Remove(source), Times.Once);
        context.HttpContext.Request.Body.Position.Should().Be(0);
    }

    [Theory, AutoMoqData]
    internal async Task Should_ForceOneNativeTranscodeWhileRetainingPlaybackSettings(
        PlaybackRequest source,
        User user,
        Movie item,
        MediaSourceInfo media,
        EncodingOptions encoding,
        HdrSessionKey key,
        ResourceExecutingContext context,
        [Frozen] Mock<ILibraryManager> library,
        [Frozen] Mock<IUserManager> users,
        [Frozen] Mock<IMediaSourceManager> sources,
        [Frozen] Mock<IServerConfigurationManager> configuration,
        [Frozen] Mock<ISender> sender,
        HdrPlaybackFilter sut)
    {
        // Arrange
        Authenticate(context.HttpContext, key);
        context.HttpContext.Request.Method = "POST";
        context.HttpContext.Request.Path = "/Items/" + item.Id + "/PlaybackInfo";
        context.RouteData.Values["itemId"] = item.Id;
        context.HttpContext.Request.Headers[HdrRequest.ModeHeader] = "true";
        source.DeviceProfile.TranscodingProfiles[0].Type = DlnaProfileType.Video;
        var jsonOptions = new JsonSerializerOptions
        {
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };
        var body = JsonSerializer.SerializeToNode(source, jsonOptions)!.AsObject();
        body["DeviceProfile"]!["TranscodingProfiles"]![0]!["MinSegments"] = "3";
        var original = new MemoryStream(Encoding.UTF8.GetBytes(body.ToJsonString()));
        context.HttpContext.Request.Body = original;
        users.Setup(manager => manager.GetUserById(key.UserId)).Returns(user);
        library.Setup(manager => manager.GetItemById<BaseItem>(item.Id, user)).Returns(item);
        sources.Setup(manager => manager.GetMediaSource(item, source.MediaSourceId, null, true, It.IsAny<CancellationToken>())).ReturnsAsync(media);
        configuration.Setup(manager => manager.GetConfiguration("encoding")).Returns(encoding);
        sender.Setup(dispatcher => dispatcher.Send(It.IsAny<SelectHdrMode.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HdrPlaybackDecision(true, true, null));
        JsonNode? bound = null;

        // Act
        await sut.OnResourceExecutionAsync(
            context: context,
            next: async () =>
            {
                bound = await JsonNode.ParseAsync(context.HttpContext.Request.Body);
                return new ResourceExecutedContext(context, context.Filters);
            }
        );

        // Assert
        bound!["EnableDirectPlay"]!.GetValue<bool>().Should().BeFalse();
        bound["EnableDirectStream"]!.GetValue<bool>().Should().BeFalse();
        bound["AllowVideoStreamCopy"]!.GetValue<bool>().Should().BeFalse();
        bound["MaxStreamingBitrate"]!.GetValue<long>().Should().Be(source.MaxStreamingBitrate);
        bound["StartTimeTicks"]!.GetValue<long>().Should().Be(source.StartTimeTicks);
        bound["MediaSourceId"]!.GetValue<string>().Should().Be(source.MediaSourceId);
        bound["AudioStreamIndex"]!.GetValue<int>().Should().Be(source.AudioStreamIndex);
        bound["SubtitleStreamIndex"]!.GetValue<int>().Should().Be(source.SubtitleStreamIndex);
        bound["DeviceProfile"]!["TranscodingProfiles"]![0]!["VideoCodec"]!.GetValue<string>().Should().Be("hevc");
        context.HttpContext.Request.Body.Should().BeSameAs(original);
    }

    internal sealed record PlaybackRequest(
        string MediaSourceId,
        long MaxStreamingBitrate,
        long StartTimeTicks,
        int AudioStreamIndex,
        int SubtitleStreamIndex,
        DeviceProfile DeviceProfile);

    private static void Authenticate(HttpContext context, HdrSessionKey key)
    {
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            claims: [new Claim("Jellyfin-UserId", key.UserId.ToString()), new Claim("Jellyfin-DeviceId", key.DeviceId)],
            authenticationType: "Test"
        ));
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
        // MVC contexts need a real HTTP feature collection, not an AutoMoq proxy.
        fixture.Register(() => new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()));
        fixture.Customizations.Add(new FilteringSpecimenBuilder(
            builder: new FixedBuilder("application/vnd.apple.mpegurl"),
            specification: new ParameterSpecification(typeof(string), "contentType")
        ));
        fixture.Customize<FileContentResult>(composer => composer.OmitAutoProperties());
        fixture.Customize<ResultExecutingContext>(composer => composer.OmitAutoProperties());
        fixture.Customize<ResourceExecutingContext>(composer => composer.OmitAutoProperties());
        fixture.Customize<User>(composer => composer.OmitAutoProperties().With(value => value.Id));
        fixture.Customize<Movie>(composer => composer.OmitAutoProperties().With(value => value.Id));
        fixture.Customize<PlaybackInfoResponse>(composer => composer.Without(response => response.ErrorCode));
        return fixture;
    }
    );
}
