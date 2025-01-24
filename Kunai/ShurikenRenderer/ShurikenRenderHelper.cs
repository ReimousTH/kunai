﻿using Hexa.NET.ImGui;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;
using SharpNeedle.Ninja.Csd;
using SharpNeedle.Ninja.Csd.Motions;
using Shuriken.Models;
using Shuriken.Rendering;
using System.IO;
using static Kunai.ShurikenRenderer.SVisibilityData;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace Kunai.ShurikenRenderer
{
    [Flags]
    public enum AnimationType : uint
    {
        None = 0,
        HideFlag = 1,
        XPosition = 2,
        YPosition = 4,
        Rotation = 8,
        XScale = 16,
        YScale = 32,
        SubImage = 64,
        Color = 128,
        GradientTL = 256,
        GradientBL = 512,
        GradientTR = 1024,
        GradientBR = 2048
    }
   
    public class SVisibilityData
    {
        public class SCast
        {
            public Cast Cast;
            public bool Active = true;
            public SCast(Cast in_Scene)
            {
                Cast = in_Scene;
            }
        }
        public class SAnimation
        {
            public KeyValuePair<string,Motion> Motion;
            public bool Active = true;
            public SAnimation(KeyValuePair<string,Motion> in_Scene)
            {
                Motion = in_Scene;
            
            }
            public KeyFrameList GetTrack(Cast layer, AnimationType type)
            {
                foreach (SharpNeedle.Ninja.Csd.Motions.FamilyMotion animation in Motion.Value.FamilyMotions)
                {
                    foreach (SharpNeedle.Ninja.Csd.Motions.CastMotion animtrack in animation.CastMotions)
                    {
                        if (layer == animtrack.Cast)
                        {
                            if (animtrack.Capacity != 0)
                            {
                                foreach (var track in animtrack)
                                {
                                    if (track.Property.ToShurikenAnimationType() == type)
                                        return track;
                                }
                            }
                        }
                    }
                }

                return null;
            }
        }
        public class SScene
        {
            public List<SAnimation> Animation = new List<SAnimation>();
            public List<SCast> Casts = new List<SCast>();
            public KeyValuePair<string, Scene> Scene;
            public bool Active = true;
            public SScene(KeyValuePair<string, Scene> in_Scene)
            {
                Scene = in_Scene;
                foreach(var group in Scene.Value.Families)
                {
                    foreach(var cast in group.Casts)
                    {
                        Casts.Add(new SCast(cast));
                    }
                }
                foreach(var mot in Scene.Value.Motions)
                {
                    Animation.Add(new SAnimation(mot));
                }
            }
            public SCast GetVisibility(Cast cast)
            {
                return Casts.FirstOrDefault(node => node.Cast == cast);
            }
        }
        public class SNode
        {

            public List<SScene> Scene = new List<SScene>();
            public KeyValuePair<string, SceneNode> Node;
            public bool Active = true;

            
            public SNode(KeyValuePair<string, SceneNode> in_Node)
            {
                Node = in_Node;
                foreach(var scene in Node.Value.Scenes)
                {
                    Scene.Add(new SScene(scene));
                }
            }
            public SScene GetVisibility(Scene scene)
            {
                return Scene.FirstOrDefault(node => node.Scene.Value == scene);
            }
        }

        public List<SNode> Nodes = new List<SNode>();

        public SVisibilityData(CsdProject in_Proj)
        {
            Nodes.Add(new SNode(new KeyValuePair<string, SceneNode>("Root", in_Proj.Project.Root)));
            foreach (var scene in in_Proj.Project.Root.Children)
            {
                Nodes.Add(new SNode(scene));
            }
        }
        public SNode GetVisibility(SceneNode scene)
        {
            foreach (var node in Nodes)
            {
                if (node.Node.Value == scene)
                    return node;
            }
            return null;
        }
        public SScene GetScene(Scene scene)
        {
            foreach (var node in Nodes)
            {
                foreach(var scene2 in node.Scene)
                    if(scene2.Scene.Value == scene)
                        return scene2;
            }
            return null;
        }
    }
    public class ShurikenRenderHelper
    {
        public Renderer renderer;
        public Vector2 size;
        public SVisibilityData sVisibilityData;
        public CsdProject WorkProjectCsd;
        int fbo;
        public double time;
        public bool playingAnimations;
        int rbo;
        public int texColor;
        //int texDepth;
        Vector2i fboSize = default;
        public GameWindow window;
        public ShurikenRenderHelper(GameWindow window2, Vector2 in_ViewportSize)
        {
            size = in_ViewportSize;
            renderer = new Renderer((int)size.X, (int)size.Y);
            renderer.SetShader(renderer.shaderDictionary["basic"]);
            window = window2;
        }
        public void LoadFile(string in_Path)
        {
            WorkProjectCsd = ResourceUtility.Open<CsdProject>(@in_Path);
            ITextureList xTextures = WorkProjectCsd.Textures;
            CsdDictionary<SharpNeedle.Ninja.Csd.Font> xFontList = WorkProjectCsd.Project.Fonts;

            string root = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(@in_Path));
            SpriteHelper.textureList = new TextureList("textures");
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
                        SpriteHelper.textureList.Textures.Add(new Texture(texPath, tempChangeExtension));
                    else
                    {
                        Console.WriteLine("AAA");
                    }
                    //    MissingTextures.Add(texture.Name);
                }
            }
            SpriteHelper.LoadTextures(WorkProjectCsd);
            sVisibilityData = new SVisibilityData(WorkProjectCsd);
        }
        public void Render(CsdProject in_CsdProject, float in_DeltaTime)
        {
            // Get the size of the child (i.e. the whole draw size of the windows).
            System.Numerics.Vector2 wsize = ImGui.GetWindowSize();

            // make sure the buffers are the currect size
            Vector2i wsizei = new((int)wsize.X, (int)wsize.Y);
            if (fboSize != wsizei)
            {
                fboSize = wsizei;

                // create our frame buffer if needed
                if (fbo == 0)
                {
                    fbo = GL.GenFramebuffer();
                    // bind our frame buffer
                    GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
                    GL.ObjectLabel(ObjectLabelIdentifier.Framebuffer, fbo, 10, "GameWindow");
                }

                // bind our frame buffer
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);

                if (texColor > 0)
                    GL.DeleteTexture(texColor);

                texColor = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, texColor);
                GL.ObjectLabel(ObjectLabelIdentifier.Texture, texColor, 16, "GameWindow:Color");
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb, wsizei.X, wsizei.Y, 0, PixelFormat.Rgb, PixelType.UnsignedByte, IntPtr.Zero);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, texColor, 0);

                if (rbo > 0)
                    GL.DeleteRenderbuffer(rbo);

                rbo = GL.GenRenderbuffer();
                GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, rbo);
                GL.ObjectLabel(ObjectLabelIdentifier.Renderbuffer, rbo, 16, "GameWindow:Depth");
                GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent32f, wsizei.X, wsizei.Y);
                GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, rbo);
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
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
                GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, rbo);
            }

            GL.Viewport(0, 0, wsizei.X, wsizei.Y); // change the viewport to window

            // actually draw the scene
            {
                GL.ClearColor(Color4.BlueViolet);
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                renderer.Width = (int)size.X;
                renderer.Height = (int)size.Y;
                renderer.Start();
                if(playingAnimations)
                time += in_DeltaTime;
                RenderNode(in_CsdProject.Project.Root, time);
                foreach (KeyValuePair<string, SceneNode> node in in_CsdProject.Project.Root.Children)
                {
                    if (!sVisibilityData.GetVisibility(node.Value).Active) continue;
                    RenderNode(node.Value, time);
                }

                renderer.End();
            }

            // unbind our bo so nothing else uses it
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            GL.Viewport(0, 0, window.ClientSize.X, window.ClientSize.Y); // back to full screen size
            
        }
        public void RenderNode(SceneNode in_Node, double in_DeltaTime)
        {
            SVisibilityData.SNode vis = sVisibilityData.GetVisibility(in_Node);
            int idx = 0;
            foreach (var scene in in_Node.Scenes)
            {
                if (!vis.GetVisibility(scene.Value).Active) continue;
                RenderScenes(scene.Value,vis, ref idx, in_DeltaTime);
                // = true;
            }
        }
        public void RenderScenes(Scene in_Scene, SVisibilityData.SNode in_Vis, ref int priority, double in_DeltaTime)
        {
            int idx = priority;
            var vis = in_Vis.GetVisibility(in_Scene);
            foreach (var family in in_Scene.Families)
            {
                var transform = new CastTransform();
                Cast cast = family.Casts[0];

                UpdateCast(in_Scene, cast, transform, idx, (float)(in_DeltaTime * in_Scene.FrameRate), vis);
                idx += cast.Children.Count + 1;
            }
            priority = idx++;
        }
        private void UpdateCast(Scene scene, Cast in_UiElement, CastTransform transform,int priority, float time, SVisibilityData.SScene vis)
        {
            bool hideFlag = in_UiElement.Info.HideFlag != 0;
            var position = new System.Numerics.Vector2(in_UiElement.Info.Translation.X, in_UiElement.Info.Translation.Y);
            float rotation = in_UiElement.Info.Rotation;
            var scale = new System.Numerics.Vector2(in_UiElement.Info.Scale.X, in_UiElement.Info.Scale.Y);
            float sprID = in_UiElement.Info.SpriteIndex;
            var color = in_UiElement.Info.Color;
            var gradientTopLeft = in_UiElement.Info.GradientTopLeft;
            var gradientBottomLeft = in_UiElement.Info.GradientBottomLeft;
            var gradientTopRight = in_UiElement.Info.GradientTopRight;
            var gradientBottomRight = in_UiElement.Info.GradientBottomRight;

            foreach (var animation in vis.Animation)
            {
                if (!animation.Active)
                    continue;
            
                for (int i = 0; i < 12; i++)
                {
                    var type = (AnimationType)(1 << i);
                    var track = animation.GetTrack(in_UiElement, type);
            
                    if (track == null)
                        continue;
            
                    switch (type)
                    {
                        case AnimationType.HideFlag:
                            hideFlag = track.GetSingle(time) != 0;
                            break;
            
                        case AnimationType.XPosition:
                            position.X = track.GetSingle(time);
                            break;
            
                        case AnimationType.YPosition:
                            position.Y = track.GetSingle(time);
                            break;
            
                        case AnimationType.Rotation:
                            rotation = track.GetSingle(time);
                            break;
            
                        case AnimationType.XScale:
                            scale.X = track.GetSingle(time);
                            break;
            
                        case AnimationType.YScale:
                            scale.Y = track.GetSingle(time);
                            break;
            
                        case AnimationType.SubImage:
                            sprID = track.GetSingle(time);
                            break;
            
                        case AnimationType.Color:
                            color = track.GetColor(time);
                            break;
            
                        case AnimationType.GradientTL:
                            gradientTopLeft = track.GetColor(time);
                            break;
            
                        case AnimationType.GradientBL:
                            gradientBottomLeft = track.GetColor(time);
                            break;
            
                        case AnimationType.GradientTR:
                            gradientTopRight = track.GetColor(time);
                            break;
            
                        case AnimationType.GradientBR:
                            gradientBottomRight = track.GetColor(time);
                            break;
                    }
                }
            }

            if (hideFlag)
                return;

            // Inherit position scale
            // TODO: Is this handled through flags?
            position.X *= transform.Scale.X;
            position.Y *= transform.Scale.Y;

            // Rotate through parent transform
            float angle = transform.Rotation * MathF.PI / 180.0f; //to radians
            float rotatedX = position.X * MathF.Cos(angle) * scene.AspectRatio + position.Y * MathF.Sin(angle);
            float rotatedY = position.Y * MathF.Cos(angle) - position.X * MathF.Sin(angle) * scene.AspectRatio;

            position.X = rotatedX / scene.AspectRatio;
            position.Y = rotatedY;

            position += in_UiElement.Origin;
            var InheritanceFlags = (ElementInheritanceFlags)in_UiElement.InheritanceFlags.Value;
            // Inherit position
            if (InheritanceFlags.HasFlag(ElementInheritanceFlags.InheritXPosition))
                position.X += transform.Position.X;

            if (InheritanceFlags.HasFlag(ElementInheritanceFlags.InheritYPosition))
                position.Y += transform.Position.Y;

            // Inherit rotation
            if (InheritanceFlags.HasFlag(ElementInheritanceFlags.InheritRotation))
                rotation += transform.Rotation;

            // Inherit scale
            if (InheritanceFlags.HasFlag(ElementInheritanceFlags.InheritScaleX))
                scale.X *= transform.Scale.X;

            if (InheritanceFlags.HasFlag(ElementInheritanceFlags.InheritScaleY))
                scale.Y *= transform.Scale.Y;

            // Inherit color
            //if (InheritanceFlags.HasFlag(ElementInheritanceFlags.InheritColor))
            //{
            //    var cF = color * transform.Color;
            //    color = new Color(cF.X, cF.Y, cF.Z, cF.W);
            //}
            var Type = (DrawType)in_UiElement.Field04;
            var Flags = (ElementMaterialFlags)in_UiElement.Field38;

            if (vis.GetVisibility(in_UiElement).Active &&  in_UiElement.Enabled)
            {
                if (Type == DrawType.Sprite)
                {
                    int test = Math.Min(in_UiElement.SpriteIndices.Length - 1, (int)sprID);
                    int test2 = Math.Min(in_UiElement.SpriteIndices.Length - 1, (int)sprID + 1);
                    Shuriken.Rendering.Sprite spr = sprID >= 0 ? SpriteHelper.TryGetSprite(in_UiElement.SpriteIndices[test]) : null;
                    Shuriken.Rendering.Sprite nextSpr = sprID >= 0 ? SpriteHelper.TryGetSprite(in_UiElement.SpriteIndices[test2]) : null;

                    spr ??= nextSpr;
                    nextSpr ??= spr;
                    renderer.DrawSprite(
                        in_UiElement.TopLeft, in_UiElement.BottomLeft, in_UiElement.TopRight, in_UiElement.BottomRight,
                        position, rotation * MathF.PI / 180.0f, scale, scene.AspectRatio, spr, nextSpr, sprID % 1, color.ToVec4(),
                        gradientTopLeft.ToVec4(), gradientBottomLeft.ToVec4(), gradientTopRight.ToVec4(), gradientBottomRight.ToVec4(),
                        priority, Flags);
                }
                else if (Type == DrawType.Font)
                {
                    //float xOffset = 0.0f;
                    //if (in_UiElement.FontCharacters == null)
                    //    in_UiElement.FontCharacters = "";
                    //for (var i = 0; i < in_UiElement.FontCharacters.Length; i++)
                    //{
                    //    var font = Project.TryGetFont(in_UiElement.FontID);
                    //    if (font == null)
                    //        continue;
                    //
                    //    Sprite spr = null;
                    //
                    //    foreach (var mapping in font.Mappings)
                    //    {
                    //        if (mapping.Character != in_UiElement.FontCharacters[i])
                    //            continue;
                    //
                    //        spr = Project.TryGetSprite(mapping.Sprite);
                    //        break;
                    //    }
                    //
                    //    if (spr == null)
                    //        continue;
                    //
                    //    float width = spr.Dimensions.X / renderer.Width;
                    //    float height = spr.Dimensions.Y / renderer.Height;
                    //
                    //    var begin = (Vector2)in_UiElement.TopLeft;
                    //    var end = begin + new Vector2(width, height);
                    //
                    //    renderer.DrawSprite(
                    //        new Vector2(begin.X + xOffset, begin.Y),
                    //        new Vector2(begin.X + xOffset, end.Y),
                    //        new Vector2(end.X + xOffset, begin.Y),
                    //        new Vector2(end.X + xOffset, end.Y),
                    //        position, Utilities.ToRadians(rotation), scale, scene.AspectRatio, spr, spr, 0, color,
                    //        gradientTopLeft, gradientBottomLeft, gradientTopRight, gradientBottomRight,
                    //        in_UiElement.ZIndex, in_UiElement.Flags
                    //    );
                    //
                    //    xOffset += width + in_UiElement.FontSpacingAdjustment;
                    //}
                }

                var childTransform = new CastTransform(position, rotation, scale, color);

                foreach (var child in in_UiElement.Children)
                    UpdateCast(scene, child, childTransform, priority++, time, vis);
            }
        }
    }
}
