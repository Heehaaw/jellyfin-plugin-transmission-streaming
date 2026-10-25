namespace Jellyfin.Plugin.HdrPlayback.Tests.Player;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using Jellyfin.Plugin.HdrPlayback.Player;
using Xunit;

public sealed class PlayerScriptInjectorTests
{
    [Theory, AutoMoqData]
    internal void Should_AddOneBasePathAwareScript(string source)
    {
        // Arrange
        var html = "<html><head></head><body>" + source + "</body></html>";
        var basePath = "/" + source;

        // Act
        var once = PlayerScriptInjector.InjectIndex(html, basePath);
        var twice = PlayerScriptInjector.InjectIndex(once, basePath);

        // Assert
        once.Should().Contain(basePath + "/HdrPlayback/player.js");
        twice.Should().Be(once);
    }

    [Theory, AutoMoqData]
    internal void Should_ExtendNativeSettingsWithoutChangingQualityChoice(string source)
    {
        // Arrange
        var script = "var u=n.f.getSupportedCommands(t),l=[];l.push({name:r.Ay.translate(\"Quality\"),id:\"quality\",asideText:f});" +
                     "function choose(e,t,a){switch(e){case\"quality\":return " + source + ";}}";

        // Act
        var result = PlayerScriptInjector.InjectMenu(script);

        // Assert
        result.Should().Contain("!window.JellyfinHdrPlayback.sourceSelected(n.f,t)&&l.push(window.JellyfinHdrPlayback.menu(n.f,t))");
        result.Should().Contain("JellyfinHdrPlayback.toggle(n.f,arguments[2])");
        result.Should().Contain("case\"quality\":return " + source);
        PlayerScriptInjector.InjectMenu(result).Should().Be(result);
    }

    [Theory, AutoMoqData]
    internal void Should_LeaveUnknownClientCodeUnchanged(string source)
    {
        // Arrange
        var script = "function unsupported(){return \"" + source + "\";}";

        // Act
        var result = PlayerScriptInjector.InjectMenu(script);

        // Assert
        result.Should().Be(script);
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
