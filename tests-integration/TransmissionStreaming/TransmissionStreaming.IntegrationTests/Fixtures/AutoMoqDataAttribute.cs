namespace TransmissionStreaming.IntegrationTests.Fixtures;

using AutoFixture;
using AutoFixture.AutoMoq;
using TestSupport.Testing;

internal sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
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
        // Each generated download is a playable filename spanning several torrent pieces.
        fixture.Customize<DownloadSource>(composer => composer
            .With(source => source.Media, fixture.CreateMany<byte>(128 * 1024).ToArray())
            .With(source => source.FileName, fixture.Create<string>() + ".mkv")
        );
        return fixture;
    }
);
