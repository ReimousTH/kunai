using Hexa.NET.ImGui;
using Hexa.NET.ImPlot;
using Hexa.NET.ImGuizmo;
using Kunai.ShurikenRenderer;
using Hexa.NET.Utilities.Text;
using SharpNeedle.Framework.Ninja.Csd.Motions;
using System.Collections.Generic;
using System.Numerics;
using IconFonts;
using OpenTK.Graphics.OpenGL;
using HekonrayBase.Base;
using HekonrayBase;
using System;
using System.Linq;
using SharpNeedle.Framework.Ninja.Csd;
using SharpNeedle.Structs;
using SharpNeedle.Framework.SurfRide.Draw;
using Motion = SharpNeedle.Framework.Ninja.Csd.Motions.Motion;
using KeyFrame = SharpNeedle.Framework.Ninja.Csd.Motions.KeyFrame;
using InterpolationType = SharpNeedle.Framework.Ninja.Csd.Motions.InterpolationType;
using System.Reflection;
using Shuriken.Rendering;
using System.Windows.Controls;
using SharpNeedle.Framework.Glitter;

namespace Kunai.Window
{

 
    
    public class AnimationsWindow : Singleton<AnimationsWindow>, IWindow
    {
        public static float AnimationButtonHeight = 96.0f;

        public struct SKeyframePropertyInfo
        {
            public string Icon;
            public string Name;
            public Vector4 Color;

            public SKeyframePropertyInfo(string icon, string name, Vector4 color)
            {
                Icon = icon;
                Name = name;
                Color = color;
            }
        }
        private static List<ImPlotPoint> ms_Points = new List<ImPlotPoint>();




        SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty DrawMotionElementPopup_SelectedTrackType = SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.HideFlag;

