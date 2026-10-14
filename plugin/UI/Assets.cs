using System.IO;
using System.Reflection;
using UnityEngine;

namespace HomuraHimeAP.UI;

/// <summary>Image assets that ship inside the plugin (resources/ap_assets).</summary>
internal static class Assets
{
    private static Sprite _logo, _logoBlack;

    /// <summary>The colored Archipelago logo, used for items that belong to other players' games.</summary>
    internal static Sprite Logo => _logo != null ? _logo : _logo = Load("ap-icon.png");

    /// <summary>The black Archipelago logo, shown next to shop entries that hold an Archipelago item.</summary>
    internal static Sprite LogoBlack => _logoBlack != null ? _logoBlack : _logoBlack = Load("ap-icon-black.png");

    /// <summary>The sprite is one world unit wide, so it can be sized by scaling the object it is attached to.</summary>
    private static Sprite Load(string resource)
    {
        // a headless game (automated tests, -nographics) has no graphics device to upload a texture to
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return null;

        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource);
        if (stream == null) return null;
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(memory.ToArray())) return null;
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
    }
}
