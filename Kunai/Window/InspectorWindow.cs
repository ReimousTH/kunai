using Kunai.ShurikenRenderer;
using Hexa.NET.ImGui;
using SharpNeedle.Framework.Ninja.Csd;
using Shuriken.Rendering;
using Sprite = Shuriken.Rendering.Sprite;
using Hexa.NET.Utilities.Text;
using Vector2 = System.Numerics.Vector2;
using System.Collections.Generic;
using System.Numerics;
using System;
using HekonrayBase.Base;
using HekonrayBase;
using SharpNeedle.Structs;
using SharpNeedle.Framework.Ninja.Csd.Motions;

namespace Kunai.Window
{
    enum EAlignmentPivot
    {
        None,
        TopLeft,
        TopCenter,
        TopRight,
        MiddleLeft,
        MiddleCenter,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }
    [Flags]
    enum CastPropertyMask : uint
    {
        None = 0x0000,
        ApplyTransform = 0x0001,
        ApplyTranslationX = 0x0002,
        ApplyTranslationY = 0x0004,
        ApplyRotation = 0x0008,
        ApplyScaleX = 0x0010,
        ApplyScaleY = 0x0020,
        Flag7 = 0x0040,
        ApplyColor = 0x0080,
        ApplyColorTL = 0x0100,
        ApplyColorBL = 0x0200,
        ApplyColorTR = 0x0400,
        ApplyColorBR = 0x0800,
        Flag13 = 0x1000,
        Flag14 = 0x2000,
        Flag15 = 0x4000
    }
    public class InspectorWindow : Singleton<InspectorWindow>, IWindow
    {
        public enum ESelectionType
        {
            None,
            Scene,
            Node,
            Cast,
            Motion
        }
        public static ESelectionType SelectionType;
        static bool ms_IsEditingCrop;

        private static List<string> ms_FontNames = new List<string>();
        public static void SelectCast(Cast in_Cast)
        {
            ms_IsEditingCrop = false;
            KunaiProject.Instance.SelectionData.SelectedCast = in_Cast;
            SelectionType = ESelectionType.Cast;
        }
        public static void SelectScene(KeyValuePair<string, Scene> in_Scene)
        {
            KunaiProject.Instance.SelectionData.SelectedScene = in_Scene;
            SelectionType = ESelectionType.Scene;
        }

        public static void SelectMotion(Motion in_Motion)
        {
            KunaiProject.Instance.SelectionData.SelectedMotion = in_Motion;
            SelectionType = ESelectionType.Motion;
        }

        private static int Gcd(int in_A, int in_B)
        {
            while (in_B != 0)
            {
                int temp = in_B;
                in_B = in_A % in_B;
                in_A = temp;
            }
            return in_A;
        }

        public void DrawSceneInspector()
        {
            KeyValuePair<string, Scene> selectedScene = KunaiProject.Instance.SelectionData.SelectedScene;

            var vis = KunaiProject.Instance.VisibilityData.GetScene(selectedScene.Value);
            if (selectedScene.Value == null)
                return;
            ImGui.SeparatorText("Scene");
            string name = vis.Scene.Key;
            int vers = selectedScene.Value.Version;
            int priority = (int)selectedScene.Value.Priority;
            float aspectRatio = selectedScene.Value.AspectRatio;
            float fps = selectedScene.Value.FrameRate;

            //Show aspect ratio as (width:height) by using greatest common divisor
            int width = 1280;
            int height = (int)(1280 / aspectRatio);
            double decimalRatio = (double)width / height;
            int gcdValue = Gcd(width, height);
            int simplifiedWidth = width / gcdValue;
            int simplifiedHeight = height / gcdValue;

            if (ImGui.InputText("Name", ref name, 256))
            {
                vis.Rename(name);
            }
            ImGui.InputFloat("Framerate", ref fps);
            ImGui.InputFloat("Aspect Ratio", ref aspectRatio);
            ImGui.InputInt("Version", ref vers);
            ImGui.InputInt("Priority", ref priority);
            ImGui.Text($"Ratio: ({simplifiedWidth}:{simplifiedHeight})");
            ImGui.SeparatorText("Info");
            var space = ImGui.GetContentRegionAvail();
            if (ImGui.BeginListBox("##crops", new Vector2(-1, space.Y / 2 - 5)))
            {
                int idx = 0;
                foreach (var s in selectedScene.Value.Sprites)
                {
                    if (ImGui.TreeNode($"Crop ({idx})"))
                    {
                        ImGui.Text($"Texture Index: {s.TextureIndex.ToString()}");
                        ImGui.Text($"Top-Left: {s.TopLeft.ToString()}");
                        ImGui.Text($"Bottom-Right: {s.BottomRight.ToString()}");
                        ImGui.TreePop();
                    }
                    idx++;
                }
                ImGui.EndListBox();
            }
            ImGui.Separator();
            if (ImGui.BeginListBox("##textures", new Vector2(-1, space.Y / 2 - 5)))
            {
                int idx = 0;
                foreach (var s in selectedScene.Value.Textures)
                {
                    if (ImGui.TreeNode($"Texture ({idx})"))
                    {
                        ImGui.Text($"Size: {s.ToString()}");
                        ImGui.TreePop();
                    }
                    idx++;
                }
                ImGui.EndListBox();
            }
            selectedScene.Value.AspectRatio = aspectRatio;
            selectedScene.Value.AspectRatio = aspectRatio;
            selectedScene.Value.FrameRate = fps;
        }

