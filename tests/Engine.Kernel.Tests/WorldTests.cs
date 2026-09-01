namespace Engine.Kernel.Tests;

public class WorldTests
{
    // TODO(M0): once IWorld has a concrete implementation —
    //   - GameObject hierarchy (parent/children) behaves correctly
    //   - Query<T>() returns exactly the GameObjects holding a T, and is
    //     O(matches) — see the World row in docs/kernel-contract.md §2
    //   - AddComponent/RemoveComponent keep the type index consistent
    [Fact(Skip = "World has no implementation yet — see M0 in docs/kernel-contract.md §8.")]
    public void Placeholder()
    {
    }
}
