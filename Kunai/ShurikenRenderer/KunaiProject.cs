using Amicitia.IO.Binary;
using BCnEncoder.Encoder;
using BCnEncoder.ImageSharp;
using BCnEncoder.Shared;
using ColoursXncpGen;
using HekonrayBase;
using HekonrayBase.Base;
using Kunai.Window;
using libWiiSharp;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Desktop;
using SharpNeedle.Framework.Ninja;
using SharpNeedle.Framework.Ninja.Csd;
using SharpNeedle.Framework.Ninja.Csd.Motions;
using SharpNeedle.IO;
using SharpNeedle.Resource;
using SharpNeedle.Utilities;
using Shuriken.Models;
using Shuriken.Rendering;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using TeamSpettro.SettingsSystem;
using PixelFormat = OpenTK.Graphics.OpenGL.PixelFormat;

namespace Kunai.ShurikenRenderer
{

    public class KunaiProject : Singleton<KunaiProject>, IProgramProject
    {
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
            public int Frame;
        }
        public struct SSelectionData
        {
            public KeyFrameList TrackAnimation;
            public KeyFrame KeyframeSelected;
            public Cast SelectedCast;
            public Motion SelectedMotion;
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
        bool m_SaveScreenshotWhenRendered = false;
        public Vector3 ViewportColor
        {
            get
            {
                var x = SettingsManager.GetFloat("ViewColor_X", 0.6627450980f);
                var y = SettingsManager.GetFloat("ViewColor_Y", 0.6627450980f);
                var z = SettingsManager.GetFloat("ViewColor_Z", 0.66274509803f);
                return new Vector3(x, y, z);
            }
            set
            {

                SettingsManager.SetFloat("ViewColor_X", value.X);
                SettingsManager.SetFloat("ViewColor_Y", value.Y);
                SettingsManager.SetFloat("ViewColor_Z", value.Z);
            }
        }

        public KunaiProject()
        {
            ViewportSize = new Vector2(1280, 720);
            Renderer = new Renderer((int)ViewportSize.X, (int)ViewportSize.Y);
            Renderer.SetShader(Renderer.ShaderDictionary["basic"]);
            _viewportData = new SViewportData();
            Config = new SProjectConfig();
        }
        public void SetWindowParameters(GameWindow in_Window2, Vector2 in_ClientSize)
        {
            ScreenSize = in_ClientSize;
            _window = in_Window2;

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
            try
            {
                bool isSplitFile = IsPathSplitFile(in_Path);
                //Reset what needs to be reset
                string root = Path.GetDirectoryName(Path.GetFullPath(@in_Path));
                SpriteHelper.ClearTextures();
                Config.WorkFilePath = in_Path;
                SelectionData.SelectedCast = null;
                SelectionData.SelectedScene = new();
                WorkProjectCsd = null;
                VisibilityData = null;
                Renderer.Quads.Clear();

                if (isSplitFile)
                {
                    // There's probably a better way to detect if a tls or dxl file exists, but 
                    // this should work.
                    bool isTlsFilePresent = File.Exists(Path.ChangeExtension(in_Path, "tls"));
                    bool isDxlFilePresent = File.Exists(Path.ChangeExtension(in_Path, "dxl"));

                    if (isTlsFilePresent || isDxlFilePresent)
                    {
                        // Colors and Colors Ultimate have a unique situation where
                        // they have texture lists as tls (wii format) and dxl (literally just TextureList)
                        // instead of being combined into the file with the project
                        // as is the case literally everywhere else except for shadow!                    
                        HandleSplitCsdColors(in_Path, isDxlFilePresent, isTlsFilePresent);
                    }
                    else
                    {
                        //File probably uses TXD or has no file asssociated with it, try to continue anyway.
                        WorkProjectCsd = new CsdProject();
                        WorkProjectCsd.Name = Path.GetFileName(in_Path);
                        WorkProjectCsd.Project = GetProjectChunkSplit(in_Path, Endianness.Big);
                        WorkProjectCsd.Textures = new TextureListNN();
                        Application.ShowMessageBoxCross("Warning", "This file is split, but the program does not know where the textures are.\nThis file will be displayed with no textures.", 1);
                    }
                }
                else
                {
                    WorkProjectCsd = ResourceUtility.Open<CsdProject>(@in_Path);
                }


                //Start loading textures
                var csdTextureList = WorkProjectCsd.Textures;
                var csdFontList = WorkProjectCsd.Project.Fonts;
                List<string> missingTextures = new List<string>();

                if (csdTextureList != null)
                {
                    foreach (ITexture texture in csdTextureList)
                    {
                        string texPath = Path.Combine(@root, texture.Name);

                        SpriteHelper.Textures.Add(new Texture(texPath));
                        if (!File.Exists(texPath))
                        {
                            missingTextures.Add(texture.Name);
                        }
                    }
                    if (missingTextures.Count > 0)
                    {
                        string textureNames = "";
                        foreach (string textureName in missingTextures)
                            textureNames += "-" + textureName + "\n";
                        Application.ShowMessageBoxCross("Warning", $"The file uses textures that could not be found, they will be replaced with squares.\n\nMissing Textures:\n{textureNames}", 1);
                    }
                }
                SpriteHelper.LoadTextures(WorkProjectCsd);
                VisibilityData = new SVisibilityData(WorkProjectCsd);

            }
            catch (Exception ex)
            {
#if !DEBUG
                Application.ShowMessageBoxCross("Error", $"An error occured whilst trying to load a file.\n{ex.Message}", 2);

#else
                throw;
#endif
            }

        }

