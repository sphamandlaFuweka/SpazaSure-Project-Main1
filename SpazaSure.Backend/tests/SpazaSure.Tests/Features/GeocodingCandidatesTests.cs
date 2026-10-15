using FluentAssertions;
using SpazaSure.UserService.Services;
using Xunit;

namespace SpazaSure.Tests.Features;

public class GeocodingCandidatesTests
{
    [Fact]
    public void FallsBackFromExactAddress_ToStreet_ToArea_ToTown()
    {
        var queries = GeocodingService.Candidates("1234 Vilakazi Street, Orlando West", "Soweto", "Gauteng", "1804").ToList();

        queries.First().Should().Be("1234 Vilakazi Street, Orlando West, Soweto, Gauteng, 1804, South Africa");
        queries.Should().Contain("Vilakazi Street, Orlando West, Soweto, Gauteng, South Africa");  // without the house number
        queries.Should().Contain("Orlando West, Soweto, Gauteng, South Africa");                    // the area
        queries.Should().Contain("Soweto, Gauteng, 1804, South Africa");                             // the town
        queries.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void TownOnly_StillProducesAQuery()
    {
        GeocodingService.Candidates(null, "Mthatha", "Eastern Cape", null)
            .Should().Contain("Mthatha, Eastern Cape, South Africa");
    }

    [Fact]
    public void NothingToSearch_ProducesNoQueries()
    {
        GeocodingService.Candidates("", null, " ", null).Should().BeEmpty();
    }
}
