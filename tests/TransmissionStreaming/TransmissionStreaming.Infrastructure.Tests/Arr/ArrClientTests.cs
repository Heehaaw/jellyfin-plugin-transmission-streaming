namespace TransmissionStreaming.Infrastructure.Tests.Arr;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using global::TransmissionStreaming.Core.Arr;
using global::TransmissionStreaming.Infrastructure.Arr;
using global::TransmissionStreaming.Infrastructure.Arr.Transport;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

public sealed class ArrClientTests
{
    [Theory, AutoMoqData]
    internal async Task Should_ReadDistinctConfiguredLibraryRoots(
        ArrConnection connection,
        RootFolder root,
        TestHttpHandler handler,
        ArrClient sut
    )
    {
        // Arrange
        var missing = root with
        {
            Path = " "
        };
        handler.Respond = _ => Response(
            new[]
            {
                root, missing, root
            }
        );

        // Act
        var result = await sut.GetRootFoldersAsync(connection, CancellationToken.None);

        // Assert
        result.Should().Equal(root.Path);
        handler.Requests.Should().ContainSingle(request => request.AbsolutePath == "/api/v3/rootfolder");
    }

    internal sealed record RootFolder(string Path);

    private static HttpResponseMessage Response<T>(T body)
    {
        return new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web)), Encoding.UTF8, "application/json")
        };
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
        {
            var fixture = new Fixture
            {
                RepeatCount = 1
            };
            fixture.Customize(
                new AutoMoqCustomization
                {
                    ConfigureMembers = true,
                    GenerateDelegates = true
                }
            );
            fixture.Register<IArrTransport>(() => fixture.Create<ArrTransport>());
            fixture.Register<IArrClient>(() => fixture.Create<ArrClient>());

            fixture.Customize<ArrConnection>(composer => composer.With(connection => connection.Url, "http://arr.invalid/"));
            fixture.Customize<MediaIdentity>(composer => composer.With(identity => identity.OriginalFileName, () => fixture.Create<string>() + ".mkv"));
            fixture.Register(() => new HttpClient(fixture.Create<TestHttpHandler>()));
            fixture.Freeze<TestHttpHandler>();
            return fixture;
        }
    );
}