        private void DrawMotionElementPopup(string key,Motion motion)
        {
            var selectedScene = KunaiProject.Instance.SelectionData.SelectedScene;
            var cast = KunaiProject.Instance.SelectionData.SelectedCast;


            ImGui.SeparatorText($"Motion");
            ImGui.Text("Empty");


            if (cast != null)
            {
                ImGui.SeparatorText($"CastAnim[cast:{cast.Name},track:{key}]");
                if (ImGui.BeginCombo("Combo", Enum.GetName(DrawMotionElementPopup_SelectedTrackType)))
                {
                    foreach (var prop in Enum.GetNames(typeof(SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty)))
                    {
                        bool isSelected = (Enum.GetName(DrawMotionElementPopup_SelectedTrackType) == prop);
                        if (ImGui.Selectable(prop, isSelected))
                        {
                            DrawMotionElementPopup_SelectedTrackType = (SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty)Enum.Parse(typeof(SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty), prop);
                        }

                        if (isSelected)
                        {
                            ImGui.SetItemDefaultFocus();
                        }
                    }

                    ImGui.EndCombo();
                }
                

                if (ImGui.MenuItem("Add KeyframeList"))
                {
                    var kf = new KeyFrameList();
                    var mt =  motion.FamilyMotions.First().CastMotions.Find(zx=>zx.Cast.Name == cast.Name);
                    kf.Property = DrawMotionElementPopup_SelectedTrackType;
                    mt.Add(kf);
                
                }

            }

        }
        private void DrawMotionElement(SVisibilityData.SAnimation in_SceneMotion)
        {
            bool selected = false;


            if (ImKunai.VisibilityNode(in_SceneMotion.Motion.Key, ref in_SceneMotion.Active, ref selected,()=> { DrawMotionElementPopup(in_SceneMotion.Motion.Key,in_SceneMotion.Motion.Value); },in_ShowArrow: true))
            {
                foreach (FamilyMotion familyMotion in in_SceneMotion.Motion.Value.FamilyMotions)
                {
                    DrawFamilyMotionElement(familyMotion);
                }
                if (selected)
                {
                    InspectorWindow.SelectMotion(in_SceneMotion.Motion.Value);
                }

                ImGui.TreePop();
            }
        }
        public SKeyframePropertyInfo GetDisplayNameAndIcon(KeyProperty property)
        {
            switch (property)
            {
                case KeyProperty.HideFlag:
                    return new SKeyframePropertyInfo() { Icon = FontAwesome6.Square, Name = "Hide Flag", Color = ColorResource.HideFlag };
                case KeyProperty.PositionX:
                    return new SKeyframePropertyInfo() { Icon = FontAwesome6.LeftRight, Name = "X Translation", Color = ColorResource.PositionX };
                case KeyProperty.PositionY:
                    return new SKeyframePropertyInfo() { Icon = FontAwesome6.UpDown, Name = "Y Translation", Color = ColorResource.PositionY };
                case KeyProperty.Rotation:
                    return new SKeyframePropertyInfo() { Icon = FontAwesome6.ArrowsRotate, Name = "Rotation", Color = ColorResource.Rotation };
                case KeyProperty.ScaleX:
                    return new SKeyframePropertyInfo() { Icon = FontAwesome6.Expand, Name = "X Scale", Color = ColorResource.ScaleX };
                case KeyProperty.ScaleY:
                    return new SKeyframePropertyInfo() { Icon = FontAwesome6.UpRightAndDownLeftFromCenter, Name = "Y Scale", Color = ColorResource.ScaleY };
                case KeyProperty.SpriteIndex:
                    return new SKeyframePropertyInfo() { Icon = FontAwesome6.PhotoFilm, Name = "Crop", Color = ColorResource.SpriteIndex };
                case KeyProperty.Color:
                    return new SKeyframePropertyInfo() { Icon = FontAwesome6.Palette, Name = "Color", Color = ColorResource.Color };
                case KeyProperty.GradientTopLeft:
                    return new SKeyframePropertyInfo() { Icon = FontAwesome6.Palette, Name = "TL Color", Color = ColorResource.GradientTopLeft };
                case KeyProperty.GradientBottomLeft:
                    return new SKeyframePropertyInfo() { Icon = FontAwesome6.Palette, Name = "BL Color", Color = ColorResource.GradientBottomLeft };
                case KeyProperty.GradientTopRight:
                    return new SKeyframePropertyInfo() { Icon = FontAwesome6.Palette, Name = "TR Color", Color = ColorResource.GradientTopRight };
                case KeyProperty.GradientBottomRight:
                    return new SKeyframePropertyInfo() { Icon = FontAwesome6.Palette, Name = "BR Color", Color = ColorResource.GradientBottomRight };
            }

            return new SKeyframePropertyInfo();
        }
        private void DrawFamilyMotionElement(FamilyMotion in_FamilyMotion)
        {
            for (int i = 0; i < in_FamilyMotion.CastMotions.Count; i++)
            {
                CastMotion castMotion = in_FamilyMotion.CastMotions[i];
                if (castMotion.Count == 0) continue;
                ImGui.PushID($"##{castMotion.Cast.Name}anim_{i}");

   
                if (ImGui.TreeNode(castMotion.Cast.Name))
                {
                    for (int t = 0; t < castMotion.Count; t++)
                    {
                        KeyFrameList track = castMotion[t];
                        ImGui.PushID($"##anim_{i}_{t}_{castMotion.Cast.Name}");
                   
                        var info = GetDisplayNameAndIcon(track.Property);

                    
                        var pos = ImGui.GetCursorPosX();
                        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.ColorConvertFloat4ToU32(info.Color));
                        ImKunai.TextFontAwesome(info.Icon);
                        ImGui.PopStyleColor();
                        ImGui.SameLine();
                        ImGui.SetCursorPosX(pos + 20);
                        ImGui.Text(info.Name);
                        ImGui.SameLine();
                        ImGui.SetCursorPosX(pos);
                        if (ImKunai.InvisibleSelectable($"{info.Icon} {info.Name}"))
                        {
                            KunaiProject.Instance.SelectionData.TrackAnimation = track;
                        }
                       

                        if (ImGui.BeginPopupContextItem($"##anim_{i}_{t}_{castMotion.Cast.Name}"))
                        {
                            ImGui.SeparatorText("Track");

                            if (ImGui.MenuItem("Delete"))
                            {
                                castMotion.Remove(track);
                            }
                            ImGui.EndPopup();
                        }
                        ImGui.PopID();

                    }
                    ImGui.Text(""); // Last Element popup getting ignored for some reason so need space


                    if (ImGui.BeginPopupContextItem($"##{castMotion.Cast.Name}anim_{i}_x_x"))
                    {
                        ImGui.SeparatorText("Cast Anim");

                        if (ImGui.MenuItem("Delete"))
                        {
                            in_FamilyMotion.CastMotions.Remove(castMotion);
                        }
                        ImGui.EndPopup();
                    }

                    ImGui.TreePop();
                }
              
                ImGui.PopID();
               
            }
        }