        /// <summary>
        /// Checks if the header of the file starts with FAPC/CPAF or not.
        /// </summary>
        /// <param name="in_Path">Path to file</param>
        /// <returns></returns>
        private bool IsPathSplitFile(string in_Path)
        {
            BinaryObjectReader reader = new BinaryObjectReader(in_Path, Endianness.Little, Encoding.UTF8);
            uint sig = reader.ReadNative<uint>();
            reader.Dispose();
            return sig != BinaryPrimitives.ReverseEndianness(CsdPackage.Signature) && sig != CsdPackage.Signature;
        }
        private ProjectChunk GetProjectChunkSplit(string in_Path, Endianness in_Endianness)
        {
            using var reader = new BinaryObjectReader(@in_Path, in_Endianness, Encoding.UTF8);
            var infoChunk = reader.ReadObject<InfoChunk>();
            foreach (IChunk chunk in infoChunk.Chunks)
            {
                switch (chunk)
                {
                    case ProjectChunk project:
                        return project;
                }
            }
            return null;
        }

        private void HandleSplitCsdColors(string in_Path, bool in_IsDxlFilePresent, bool in_IsTlsFilePresent)
        {
            string pathExtra = in_IsDxlFilePresent ? Path.ChangeExtension(in_Path, "dxl") : Path.ChangeExtension(in_Path, "tls");
            byte[] csdFile = File.ReadAllBytes(in_Path);
            byte[] textureList = File.ReadAllBytes(pathExtra);

            //File.WriteAllBytes(in_Path + "_Test", output);

            if (in_IsTlsFilePresent)
            {
                var tlsFile = TPL.Load(pathExtra);
                TextureListNN newTexList = new TextureListNN();
                string csdName = Path.GetFileNameWithoutExtension(in_Path);
                string parentDir = Directory.GetParent(in_Path).FullName;

                bool showWarning = false;
                for (var i = 0; i < tlsFile.NumOfTextures; i++)
                {
                    string filePath = Path.Combine(parentDir, $"{csdName}_tex{i}.dds");
                    if (!File.Exists(filePath))
                    {
                        if (!showWarning)
                        {
                            showWarning = true;
                        }

                        var image = tlsFile.ExtractTextureBytes(i);

                        using Image<Bgra32> newDds = Image.LoadPixelData<Bgra32>(image, tlsFile.GetTexture(i).TextureWidth, tlsFile.GetTexture(i).TextureHeight);

                        BcEncoder encoder = new BcEncoder();

                        encoder.OutputOptions.GenerateMipMaps = true;
                        encoder.OutputOptions.Quality = CompressionQuality.BestQuality;
                        encoder.OutputOptions.Format = CompressionFormat.Bc3;
                        encoder.OutputOptions.FileFormat = OutputFileFormat.Dds; //Change to Dds for a dds file.

                        using FileStream fs = File.OpenWrite(filePath);
                        encoder.EncodeToStream(newDds.CloneAs<Rgba32>(), fs);
                    }

                    newTexList.Add(new TextureNN($"{csdName}_tex{i}.dds"));
                }

                WorkProjectCsd = new CsdProject();
                WorkProjectCsd.Project = GetProjectChunkSplit(in_Path, Endianness.Big);
                WorkProjectCsd.Textures = newTexList;
            }
            if (in_IsDxlFilePresent)
            {
                //Merge both files using the same method as ColoursXncpGen
                byte[] output = FileManager.Combine(csdFile, textureList);
                using (var memstr = new MemoryStream(output))
                {
                    VirtualFile file = new VirtualFile(Path.GetFileName(in_Path), new VirtualDirectory(Directory.GetParent(in_Path).FullName));
                    file.BaseStream = memstr;
                    WorkProjectCsd = ResourceManager.Instance.Open<CsdProject>(file, true);
                }
            }
        }

