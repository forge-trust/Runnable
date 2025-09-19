using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CliFx.Attributes;
using CliFx.Infrastructure;
using FakeItEasy;
using ForgeTrust.Runnable.Aspire;

public class AspireProfileTests
{
    [Fact]
    public async Task ExecuteAsync_ResolvesDependenciesBeforeOwnComponents()
    {
        var callOrder = new List<string>();
        var dependencyComponent = A.Fake<IAspireComponent<IResource>>();
        var dependencyBuilder = A.Fake<IResourceBuilder<IResource>>();
        var component = A.Fake<IAspireComponent<IResource>>();
        var console = A.Fake<IConsole>();

        A.CallTo(() => dependencyComponent.Generate(A<AspireStartupContext>._, A<IDistributedApplicationBuilder>._))
            .Invokes((AspireStartupContext _, IDistributedApplicationBuilder _) => callOrder.Add("dependency"))
            .Returns(dependencyBuilder);

        A.CallTo(() => component.Generate(A<AspireStartupContext>._, A<IDistributedApplicationBuilder>._))
            .ReturnsLazily((AspireStartupContext _, IDistributedApplicationBuilder _) =>
            {
                callOrder.Add("component");
                throw new InvalidOperationException("Stop execution");
            });

        var profile = new TestProfile(
            [new TestProfile([], [dependencyComponent])],
            [component]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => profile.ExecuteAsync(console).AsTask());

        Assert.Equal("Stop execution", exception.Message);
        Assert.Equal(["dependency", "component"], callOrder);
        A.CallTo(() => dependencyComponent.Generate(A<AspireStartupContext>._, A<IDistributedApplicationBuilder>._)).MustHaveHappenedOnceExactly();
    }

    [Command("test-profile")]
    private sealed class TestProfile : AspireProfile
    {
        private readonly IReadOnlyList<AspireProfile> _dependencies;
        private readonly IReadOnlyList<IAspireComponent> _components;

        public TestProfile(IReadOnlyList<AspireProfile> dependencies, IReadOnlyList<IAspireComponent> components)
        {
            _dependencies = dependencies;
            _components = components;
        }

        public override IEnumerable<AspireProfile> GetDependencies() => _dependencies;

        public override IEnumerable<IAspireComponent> GetComponents() => _components;
    }
}