        private void DrawPlot(KunaiProject in_Renderer)
        {
            unsafe
            {
                ImPlotPoint mousePosPlot = new();
                if (ImPlot.BeginPlot("##Bezier", new System.Numerics.Vector2(ImGui.GetWindowSize().X / 1.73f, -1)))
                {
                    const int bufferSize = 256;
                    byte* buffer = stackalloc byte[bufferSize];
                    StrBuilder sb = new(buffer, bufferSize);
                    sb.Append($"##anim");
                    sb.End();
                    var selectedScene = KunaiProject.Instance.SelectionData.SelectedScene;
                    ImPlot.SetupAxisLimits(ImAxis.X1, 0, 60);
                    ImPlot.SetupAxisLimits(ImAxis.Y1, 0, 10);
                    if (selectedScene.Value != null)
                    {
                        if (in_Renderer.SelectionData.TrackAnimation != null)
                        {
                            double time = in_Renderer.Config.Time * selectedScene.Value.FrameRate;
                            ms_Points.Clear();
                            //Line for the anim time
                            if(ImPlot.DragLineX(0, &time, new Vector4(1, 1, 1, 1), 1))
                            {
                                in_Renderer.Config.Time = time / selectedScene.Value.FrameRate;
                            }

                            bool isFloatValue = in_Renderer.SelectionData.TrackAnimation.Property != KeyProperty.Color
                                && in_Renderer.SelectionData.TrackAnimation.Property != KeyProperty.GradientBottomRight
                                && in_Renderer.SelectionData.TrackAnimation.Property != KeyProperty.GradientBottomLeft
                                && in_Renderer.SelectionData.TrackAnimation.Property != KeyProperty.GradientTopLeft
                                && in_Renderer.SelectionData.TrackAnimation.Property != KeyProperty.GradientTopRight;
                            //Animation keyframe points
                            for (int i = 0; i < in_Renderer.SelectionData.TrackAnimation.Frames.Count; i++)
                            {
                                ImPlotPoint point = new ImPlotPoint(in_Renderer.SelectionData.TrackAnimation.Frames[i].Frame, isFloatValue ? in_Renderer.SelectionData.TrackAnimation.Frames[i].Value.Float : 0);
                                ms_Points.Add(point);
                                bool isClicked = false;
                                if (ImPlot.DragPoint(i, &point.X, &point.Y, in_Renderer.SelectionData.KeyframeSelected == in_Renderer.SelectionData.TrackAnimation.Frames[i] ? new System.Numerics.Vector4(1, 0.9f, 1, 1) : new System.Numerics.Vector4(0, 0.9f, 0, 1), 8, ImPlotDragToolFlags.None, &isClicked))
                                {
                                    if (isFloatValue)
                                        in_Renderer.SelectionData.TrackAnimation.Frames[i].Value = new SharpNeedle.Framework.Ninja.Csd.Motions.KeyFrame.Union((float)point.Y);
                                    in_Renderer.SelectionData.TrackAnimation.Frames[i].Frame = (uint)point.X;
                                }
                                if (isClicked)
                                    in_Renderer.SelectionData.KeyframeSelected = in_Renderer.SelectionData.TrackAnimation.Frames[i];
                            }
                            //var p1 = points.ToArray()[0];
                            //ImPlot.PlotLine("##bez", &p1.X, &p1.Y, points.Count, ImPlotLineFlags.Loop, 0, sizeof(ImPlotPoint));

                            //    ImPlotPoint p1 = new ImPlotPoint(.05f, .05f);
                            //ImPlotPoint p2 = new ImPlotPoint(1, 1);
                            //ImPlot.DragPoint(0, &p1.X, &p1.Y, new System.Numerics.Vector4(0, 0.9f, 0, 1), 4, flags, &test, &test, &test);
                            //ImPlot.DragPoint(1, &p2.X, &p2.Y, new System.Numerics.Vector4(1, 0.5f, 1, 1), 4, flags, &test, &test, &test);
                            //
                            //ImPlot.PlotLine("##h1", &p1.X, &p1.Y, 2, 0, 0, sizeof(ImPlotPoint));
                            //ImPlot.PlotLine("##h1", &p2.X, &p2.Y, 2, 0, 0, sizeof(ImPlotPoint));

                        }
                    }

                    mousePosPlot = ImPlot.GetPlotMousePos();
                }
                ImPlot.EndPlot();

                if (ImGui.BeginPopupContextItem())
                {
                    var selectedScene = KunaiProject.Instance.SelectionData.SelectedScene;
                    if (selectedScene.Value != null)
                    {
                        if (in_Renderer.SelectionData.TrackAnimation != null)
                        {
                            if (ImGui.MenuItem("Add Keyframe"))
                            {
                                var frame = new KeyFrame();
                                bool isFloatValue = in_Renderer.SelectionData.TrackAnimation.Property != KeyProperty.Color
                                    && in_Renderer.SelectionData.TrackAnimation.Property != KeyProperty.GradientBottomRight
                                    && in_Renderer.SelectionData.TrackAnimation.Property != KeyProperty.GradientBottomLeft
                                    && in_Renderer.SelectionData.TrackAnimation.Property != KeyProperty.GradientTopLeft
                                    && in_Renderer.SelectionData.TrackAnimation.Property != KeyProperty.GradientTopRight;
                                frame.Frame = (uint)mousePosPlot.X;
                                if (isFloatValue)
                                    frame.Value = (KeyFrame.Union)mousePosPlot.Y;
                                in_Renderer.SelectionData.TrackAnimation.Add(frame);
                            }
                            if(in_Renderer.SelectionData.KeyframeSelected != null)
                            {
                                if (ImGui.MenuItem("Delete Keyframe"))
                                {
                                    in_Renderer.SelectionData.TrackAnimation.Remove(in_Renderer.SelectionData.KeyframeSelected);
                                    in_Renderer.SelectionData.KeyframeSelected = null;
                                }
                            }
                        }
                    }
                    ImGui.EndPopup();
                }
            }
        }
        private void DrawKeyframeInspector()
        {
            if (ImGui.BeginListBox("##animlist2", new System.Numerics.Vector2(-1, -1)))
            {
                var renderer = KunaiProject.Instance;
                ImGui.SeparatorText("Keyframe");
                if (renderer.SelectionData.KeyframeSelected == null)
                    ImGui.TextWrapped("Select a keyframe in the timeline to edit its values.");
                else
                {
                    var keyframe = renderer.SelectionData.KeyframeSelected;
                    int frame = (int)keyframe.Frame;
                    var val = keyframe.Value;
                    var valColor = keyframe.Value.Color.ToVec4();
                    var interp = (int)keyframe.Interpolation;
                    ImGui.InputInt("Frame", ref frame);
                    bool isFloatValue = renderer.SelectionData.TrackAnimation.Property != KeyProperty.Color
                       && renderer.SelectionData.TrackAnimation.Property != KeyProperty.GradientBottomRight
                       && renderer.SelectionData.TrackAnimation.Property != KeyProperty.GradientBottomLeft
                       && renderer.SelectionData.TrackAnimation.Property != KeyProperty.GradientTopLeft
                       && renderer.SelectionData.TrackAnimation.Property != KeyProperty.GradientTopRight;

                    if (isFloatValue)
                    {
                        ImGui.InputFloat("Value", ref val.Float);
                        keyframe.Value = val.Float;
                    }
                    else
                    {
                        if(ImGui.ColorEdit4("Value", ref valColor))
                        keyframe.Value = valColor.ToSharpNeedleColor();
                    }

                    if (ImGui.Combo("Interpolation", ref interp, ["Const", "Linear", "Hermite"], 3))
                        keyframe.Interpolation = (InterpolationType)interp;


                    keyframe.Frame = (uint)frame;
                }
                ImGui.EndListBox();
            }
        }

