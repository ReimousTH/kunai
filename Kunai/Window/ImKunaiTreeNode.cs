﻿using Hexa.NET.ImGui;
using Hexa.NET.Utilities.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Kunai.Window
{
    public static class ImKunaiTreeNode
    {
        public struct SIconData
        {
            public string Icon;
            public Vector4 Color = Vector4.One;
            public SIconData(string icon)
            {
                Icon = icon;
            }
            public SIconData(string icon, Vector4 color)
            {
                Icon = icon;
                Color = color;
            }
            public bool IsNull() => string.IsNullOrEmpty(Icon);
        }
        public static unsafe bool SpriteImageButton(string in_ID, Shuriken.Rendering.Sprite in_Spr)
        {
            //This is so stupid, this is how youre supposed to do it according to the HexaNET issues
            unsafe
            {
                const int bufferSize = 256;
                byte* buffer = stackalloc byte[bufferSize];
                StrBuilder sb = new(buffer, bufferSize);
                sb.Append($"##{in_ID}");
                sb.End();
                var uvCoords = in_Spr.GetImGuiUV();
                //Draw sprite
                return ImGui.ImageButton(sb, new ImTextureID(in_Spr.Texture.GlTex.Id), new System.Numerics.Vector2(50, 50), uvCoords[0], uvCoords[1]);

            }
        }
        public static unsafe void SpriteImage(string in_ID, Shuriken.Rendering.Sprite in_Spr)
        {
            unsafe
            {
                const int bufferSize = 256;
                byte* buffer = stackalloc byte[bufferSize];
                StrBuilder sb = new(buffer, bufferSize);
                sb.Append($"##{in_ID}");
                sb.End();
                var uvCoords = in_Spr.GetImGuiUV();
                //Draw sprite
                ImGui.Image(new ImTextureID(in_Spr.Texture.GlTex.Id), new System.Numerics.Vector2(50, 50), uvCoords[0], uvCoords[1]);

            }
        }
        public static bool VisibilityNode(string in_Name, ref bool in_Visibile, ref bool in_IsSelected, Action in_RightClickAction = null, bool in_ShowArrow = true, SIconData in_Icon = new(), string in_ID = "")
        {
            bool returnVal = true;
            bool idPresent = !string.IsNullOrEmpty(in_ID);
            string idName = idPresent ? in_ID : in_Name;
            //Make header fit the content
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new System.Numerics.Vector2(0, 3));
            var isLeaf = !in_ShowArrow ? ImGuiTreeNodeFlags.Leaf : ImGuiTreeNodeFlags.None;
            returnVal = ImGui.TreeNodeEx($"##{idName}header", isLeaf | ImGuiTreeNodeFlags.SpanFullWidth | ImGuiTreeNodeFlags.FramePadding | ImGuiTreeNodeFlags.AllowOverlap);
            ImGui.PopStyleVar();
            //Rightclick action
            if (in_RightClickAction != null)
            {
                if (ImGui.BeginPopupContextItem())
                {
                    in_RightClickAction.Invoke();
                    ImGui.EndPopup();
                }
            }                
            //Visibility checkbox
            ImGui.SameLine(0, 1 * ImGui.GetStyle().ItemSpacing.X);
            ImGui.Checkbox($"##{idName}togg", ref in_Visibile);
            ImGui.SameLine(0, 1 * ImGui.GetStyle().ItemSpacing.X);
            //Show text with icon (cant have them merged because of stupid imgui c# bindings)

            Vector2 p = ImGui.GetCursorScreenPos();
            ImGui.SetNextItemAllowOverlap();

            //Setup button so that the borders and background arent seen unless its hovered
            ImGui.PushStyleColor(ImGuiCol.FrameBg, ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 0)));
            ImGui.PushStyleColor(ImGuiCol.Border, ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 0)));
            ImGui.PushStyleColor(ImGuiCol.Button, ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 0)));
            bool iconPresent = !in_Icon.IsNull();
            in_IsSelected = ImGui.Button($"##invButton{idName}", new Vector2(-1, 25));
            ImGui.PopStyleColor(3);

            //Begin drawing text & icon if it exists
            ImGui.SetNextItemAllowOverlap();
            ImGui.PushID($"##text{idName}");
            ImGui.BeginGroup();

            if (iconPresent)
            {
                //Draw icon
                ImGui.PushFont(ImGuiController.FontAwesomeFont);
                ImGui.SameLine(0, 0);
                ImGui.SetNextItemAllowOverlap();
                ImGui.SetCursorScreenPos(p);
                ImGui.TextColored(in_Icon.Color, in_Icon.Icon);
                ImGui.PopFont();
                ImGui.SameLine(0, 0);
            }
            else
            {
                //Set size for the text as if there was an icon
                ImGui.SetCursorScreenPos(p + new Vector2(0, 2));
            }
            ImGui.SetNextItemAllowOverlap();
            ImGui.Text(iconPresent ? $" {in_Name}" : in_Name);

            ImGui.EndGroup();
            ImGui.PopID();
            return returnVal;
        }
        public static void ItemRowsBackground(Vector4 color, float lineHeight = -1.0f)
        {
            var drawList = ImGui.GetWindowDrawList();
            var style = ImGui.GetStyle();

            if (lineHeight < 0)
            {
                lineHeight = ImGui.GetTextLineHeight();
            }
            lineHeight += style.ItemSpacing.Y;

            float scrollOffsetH = ImGui.GetScrollX();
            float scrollOffsetV = ImGui.GetScrollY();
            float scrolledOutLines = (float)Math.Floor(scrollOffsetV / lineHeight);
            scrollOffsetV -= lineHeight * scrolledOutLines;

            Vector2 clipRectMin = new Vector2(ImGui.GetWindowPos().X, ImGui.GetWindowPos().Y);
            Vector2 clipRectMax = new Vector2(clipRectMin.X + ImGui.GetWindowWidth(), clipRectMin.Y + ImGui.GetWindowHeight());

            if (ImGui.GetScrollMaxX() > 0)
            {
                clipRectMax.Y -= style.ScrollbarSize;
            }

            drawList.PushClipRect(clipRectMin, clipRectMax);

            bool isOdd = ((int)(scrolledOutLines) % 2) == 0;

            float yMin = clipRectMin.Y - scrollOffsetV + ImGui.GetCursorPosY();
            float yMax = clipRectMax.Y - scrollOffsetV + lineHeight;
            float xMin = clipRectMin.X + scrollOffsetH + ImGui.GetContentRegionAvail().X;
            float xMax = clipRectMin.X + scrollOffsetH + ImGui.GetContentRegionAvail().X;

            for (float y = yMin; y < yMax; y += lineHeight, isOdd = !isOdd)
            {
                if (isOdd)
                {
                    drawList.AddRectFilled(new Vector2(xMin, y - style.ItemSpacing.Y), new Vector2(xMax, y + lineHeight), ImGui.ColorConvertFloat4ToU32(color));
                }
            }
        }
    }
}
