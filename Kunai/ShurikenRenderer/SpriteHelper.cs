using SharpNeedle.Framework.Ninja.Csd;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Numerics;
using Sprite = Shuriken.Rendering.Sprite;
using Texture = Shuriken.Rendering.Texture;
namespace Kunai.ShurikenRenderer
{
    public struct Crop
    {
        public uint TextureIndex;
        public Vector2 TopLeft;
        public Vector2 BottomRight;
    }
    public class UiFont
    {
        public int Id { get; private set; }

        private string _name;
        public string Name
        {
            get => _name;
            set
            {
                if (!string.IsNullOrEmpty(value))
                    _name = value;
            }
        }

        public List<CharacterMapping> Mappings { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public UiFont(string in_Name, int in_Id)
        {
            Id = in_Id;
            Name = in_Name;
            Mappings = new List<CharacterMapping>();
        }
    }
    public class CharacterMapping
    {
        private char _character;
        public char Character
        {
            get => _character;
            set
            {
                if (!string.IsNullOrEmpty(value.ToString()))
                    _character = value;
            }
        }

        public int Sprite { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public CharacterMapping(char in_C, int in_SprId)
        {
            Character = in_C;
            Sprite = in_SprId;
        }

        public CharacterMapping()
        {
            Sprite = -1;
        }
    }
    public static class SpriteHelper
    {
        public static Dictionary<int, Shuriken.Rendering.Sprite> Sprites { get; set; } = new Dictionary<int, Sprite>();
        private static int ms_NextSpriteId = 1;
        private static List<Crop> ms_NcpSubimages = new List<Crop>();
        public static List<Texture> Textures { get; set; } = new List<Texture>();
        public static List<Vector2> TextureSizesOriginal = new List<Vector2>();

        public static void AddTexture(Texture in_Texture)
        {
            var texture2 = new TextureMirage(in_Texture.Name + ".dds");
            KunaiProject.Instance.WorkProjectCsd.Textures.Add(texture2);
            in_Texture.Sprites.Add(CreateSprite(in_Texture));
            Textures.Add(in_Texture);
            TextureSizesOriginal.Add(in_Texture.Size / new Vector2(1280, 720));
        }
        public static void BuildCropList(ref List<SharpNeedle.Framework.Ninja.Csd.Sprite> in_SubImages, ref List<Vector2> in_TextureSizes)
        {
            in_SubImages = new();

            in_TextureSizes = TextureSizesOriginal;
            if (in_TextureSizes.Count < Textures.Count)
            {
                for (int i = 0; i < Textures.Count - in_TextureSizes.Count; i++)
                {
                    in_TextureSizes.Add(Textures[i].Size / KunaiProject.Instance.ViewportSize);
                }

            }
            foreach (var entry in Sprites)
            {
                Shuriken.Rendering.Sprite sprite = entry.Value;
                int textureIndex = Textures.IndexOf(sprite.Texture);
                if (sprite.Crop != null)
                {
                    SharpNeedle.Framework.Ninja.Csd.Sprite subImage = new();
                    subImage.TextureIndex = textureIndex;
                    subImage.TopLeft = new Vector2((float)sprite.X / sprite.Texture.Width, (float)sprite.Y / sprite.Texture.Height);
                    subImage.BottomRight = new Vector2((float)(sprite.X + sprite.Width) / sprite.Texture.Width, (float)(sprite.Y + sprite.Height) / sprite.Texture.Height);
                    in_SubImages.Add(subImage);
                }
                else
                {
                    var size = in_TextureSizes[textureIndex] * KunaiProject.Instance.ViewportSize;
                    sprite.GenerateCoordinates(size);
                    SharpNeedle.Framework.Ninja.Csd.Sprite subImage = new();
                    subImage.TextureIndex = textureIndex;
                    subImage.TopLeft = new Vector2((float)sprite.X / size.X, (float)sprite.Y / size.Y);
                    subImage.BottomRight = new Vector2((float)(sprite.X + sprite.Width) / size.X, (float)(sprite.Y + sprite.Height) / size.Y);
                    in_SubImages.Add(subImage);
                }
            }

        }
        public static Sprite TryGetSprite(int in_Id)
        {
            Sprites.TryGetValue(in_Id + 1, out Sprite sprite);
            return sprite;
        }
        public static int AppendSprite(Sprite in_Spr)
        {
            Sprites.Add(ms_NextSpriteId, in_Spr);
            return ms_NextSpriteId++;
        }
        public static int CreateSprite(Texture in_Tex, float in_Top = 0.0f, float in_Left = 0.0f, float in_Bottom = 1.0f, float in_Right = 1.0f)
        {
            Sprite spr = new Sprite(in_Tex, in_Top, in_Left, in_Bottom, in_Right);
            return AppendSprite(spr);
        }
        public static void DeleteSprite(int in_SprIndex)
        {
            Sprites.Remove(in_SprIndex);
            ms_NextSpriteId--;
        }
        public static int CreateSprite(Texture in_Tex, Vector2 start, Vector2 dimensions)
        {
            float in_Left = MathF.Max(0.0f, MathF.Min(1.0f, start.X));
            float in_Top = MathF.Max(0.0f, MathF.Min(1.0f, start.Y));

            float in_Right = MathF.Max(0.0f, MathF.Min(1.0f, start.X + dimensions.X));
            float in_Bottom = MathF.Max(0.0f, MathF.Min(1.0f, start.Y + dimensions.Y));
            return CreateSprite(in_Tex, in_Top, in_Left, in_Bottom, in_Right);
        }

