using Engine.Kernel.World;
using ImGuiNET;

namespace Engine.Editor;

/// <summary>
/// Walks IWorld.Roots/GameObject.Children directly — not Query&lt;T&gt;(),
/// there's no component type to query for here — so, like EditorPlugin
/// itself, this never touches SystemAccessScope and needs no declared
/// Reads/Writes.
/// </summary>
internal static class HierarchyPanel
{
    public static void Draw(IWorld world, EditorState state)
    {
        ImGui.SetNextWindowPos(new(10, 110), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new(220, 300), ImGuiCond.FirstUseEver);
        ImGui.Begin("Hierarchy");

        foreach (var root in world.Roots)
            DrawNode(root, state);

        ImGui.End();
    }

    private static void DrawNode(GameObject go, EditorState state)
    {
        // PushID/PopID, not relying on go.Name for identity: sibling
        // GameObjects can share a name (nothing stops it), and ImGui's
        // default ID-from-label would then merge their open/selected state.
        ImGui.PushID(go.GetHashCode());

        var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;
        if (go.Children.Count == 0)
            flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;
        if (ReferenceEquals(state.Selected, go))
            flags |= ImGuiTreeNodeFlags.Selected;

        var open = ImGui.TreeNodeEx(go.Name, flags);
        if (ImGui.IsItemClicked())
            state.Selected = go;

        if (open && go.Children.Count > 0)
        {
            foreach (var child in go.Children)
                DrawNode(child, state);
            ImGui.TreePop();
        }

        ImGui.PopID();
    }
}
