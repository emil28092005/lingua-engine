using System.Numerics;
using System.Reflection;
using Engine.Kernel.World;
using ImGuiNET;

namespace Engine.Editor;

/// <summary>
/// Shows EditorState.Selected's Transform (direct field access — Transform
/// is a known struct, not something to reflect over) and, below it, every
/// Component's public fields via reflection: there's no per-component-type
/// editor code anywhere in engine.editor, on purpose — a new component
/// type in any plugin gets an Inspector for free, which is the entire
/// point of doing this by reflection instead of a registry of per-type
/// drawers. Only int/float/bool/string/Vector3 fields are editable;
/// anything else falls back to a read-only ToString() so an unsupported
/// field type degrades to "visible but not editable" instead of being
/// silently hidden.
/// </summary>
internal static class InspectorPanel
{
    public static void Draw(EditorState state)
    {
        ImGui.Begin("Inspector");

        var go = state.Selected;
        if (go is null)
        {
            ImGui.TextDisabled("Nothing selected.");
            ImGui.End();
            return;
        }

        ImGui.Text(go.Name);
        ImGui.Separator();

        if (ImGui.CollapsingHeader("Transform", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var t = go.Transform;
            var changed = ImGui.DragFloat3("Position", ref t.LocalPosition, 0.1f);
            changed |= ImGui.DragFloat3("Scale", ref t.LocalScale, 0.1f);
            ImGui.Text($"Rotation (quat): {t.LocalRotation}");

            if (changed)
                go.Transform = t;
        }

        foreach (var component in go.Components)
            DrawComponent(component);

        ImGui.End();
    }

    private static void DrawComponent(Component component)
    {
        var type = component.GetType();
        if (!ImGui.CollapsingHeader(type.Name, ImGuiTreeNodeFlags.DefaultOpen))
            return;

        ImGui.PushID(component.GetHashCode());

        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
        if (fields.Length == 0)
            ImGui.TextDisabled("(no fields)");

        foreach (var field in fields)
            DrawField(component, field);

        ImGui.PopID();
    }

    private static void DrawField(Component component, FieldInfo field)
    {
        var value = field.GetValue(component);

        switch (value)
        {
            case int i:
                if (ImGui.DragInt(field.Name, ref i))
                    field.SetValue(component, i);
                break;

            case float f:
                if (ImGui.DragFloat(field.Name, ref f))
                    field.SetValue(component, f);
                break;

            case bool b:
                if (ImGui.Checkbox(field.Name, ref b))
                    field.SetValue(component, b);
                break;

            case string s:
                if (ImGui.InputText(field.Name, ref s, 256))
                    field.SetValue(component, s);
                break;

            case Vector3 v:
                if (ImGui.DragFloat3(field.Name, ref v, 0.1f))
                    field.SetValue(component, v);
                break;

            default:
                ImGui.Text($"{field.Name}: {value}");
                break;
        }
    }
}
