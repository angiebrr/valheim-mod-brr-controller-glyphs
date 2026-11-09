// ------------------------------------------------------------------------------------------------
// PngFiles.cs: PNG decode and encode through Unity's ImageConversion, called by reflection (F5).
// ------------------------------------------------------------------------------------------------

using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs
{
    /// <summary>
    /// Unity's ImageConversion module is built against netstandard 2.1, which this net48 plugin can't
    /// reference at compile time (CS1705), so its methods are found by name. Contract tests check
    /// they still exist.
    /// </summary>
    internal static class PngFiles
    {
        /// <summary>Unity's ImageConversion type name.</summary>
        private const string ImageConversion = "UnityEngine.ImageConversion";

        /// <summary>
        /// Decodes a PNG into a texture (resized to the image; stays readable).
        /// </summary>
        /// <param name="texture">The texture to fill.</param>
        /// <param name="png">The PNG file's bytes.</param>
        /// <returns>True if the image was decoded.</returns>
        /// <exception cref="InvalidOperationException">LoadImage isn't there (Unity changed).</exception>
        internal static bool Load(Texture2D texture, byte[] png)
        {
            return (bool)Method("LoadImage", typeof(Texture2D), typeof(byte[])).Invoke(null, new object[] { texture, png });
        }

        /// <summary>
        /// Encodes any texture as a PNG, copying it through a temporary render texture first so
        /// textures that aren't readable (sprite sheets, packed atlases) work too.
        /// </summary>
        /// <param name="source">The texture to save.</param>
        /// <returns>The PNG file's bytes.</returns>
        /// <exception cref="InvalidOperationException">EncodeToPNG isn't there (Unity changed).</exception>
        internal static byte[] Encode(Texture source)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture copy = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, mipChain: false);
            try
            {
                Graphics.Blit(source, copy);
                RenderTexture.active = copy;
                readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                readable.Apply();
                return (byte[])Method("EncodeToPNG", typeof(Texture2D)).Invoke(null, new object[] { readable });
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(copy);
                Object.Destroy(readable);
            }
        }

        /// <summary>
        /// Finds a static ImageConversion method.
        /// </summary>
        /// <param name="name">Method name.</param>
        /// <param name="parameters">Its parameter types.</param>
        /// <returns>The method.</returns>
        /// <exception cref="InvalidOperationException">It isn't there.</exception>
        private static MethodInfo Method(string name, params Type[] parameters)
        {
            return AccessTools.Method(ImageConversion + ":" + name, parameters)
                ?? throw new InvalidOperationException($"{ImageConversion}.{name} not found");
        }
    }
}
