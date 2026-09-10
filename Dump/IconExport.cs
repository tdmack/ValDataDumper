using System;
using System.IO;

using UnityEngine;

namespace ValDataDumper.Dump
{
    /// <summary>
    /// Writes a <see cref="Sprite"/> to a PNG. Icon textures live in non-readable atlases, so
    /// the sprite's texture is blitted to a temporary RenderTexture and its <c>textureRect</c>
    /// read back into a readable Texture2D before encoding.
    /// </summary>
    internal static class IconExport
    {
        public static bool Write(Sprite sprite, string path)
        {
            if (sprite == null || sprite.texture == null) return false;

            var tex = sprite.texture;
            var rect = sprite.textureRect;
            int w = Mathf.Max(1, Mathf.RoundToInt(rect.width));
            int h = Mathf.Max(1, Mathf.RoundToInt(rect.height));

            RenderTexture rt = null;
            Texture2D readable = null;
            var previous = RenderTexture.active;
            try
            {
                rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;

                readable = new Texture2D(w, h, TextureFormat.RGBA32, false);
                // ReadPixels' source rect and Sprite.textureRect share a bottom-left origin.
                readable.ReadPixels(new Rect(rect.x, rect.y, w, h), 0, 0, false);
                readable.Apply(false, false);

                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, ImageConversion.EncodeToPNG(readable));
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"icon export failed for {Path.GetFileName(path)}: {e.Message}");
                return false;
            }
            finally
            {
                RenderTexture.active = previous;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                if (readable != null) UnityEngine.Object.Destroy(readable);
            }
        }
    }
}
