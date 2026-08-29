using System.Reflection;
using FluentAssertions;
using QuoteDesk.Data.Repositories;

namespace QuoteDesk.IntegrationTests.Data;

/// <summary>
/// "No entity type appears in a signature outside QuoteDesk.Data" (tasks/task-02-data-efcore.md) —
/// checked by reflection over the actual repository interfaces, not by convention.
/// </summary>
public class DataLayerBoundaryTests
{
    [Fact]
    public void RepositoryInterfaces_NeverExposeAnEntityTypeInTheirSignatures()
    {
        var repositoryInterfaces = typeof(ICatalogRepository).Assembly.GetTypes()
            .Where(t => t.IsInterface && t.Namespace == typeof(ICatalogRepository).Namespace);

        var offendingMembers = new List<string>();

        foreach (var repoInterface in repositoryInterfaces)
        {
            foreach (var method in repoInterface.GetMethods())
            {
                foreach (var type in ParameterAndReturnTypes(method))
                {
                    if (IsOrContainsEntityType(type))
                    {
                        offendingMembers.Add($"{repoInterface.Name}.{method.Name}");
                    }
                }
            }
        }

        offendingMembers.Should().BeEmpty("entities must never leave QuoteDesk.Data — repositories return plain records");
    }

    private static IEnumerable<Type> ParameterAndReturnTypes(MethodInfo method)
    {
        yield return method.ReturnType;
        foreach (var parameter in method.GetParameters())
        {
            yield return parameter.ParameterType;
        }
    }

    private static bool IsOrContainsEntityType(Type type)
    {
        // Unwrap Task<T> / generic collections to inspect the type argument itself.
        var candidate = type.IsGenericType ? type.GetGenericArguments().FirstOrDefault() ?? type : type;
        return candidate.Namespace == "QuoteDesk.Data.Entities";
    }
}
