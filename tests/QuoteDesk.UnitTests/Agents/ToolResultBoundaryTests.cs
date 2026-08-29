using System.Reflection;
using FluentAssertions;
using QuoteDesk.Agents.Tools.Results;
using QuoteDesk.Domain;

namespace QuoteDesk.UnitTests.Agents;

/// <summary>
/// "CostPrice and margin appear in no tool result type" and "no EF entity type escapes
/// QuoteDesk.Data" (tasks/task-05-tools.md) — checked by reflecting over the actual result types
/// rather than trusting a review to catch a future addition, in the style of
/// QuoteDesk.UnitTests.Domain.DomainPurityTests and DataLayerBoundaryTests.
/// </summary>
public class ToolResultBoundaryTests
{
    private static IEnumerable<Type> ResultTypes => typeof(CustomerMatch).Assembly.GetTypes()
        .Where(t => t.Namespace == "QuoteDesk.Agents.Tools.Results");

    [Fact]
    public void ToolResultTypes_NeverExposeCostOrMargin()
    {
        var offenders = new List<string>();

        foreach (var type in ResultTypes)
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.Name.Contains("Cost", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Contains("Margin", StringComparison.OrdinalIgnoreCase))
                {
                    offenders.Add($"{type.Name}.{property.Name}");
                }
            }
        }

        offenders.Should().BeEmpty("no tool result may expose cost price or margin — docs/DOMAIN.md 'What the model is never allowed to do'");
    }

    [Fact]
    public void ToolResultTypes_NeverExposeAnEntityOrTheDomainPricedLine()
    {
        var offenders = new List<string>();

        foreach (var type in ResultTypes)
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var propertyType = UnwrapCollection(property.PropertyType);
                if (propertyType.Namespace == "QuoteDesk.Data.Entities" || propertyType == typeof(PricedLine))
                {
                    offenders.Add($"{type.Name}.{property.Name}");
                }
            }
        }

        offenders.Should().BeEmpty("entities never leave QuoteDesk.Data, and PricedLine carries MarginPct which must not reach the model");
    }

    private static Type UnwrapCollection(Type type)
    {
        if (type.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
        {
            return type.GetGenericArguments().Single();
        }

        return type;
    }
}
