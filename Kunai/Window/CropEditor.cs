using HekonrayBase;
using HekonrayBase.Base;
using Hexa.NET.ImGui;
using Kunai.ShurikenRenderer;
using Shuriken.Rendering;
using System;
using System.Numerics;

namespace Kunai.Window
{
    public class CropEditor : Singleton<CropEditor>, IWindow
    {
        public static float ZoomFactor = 1;
        public static bool Enabled = false;
        int m_SelectedIndex = 0;
        int m_SelectedSpriteIndex = 0;
        bool m_ShowAllCoords = false;
        bool m_CutMenuOpen = false;
        int m_CutType;
        Vector2 m_CutParam;
        Vector2 m_ViewportStart;
        bool isDragging = false;
        public void OnReset(IProgramProject in_Renderer)
        {
            throw new NotImplementedException();
        }
        private void DrawQuadList(SCenteredImageData in_Data)
        {
            var cursorpos = ImGui.GetItemRectMin();
            Vector2 screenPos = in_Data.Position + in_Data.ImagePosition - new Vector2(3, 2);


            var viewSize = in_Data.ImageSize;

            for (int i = 0; i < SpriteHelper.Textures[m_SelectedIndex].Sprites.Count; i++)
            {
                int spriteIdx = SpriteHelper.Textures[m_SelectedIndex].Sprites[i];
                var sprite = SpriteHelper.Sprites[spriteIdx];
                var qTopLeft = new Vector2(sprite.OriginalLeft, sprite.OriginalTop);
                var qTopRight = new Vector2(sprite.OriginalRight, sprite.OriginalTop);
                var qBotLeft = new Vector2(sprite.OriginalLeft, sprite.OriginalBottom);
                var qBotRight = new Vector2(sprite.OriginalRight, sprite.OriginalBottom);
                Vector2 pTopLeft = screenPos + new Vector2(qTopLeft.X * viewSize.X, qTopLeft.Y * viewSize.Y);
                Vector2 pBotRight = screenPos + new Vector2(qBotRight.X * viewSize.X, qBotRight.Y * viewSize.Y);
                Vector2 pTopRight = screenPos + new Vector2(qTopRight.X * viewSize.X, qTopRight.Y * viewSize.Y);
                Vector2 pBotLeft = screenPos + new Vector2(qBotLeft.X * viewSize.X, qBotLeft.Y * viewSize.Y);

                Vector2 mousePos = ImGui.GetMousePos();

                if (m_ShowAllCoords)
                    ImGui.GetWindowDrawList().AddQuad(pTopLeft, pTopRight, pBotRight, pBotLeft, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 1)), 1.5f);
                if (KunaiMath.IsPointInRect(mousePos, pTopLeft, pTopRight, pBotRight, pBotLeft) || i == m_SelectedSpriteIndex)
                {
                    //Add selection box
                    ImGui.GetWindowDrawList().AddQuad(pTopLeft, pTopRight, pBotRight, pBotLeft, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0.3f, 0, 1)), 3);
                 //   if (MainWindow.IsMouseLeftTapped)
                    {
                        m_SelectedSpriteIndex = i;
                    }
                    //if (ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                    //{
                    //    Vector2 mouseInWindow = mousePos - in_WindowPos;
                    //
                    //    Vector2 adjustedMousePos = mouseInWindow - screenPos;
                    //    quad.OriginalData.OriginCast.Position = adjustedMousePos / Renderer.ViewportSize;
                    //}
                }

            }
        }
        void CreateCropGrid(Vector2 in_IndividualSize)
        {
            var texture = SpriteHelper.Textures[m_SelectedIndex];
            Vector2 count = new Vector2(
                MathF.Floor(texture.Size.X / in_IndividualSize.X),
                MathF.Floor(texture.Size.Y / in_IndividualSize.Y)
            );

            // Arbitrary limit
            if (count.X > 256 || count.Y > 256) return;
            if (count.X == 0 || count.Y == 0) return;

            for (int y = 0; y < count.Y; y++)
            {
                for (int x = 0; x < count.X; x++)
                {
                    // Calculate position in [0,1] range
                    var position = new Vector2(
                        (x * in_IndividualSize.X) / texture.Size.X,
                        (y * in_IndividualSize.Y) / texture.Size.Y
                    );

                    // Calculate size in [0,1] range
                    var newSize = new Vector2(
                        in_IndividualSize.X / texture.Size.X,
                        in_IndividualSize.Y / texture.Size.Y
                    );

                    // Create and add sprite with normalized position and size
                    var newSprite = SpriteHelper.CreateSprite(texture, position, newSize);
                    texture.Sprites.Add(newSprite);
                }
            }
        }
        public void Render(IProgramProject in_Renderer)
        {
            var renderer = (KunaiProject)in_Renderer;
            if (renderer.WorkProjectCsd == null)
                return;
            if (Enabled)
            {
                DrawCropCutMenu();
                if (ImGui.Begin("Crop", ref Enabled, ImGuiWindowFlags.MenuBar))
                {
                    if (ImGui.BeginMenuBar())
                    {
                        if (ImGui.BeginMenu("Add"))
                        {
                            if (ImGui.MenuItem("Add Texture"))
                            {
                                var res = NativeFileDialogSharp.Dialog.FileOpen("dds");
                                if (res.IsOk)
                                {
                                    if (!SpriteHelper.DoesTextureExist(res.Path))
                                    {
                                        SpriteHelper.AddTexture(new Texture(res.Path));
                                    }
                                    else
                                    {
                                        Application.ShowMessageBoxCross("Error", "A texture with this exact name already exists!\nPlease rename the target texture and try again.");
                                    }
                                }
                            }
                            ImGui.EndMenu();
                        }
                        if (ImGui.BeginMenu("Edit"))
                        {
                            if (ImGui.MenuItem("Generate Crops"))
                            {
                                ImGui.OpenPopup("##generatecrops");
                                m_CutMenuOpen = true;
                            }
                            ImGui.Separator();
                            if (ImGui.MenuItem("Show all crops on texture", m_ShowAllCoords))
                                m_ShowAllCoords = !m_ShowAllCoords;

                            ImGui.EndMenu();
                        }
                        ImGui.EndMenuBar();
                    }
                    ZoomFactor = Math.Clamp(ZoomFactor, 0.5f, 5);
                    var padding = ImGui.GetStyle().ItemSpacing;
                    var size1 = (ImGui.GetWindowSize().X / 4) - padding.X;
                    if (ImGui.BeginListBox("##texturelist", new Vector2(size1, -1)))
                    {
                        int idx = 0;
                        var result = ImKunai.TextureSelector(renderer, true);
                      
                        if (result.TextureIndex != -2)
                            m_SelectedIndex = result.TextureIndex;
                        if (result.SpriteIndex != -2)
                            m_SelectedSpriteIndex = result.SpriteIndex;

                   


                        ImGui.EndListBox();
                    }
                    ImGui.SameLine();
                    Vector2 availableSize = new Vector2(ImGui.GetWindowSize().X / 2, ImGui.GetContentRegionAvail().Y);
                    Vector2 viewportPos = ImGui.GetWindowPos() + ImGui.GetCursorPos();

                    Vector2 textureSize = new Vector2(0, 0);
                    if (m_SelectedIndex < SpriteHelper.Textures.Count)
                    {
                        textureSize = SpriteHelper.Textures[m_SelectedIndex].Size;
                    }

                    Vector2 imageSize;
                    if (textureSize.X > textureSize.Y)
                        imageSize = new Vector2(availableSize.Y, (textureSize.Y / textureSize.X) * availableSize.Y);
                    else
                        imageSize = new Vector2(availableSize.X, (textureSize.X / textureSize.Y) * availableSize.X);

                    //Texture Image
                    var size2 = ImGui.GetContentRegionAvail().X - size1;

                    if (m_SelectedIndex < SpriteHelper.Textures.Count)
                        ImKunai.ImageViewport("##cropEdit", new Vector2(size2, -1), SpriteHelper.Textures[m_SelectedIndex].Size.Y / SpriteHelper.Textures[m_SelectedIndex].Size.X, ZoomFactor, new ImTextureID(SpriteHelper.Textures[m_SelectedIndex].GlTex.Id), DrawQuadList, new Vector4(0.5f, 0.5f, 0.5f, 1));

                    bool windowHovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows) && ImGui.IsItemHovered();
                    if (windowHovered)
                        ZoomFactor += ImGui.GetIO().MouseWheel / 5;
                    ImGui.SameLine();

                    if (m_SelectedIndex < SpriteHelper.Textures.Count)
                        if (ImGui.BeginListBox("##texturelist2", new Vector2(size1, -1)))
                        {
                            var texture = SpriteHelper.Textures[m_SelectedIndex];
                            var sprite = SpriteHelper.Sprites[texture.Sprites[m_SelectedSpriteIndex]];
                            Vector2 spriteStart = sprite.Start;
                            Vector2 spriteSize = sprite.Dimensions;
                            ImGui.SeparatorText("Texture Info");
                            ImGui.Text($"Name: {texture.Name}");
                            ImGui.Text($"Width: {texture.Width}");
                            ImGui.Text($"Height: {texture.Height}");
                            ImGui.SeparatorText("Crop");
                            ImGui.Text($"Currently editing: Crop ({m_SelectedSpriteIndex})");
                            ImGui.DragFloat2("Position", ref spriteStart, "%.0f");
                            ImGui.DragFloat2("Dimension", ref spriteSize, "%.0f");
                            spriteStart.X = Math.Clamp(spriteStart.X, 0, texture.Size.X);
                            spriteStart.Y = Math.Clamp(spriteStart.Y, 0, texture.Size.Y);

                            spriteSize.X = Math.Clamp(spriteSize.X, 1, texture.Size.X);
                            spriteSize.Y = Math.Clamp(spriteSize.Y, 1, texture.Size.Y);
                            sprite.Start = spriteStart;
                            sprite.Dimensions = spriteSize;
                            sprite.Recalculate();
                            ImGui.EndListBox();
                        }

                    ImGui.End();
                }
            }
        }

        private void DrawCropCutMenu()
        {
            if (m_CutMenuOpen)
            {
                ImGui.OpenPopup("##generatecrops");
                Vector2 windowSize = new Vector2(500, 160);

                // Calculate centered position
                var viewport = ImGui.GetMainViewport();
                Vector2 centerPos = new Vector2(
                    viewport.WorkPos.X + (viewport.WorkSize.X - windowSize.X) * 0.5f,
                    viewport.WorkPos.Y + (viewport.WorkSize.Y - windowSize.Y) * 0.5f
                );
                ImGui.SetNextWindowPos(centerPos);
                ImGui.SetNextWindowSize(windowSize);
                if (ImGui.BeginPopupModal("##generatecrops", ref m_CutMenuOpen, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize))
                {
                    var texture = SpriteHelper.Textures[m_SelectedIndex];
                    ImGui.Combo("Type", ref m_CutType, ["Size", "Count"], 2);

                    ImGui.InputFloat2(m_CutType == 0 ? "Individual Size" : "Grid Count", ref m_CutParam);
                    ImGui.Separator();
                    Vector2 estimate = m_CutType == 0 ? texture.Size / m_CutParam : m_CutParam;
                    float estimateCount = (int)(estimate.Y * estimate.X);
                    if (estimateCount < 0 || estimateCount > 256)
                        estimateCount = 0;

                    ImGui.Text($"{estimateCount} crops will be generated");
                    ImGui.Separator();
                    if (ImGui.Button("Execute"))
                    {
                        switch (m_CutType)
                        {
                            case 0:
                                {
                                    CreateCropGrid(m_CutParam);
                                    break;
                                }
                            case 1:
                                {
                                    CreateCropGrid(texture.Size / m_CutParam);
                                    break;
                                }
                        }
                        m_CutMenuOpen = false;
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Cancel"))
                    {
                        m_CutMenuOpen = false;
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
                }
            }
        }
    }
}