        public void DrawCastInspector()
        {
            /// Before you ask
            /// ImGui bindings for C# are mega ass.
            /// Keep that in mind.
            Cast selectedCast = KunaiProject.Instance.SelectionData.SelectedCast;
            if (selectedCast == null)
                return;
            ImGui.SeparatorText("Cast");
            string[] typeStrings = { "Null (No Draw)", "Sprite", "Font" };
            string[] blendingStr = { "NRM", "ADD" };
            string[] filteringStr = { "NONE", "LINEAR" };

            CastPropertyMask unknownFlags = (CastPropertyMask)((BitSet<uint>)selectedCast.Field2C).Value;
            ElementMaterialFlags materialFlags = (ElementMaterialFlags)selectedCast.Field38;
            CastInfo info = selectedCast.Info;
            ElementInheritanceFlags inheritanceFlags = (ElementInheritanceFlags)selectedCast.InheritanceFlags.Value;

            string name = selectedCast.Name;
            int field00 = (int)selectedCast.Field00;
            int type = (int)selectedCast.Field04;
            bool enabled = selectedCast.Enabled;
            bool hideflag = info.HideFlag == 1;
            Vector2 scale = info.Scale;
            bool mirrorX = materialFlags.HasFlag(ElementMaterialFlags.MirrorX);
            bool mirrorY = materialFlags.HasFlag(ElementMaterialFlags.MirrorY);
            Vector2 rectSize = new System.Numerics.Vector2((int)selectedCast.Width, (int)selectedCast.Height);
            Vector2 topLeftVert = selectedCast.TopLeft * KunaiProject.Instance.ViewportSize;
            Vector2 topRightVert = selectedCast.TopRight * KunaiProject.Instance.ViewportSize;
            Vector2 bottomLeftVert = selectedCast.BottomLeft * KunaiProject.Instance.ViewportSize;
            Vector2 bottomRightVert = selectedCast.BottomRight * KunaiProject.Instance.ViewportSize;
            float rotation = info.Rotation;
            Vector2 origin = selectedCast.Origin;
            Vector2 translation = info.Translation;
            Vector4 color = info.Color.ToVec4().Invert();
            Vector4 colorTl = info.GradientTopLeft.ToVec4().Invert();
            Vector4 colorTr = info.GradientTopRight.ToVec4().Invert();
            Vector4 colorBl = info.GradientBottomLeft.ToVec4().Invert();
            Vector4 colorBr = info.GradientBottomRight.ToVec4().Invert();
            bool inheritPosX = inheritanceFlags.HasFlag(ElementInheritanceFlags.InheritXPosition);
            bool inheritPosY = inheritanceFlags.HasFlag(ElementInheritanceFlags.InheritYPosition);
            bool inheritRot = inheritanceFlags.HasFlag(ElementInheritanceFlags.InheritRotation);
            bool inheritCol = inheritanceFlags.HasFlag(ElementInheritanceFlags.InheritColor);
            bool inheritScaleX = inheritanceFlags.HasFlag(ElementInheritanceFlags.InheritScaleX);
            bool inheritScaleY = inheritanceFlags.HasFlag(ElementInheritanceFlags.InheritScaleY);
            int spriteIndex = (int)info.SpriteIndex;
            string text = selectedCast.Text;
            float kerning = BitConverter.ToSingle(BitConverter.GetBytes(selectedCast.Field4C)) * 100;
            string fontname = selectedCast.FontName;

            int indexFont = ms_FontNames.IndexOf(fontname);

            ImGui.InputText("Room Name", ref name, 100, ImGuiInputTextFlags.AutoSelectAll);
            ImGui.InputInt("Field00", ref field00);
            ImGui.Combo("Type", ref type, typeStrings, 3);
            ImGui.SeparatorText("Status");
            ImGui.Checkbox("Enabled", ref enabled);
            ImGui.SameLine();
            ImGui.Checkbox("Hidden", ref hideflag);

            if (ImGui.CollapsingHeader("Dimensions", ImGuiTreeNodeFlags.DefaultOpen))
            {
                var cursorPosAlign = ImGui.GetCursorPosY();
                if (DrawAlignmentGridRadio(ref selectedCast))
                {
                    topLeftVert = selectedCast.TopLeft * KunaiProject.Instance.ViewportSize;
                    topRightVert = selectedCast.TopRight * KunaiProject.Instance.ViewportSize;
                    bottomLeftVert = selectedCast.BottomLeft * KunaiProject.Instance.ViewportSize;
                    bottomRightVert = selectedCast.BottomRight * KunaiProject.Instance.ViewportSize;
                    translation = selectedCast.Info.Translation;
                }
                ImGui.SameLine();

                ImGui.SetCursorPosY(cursorPosAlign);
                ImGui.BeginGroup();
                ImGui.SeparatorText("Invert UV");
                ImGui.PushID("mirrorH");
                ImGui.Checkbox("H", ref mirrorX);
                ImGui.SetItemTooltip("Mirror the cast horizontally.");
                ImGui.PopID();
                ImGui.SameLine();
                ImGui.Checkbox("V", ref mirrorY);
                ImGui.SetItemTooltip("Mirror the cast vertically.");
                ImGui.EndGroup();

                ImGui.BeginGroup();
                ImGui.SeparatorText("Rect Size");
                ImGui.InputFloat2("Quad Size", ref rectSize);
                ImGui.SetItemTooltip("This does not change any value in the tool, this is a leftover from the CellSprite Editor.");
                ImGui.EndGroup();

                ImGui.SeparatorText("Vertices");
                ImGui.SetItemTooltip("These 4 values determine the 4 points that generate the quad (3D element) that the cast will render on. Use with caution");
                ImGui.DragFloat2("Top Left", ref topLeftVert);
                ImGui.DragFloat2("Top Right", ref topRightVert);
                ImGui.DragFloat2("Bottom Left", ref bottomLeftVert);
                ImGui.DragFloat2("Bottom Right", ref bottomRightVert);

            }
            if (ImGui.CollapsingHeader("Transform", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.DragFloat("Rotation", ref rotation);
                ImGui.SetItemTooltip("Rotation in degrees.");
                ImGui.InputFloat2("Origin", ref origin);
                ImGui.SetItemTooltip("Value used to offset the position of the cast (translation), this cannot be changed by animations.");
                ImGui.InputFloat2("Translation", ref translation);
                ImGui.SetItemTooltip("Position of the cast.");
                ImGui.InputFloat2("Scale", ref scale);
            }
            if (ImGui.CollapsingHeader("Color", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.ColorEdit4("Color", ref color);
                ImGui.ColorEdit4("Top Left", ref colorTl);
                ImGui.ColorEdit4("Top Right", ref colorTr);
                ImGui.ColorEdit4("Bottom Left", ref colorBl);
                ImGui.ColorEdit4("Bottom Right", ref colorBr);
            }
            if (ImGui.CollapsingHeader("Inheritance", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Checkbox("Inherit Horizontal Position", ref inheritPosX);
                ImGui.Checkbox("Inherit Vertical Position", ref inheritPosY);
                ImGui.Checkbox("Inherit Rotation", ref inheritRot);
                ImGui.Checkbox("Inherit Color", ref inheritCol);
                ImGui.Checkbox("Inherit Width", ref inheritScaleX);
                ImGui.Checkbox("Inherit Height", ref inheritScaleY);
            }

            if (type == 2)
            {
                if (ImGui.CollapsingHeader("Text", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    //make combo box eventually
                    if (ImGui.Combo("Font", ref indexFont, ms_FontNames.ToArray(), ms_FontNames.Count))
                    {
                        fontname = ms_FontNames[indexFont];
                    }
                    ImGui.PushID("textInput");
                    ImGui.InputText("Text", ref text, 512);
                    ImGui.PopID();
                    ImGui.DragFloat("Kerning", ref kerning, 0.005f);
                }
            }
            if (ImGui.CollapsingHeader("Property Mask", ImGuiTreeNodeFlags.DefaultOpen))
            {
                //if(ImGui.TreeNodeEx("Field2C"))
                //{
                bool flag1Active = !unknownFlags.HasFlag(CastPropertyMask.ApplyTransform);
                bool flag2Active = !unknownFlags.HasFlag(CastPropertyMask.ApplyTranslationX);
                bool flag3Active = !unknownFlags.HasFlag(CastPropertyMask.ApplyTranslationY);
                bool flag4Active = !unknownFlags.HasFlag(CastPropertyMask.ApplyRotation);
                bool flag5Active = !unknownFlags.HasFlag(CastPropertyMask.ApplyScaleX);
                bool flag6Active = !unknownFlags.HasFlag(CastPropertyMask.ApplyScaleY);
                bool flag7Active = !unknownFlags.HasFlag(CastPropertyMask.Flag7);
                bool flag8Active = !unknownFlags.HasFlag(CastPropertyMask.ApplyColor);
                bool flag9Active = !unknownFlags.HasFlag(CastPropertyMask.ApplyColorTL);
                bool flag10Active = !unknownFlags.HasFlag(CastPropertyMask.ApplyColorBL);
                bool flag11Active = !unknownFlags.HasFlag(CastPropertyMask.ApplyColorTR);
                bool flag12Active = !unknownFlags.HasFlag(CastPropertyMask.ApplyColorBR);
                bool flag13Active = !unknownFlags.HasFlag(CastPropertyMask.Flag13);
                bool flag14Active = !unknownFlags.HasFlag(CastPropertyMask.Flag14);
                bool flag15Active = !unknownFlags.HasFlag(CastPropertyMask.Flag15);

                ImGui.Checkbox("Ignore Transform", ref flag1Active);
                if (!flag1Active)
                {
                    ImGui.Indent();
                    ImGui.Checkbox("Ignore Horizontal Translation", ref flag2Active);
                    ImGui.Checkbox("Ignore Vertical Translation", ref flag3Active);
                    ImGui.Unindent();
                }
                ImGui.Checkbox("Ignore Rotation", ref flag4Active);
                ImGui.Checkbox("Ignore Horizontal Scale", ref flag5Active);
                ImGui.Checkbox("Ignore Vertical Scale", ref flag6Active);
                ImGui.Checkbox("Flag7", ref flag7Active);
                ImGui.Checkbox("Ignore Color", ref flag8Active);
                ImGui.Checkbox("Ignore Color TL", ref flag9Active);
                ImGui.Checkbox("Ignore Color BL", ref flag10Active);
                ImGui.Checkbox("Ignore Color TR", ref flag11Active);
                ImGui.Checkbox("Ignore Color BR", ref flag12Active);
                ImGui.Checkbox("Flag13", ref flag13Active);
                ImGui.Checkbox("Flag14", ref flag14Active);
                ImGui.Checkbox("Flag15", ref flag15Active);

                if (!flag1Active) unknownFlags |= CastPropertyMask.ApplyTransform; else unknownFlags &= ~CastPropertyMask.ApplyTransform;
                if (!flag2Active) unknownFlags |= CastPropertyMask.ApplyTranslationX; else unknownFlags &= ~CastPropertyMask.ApplyTranslationX;
                if (!flag3Active) unknownFlags |= CastPropertyMask.ApplyTranslationY; else unknownFlags &= ~CastPropertyMask.ApplyTranslationY;
                if (!flag4Active) unknownFlags |= CastPropertyMask.ApplyRotation; else unknownFlags &= ~CastPropertyMask.ApplyRotation;
                if (!flag5Active) unknownFlags |= CastPropertyMask.ApplyScaleX; else unknownFlags &= ~CastPropertyMask.ApplyScaleX;
                if (!flag6Active) unknownFlags |= CastPropertyMask.ApplyScaleY; else unknownFlags &= ~CastPropertyMask.ApplyScaleY;
                if (!flag7Active) unknownFlags |= CastPropertyMask.Flag7; else unknownFlags &= ~CastPropertyMask.Flag7;
                if (!flag8Active) unknownFlags |= CastPropertyMask.ApplyColor; else unknownFlags &= ~CastPropertyMask.ApplyColor;
                if (!flag9Active) unknownFlags |= CastPropertyMask.ApplyColorTL; else unknownFlags &= ~CastPropertyMask.ApplyColorTL;
                if (!flag10Active) unknownFlags |= CastPropertyMask.ApplyColorBL; else unknownFlags &= ~CastPropertyMask.ApplyColorBL;
                if (!flag11Active) unknownFlags |= CastPropertyMask.ApplyColorTR; else unknownFlags &= ~CastPropertyMask.ApplyColorTR;
                if (!flag12Active) unknownFlags |= CastPropertyMask.ApplyColorBR; else unknownFlags &= ~CastPropertyMask.ApplyColorBR;
                if (!flag13Active) unknownFlags |= CastPropertyMask.Flag13; else unknownFlags &= ~CastPropertyMask.Flag13;
                if (!flag14Active) unknownFlags |= CastPropertyMask.Flag14; else unknownFlags &= ~CastPropertyMask.Flag14;
                if (!flag15Active) unknownFlags |= CastPropertyMask.Flag15; else unknownFlags &= ~CastPropertyMask.Flag15;

                selectedCast.Field2C = (uint)unknownFlags;
                //}
            }
            if (type != 0)
            {

                if (ImGui.CollapsingHeader("Material", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    int blendingType = materialFlags.HasFlag(ElementMaterialFlags.AdditiveBlending) ? 1 : 0;
                    int filterType = materialFlags.HasFlag(ElementMaterialFlags.LinearFiltering) ? 1 : 0;
                    float width = ImGui.GetWindowSize().X / 2 - 80;
                    ImGui.PushItemWidth(width);
                    if (ImGui.Combo("Blending", ref blendingType, blendingStr, 2))
                    {
                        materialFlags = materialFlags.SetFlag(ElementMaterialFlags.AdditiveBlending, blendingType == 1);
                    }
                    ImGui.SameLine();
                    ImGui.PushItemWidth(width);
                    if (ImGui.Combo("Filtering", ref filterType, filteringStr, 2))
                    {
                        materialFlags = materialFlags.SetFlag(ElementMaterialFlags.LinearFiltering, filterType == 1);
                    }
                    ImGui.BeginDisabled(type != (int)DrawType.Sprite);
                    ImGui.InputInt("Selected Sprite", ref spriteIndex);
                    spriteIndex = Math.Clamp(spriteIndex, -1, 32); //can go over 32 for scu

                    if (ImKunai.BeginListBoxCustom("##listpatterns", new Vector2(ImGui.GetContentRegionAvail().X, 160)))
                    {
                        //Draw Pattern selector
                        for (int i = 0; i < selectedCast.SpriteIndices.Length; i++)
                        {
                            int patternIdx = Math.Min(selectedCast.SpriteIndices.Length - 1, (int)selectedCast.SpriteIndices[i]);
                            //Avoid stylecolor issue if the index gets changed
                            int sprIndexCopy = spriteIndex;
                            //Draw button with a different color if its the currently active pattern
                            if (i == sprIndexCopy) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.7f, 0.7f, 0.7f, 0.7f));
                            // Draw sprite preview if it isnt set to the default square
                            if (patternIdx == -1)
                            {
                                ImKunai.EmptyTextureButton(i);
                            }
                            else
                            {
                                Sprite spriteReference = SpriteHelper.TryGetSprite(selectedCast.SpriteIndices[i]);
                                if (spriteReference != null)
                                {

                                    var uvCoords = spriteReference.GetImGuiUv();

                                    bool isPressed;
                                    if (spriteReference.Texture.GlTex == null)
                                    {
                                        isPressed = ImKunai.EmptyTextureButton(i);
                                    }
                                    else
                                    {
                                        isPressed = ImKunai.SpriteImageButton($"##pattern{i}", spriteReference);
                                    }
                                    if (isPressed)
                                        spriteIndex = i;

                                }
                                if (i != selectedCast.SpriteIndices.Length - 1)
                                    ImGui.SameLine();
                            }
                            if (i == sprIndexCopy)
                                ImGui.PopStyleColor();
                        }
                    }
                    ImKunai.EndListBoxCustom();
                    if (!ms_IsEditingCrop)
                    {
                        ImGui.SetNextItemWidth(-1);
                        if (ImGui.Button("Edit current pattern", new Vector2(-1, 32)))
                        {
                            ms_IsEditingCrop = true;
                        }
                    }
                    else
                    {
                        if (ImGui.BeginListBox("##listpatternsselection", new Vector2(-1, 250)))
                        {
                            var result = ImKunai.TextureSelector(KunaiProject.Instance, false);
                            if (result.IsCropSelected())
                            {
                                //Avoid a crash if a user decides to not change this
                                if (spriteIndex == -1)
                                    spriteIndex = 0;
                                selectedCast.SpriteIndices[spriteIndex] = result.GetSpriteIndex();
                            }

                            ImGui.EndListBox();
                        }
                        if (ImGui.Button("Stop editing pattern", new Vector2(-1, 32)))
                        {
                            ms_IsEditingCrop = false;
                        }

                    }
                    ImGui.EndDisabled();
                }
            }
            selectedCast.Name = name;
            selectedCast.Field00 = (uint)field00;
            selectedCast.Field04 = (uint)type;
            selectedCast.Enabled = enabled;
            info.HideFlag = (uint)(hideflag ? 1 : 0);
            //ADD EDIT FOR HIDE FLAG
            if (mirrorX) materialFlags |= ElementMaterialFlags.MirrorX; else materialFlags &= ~ElementMaterialFlags.MirrorX;
            if (mirrorY) materialFlags |= ElementMaterialFlags.MirrorY; else materialFlags &= ~ElementMaterialFlags.MirrorY;
            selectedCast.Width = (uint)rectSize.X;
            selectedCast.Height = (uint)rectSize.Y;
            selectedCast.TopLeft = topLeftVert / KunaiProject.Instance.ViewportSize;
            selectedCast.TopRight = topRightVert / KunaiProject.Instance.ViewportSize;
            selectedCast.BottomLeft = bottomLeftVert / KunaiProject.Instance.ViewportSize;
            selectedCast.BottomRight = bottomRightVert / KunaiProject.Instance.ViewportSize;
            info.Rotation = rotation;
            selectedCast.Origin = origin;
            info.Translation = translation;
            info.Color = color.Invert().ToSharpNeedleColor();
            info.GradientTopLeft = colorTl.Invert().ToSharpNeedleColor();
            info.GradientTopRight = colorTr.Invert().ToSharpNeedleColor();
            info.GradientBottomLeft = colorBl.Invert().ToSharpNeedleColor();
            info.GradientBottomRight = colorBr.Invert().ToSharpNeedleColor();
            info.SpriteIndex = spriteIndex;
            info.Scale = scale;

            if (inheritPosX) inheritanceFlags |= ElementInheritanceFlags.InheritXPosition; else inheritanceFlags &= ~ElementInheritanceFlags.InheritXPosition;
            if (inheritPosY) inheritanceFlags |= ElementInheritanceFlags.InheritYPosition; else inheritanceFlags &= ~ElementInheritanceFlags.InheritYPosition;
            if (inheritRot) inheritanceFlags |= ElementInheritanceFlags.InheritRotation; else inheritanceFlags &= ~ElementInheritanceFlags.InheritRotation;
            if (inheritCol) inheritanceFlags |= ElementInheritanceFlags.InheritColor; else inheritanceFlags &= ~ElementInheritanceFlags.InheritColor;
            if (inheritScaleX) inheritanceFlags |= ElementInheritanceFlags.InheritScaleX; else inheritanceFlags &= ~ElementInheritanceFlags.InheritScaleX;
            if (inheritScaleY) inheritanceFlags |= ElementInheritanceFlags.InheritScaleY; else inheritanceFlags &= ~ElementInheritanceFlags.InheritScaleY;
            selectedCast.InheritanceFlags = (uint)inheritanceFlags;
            selectedCast.Info = info;
            selectedCast.Field38 = (uint)materialFlags;
            selectedCast.FontName = fontname;
            selectedCast.Text = text;
            selectedCast.Field4C = (uint)BitConverter.ToInt32(BitConverter.GetBytes(kerning / 100), 0);
        }
        static Vector2 CalculatePivot(Vector2[] in_Quad)
        {
            // Find the min and max X and Y values from the 4 corners
            float minX = Math.Min(Math.Min(in_Quad[0].X, in_Quad[1].X), Math.Min(in_Quad[2].X, in_Quad[3].X));
            float maxX = Math.Max(Math.Max(in_Quad[0].X, in_Quad[1].X), Math.Max(in_Quad[2].X, in_Quad[3].X));

            float minY = Math.Min(Math.Min(in_Quad[0].Y, in_Quad[1].Y), Math.Min(in_Quad[2].Y, in_Quad[3].Y));
            float maxY = Math.Max(Math.Max(in_Quad[0].Y, in_Quad[1].Y), Math.Max(in_Quad[2].Y, in_Quad[3].Y));

            // Calculate the center of the quad (for simplicity as pivot example)
            Vector2 center = (in_Quad[0] + in_Quad[1] + in_Quad[2] + in_Quad[3]) / 4.0f;

            // Normalize the center based on the bounding box
            float normalizedX = (center.X - minX) / (maxX - minX);
            float normalizedY = (center.Y - minY) / (maxY - minY);

            return new Vector2(normalizedX, normalizedY);
        }
        private bool DrawAlignmentGridRadio(ref Cast in_Cast)
        {
            EAlignmentPivot currentPivot = GetPivot(in_Cast);
            Vector2 quadCenter = CalculatePivot([in_Cast.TopLeft, in_Cast.TopRight, in_Cast.BottomRight, in_Cast.BottomLeft]);
            //Console.WriteLine(quadCenter);
            bool changed = false;
            if (ImGui.RadioButton("##tl", currentPivot == EAlignmentPivot.TopLeft))
            {
                changed = true;
                AlignQuadTo(EAlignmentPivot.TopLeft, ref in_Cast);
            }
            ImGui.SameLine();
            if (ImGui.RadioButton("##tc", currentPivot == EAlignmentPivot.TopCenter))
            {
                changed = true;
                AlignQuadTo(EAlignmentPivot.TopCenter, ref in_Cast);
            }
            ImGui.SameLine();
            if (ImGui.RadioButton("##tr", currentPivot == EAlignmentPivot.TopRight))
            {
                changed = true;
                AlignQuadTo(EAlignmentPivot.TopRight, ref in_Cast);
            }

            if (ImGui.RadioButton("##ml", currentPivot == EAlignmentPivot.MiddleLeft))
            {
                changed = true;
                AlignQuadTo(EAlignmentPivot.MiddleLeft, ref in_Cast);
            }
            ImGui.SameLine();
            if (ImGui.RadioButton("##mc", currentPivot == EAlignmentPivot.MiddleCenter))
            {
                changed = true;
                AlignQuadTo(EAlignmentPivot.MiddleCenter, ref in_Cast);
            }
            ImGui.SameLine();
            if (ImGui.RadioButton("##mr", currentPivot == EAlignmentPivot.MiddleRight))
            {
                changed = true;
                AlignQuadTo(EAlignmentPivot.MiddleRight, ref in_Cast);
            }

            if (ImGui.RadioButton("##bl", currentPivot == EAlignmentPivot.BottomLeft))
            {
                changed = true;
                AlignQuadTo(EAlignmentPivot.BottomLeft, ref in_Cast);
            }
            ImGui.SameLine();
            if (ImGui.RadioButton("##bc", currentPivot == EAlignmentPivot.BottomCenter))
            {
                changed = true;
                AlignQuadTo(EAlignmentPivot.BottomCenter, ref in_Cast);
            }
            ImGui.SameLine();
            if (ImGui.RadioButton("##br", currentPivot == EAlignmentPivot.BottomRight))
            {
                changed = true;
                AlignQuadTo(EAlignmentPivot.BottomRight, ref in_Cast);
            }
            return changed;
        }

        internal static void Reset()
        {
            KunaiProject.Instance.SelectionData.SelectedCast = null;
            KunaiProject.Instance.SelectionData.SelectedScene = new();
        }

        public static Vector2 CalculateQuadCenter(Vector2 in_TopLeft, Vector2 in_TopRight, Vector2 in_BottomLeft, Vector2 in_BottomRight)
        {
            return new Vector2(
                (in_TopLeft.X + in_TopRight.X + in_BottomLeft.X + in_BottomRight.X) / 4,
                (in_TopLeft.Y + in_TopRight.Y + in_BottomLeft.Y + in_BottomRight.Y) / 4
            );
        }

        EAlignmentPivot InvertPivot(EAlignmentPivot in_Pivot)
        {
            EAlignmentPivot newPivot = EAlignmentPivot.None;
            switch (in_Pivot)
            {
                case EAlignmentPivot.TopLeft:
                    newPivot = EAlignmentPivot.BottomRight;
                    break;
                case EAlignmentPivot.TopCenter:
                    newPivot = EAlignmentPivot.BottomCenter;
                    break;
                case EAlignmentPivot.TopRight:
                    newPivot = EAlignmentPivot.BottomLeft;
                    break;
                case EAlignmentPivot.MiddleLeft:
                    newPivot = EAlignmentPivot.MiddleRight;
                    break;
                case EAlignmentPivot.MiddleCenter:
                    newPivot = EAlignmentPivot.MiddleCenter;
                    break;
                case EAlignmentPivot.MiddleRight:
                    newPivot = EAlignmentPivot.MiddleLeft;
                    break;
                case EAlignmentPivot.BottomLeft:
                    newPivot = EAlignmentPivot.TopRight;
                    break;
                case EAlignmentPivot.BottomCenter:
                    newPivot = EAlignmentPivot.TopCenter;
                    break;
                case EAlignmentPivot.BottomRight:
                    newPivot = EAlignmentPivot.TopLeft;
                    break;
            }
            return newPivot;
        }
        EAlignmentPivot GetPivot(Cast in_Cast)
        {
            var renderer = KunaiProject.Instance;

            var size = new Vector2(in_Cast.Width, in_Cast.Height) * 2;
            var topLeft = in_Cast.TopLeft / (size / renderer.ViewportSize);
            var bottomRight = in_Cast.BottomRight / (size / renderer.ViewportSize);
            var topRight = in_Cast.TopRight / (size / renderer.ViewportSize);
            var bottomLeft = in_Cast.BottomLeft / (size / renderer.ViewportSize);
            EAlignmentPivot returnValue = EAlignmentPivot.None;
            if (topLeft == new Vector2(-1, -1) && topRight == new Vector2(0, -1) && bottomLeft == new Vector2(-1, 0) && bottomRight == new Vector2(0, 0))
                returnValue = EAlignmentPivot.TopLeft;

            if (topLeft == new Vector2(-0.5f, -1) && topRight == new Vector2(0.5f, -1) && bottomLeft == new Vector2(-0.5f, 0) && bottomRight == new Vector2(0.5f, 0))
                returnValue = EAlignmentPivot.TopCenter;

            if (topLeft == new Vector2(0, -1) && topRight == new Vector2(1, -1) && bottomLeft == new Vector2(0, 0) && bottomRight == new Vector2(1, 0))
                returnValue = EAlignmentPivot.TopRight;

            if (topLeft == new Vector2(-1, -0.5f) && topRight == new Vector2(0, -0.5f) && bottomLeft == new Vector2(-1, 0.5f) && bottomRight == new Vector2(0, 0.5f))
                returnValue = EAlignmentPivot.MiddleLeft;

            if (topLeft == new Vector2(-0.5f, -0.5f) && topRight == new Vector2(0.5f, -0.5f) && bottomLeft == new Vector2(-0.5f, 0.5f) && bottomRight == new Vector2(0.5f, 0.5f))
                returnValue = EAlignmentPivot.MiddleCenter;

            if (topLeft == new Vector2(0, -0.5f) && topRight == new Vector2(1, -0.5f) && bottomLeft == new Vector2(0, 0.5f) && bottomRight == new Vector2(1, 0.5f))
                returnValue = EAlignmentPivot.MiddleRight;

            if (topLeft == new Vector2(-1, 0) && topRight == new Vector2(0, 0) && bottomLeft == new Vector2(-1, 1) && bottomRight == new Vector2(0, 1))
                returnValue = EAlignmentPivot.BottomLeft;

            if (topLeft == new Vector2(-0.5f, 0) && topRight == new Vector2(0.5f, 0) && bottomLeft == new Vector2(-0.5f, 1) && bottomRight == new Vector2(0.5f, 1))
                returnValue = EAlignmentPivot.BottomCenter;

            if (topLeft == new Vector2(0, 0) && topRight == new Vector2(1, 0) && bottomLeft == new Vector2(0, 1) && bottomRight == new Vector2(1, 1))
                returnValue = EAlignmentPivot.BottomRight;
            return InvertPivot(returnValue);
        }
        private void AlignQuadTo(EAlignmentPivot in_AlignmentPosition, ref Cast in_Cast)
        {
            Vector2 quadCenter = CalculateQuadCenter(in_Cast.TopLeft, in_Cast.TopRight, in_Cast.BottomLeft, in_Cast.BottomRight);

            var diff1 = in_Cast.Position - quadCenter;
            Vector2 topLeft = new Vector2(0, 0), topRight = new Vector2(0, 0), bottomLeft = new Vector2(0, 0), bottomRight = new Vector2(0, 0);
            Vector2 size = (new Vector2(in_Cast.Width, in_Cast.Height) * 2) / KunaiProject.Instance.ViewportSize;
            switch (InvertPivot(in_AlignmentPosition))
            {
                case EAlignmentPivot.TopLeft:
                    topLeft = new Vector2(-1, -1);
                    topRight = new Vector2(0, -1);
                    bottomLeft = new Vector2(-1, 0);
                    bottomRight = new Vector2(0, 0);
                    break;

                case EAlignmentPivot.TopCenter:
                    topLeft = new Vector2(-0.5f, -1);
                    topRight = new Vector2(0.5f, -1);
                    bottomLeft = new Vector2(-0.5f, 0);
                    bottomRight = new Vector2(0.5f, 0);
                    break;

                case EAlignmentPivot.TopRight:
                    topLeft = new Vector2(0, -1);
                    topRight = new Vector2(1, -1);
                    bottomLeft = new Vector2(0, 0);
                    bottomRight = new Vector2(1, 0);
                    break;

                case EAlignmentPivot.MiddleLeft:
                    topLeft = new Vector2(-1, -0.5f);
                    topRight = new Vector2(0, -0.5f);
                    bottomLeft = new Vector2(-1, 0.5f);
                    bottomRight = new Vector2(0, 0.5f);
                    break;

                case EAlignmentPivot.MiddleCenter:
                    topLeft = new Vector2(-0.5f, -0.5f);
                    topRight = new Vector2(0.5f, -0.5f);
                    bottomLeft = new Vector2(-0.5f, 0.5f);
                    bottomRight = new Vector2(0.5f, 0.5f);
                    break;

                case EAlignmentPivot.MiddleRight:
                    topLeft = new Vector2(0, -0.5f);
                    topRight = new Vector2(1, -0.5f);
                    bottomLeft = new Vector2(0, 0.5f);
                    bottomRight = new Vector2(1, 0.5f);
                    break;

                case EAlignmentPivot.BottomLeft:
                    topLeft = new Vector2(-1, 0);
                    topRight = new Vector2(0, 0);
                    bottomLeft = new Vector2(-1, 1);
                    bottomRight = new Vector2(0, 1);
                    break;

                case EAlignmentPivot.BottomCenter:
                    topLeft = new Vector2(-0.5f, 0);
                    topRight = new Vector2(0.5f, 0);
                    bottomLeft = new Vector2(-0.5f, 1);
                    bottomRight = new Vector2(0.5f, 1);
                    break;

                case EAlignmentPivot.BottomRight:
                    topLeft = new Vector2(0, 0);
                    topRight = new Vector2(1, 0);
                    bottomLeft = new Vector2(0, 1);
                    bottomRight = new Vector2(1, 1);
                    break;
            }
            // Calculate the offset to move the center to the alignment position
            in_Cast.TopLeft = topLeft * size;
            in_Cast.TopRight = topRight * size;
            in_Cast.BottomLeft = bottomLeft * size;
            in_Cast.BottomRight = bottomRight * size;

            Vector2 quadCenter2 = CalculateQuadCenter(in_Cast.TopLeft, in_Cast.TopRight, in_Cast.BottomLeft, in_Cast.BottomRight);
            var diff2 = in_Cast.Position - quadCenter2;
            var t = in_Cast.Info;
            var diff = (((quadCenter2 - quadCenter)));
            in_Cast.Info = t;
        }
        bool IsQuadAligned(Vector2 in_AlignmentPosition, ref Cast in_Cast)
        {
            Vector2 quadCenter = CalculateQuadCenter(in_Cast.TopLeft, in_Cast.TopRight, in_Cast.BottomLeft, in_Cast.BottomRight);

            // Check if the quad's center is within a threshold of the alignment
            float tolerance = 0.01f;
            return (Math.Abs(quadCenter.X - in_AlignmentPosition.X) < tolerance) &&
                   (Math.Abs(quadCenter.Y - in_AlignmentPosition.Y) < tolerance);
        }

        public void OnReset(IProgramProject in_Renderer)
        {
            throw new NotImplementedException();
        }

        public void Render(IProgramProject in_Renderer)
        {
            var renderer = (KunaiProject)in_Renderer;
            ImGui.SetNextWindowPos(new System.Numerics.Vector2((ImGui.GetWindowViewport().Size.X / 4.5f) * 3.5f, MenuBarWindow.MenuBarHeight), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(ImGui.GetWindowViewport().Size.X / 4.5f, (ImGui.GetWindowViewport().Size.Y - AnimationsWindow.AnimationButtonHeight) - MenuBarWindow.MenuBarHeight), ImGuiCond.Always);
            if (ImGui.Begin("Inspector", MainWindow.WindowFlags))
            {
                if (renderer.WorkProjectCsd != null)
                {
                    ms_FontNames.Clear();
                    foreach (KeyValuePair<string, Font> font in renderer.WorkProjectCsd.Project.Fonts)
                    {
                        ms_FontNames.Add(font.Key);
                    }
                    switch (SelectionType)
                    {
                        case ESelectionType.Scene:
                            {
                                DrawSceneInspector();
                                break;
                            }
                        case ESelectionType.Cast:
                            {
                                DrawCastInspector();
                                break;
                            }
                    }
                }
                ImGui.End();
            }
        }
    }
}