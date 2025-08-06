using System.Reflection;

namespace CorchEdges.Tests.Infrastructure;

[Trait("Category", TestCategories.Contract)]
public class TestClassesMissingCategoryTraits
{
    [Fact]
    public void AllTestClasses_MustHaveCategoryTraits()
    {
        var testAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.Contains("Tests") == true);

        var missingCategoryClasses = new List<string>();

        foreach (var assembly in testAssemblies)
        {
            var testClasses = assembly.GetTypes()
                .Where(t =>
                    !t.IsAbstract &&
                    t.GetMethods().Any(m =>
                        m.GetCustomAttributes(typeof(FactAttribute), false).Any() ||
                        m.GetCustomAttributes(typeof(TheoryAttribute), false).Any()))
                .ToList();

            missingCategoryClasses.AddRange(from testClass in testClasses
                let hasCategoryTrait = testClass.GetCustomAttributes<TraitAttribute>()
                    .Any(t => t.Name == "Category")
                where !hasCategoryTrait
                select testClass.FullName ?? testClass.Name);
        }

        Assert.True(missingCategoryClasses.Count == 0,
            $"These test classes are missing Category traits:\n" +
            string.Join("\n", missingCategoryClasses.Select(c => $"  - {c}")));
    }

    [Fact]
    public void AllIntegrationTests_MustHaveInfrastructureRequirements()
    {
        var testAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.Contains("Tests") == true);

        var missingRequirementsClasses = new List<string>();

        foreach (var assembly in testAssemblies)
        {
            var integrationTestClasses = assembly.GetTypes()
                .Where(t =>
                    !t.IsAbstract &&
                    t.GetCustomAttributes<TraitAttribute>()
                        .Any(trait => trait.Name == "Category" && trait.Value == TestCategories.Integration) &&
                    t.GetMethods().Any(m =>
                        m.GetCustomAttributes(typeof(FactAttribute), false).Any() ||
                        m.GetCustomAttributes(typeof(TheoryAttribute), false).Any()))
                .ToList();

            missingRequirementsClasses.AddRange(from testClass in integrationTestClasses
                let hasRequiresTrait = testClass.GetCustomAttributes<TraitAttribute>()
                    .Any(t => t.Name == "Requires")
                where !hasRequiresTrait
                select testClass.FullName ?? testClass.Name);
        }

        Assert.True(missingRequirementsClasses.Count == 0,
            $"These Integration test classes are missing 'Requires' infrastructure requirement traits:\n" +
            string.Join("\n", missingRequirementsClasses.Select(c => $"  - {c}")) +
            $"\n\nIntegration tests must specify their infrastructure requirements using:" +
            $"\n[Trait(\"Requires\", InfrastructureRequirements.XYZ)]" +
            $"\n\nAvailable requirements: PostgreSql, AzureServiceBus, AzureFunctions, SharePoint, AzureGraphApi, AzureKeyVault");
    }
}