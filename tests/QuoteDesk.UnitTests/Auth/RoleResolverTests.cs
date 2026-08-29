using FluentAssertions;
using QuoteDesk.Api.Auth;
using Xunit;

namespace QuoteDesk.UnitTests.Auth;

public class RoleResolverTests
{
    [Fact]
    public void Resolve_EmailInAdminList_ReturnsAdmin()
    {
        RoleResolver.Resolve("harsh@example.com", ["harsh@example.com"]).Should().Be(RoleResolver.Admin);
    }

    [Fact]
    public void Resolve_EmailInAdminListDifferentCasing_ReturnsAdmin()
    {
        RoleResolver.Resolve("Harsh@Example.com", ["harsh@example.com"]).Should().Be(RoleResolver.Admin);
    }

    [Fact]
    public void Resolve_UnknownEmail_ReturnsSales()
    {
        RoleResolver.Resolve("kiran@shreejitextiles.example", ["harsh@example.com"]).Should().Be(RoleResolver.Sales);
    }

    [Fact]
    public void Resolve_EmptyAdminList_ReturnsSales()
    {
        RoleResolver.Resolve("kiran@shreejitextiles.example", []).Should().Be(RoleResolver.Sales);
    }
}