        public void OnReset(IProgramProject in_Renderer)
        {
            throw new System.NotImplementedException();
        }


        void ApplyAnimationFrameKey<T>(Cast cast, SharpNeedle.Framework.Ninja.Csd.Motions.Motion motion, SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty keyproperty,InterpolationType interp, T color, double frame)
        {



            var KeyFrame = new SharpNeedle.Framework.Ninja.Csd.Motions.KeyFrame();
            var mt = motion.FamilyMotions.First().CastMotions.Find(zx => zx.Cast.Name == cast.Name);
            KeyFrameList? kl = new KeyFrameList() { Property = keyproperty };

            if ((kl = mt.Find(zx => zx.Property == keyproperty)) == null)
            {
                kl = new KeyFrameList() { Property = keyproperty };
                mt.Add(kl);
                kl.Add(KeyFrame);
            }
            else
            {
                int kindex = -1;
                if ((kindex = kl.FindKeyframeStrict((float)frame * motion.Scene.FrameRate)) != -1)
                {
                    KeyFrame = kl.Frames[kindex];
                }
                else
                {
                    kl.Add(KeyFrame);
                }
            }

            //Bypass T limitation
            var ku = (KeyFrame.Union)typeof(SharpNeedle.Framework.Ninja.Csd.Motions.KeyFrame.Union).GetConstructor(BindingFlags.Public | BindingFlags.Instance, new Type[] { typeof(T) }).Invoke(new object[] { color });
            KeyFrame.Frame = (uint)(frame * motion.Scene.FrameRate);
            KeyFrame.Value = ku;
            KeyFrame.Interpolation = interp;


            }



