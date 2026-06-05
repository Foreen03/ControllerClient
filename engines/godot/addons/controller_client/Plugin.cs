#if TOOLS
using Godot;

[Tool]
public partial class Plugin : EditorPlugin
{
    public override void _EnterTree()
    {
        // ControllerBridge is registered via [GlobalClass] so it
        // already appears in the "Add Node" dialog after a build.
        // No extra registration needed here.
    }

    public override void _ExitTree()
    {
    }
}
#endif
