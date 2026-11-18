using UnityEditor;

/// <summary>
/// Runs automatically for the pictures in Assets/Project/Art/UI/Book:
///  - the FIRST time a picture is imported it gets the pixel-art sprite settings and its border (see BookSkinSetupMenu.Pictures);
///    after that your own changes in the Inspector or Sprite Editor are left alone,
///  - when the Book Skin asset (Assets/Resources/BookSkin.asset) doesn't exist yet, it is made and filled in
///    (checked after pictures are imported and each time the editor reloads its scripts).
/// So after drawing or copying the pictures there is nothing to click. Farm > Set Up > Book UI does the same by hand.
/// </summary>
[InitializeOnLoad]
public class BookArtImporter : AssetPostprocessor
{
    static BookArtImporter()
    {
        // The pictures may have been imported before this script existed. Once the editor is ready, finish the setup.
        EditorApplication.delayCall += MakeSkinIfMissing;
    }

    private void OnPreprocessTexture()
    {
        string name = BookSkinSetupMenu.BookPictureName(assetPath);
        if (name == null) return;
        var imp = (TextureImporter)assetImporter;
        if (imp.importSettingsMissing) BookSkinSetupMenu.ApplyImportSettings(imp, name, true);
    }

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (string p in imported)
        {
            if (BookSkinSetupMenu.BookPictureName(p) == null) continue;
            EditorApplication.delayCall += MakeSkinIfMissing;      // wait until the import has finished
            return;
        }
    }

    private static void MakeSkinIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (AssetDatabase.LoadAssetAtPath<BookSkin>(BookSkinSetupMenu.SkinPath) != null) return;
        if (!AssetDatabase.IsValidFolder(BookSkinSetupMenu.ArtFolder)) return;
        UnityEngine.Debug.Log(BookSkinSetupMenu.Run(true));
    }
}