        public static void RecurFindFirstTextureListFromFile(SceneNode in_Node)
        {
            foreach (var s in in_Node.Scenes)
            {
                if (s.Value.Textures.Count != 0)
                {
                    TextureSizesOriginal = s.Value.Textures;
                    return;
                }
            }
            foreach (var n in in_Node.Children)
            {
                RecurFindFirstTextureListFromFile(n.Value);
            }
        }
        public static void LoadTextures(CsdProject in_CsdProject)
        {
            RecurFindFirstTextureListFromFile(in_CsdProject.Project.Root);
            GetSubImages(in_CsdProject.Project.Root);
            LoadSubimages(ms_NcpSubimages);
        }
        public static void GetSubImages(SharpNeedle.Framework.Ninja.Csd.SceneNode in_Node)
        {
            foreach (var scene in in_Node.Scenes)
            {
                if (ms_NcpSubimages.Count > 0)
                    return;


                foreach (var item in scene.Value.Sprites)
                {
                    var i = new Crop();
                    i.TextureIndex = (uint)item.TextureIndex;
                    i.TopLeft = item.TopLeft;
                    i.BottomRight = item.BottomRight;
                    ms_NcpSubimages.Add(i);
                }
            }

            foreach (KeyValuePair<string, SceneNode> child in in_Node.Children)
            {
                if (ms_NcpSubimages.Count > 0)
                    return;

                GetSubImages(child.Value);
            }
        }
        private static void LoadSubimages(List<Crop> in_Subimages)
        {
            foreach (var image in in_Subimages)
            {
                int textureIndex = (int)image.TextureIndex;
                if (textureIndex >= 0 && textureIndex < Textures.Count)
                {
                    int id = CreateSprite(Textures[textureIndex], image.TopLeft.Y, image.TopLeft.X,
                        image.BottomRight.Y, image.BottomRight.X);

                    Textures[textureIndex].Sprites.Add(id);
                }
            }
        }

        internal static void ClearTextures()
        {
            if (Textures == null)
            {
                Textures = new List<Texture>();
            }
            foreach (var tex in Textures)
            {
                tex.Destroy();
            }
            Textures.Clear();
            ms_NcpSubimages.Clear();
            Sprites.Clear();
            ms_NextSpriteId = 1;
        }

        internal static bool DoesTextureExist(string in_Path)
        {
            string filename = Path.GetFileNameWithoutExtension(in_Path);
            foreach (var t in Textures)
            {
                if (t.Name == filename)
                    return true;
            }
            return false;
        }
    }
}
