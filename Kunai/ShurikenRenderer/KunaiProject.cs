using Amicitia.IO.Binary;
using ColoursXncpGen;
using Hexa.NET.ImGui;
using Hexa.NET.ImGuizmo;
using Kunai.Window;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Desktop;
using SharpNeedle;
using SharpNeedle.Ninja.Csd;
using SharpNeedle.Ninja.Csd.Motions;
using SharpNeedle.Utilities;
using Shuriken.Models;
using Shuriken.Rendering;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Font = SharpNeedle.Ninja.Csd.Font;
using PixelFormat = OpenTK.Graphics.OpenGL.PixelFormat;

namespace Kunai.ShurikenRenderer
{
    
    public class KunaiProject
    {
        public static KunaiProject Instance;
        public struct SViewportData
        {
            public int CsdRenderTextureHandle;
            public OpenTK.Mathematics.Vector2i FramebufferSize;
            public int RenderbufferHandle;
            public int FramebufferHandle;
        }
        public struct SProjectConfig
        {
            public string WorkFilePath;
            public bool PlayingAnimations;
            public bool ShowQuads;
            public double Time;
        }
        public struct SSelectionData
        {
            public KeyFrameList TrackAnimation;
            public KeyFrame KeyframeSelected;
            public Cast SelectedCast;
            public KeyValuePair<string, Scene> SelectedScene;
        }
        public Renderer Renderer;
        public Vector2 ViewportSize;
        public Vector2 ScreenSize;
        public SVisibilityData VisibilityData;
        public SSelectionData SelectionData;
        public CsdProject WorkProjectCsd;
        public SProjectConfig Config;
        private SViewportData _viewportData;
        private GameWindow _window;
        private int _currentDrawPriority;
        public List<WindowBase> Windows = new List<WindowBase>();
        bool saveScreenshotWhenRendered = false;
        public KunaiProject(GameWindow in_Window2, Vector2 in_ViewportSize, Vector2 in_ClientSize)
        {
            if (Instance != null)
                return;
            ViewportSize = in_ViewportSize;
            Renderer = new Renderer((int)ViewportSize.X, (int)ViewportSize.Y);
            Renderer.SetShader(Renderer.ShaderDictionary["basic"]);
            _window = in_Window2;
            _viewportData = new SViewportData();
            Config = new SProjectConfig();
            ScreenSize = in_ClientSize;
            Instance = this;
        }

