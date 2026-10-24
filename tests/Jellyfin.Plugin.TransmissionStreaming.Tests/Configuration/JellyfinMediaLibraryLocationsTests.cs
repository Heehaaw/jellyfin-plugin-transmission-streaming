namespace Jellyfin.Plugin.TransmissionStreaming.Tests.Configuration;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using global::TransmissionStreaming.Core.Configuration;
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

    [Theory, AutoMoqData]
    internal void Should_KeepServiceLibraryTypesSeparateAndIncludeDownloadLibraries(MediaLibraries source,
        [Frozen] Mock<ILibraryManager> library, JellyfinMediaLibraryLocations sut)
    {
        // Arrange
        source.Movies.CollectionType = CollectionTypeOptions.movies;
        source.Shows.CollectionType = CollectionTypeOptions.tvshows;
        source.Other.CollectionType = null;
        library.Setup(value => value.GetVirtualFolders(false)).Returns([source.Movies, source.Shows, source.Other]);
        var all = source.Movies.Locations.Concat(source.Shows.Locations).Concat(source.Other.Locations).Distinct();

        // Act
        var movies = sut.GetServicePaths(MediaService.Radarr);
        var shows = sut.GetServicePaths(MediaService.Sonarr);
        var downloadsAndLibraries = sut.GetAllPaths();

        // Assert
        movies.Should().BeEquivalentTo(source.Movies.Locations);
        shows.Should().BeEquivalentTo(source.Shows.Locations);
        downloadsAndLibraries.Should().BeEquivalentTo(all);
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
