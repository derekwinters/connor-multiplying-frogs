using Frogs.Core;

namespace Frogs.EditorTools
{
    /// <summary>Puts the generated app icons into the build (#459). Not yet.</summary>
    public static class AppIcons
    {
        public static void ApplyToBuild()
        {
            ApplyFrom(AppIconSet.Folder);
        }

        public static void ApplyFrom(string folder)
        {
        }
    }
}