        /// <summary>
        /// Renders contents of a CsdProject to a GL texture for use in ImGui
        /// </summary>
        /// <param name="in_CsdProject"></param>
        /// <param name="in_DeltaTime"></param>
        /// <exception cref="Exception"></exception>
        public void Render(CsdProject in_CsdProject, float in_DeltaTime)
        {
            //If one or both of these are 0, it means the application is minimized.
            if (ScreenSize.X == 0 || ScreenSize.Y == 0)
                return;
            if (in_CsdProject != null)
            {
                bool isSavingScreenshot = m_SaveScreenshotWhenRendered;
                // Get the size of the child (i.e. the whole draw size of the windows).
                Vector2 wsize = ScreenSize;
                // make sure the buffers are the currect size
                OpenTK.Mathematics.Vector2i wsizei = new((int)wsize.X, (int)wsize.Y);
                if (_viewportData.FramebufferSize != wsizei || isSavingScreenshot)
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
                    var pixelInternalFormat = isSavingScreenshot ? PixelInternalFormat.Rgba : PixelInternalFormat.Rgb;
                    var pixelFormat = isSavingScreenshot ? PixelFormat.Rgba : PixelFormat.Rgb;
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
                    if (errorCode == FramebufferErrorCode.FramebufferIncompleteAttachment)
                        return;
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
                    GL.Enable(EnableCap.Blend);
                    RenderToViewport(in_CsdProject, in_DeltaTime, isSavingScreenshot);
                }

                if (isSavingScreenshot)
                {
                    //Save framebuffer to a pixel buffer
                    byte[] buffer = new byte[wsizei.X * wsizei.Y * 4];
                    GL.ReadPixels(0, 0, wsizei.X, wsizei.Y, PixelFormat.Rgba, PixelType.UnsignedByte, buffer);

                    Image<SixLabors.ImageSharp.PixelFormats.Rgba32> screenshot =
                        Image.LoadPixelData<SixLabors.ImageSharp.PixelFormats.Rgba32>(buffer, wsizei.X, wsizei.Y);

                    //Flip vertically to fix orientation
                    screenshot.Mutate(in_X => in_X.Flip(FlipMode.Vertical));

                    //screenshot.Mutate(ctx =>
                    //{
                    //    ctx.ProcessPixelRowsAsVector4(rows =>
                    //    {
                    //        for (int y = 0; y < rows.Length; y++)
                    //        {
                    //            rows[y].W = 1.0f - rows[y].W;
                    //        }
                    //    });
                    //});
                    var fileDialog = NativeFileDialogSharp.Dialog.FileSave("png");
                    if (fileDialog.IsOk)
                    {
                        string path = fileDialog.Path;
                        if (!Path.HasExtension(path))
                            path += ".png";
                        screenshot.SaveAsPng(path);
                    }

                    m_SaveScreenshotWhenRendered = false;
                }
                // unbind our bo so nothing else uses it
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                GL.Viewport(0, 0, _window.ClientSize.X, _window.ClientSize.Y); // back to full screen size
            }
            UpdateWindows();
        }
        private void RenderToViewport(CsdProject in_CsdProject, float in_DeltaTime, bool in_ScreenshotMode)
        {
            //eventually set to transparent in case its a screenshot
            GL.ClearColor(ViewportColor.X, ViewportColor.Y, ViewportColor.Z, 1);
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
                transform.Color = new Vector4(1, 1, 1, 1);
                if (family.Casts.Count == 0)
                    continue;
                Cast cast = family.Casts[0];

                UpdateCast(in_Scene, cast, transform, idx, (float)(in_DeltaTime * in_Scene.FrameRate), vis);
                idx += cast.Children.Count + 1;
            }
            in_Priority = idx++;
        }
        private void ApplyAnimationValues(ref SSpriteDrawData in_SpriteDraw, ref SVisibilityData.SScene in_Vis, ref float out_SpriteIndex, Cast in_UiElement, float in_Time)
        {
            //Redo this at some point
            foreach (var animation in in_Vis.Animation.Where(in_A => in_A.Active))
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
                            in_SpriteDraw.Hidden = track.GetSingle(in_Time) != 0;
                            break;

                        case AnimationType.XPosition:
                            in_SpriteDraw.Position.X = track.GetSingle(in_Time);
                            break;

                        case AnimationType.YPosition:
                            in_SpriteDraw.Position.Y = track.GetSingle(in_Time);
                            break;

                        case AnimationType.Rotation:
                            in_SpriteDraw.Rotation = track.GetSingle(in_Time);
                            break;

                        case AnimationType.XScale:
                            in_SpriteDraw.Scale.X = track.GetSingle(in_Time);
                            break;

                        case AnimationType.YScale:
                            in_SpriteDraw.Scale.Y = track.GetSingle(in_Time);
                            break;

                        case AnimationType.SubImage:
                            out_SpriteIndex = track.GetSingle(in_Time);
                            break;

                        case AnimationType.Color:
                            in_SpriteDraw.Color = track.GetColor(in_Time);
                            break;

                        case AnimationType.GradientTl:
                            in_SpriteDraw.GradientTopLeft = track.GetColor(in_Time);
                            break;

                        case AnimationType.GradientBl:
                            in_SpriteDraw.GradientBottomLeft = track.GetColor(in_Time);
                            break;

                        case AnimationType.GradientTr:
                            in_SpriteDraw.GradientTopRight = track.GetColor(in_Time);
                            break;

                        case AnimationType.GradientBr:
                            in_SpriteDraw.GradientBottomRight = track.GetColor(in_Time);
                            break;
                    }
                }
            }
        }
        private void ApplyInheritance(ref SSpriteDrawData in_SpriteDraw, ElementInheritanceFlags in_Inheritance, CastTransform in_Transform)
        {
            // Inherit position
            if (in_Inheritance.HasFlag(ElementInheritanceFlags.InheritXPosition))
                in_SpriteDraw.Position.X += in_Transform.Position.X;

            if (in_Inheritance.HasFlag(ElementInheritanceFlags.InheritYPosition))
                in_SpriteDraw.Position.Y += in_Transform.Position.Y;

            // Inherit rotation
            if (in_Inheritance.HasFlag(ElementInheritanceFlags.InheritRotation))
                in_SpriteDraw.Rotation += in_Transform.Rotation;

            // Inherit scale
            if (in_Inheritance.HasFlag(ElementInheritanceFlags.InheritScaleX))
                in_SpriteDraw.Scale.X *= in_Transform.Scale.X;

            if (in_Inheritance.HasFlag(ElementInheritanceFlags.InheritScaleY))
                in_SpriteDraw.Scale.Y *= in_Transform.Scale.Y;

            // Inherit color
            if (in_Inheritance.HasFlag(ElementInheritanceFlags.InheritColor))
            {
                in_SpriteDraw.Color *= in_Transform.Color;
            }
        }
        private void UpdateCast(Scene in_Scene, Cast in_UiElement, CastTransform in_Transform, int in_Priority, float in_Time, SVisibilityData.SScene in_Vis)
        {
            float sprId = in_UiElement.Info.SpriteIndex;
            SSpriteDrawData sSpriteDrawData = new SSpriteDrawData(in_UiElement, in_Scene);
            float angle = in_Transform.Rotation * MathF.PI / 180.0f; //to radians

            ApplyAnimationValues(ref sSpriteDrawData, ref in_Vis, ref sprId, in_UiElement, in_Time);
            if (sSpriteDrawData.Hidden)
                return;

            var visibilityDataCast = in_Vis.GetVisibility(in_UiElement);
            if (visibilityDataCast == null)
            {
                Console.WriteLine("CRITICAL ERROR! Missing visibility for cast!!! Please fix!");
                return;
            }
            // Inherit position scale
            // TODO: Is this handled through flags?
            sSpriteDrawData.Position.X *= in_Transform.Scale.X;
            sSpriteDrawData.Position.Y *= in_Transform.Scale.Y;

            // Rotate through parent transform
            float rotatedX = sSpriteDrawData.Position.X * MathF.Cos(angle) * in_Scene.AspectRatio + sSpriteDrawData.Position.Y * MathF.Sin(angle);
            float rotatedY = sSpriteDrawData.Position.Y * MathF.Cos(angle) - sSpriteDrawData.Position.X * MathF.Sin(angle) * in_Scene.AspectRatio;

            sSpriteDrawData.Position.X = rotatedX / in_Scene.AspectRatio;
            sSpriteDrawData.Position.Y = rotatedY;

            sSpriteDrawData.Position += in_UiElement.Origin;
            ApplyInheritance(ref sSpriteDrawData, (ElementInheritanceFlags)in_UiElement.InheritanceFlags.Value, in_Transform);
            ApplyPropertyMask(ref sSpriteDrawData, (CastPropertyMask)in_UiElement.Field2C);
            var type = (DrawType)in_UiElement.Field04;
            var flags = (ElementMaterialFlags)in_UiElement.Field38;

            if (visibilityDataCast.Active && in_UiElement.Enabled)
            {
                sSpriteDrawData.Rotation *= MathF.PI / 180.0f;
                if (type == DrawType.Sprite)
                {
                    int spriteIdx1 = Math.Min(in_UiElement.SpriteIndices.Length - 1, (int)sprId);
                    int spriteIdx2 = Math.Min(in_UiElement.SpriteIndices.Length - 1, (int)sprId + 1);
                    Shuriken.Rendering.Sprite spr = sprId >= 0 ? SpriteHelper.TryGetSprite(in_UiElement.SpriteIndices[spriteIdx1]) : null;
                    Shuriken.Rendering.Sprite nextSpr = sprId >= 0 ? SpriteHelper.TryGetSprite(in_UiElement.SpriteIndices[spriteIdx2]) : null;

                    spr ??= nextSpr;
                    nextSpr ??= spr;
                    sSpriteDrawData.NextSprite = nextSpr;
                    sSpriteDrawData.SpriteFactor = sprId % 1;
                    sSpriteDrawData.Sprite = spr;
                    Renderer.DrawSprite(sSpriteDrawData);
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

                        sSpriteDrawData.OverrideUvCoords = true;
                        sSpriteDrawData.TopLeft = new Vector2(begin.X + xOffset, begin.Y);
                        sSpriteDrawData.BottomLeft = new Vector2(begin.X + xOffset, end.Y);
                        sSpriteDrawData.TopRight = new Vector2(end.X + xOffset, begin.Y);
                        sSpriteDrawData.BottomRight = new Vector2(end.X + xOffset, end.Y);

                        sSpriteDrawData.Sprite = spr;
                        sSpriteDrawData.NextSprite = spr;
                        Renderer.DrawSprite(sSpriteDrawData);
                        xOffset += width + BitConverter.ToSingle(BitConverter.GetBytes(in_UiElement.Field4C));
                    }
                }

                var childTransform = new CastTransform(sSpriteDrawData.Position, sSpriteDrawData.Rotation, sSpriteDrawData.Scale, sSpriteDrawData.Color);

                foreach (var child in in_UiElement.Children)
                    UpdateCast(in_Scene, child, childTransform, in_Priority++, in_Time, in_Vis);
            }

        }

        private void ApplyPropertyMask(ref SSpriteDrawData sSpriteDrawData, CastPropertyMask field2C)
        {
            if(!field2C.HasFlag(CastPropertyMask.ApplyTransform))
            {
                sSpriteDrawData.Position = Vector2.Zero;
            }
            else
            {
                if (!field2C.HasFlag(CastPropertyMask.ApplyTranslationX))
                    sSpriteDrawData.Position.X = 0;

                if (!field2C.HasFlag(CastPropertyMask.ApplyTranslationY))
                    sSpriteDrawData.Position.Y = 0;
            }
            if (!field2C.HasFlag(CastPropertyMask.ApplyRotation))
                sSpriteDrawData.Rotation = 0;

            if (!field2C.HasFlag(CastPropertyMask.ApplyScaleX))
                sSpriteDrawData.Scale.X = 1;

            if (!field2C.HasFlag(CastPropertyMask.ApplyScaleY))
                sSpriteDrawData.Scale.Y = 1;

            if (!field2C.HasFlag(CastPropertyMask.ApplyColor))
                sSpriteDrawData.Color = new Vector4(1, 1, 1, 1);

            if (!field2C.HasFlag(CastPropertyMask.ApplyColorBL))
                sSpriteDrawData.GradientBottomLeft = new Vector4(1, 1, 1, 1);

            if (!field2C.HasFlag(CastPropertyMask.ApplyColorBR))
                sSpriteDrawData.GradientBottomRight = new Vector4(1, 1, 1, 1);

            if (!field2C.HasFlag(CastPropertyMask.ApplyColorTL))
                sSpriteDrawData.GradientTopLeft = new Vector4(1, 1, 1, 1);

            if (!field2C.HasFlag(CastPropertyMask.ApplyColorTR))
                sSpriteDrawData.GradientTopRight = new Vector4(1, 1, 1, 1);
        }

        public int GetViewportImageHandle()
        {
            return _viewportData.CsdRenderTextureHandle;
        }
        void RecursiveSetCropListNode(SceneNode in_Node, List<SharpNeedle.Framework.Ninja.Csd.Sprite> in_Sprites, List<Vector2> in_TexSizes)
        {
            foreach (var s in in_Node.Scenes)
            {
                s.Value.Sprites = in_Sprites;
                s.Value.Textures = in_TexSizes;
                foreach (var a in s.Value.Motions)
                {

                    try
                    {
                        var maxFrame = a.Value.FamilyMotions.SelectMany(fm => fm.CastMotions)
                            .SelectMany(cm => cm)
                            .SelectMany(kfl => kfl.Frames)
                            .Max(kf => kf.Frame);
                        
                    if(a.Value.EndFrame < maxFrame)
                        a.Value.EndFrame = maxFrame;
                    }
                    catch (InvalidOperationException e)
                    {
                        continue;
                    }
                }
            }
            foreach (var c in in_Node.Children)
            {
                RecursiveSetCropListNode(c.Value, in_Sprites, in_TexSizes);
            }
        }
        public void SaveCurrentFile(string in_Path)
        {
            List<SharpNeedle.Framework.Ninja.Csd.Sprite> subImageList = new();
            List<Vector2> sizes = new List<Vector2>();
            SpriteHelper.BuildCropList(ref subImageList, ref sizes);
            RecursiveSetCropListNode(WorkProjectCsd.Project.Root, subImageList, sizes);
            
            WorkProjectCsd.Write(string.IsNullOrEmpty(in_Path) ? Config.WorkFilePath : in_Path);
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
            m_SaveScreenshotWhenRendered = true;
        }
        void CreatePackageFile(IChunk in_Chunk, string in_Path, Endianness in_Endianness)
        {
            using BinaryObjectWriter infoWriter = new BinaryObjectWriter(in_Path, Endianness.Little, Encoding.UTF8);
            InfoChunk info = new()
            {
                Signature = BinaryHelper.MakeSignature<uint>(in_Endianness == Endianness.Little ? "NXIF" : "NYIF"),
            };
            info.Chunks.Add(in_Chunk);
            infoWriter.WriteObject(info);
        }
        internal void ExportProjectChunk(string in_Path, bool in_Ultimate)
        {
            string path = string.IsNullOrEmpty(in_Path) ? Config.WorkFilePath : in_Path;
            if (in_Ultimate)
            {
                CreatePackageFile(WorkProjectCsd.Project, path, Endianness.Little);
                CreatePackageFile(WorkProjectCsd.Textures, Path.ChangeExtension(path, "dxl"), Endianness.Little);
            }
            else
            {
                CreatePackageFile(WorkProjectCsd.Project, path, Endianness.Big);
                ShowMessageBoxCross("Warning", "This program can't export tls files.\nYou will have to create them yourself using BrawlBox.", true);
            }
        }
    }
}
