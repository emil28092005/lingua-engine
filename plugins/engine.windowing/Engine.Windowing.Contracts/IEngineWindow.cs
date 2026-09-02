using Silk.NET.Windowing;

namespace Engine.Windowing.Contracts;

/// <summary>
/// Not named <c>IWindow</c> — Silk.NET's own windowing type already has
/// that name in this same file's scope, and living with that would be a
/// standing invitation for the exact ambiguous-name mistake that forced
/// renaming the kernel's own <c>World</c> to <c>GameWorld</c> — see
/// docs/kernel-contract.md §2.
///
/// Exposes the real Silk.NET window directly rather than re-wrapping it:
/// GL context creation and event pumping both need it, and hiding it
/// behind another layer buys nothing at M1's stage. See M1 in
/// docs/kernel-contract.md §8.
/// </summary>
public interface IEngineWindow
{
    IWindow Native { get; }

    bool IsClosing { get; }
}
