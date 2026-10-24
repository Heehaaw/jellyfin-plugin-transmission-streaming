namespace Jellyfin.Plugin.TransmissionStreaming.Tests.Configuration;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using Jellyfin.Plugin.TransmissionStreaming.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Moq;
using Xunit;

public sealed class JellyfinMediaLibraryLocationsTests
{
    [Theory, AutoMoqData]
    internal void Should_UseMovieAndShowPathsWithoutAdoptingOtherLibraryTypes(MediaLibraries source,
        [Frozen] Mock<ILibraryManager> library, JellyfinMediaLibraryLocations sut)
    {
        // Arrange
        source.Movies.CollectionType = CollectionTypeOptions.movies;
        source.Shows.CollectionType = CollectionTypeOptions.tvshows;
        source.Other.CollectionType = CollectionTypeOptions.boxsets;
        library.Setup(value => value.GetVirtualFolders(false)).Returns([source.Movies, source.Shows, source.Other]);
        var expected = source.Movies.Locations.Concat(source.Shows.Locations).Distinct().ToArray();

        // Act
        var result = sut.GetPaths();

        // Assert
        result.Should().BeEquivalentTo(expected);
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        fixture.Customize<VirtualFolderInfo>(composer => composer.Without(value => value.LibraryOptions));
        return fixture;
    });
}

public sealed record MediaLibraries(VirtualFolderInfo Movies, VirtualFolderInfo Shows, VirtualFolderInfo Other);