        int Interpolation = (int)InterpolationType.Linear;

        public void RenderAnimationButton(IProgramProject in_Renderer)
        {
            var sizex = ImGui.GetWindowViewport().Size.X;
            var sizey = ImGui.GetWindowViewport().Size.Y;
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, sizey - AnimationButtonHeight), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(sizex, sizey), ImGuiCond.Always);

            if (ImGui.Begin("AnimationsButton",MainWindow.WindowFlags | ImGuiWindowFlags.NoTitleBar)){


                var renderer = (KunaiProject)in_Renderer;
                var selectedScene2 = KunaiProject.Instance.SelectionData.SelectedScene;
                var cast = KunaiProject.Instance.SelectionData.SelectedCast;
                var motion = KunaiProject.Instance.SelectionData.SelectedMotion;

                ImGui.SeparatorText($"Cast : {(cast != null ? cast.Name : cast)}, motion : {(motion != null ? motion : motion)}");
                bool disabled = !(selectedScene2.Value != null && cast != null && motion != null);

                if (!disabled)
                {
                    disabled = !(motion.Scene == selectedScene2.Value);
                }

                ImGui.BeginDisabled(disabled);


                if (ImGui.Button("COLOR"))
                {
                    ApplyAnimationFrameKey(cast, motion, SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.Color,(InterpolationType)Interpolation, cast.Info.Color, renderer.Config.Time);
                }
                ImGui.SameLine();
                if (ImGui.Button("VC\nALL"))
                {
                    ApplyAnimationFrameKey(cast, motion, SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.GradientTopLeft, (InterpolationType)Interpolation, cast.Info.GradientTopLeft, renderer.Config.Time);
                    ApplyAnimationFrameKey(cast, motion, SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.GradientTopRight, (InterpolationType)Interpolation, cast.Info.GradientTopRight, renderer.Config.Time);
                    ApplyAnimationFrameKey(cast, motion, SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.GradientBottomLeft, (InterpolationType)Interpolation, cast.Info.GradientBottomLeft, renderer.Config.Time);
                    ApplyAnimationFrameKey(cast, motion, SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.GradientBottomRight, (InterpolationType)Interpolation, cast.Info.GradientBottomRight, renderer.Config.Time);
                }

                ImGui.SameLine();

                ImGui.BeginGroup();

                if (ImGui.Button("TL"))
                {
                    ApplyAnimationFrameKey(cast, motion, SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.GradientTopLeft, (InterpolationType)Interpolation, cast.Info.GradientTopLeft, renderer.Config.Time);
                }
                if (ImGui.Button("TR"))
                {
                    ApplyAnimationFrameKey(cast, motion, SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.GradientTopRight, (InterpolationType)Interpolation, cast.Info.GradientTopRight, renderer.Config.Time);
                }
                ImGui.EndGroup();

                ImGui.SameLine();
                ImGui.BeginGroup();
                if (ImGui.Button("BL"))
                {
                    ApplyAnimationFrameKey(cast, motion, SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.GradientBottomLeft, (InterpolationType)Interpolation, cast.Info.GradientBottomRight, renderer.Config.Time);
                }
                if (ImGui.Button("BR"))
                {

                    ApplyAnimationFrameKey(cast, motion, SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.GradientBottomRight, (InterpolationType)Interpolation, cast.Info.GradientBottomRight, renderer.Config.Time);
                }
                ImGui.EndGroup();


                ImGui.SameLine();
                if (ImGui.Button("TRS\nALL"))
                {
                    ApplyAnimationFrameKey(cast, motion, SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.PositionX, (InterpolationType)Interpolation, cast.Info.Translation.X, renderer.Config.Time);
                    ApplyAnimationFrameKey(cast, motion, SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.PositionY, (InterpolationType)Interpolation, cast.Info.Translation.Y, renderer.Config.Time);
                    ApplyAnimationFrameKey(cast, motion, SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.ScaleX, (InterpolationType)Interpolation, cast.Info.Scale.X, renderer.Config.Time);
                    ApplyAnimationFrameKey(cast, motion, SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.ScaleY, (InterpolationType)Interpolation, cast.Info.Scale.Y, renderer.Config.Time);
                    ApplyAnimationFrameKey(cast, motion, SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.Rotation, (InterpolationType)Interpolation, cast.Info.Rotation, renderer.Config.Time);
                }

                ImGui.SameLine();

                ImGui.BeginGroup();

                if (ImGui.Button("TXY"))
                {
                    ApplyAnimationFrameKey(cast, motion, SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.PositionX, (InterpolationType)Interpolation, cast.Info.Translation.X, renderer.Config.Time);
                    ApplyAnimationFrameKey(cast, motion, SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.PositionX, (InterpolationType)Interpolation, cast.Info.Translation.Y, renderer.Config.Time);
                }
                if (ImGui.Button("SXY"))
                {

                    ApplyAnimationFrameKey(cast, motion, SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.ScaleX, (InterpolationType)Interpolation, cast.Info.Scale.X, renderer.Config.Time);
                    ApplyAnimationFrameKey(cast, motion, SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.ScaleY, (InterpolationType)Interpolation, cast.Info.Scale.Y, renderer.Config.Time);
                }


                ImGui.EndGroup();


                ImGui.SameLine();

                ImGui.BeginGroup();


                if (ImGui.Button("TX"))
                {
                    ApplyAnimationFrameKey(cast, motion, SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.PositionX, (InterpolationType)Interpolation, cast.Info.Translation.X, renderer.Config.Time);
                }
                if (ImGui.Button("SX"))
                {
                    ApplyAnimationFrameKey(cast, motion, SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.ScaleX, (InterpolationType)Interpolation, cast.Info.Scale.X, renderer.Config.Time);
                }


                ImGui.EndGroup();


                ImGui.SameLine();

                ImGui.BeginGroup();

                if (ImGui.Button("TY"))
                {
                    ApplyAnimationFrameKey(cast, motion, SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.PositionY, (InterpolationType)Interpolation, cast.Info.Translation.Y, renderer.Config.Time);
                }
                if (ImGui.Button("RY"))
                {
                    ApplyAnimationFrameKey(cast, motion, SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.ScaleY, (InterpolationType)Interpolation, cast.Info.Scale.Y, renderer.Config.Time);
                }
                ImGui.EndGroup();


                ImGui.SameLine();

                ImGui.BeginGroup();

                if (ImGui.Button("RZ"))
                {
                    ApplyAnimationFrameKey(cast, motion, SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.Rotation, (InterpolationType)Interpolation, cast.Info.Rotation, renderer.Config.Time);
                }

                ImGui.EndGroup();


                ImGui.SameLine();
                ImGui.Combo("Interpolation", ref Interpolation, ["Const", "Linear", "Hermite"], 3);
    

                ImGui.EndDisabled();


                ImGui.End();
            }

        }

        public void Render(IProgramProject in_Renderer)
        {
            var renderer = (KunaiProject)in_Renderer;
            var size1 = ImGui.GetWindowViewport().Size.X / 4.5f;
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(size1, (ImGui.GetWindowViewport().Size.Y) / 1.5f), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(size1 * 2.5f, (ImGui.GetWindowViewport().Size.Y - AnimationsWindow.AnimationButtonHeight - MenuBarWindow.MenuBarHeight) / 3), ImGuiCond.Always);
            if (ImGui.Begin("Animations", MainWindow.WindowFlags | ImGuiWindowFlags.NoTitleBar))
            {



                ImGui.Checkbox("Show Quads", ref renderer.Config.ShowQuads);
                ImGui.SetNextItemWidth(60);
                ImGui.SameLine();
                ImGui.InputDouble("Time", ref renderer.Config.Time, "%.2f");
                ImGui.SameLine();
                if (KunaiProject.Instance.SelectionData.SelectedScene.Value != null) {


                    renderer.Config.Frame = (int)(renderer.Config.Time * KunaiProject.Instance.SelectionData.SelectedScene.Value.FrameRate);
                    ImGui.SetNextItemWidth(128);
                    ImGui.InputInt("Frame", ref renderer.Config.Frame,ImGuiInputTextFlags.ReadOnly);
                }
                    
                ImGui.SameLine();
                ImGui.BeginGroup();
                ImGui.PushFont(ImGuiController.FontAwesomeFont);
                if (ImGui.Button(FontAwesome6.Camera))
                {
                    renderer.SaveScreenshot();
                }
                ImGui.SameLine();
                if (ImGui.Button(FontAwesome6.Stop))
                {
                    renderer.Config.PlayingAnimations = false;
                    renderer.Config.Time = 0;
                }

 

                ImGui.SameLine();
                if (ImGui.Button(renderer.Config.PlayingAnimations ? FontAwesome6.Pause : FontAwesome6.Play))
                    renderer.Config.PlayingAnimations = !renderer.Config.PlayingAnimations;

                ImGui.SameLine();
                if (ImGui.Button(FontAwesome6.RotateRight))
                {
                    renderer.Config.Time = 0;
                }

                ImGui.SameLine();
                if (KunaiProject.Instance.SelectionData.SelectedScene.Value != null)
                {
                    if (ImGui.Button(FontAwesome6.ArrowLeft))
                    {
                        renderer.Config.Time -= 1.0/KunaiProject.Instance.SelectionData.SelectedScene.Value.FrameRate;
                    }

                    ImGui.SameLine();
                    if (ImGui.Button(FontAwesome6.ArrowRight))
                    {
                        renderer.Config.Time += 1.0/KunaiProject.Instance.SelectionData.SelectedScene.Value.FrameRate;
                    }
                }


                ImGui.PopFont();
                ImGui.EndGroup();


                //The list of anims, anim tracks and cast animations
                if (ImGui.BeginListBox("##animlist", new System.Numerics.Vector2(ImGui.GetWindowSize().X / 5, -1)))
                {




                    var selectedScene = KunaiProject.Instance.SelectionData.SelectedScene;
                    if (selectedScene.Value != null)
                    {

                        // Render the popup content



                        SVisibilityData.SScene sceneVisData = renderer.VisibilityData.GetScene(selectedScene.Value);
                        if (sceneVisData != null)
                        {


                            bool _add = false;


                            if (ImGui.BeginPopupContextWindow("##animlist"))
                            {
                                ImGui.SeparatorText("Bank");
                                if (ImGui.MenuItem("Add"))
                                {
                                    _add = true;
                                }
                                if (ImGui.MenuItem("Remove"))
                                {

                                }
                                ImGui.EndPopup();
                            }
                            if (_add) ImGui.OpenPopup("animlist_popup");  // EndPopup() completly empty buffer thats why it like this

                            if (ImGui.BeginPopup("animlist_popup", ImGuiWindowFlags.AlwaysAutoResize))
                            {
                                string inputText = $"animation_{sceneVisData.Animation.Count}";
                                // Render the popup content here
                                {
                                    ImGui.Text("Enter animation name:");
                                    ImGui.InputText("##animlist_ADD_input", ref inputText, 256);

                                    if (ImGui.Button("OK")) //enter-button
                                    {
                                        Console.WriteLine($"Animation name added: {inputText}");
                                        selectedScene.Value.Motions.Add(inputText, new Motion(selectedScene.Value));
                                        sceneVisData.Animation.Add(new SVisibilityData.SAnimation(selectedScene.Value.Motions.Last()));
                                    }

                                }

                                ImGui.EndPopup();
                            }



                            foreach (SVisibilityData.SAnimation sceneMotion in sceneVisData.Animation)
                            {

                                DrawMotionElement(sceneMotion);

                            }
                        }
                    }
                    ImGui.EndListBox();
                }
                ImGui.SameLine();
                DrawPlot(renderer);
                ImGui.SameLine();
                DrawKeyframeInspector();
                // ImGui.SameLine();

                ImGui.End();
            }
            
             RenderAnimationButton(renderer);

        }
    }
}
