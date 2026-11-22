using UnityEditor;

namespace DarkLegion.Editor
{
    internal static class DesignAssetFolder
    {
        public static void EnsureExists(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            EnsureExists(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
        }
    }
}