        public void ShowMessageBoxCross(string in_Title, string in_Message, bool in_IsWarning)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                System.Windows.MessageBox.Show(in_Message, in_Title, System.Windows.MessageBoxButton.OK, in_IsWarning ? System.Windows.MessageBoxImage.Warning : System.Windows.MessageBoxImage.Information);
            }
        }
        public void LoadFile(string in_Path)
        {
            /// Colors and Colors Ultimate have a unique situation where
            /// they have texture lists as files instead of being combined
            /// as is the case literally everywhere else!
            /// 
            /// There's probably a better way to detect if a tls or dxl file exists, but 
            /// this should work.
            bool isTlsFilePresent = File.Exists(Path.ChangeExtension(in_Path, "tls"));
            bool isDxlFilePresent = File.Exists(Path.ChangeExtension(in_Path, "dxl"));

            if(isTlsFilePresent || isDxlFilePresent)
            {
                byte[] file = File.ReadAllBytes(in_Path);
                byte[] textureList = File.ReadAllBytes(Path.ChangeExtension(in_Path, isTlsFilePresent ? "tls" : "dxl"));
                byte[] output = FileManager.Combine(file, textureList);
                MemoryStream stream = new MemoryStream(output);
                BinaryObjectReader reader = new BinaryObjectReader(stream,Amicitia.IO.Streams.StreamOwnership.Retain, Endianness.Little);
                File.WriteAllBytes(in_Path + "_Test", output);
                WorkProjectCsd = ResourceUtility.Open<CsdProject>(@in_Path + "_Test");
                Console.WriteLine("");
            }
            else
            {
                //try
                //{
                WorkProjectCsd = ResourceUtility.Open<CsdProject>(@in_Path);
                //}
                //catch (Exception ex)
                //{
                //    //Implement cross platform messagebox
                //    ShowMessageBoxCross("Error", ex.Message, true);
                //    return;
                //}
            }

            //Reset what needs to be reset
            string root = Path.GetDirectoryName(Path.GetFullPath(@in_Path));
            Config.WorkFilePath = in_Path;
            VisibilityData = null;
            InspectorWindow.Reset();
            SpriteHelper.ClearTextures();

            /// TODO: SHARPNEEDLE FIX
           // ExtensionKillMe.IsColorLittleEndian = WorkProjectCsd.Endianness == Endianness.Little;

            //Start loading textures
            ITextureList xTextures = WorkProjectCsd.Textures;
            CsdDictionary<Font> xFontList = WorkProjectCsd.Project.Fonts;
            List<string> missingTextures = new List<string>();
            SpriteHelper.TextureList = new TextureList("textures");
            if (xTextures != null)
            {
                bool tempChangeExtension = false;
                string t = Path.GetExtension(xTextures[0].Name).ToLower();
                if (t != ".dds")
                {
                    //MessageBox.Show("This tool is not capable of loading non-dds images yet, convert them to dds manually to make them show up in the tool.", "", MessageBoxButton.OK, MessageBoxImage.Warning);
                    tempChangeExtension = true;
                }
                foreach (ITexture texture in xTextures)
                {
                    string texPath = System.IO.Path.Combine(@root, texture.Name);

                    if (File.Exists(texPath))
                        SpriteHelper.TextureList.Textures.Add(new Texture(texPath, tempChangeExtension));
                    else
                    {
                        SpriteHelper.TextureList.Textures.Add(new Texture("", tempChangeExtension));
                        missingTextures.Add(texture.Name);
                    }
                    //    MissingTextures.Add(texture.Name);
                }
                if (missingTextures.Count > 0)
                {
                    string textureNames = "";
                    foreach (string textureName in missingTextures)
                        textureNames += "-" + textureName + "\n";
                    ShowMessageBoxCross("Warning", $"The file uses textures that could not be found, they will be replaced with squares.\n\nMissing Textures:\n{textureNames}", true);
                }
            }
            SpriteHelper.LoadTextures(WorkProjectCsd);
            VisibilityData = new SVisibilityData(WorkProjectCsd);
        }
        /// <summary>
        /// Renders contents of a CsdProject to a GL texture for use in ImGui
        /// </summary>
        /// <param name="in_CsdProject"></param>
        /// <param name="in_DeltaTime"></param>
        /// <exception cref="Exception"></exception>
        public void Render(CsdProject in_CsdProject, float in_DeltaTime)
        {
            if (in_CsdProject != null)
            {
                bool isSavingScreenshot = saveScreenshotWhenRendered;
                // Get the size of the child (i.e. the whole draw size of the windows).
                System.Numerics.Vector2 wsize = ScreenSize;

                // make sure the buffers are the currect size
                OpenTK.Mathematics.Vector2i wsizei = new((int)wsize.X, (int)wsize.Y);
                if (_viewportData.FramebufferSize != wsizei)
                {
                    _viewportData.FramebufferSize = wsizei;

                    // create our frame buffer if needed
                    if (_viewportData.FramebufferHandle == 0)
                    {
                        _viewportData.FramebufferHandle = GL.GenFramebuffer();
                        // bind our frame buffer
                        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _viewportData.FramebufferHandle);
                        GL.ObjectLabel(ObjectLabelIdentifier.Framebuffer, _viewportData.FramebufferHandle, 10, "GameWindow");
                    }

                    // bind our frame buffer
                    GL.BindFramebuffer(FramebufferTarget.Framebuffer, _viewportData.FramebufferHandle);

                    if (_viewportData.CsdRenderTextureHandle > 0)
                        GL.DeleteTexture(_viewportData.CsdRenderTextureHandle);

                    _viewportData.CsdRenderTextureHandle = GL.GenTexture();
                    GL.BindTexture(TextureTarget.Texture2D, _viewportData.CsdRenderTextureHandle);
                    GL.ObjectLabel(ObjectLabelIdentifier.Texture, _viewportData.CsdRenderTextureHandle, 16, "GameWindow:Color");
                    //IMPORTANT!
                    //Rgba for screenshots, rgb for everything else
                    var pixelInternalFormat = isSavingScreenshot ? PixelInternalFormat.Rgba : PixelInternalFormat.Rgba;
                    var pixelFormat = isSavingScreenshot ? PixelFormat.Rgba : PixelFormat.Rgba;
                    GL.TexImage2D(TextureTarget.Texture2D, 0, pixelInternalFormat, wsizei.X, wsizei.Y, 0, pixelFormat, PixelType.UnsignedByte, IntPtr.Zero);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                    GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _viewportData.CsdRenderTextureHandle, 0);

                    if (_viewportData.RenderbufferHandle > 0)
                        GL.DeleteRenderbuffer(_viewportData.RenderbufferHandle);

                    _viewportData.RenderbufferHandle = GL.GenRenderbuffer();
                    GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _viewportData.RenderbufferHandle);
                    GL.ObjectLabel(ObjectLabelIdentifier.Renderbuffer, _viewportData.RenderbufferHandle, 16, "GameWindow:Depth");
                    GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent32f, wsizei.X, wsizei.Y);

                    GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, _viewportData.RenderbufferHandle);
                    //GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);

                    //texDepth = GL.GenTexture();
                    //GL.BindTexture(TextureTarget.Texture2D, texDepth);
                    //GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent32f, 800, 600, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
                    //GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, texDepth, 0);

                    // make sure the frame buffer is complete
                    FramebufferErrorCode errorCode = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
                    if (errorCode != FramebufferErrorCode.FramebufferComplete)
                        throw new Exception();
                }
                else
                {
                    // bind our frame and depth buffer
                    GL.BindFramebuffer(FramebufferTarget.Framebuffer, _viewportData.FramebufferHandle);
                    GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _viewportData.RenderbufferHandle);
                }
                GL.Viewport(0, 0, wsizei.X, wsizei.Y); // change the viewport to window
                // actually draw the scene
                {
                    RenderToViewport(in_CsdProject, in_DeltaTime, true);
                    

                    if (isSavingScreenshot)
                    {
                        GL.Disable(EnableCap.Blend);

                        //Save framebuffer to a pixel buffer
                        byte[] buffer = new byte[wsizei.X * wsizei.Y * 4];
                        GL.ReadPixels(0, 0, wsizei.X, wsizei.Y, PixelFormat.Rgba, PixelType.UnsignedByte, buffer);

                        Image<SixLabors.ImageSharp.PixelFormats.Rgba32> screenshot =
                            Image.LoadPixelData<SixLabors.ImageSharp.PixelFormats.Rgba32>(buffer, wsizei.X, wsizei.Y);

                        //Flip vertically to fix orientation
                        screenshot.Mutate(x => x.Flip(FlipMode.Vertical));

                        var fileDialog = NativeFileDialogSharp.Dialog.FileSave("png");
                        if (fileDialog.IsOk)
                        {
                            string path = fileDialog.Path;
                            if (!Path.HasExtension(path))
                                path += ".png";
                            screenshot.SaveAsPng(path);
                        }

                        saveScreenshotWhenRendered = false;
                    }
                }

               
                // unbind our bo so nothing else uses it
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                GL.Viewport(0, 0, _window.ClientSize.X, _window.ClientSize.Y); // back to full screen size
            }
            UpdateWindows();
        }
        private void RenderToViewport(CsdProject in_CsdProject, float in_DeltaTime, bool in_ScreenshotMode)
        {            
            GL.ClearColor(in_ScreenshotMode ? new OpenTK.Mathematics.Color4(255,255,255,1) : OpenTK.Mathematics.Color4.DarkGray);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            Renderer.Width = (int)ViewportSize.X;
            Renderer.Height = (int)ViewportSize.Y;
            Renderer.Start();
            if (Config.PlayingAnimations)
                Config.Time += in_DeltaTime;
            RenderNode(in_CsdProject.Project.Root, Config.Time);
            foreach (KeyValuePair<string, SceneNode> node in in_CsdProject.Project.Root.Children)
            {
                if (!VisibilityData.GetVisibility(node.Value).Active) continue;
                RenderNode(node.Value, Config.Time);
            }
            //if (ImGui.Begin("teatatat"))
            //{
            //    foreach (var f in renderer.GetQuads())
            //    {
            //
            //        var tl = new Vector2(size.X * f.TopLeft.UV.X, size.Y * f.TopLeft.UV.Y);
            //        var br = new Vector2(size.X * f.BottomRight.UV.X, size.Y * f.BottomRight.UV.Y);
            //        ImGui.GetWindowDrawList().AddLine(tl, br, ImGui.GetColorU32(new System.Numerics.Vector4(255, 255, 255, 255)), 2);
            //
            //
            //
            //        var tr = new Vector2(size.X * f.TopRight.UV.X, size.Y * f.TopRight.UV.Y);
            //        var bl = new Vector2(size.X * f.BottomLeft.UV.X, size.Y * f.BottomLeft.UV.Y);
            //        ImGui.GetWindowDrawList().AddLine(tr, bl, ImGui.GetColorU32(new System.Numerics.Vector4(255, 255, 255, 255)), 2);
            //    }
            //    ImGui.End();
            //}
            Renderer.End();
        }

        public void RenderNode(SceneNode in_Node, double in_DeltaTime)
        {
            SVisibilityData.SNode vis = VisibilityData.GetVisibility(in_Node);
            int idx = 0;
            foreach (var scene in in_Node.Scenes)
            {
                if (!vis.GetVisibility(scene.Value).Active) continue;
                RenderScenes(scene.Value, vis, ref idx, in_DeltaTime);
                // = true;
            }
        }
        public void RenderScenes(Scene in_Scene, SVisibilityData.SNode in_Vis, ref int in_Priority, double in_DeltaTime)
        {
            int idx = in_Priority;
            var vis = in_Vis.GetVisibility(in_Scene);
            foreach (var family in in_Scene.Families)
            {
                var transform = new CastTransform();
                transform.Color = new System.Numerics.Vector4(1, 1, 1, 1);
                if (family.Casts.Count == 0)
                    continue;
                Cast cast = family.Casts[0];

                UpdateCast(in_Scene, cast, transform, idx, (float)(in_DeltaTime * in_Scene.FrameRate), vis);
                idx += cast.Children.Count + 1;
            }
            in_Priority = idx++;
        }
        private void UpdateCast(Scene in_Scene, Cast in_UiElement, CastTransform in_Transform, int in_Priority, float in_Time, SVisibilityData.SScene in_Vis)
        {
            bool hideFlag = in_UiElement.Info.HideFlag != 0;
            System.Numerics.Vector2 position = new System.Numerics.Vector2(in_UiElement.Info.Translation.X, in_UiElement.Info.Translation.Y);
            float rotation = in_UiElement.Info.Rotation;
            var scale = new System.Numerics.Vector2(in_UiElement.Info.Scale.X, in_UiElement.Info.Scale.Y);
            float sprId = in_UiElement.Info.SpriteIndex;
            var color = in_UiElement.Info.Color.ToVec4();
            var gradientTopLeft = in_UiElement.Info.GradientTopLeft.ToVec4();
            var gradientBottomLeft = in_UiElement.Info.GradientBottomLeft.ToVec4();
            var gradientTopRight = in_UiElement.Info.GradientTopRight.ToVec4();
            var gradientBottomRight = in_UiElement.Info.GradientBottomRight.ToVec4();

            foreach (var animation in in_Vis.Animation.Where(a => a.Active))
            {
                for (int i = 0; i < 12; i++)
                {
                    var animationType = (AnimationType)(1 << i);
                    var track = animation.GetTrack(in_UiElement, animationType);

                    if (track == null)
                        continue;

                    switch (animationType)
                    {
                        case AnimationType.HideFlag:
                            hideFlag = track.GetSingle(in_Time) != 0;
                            break;

                        case AnimationType.XPosition:
                            position.X = track.GetSingle(in_Time);
                            break;

                        case AnimationType.YPosition:
                            position.Y = track.GetSingle(in_Time);
                            break;

                        case AnimationType.Rotation:
                            rotation = track.GetSingle(in_Time);
                            break;

                        case AnimationType.XScale:
                            scale.X = track.GetSingle(in_Time);
                            break;

                        case AnimationType.YScale:
                            scale.Y = track.GetSingle(in_Time);
                            break;

                        case AnimationType.SubImage:
                            sprId = track.GetSingle(in_Time);
                            break;

                        case AnimationType.Color:
                            color = track.GetColor(in_Time);
                            break;

                        case AnimationType.GradientTl:
                            gradientTopLeft = track.GetColor(in_Time);
                            break;

                        case AnimationType.GradientBl:
                            gradientBottomLeft = track.GetColor(in_Time);
                            break;

                        case AnimationType.GradientTr:
                            gradientTopRight = track.GetColor(in_Time);
                            break;

                        case AnimationType.GradientBr:
                            gradientBottomRight = track.GetColor(in_Time);
                            break;
                    }
                }
            }

            if (hideFlag)
                return;

            var visibilityDataCast = in_Vis.GetVisibility(in_UiElement);
            if (visibilityDataCast == null)
            {
                Console.WriteLine("CRITICAL ERROR! Missing visibility for cast!!! Please fix!");
                return;
            }
            // Inherit position scale
            // TODO: Is this handled through flags?
            position.X *= in_Transform.Scale.X;
            position.Y *= in_Transform.Scale.Y;

            // Rotate through parent transform
            float angle = in_Transform.Rotation * MathF.PI / 180.0f; //to radians
            float rotatedX = position.X * MathF.Cos(angle) * in_Scene.AspectRatio + position.Y * MathF.Sin(angle);
            float rotatedY = position.Y * MathF.Cos(angle) - position.X * MathF.Sin(angle) * in_Scene.AspectRatio;

            position.X = rotatedX / in_Scene.AspectRatio;
            position.Y = rotatedY;

            position += in_UiElement.Origin;
            var inheritanceFlags = (ElementInheritanceFlags)in_UiElement.InheritanceFlags.Value;
            // Inherit position
            if (inheritanceFlags.HasFlag(ElementInheritanceFlags.InheritXPosition))
                position.X += in_Transform.Position.X;

            if (inheritanceFlags.HasFlag(ElementInheritanceFlags.InheritYPosition))
                position.Y += in_Transform.Position.Y;

            // Inherit rotation
            if (inheritanceFlags.HasFlag(ElementInheritanceFlags.InheritRotation))
                rotation += in_Transform.Rotation;

            // Inherit scale
            if (inheritanceFlags.HasFlag(ElementInheritanceFlags.InheritScaleX))
                scale.X *= in_Transform.Scale.X;

            if (inheritanceFlags.HasFlag(ElementInheritanceFlags.InheritScaleY))
                scale.Y *= in_Transform.Scale.Y;

            // Inherit color
            if (inheritanceFlags.HasFlag(ElementInheritanceFlags.InheritColor))
            {
                var cF = color * in_Transform.Color;
                color = cF;
            }
            var type = (DrawType)in_UiElement.Field04;
            var flags = (ElementMaterialFlags)in_UiElement.Field38;

            if (visibilityDataCast.Active && in_UiElement.Enabled)
            {
                if (type == DrawType.Sprite)
                {
                    int spriteIdx1 = Math.Min(in_UiElement.SpriteIndices.Length - 1, (int)sprId);
                    int spriteIdx2 = Math.Min(in_UiElement.SpriteIndices.Length - 1, (int)sprId + 1);
                    Shuriken.Rendering.Sprite spr = sprId >= 0 ? SpriteHelper.TryGetSprite(in_UiElement.SpriteIndices[spriteIdx1]) : null;
                    Shuriken.Rendering.Sprite nextSpr = sprId >= 0 ? SpriteHelper.TryGetSprite(in_UiElement.SpriteIndices[spriteIdx2]) : null;
                    if (Config.ShowQuads)
                    {
                        spr = null;
                        nextSpr = null;
                    }
                    spr ??= nextSpr;
                    nextSpr ??= spr;
                    Renderer.DrawSprite(
                        in_UiElement.TopLeft, in_UiElement.BottomLeft, in_UiElement.TopRight, in_UiElement.BottomRight,
                        position, rotation * MathF.PI / 180.0f, scale, in_Scene.AspectRatio, spr, nextSpr, sprId % 1, color,
                        gradientTopLeft, gradientBottomLeft, gradientTopRight, gradientBottomRight,
                        (int)in_Scene.Priority + in_UiElement.Priority, flags);
                }
                else if (type == DrawType.Font)
                {
                    float xOffset = 0.0f;
                    if (string.IsNullOrEmpty(in_UiElement.Text)) in_UiElement.Text = "";
                    foreach (char character in in_UiElement.Text)
                    {

                        var font = WorkProjectCsd.Project.Fonts[in_UiElement.FontName];
                        if (font == null)
                            continue;

                        Shuriken.Rendering.Sprite spr = null;

                        foreach (var mapping in font)
                        {
                            if (mapping.SourceIndex != character)
                                continue;

                            spr = SpriteHelper.TryGetSprite(mapping.DestinationIndex);
                            break;
                        }

                        if (spr == null)
                            continue;

                        float width = spr.Dimensions.X / Renderer.Width;
                        float height = spr.Dimensions.Y / Renderer.Height;

                        var begin = (Vector2)in_UiElement.TopLeft;
                        var end = begin + new Vector2(width, height);

                        Renderer.DrawSprite(
                            new Vector2(begin.X + xOffset, begin.Y),
                            new Vector2(begin.X + xOffset, end.Y),
                            new Vector2(end.X + xOffset, begin.Y),
                            new Vector2(end.X + xOffset, end.Y),
                             position, rotation * MathF.PI / 180.0f, scale, in_Scene.AspectRatio, spr, spr, 0, color,
                        gradientTopLeft, gradientBottomLeft, gradientTopRight, gradientBottomRight,
                        in_UiElement.Priority, flags
                        );
                        //in_UiElement.Field4C = kerning (space between letters)
                        xOffset += width + BitConverter.ToSingle(BitConverter.GetBytes(in_UiElement.Field4C));
                    }
                }

                var childTransform = new CastTransform(position, rotation, scale, color);

                foreach (var child in in_UiElement.Children)
                    UpdateCast(in_Scene, child, childTransform, in_Priority++, in_Time, in_Vis);
            }

        }

        public int GetViewportImageHandle()
        {
            return _viewportData.CsdRenderTextureHandle;
        }
        void RecursiveSetCropListNode(SceneNode in_Node, List<SharpNeedle.Ninja.Csd.Sprite> in_Sprites, List<Vector2> in_TexSizes)
        {
            foreach(var s in in_Node.Scenes)
            {
                s.Value.Sprites = in_Sprites;
                s.Value.Textures = in_TexSizes;
            }
            foreach(var c in in_Node.Children)
            {
                RecursiveSetCropListNode(c.Value, in_Sprites, in_TexSizes);
            }
        }
        public void SaveCurrentFile(string in_Path)
        {
            List<SharpNeedle.Ninja.Csd.Sprite> subImageList = new();
            List<Vector2> sizes = new List<Vector2>();
            //SpriteHelper.BuildCropList(ref subImageList, ref sizes);
            //RecursiveSetCropListNode(WorkProjectCsd.Project.Root, subImageList, sizes);
            WorkProjectCsd.Write(in_Path == null ? Config.WorkFilePath : in_Path, false);
        }

        internal void UpdateWindows()
        {
            foreach (WindowBase window in Windows)
            {
                window.Renderer = this;
                window.Update(this);
            }
        }

        internal void SaveScreenshot()
        {
            saveScreenshotWhenRendered = true;
        }
    }
}
