using HekonrayBase;
using HekonrayBase.Base;
using Hexa.NET.ImGui;
using IconFonts;
using Kunai.ShurikenRenderer;
using SharpNeedle.Framework.Ninja.Csd;
using Shuriken.Rendering;
using System;
using System.Numerics;
using static Kunai.Window.ImKunai;

namespace Kunai.Window
{
    public class HierarchyWindow : Singleton<HierarchyWindow>, IWindow
    {
        private static TempSearchBox ms_SearchBox = new TempSearchBox();
        private static void RecursiveCastWidget(SVisibilityData.SScene in_Scene, Cast in_Cast)
        {
            bool selectedcast = false;
            var vis = in_Scene.GetVisibility(in_Cast);
            if (vis == null)
                return;
            SIconData icon = new();
            switch(in_Cast.GetDrawType())
            {
                case Shuriken.Rendering.DrawType.Sprite:
                    {
                        icon = NodeIconResource.CastSprite;
                        break;
                    }
                case Shuriken.Rendering.DrawType.None:
                    {
                        icon = NodeIconResource.CastNull;
                        break;
                    }
                case Shuriken.Rendering.DrawType.Font:
                    {
                        icon = NodeIconResource.CastFont;
                        break;
                    }
            }
            //Casts
            if (ImKunai.VisibilityNode(in_Cast.Name, ref vis.Active, ref selectedcast, CastRightClickAction(vis),in_ShowArrow: in_Cast.Children.Count > 0, in_Icon: icon, in_Id: $"##{in_Cast.Name}_{vis.Id}"))
            {
                for (int x = 0; x < in_Cast.Children.Count; x++)
                {
                    RecursiveCastWidget(in_Scene, in_Cast.Children[x]);
                }
                ImGui.TreePop();
            }
            if (selectedcast)
            {
                InspectorWindow.SelectScene(in_Scene.Scene);
                InspectorWindow.SelectCast(in_Cast);
            }
        }
        public static Action SceneNodeRightClickAction(SVisibilityData.SNode in_Cast)
        {
            Action rightClick = () =>
            {
                if (ImGui.Selectable("New Scene"))
                {
                    CreationHelper.CreateNewScene(in_Cast);
                }
                //if (ImGui.Selectable("Delete"))
                //    in_Cast.Parent.Remove(in_Cast);
            };
            return rightClick;
        }
        public static Action CastRightClickAction(SVisibilityData.SCast in_Cast)
        {
            Action rightClick = () =>
            {
                if (ImGui.BeginMenu("New Cast..."))
                {
                    if (ImGui.MenuItem("Null Cast"))
                    {
                        CreationHelper.CreateNewCast(in_Cast, DrawType.None);
                    }
                    if (ImGui.MenuItem("Sprite Cast"))
                    {
                        CreationHelper.CreateNewCast(in_Cast, DrawType.Sprite);
                    }
                    if (ImGui.MenuItem("Font Cast"))
                    {
                        CreationHelper.CreateNewCast(in_Cast, DrawType.Font);
                    }
                    ImGui.EndMenu();
                }
                if (ImGui.Selectable("Delete"))
                    in_Cast.Parent.Remove(in_Cast);
            };
            return rightClick;
        }
        public static Action SceneRightClickAction(SVisibilityData.SScene in_Scene)
        {
            Action rightClick = () =>
            {
                if (ImGui.BeginMenu("New Cast..."))
                {
                    if (ImGui.MenuItem("Null Cast"))
                    {
                        CreationHelper.CreateNewCast(in_Scene, DrawType.None);
                    }
                    if (ImGui.MenuItem("Sprite Cast"))
                    {
                        CreationHelper.CreateNewCast(in_Scene, DrawType.Sprite);
                    }
                    if (ImGui.MenuItem("Font Cast"))
                    {
                        CreationHelper.CreateNewCast(in_Scene, DrawType.Font);
                    }
                    ImGui.EndMenu();
                }
                if (ImGui.Selectable("Delete"))
                    in_Scene.Parent.Remove(in_Scene);
            };
            return rightClick;
        }
        private static void DrawSceneNode(SVisibilityData.SNode in_VisNode)
        {
            bool selectedNode = false;
            bool selectedScene = false;
            //Scene Node
            if (ImKunai.VisibilityNode($"{in_VisNode.Node.Key}", ref in_VisNode.Active, ref selectedNode, SceneNodeRightClickAction(in_VisNode), in_Icon: NodeIconResource.SceneNode))
            {
                foreach (var inNode in in_VisNode.Nodes)
                    DrawSceneNode(inNode);
                for (int i = 0; i < in_VisNode.Scene.Count; i++)
                {
                    SVisibilityData.SScene scene = in_VisNode.Scene[i];
                    //Scene
                    if (ImKunai.VisibilityNode(scene.Scene.Key, ref scene.Active, ref selectedScene, SceneRightClickAction(scene), in_Icon: NodeIconResource.Scene))
                    {
                        for (int x = 0; x < scene.Scene.Value.Families.Count; x++)
                        {
                            var family = scene.Scene.Value.Families[x];
                            var castFamilyRoot = family.Casts[0];
                            RecursiveCastWidget(scene, castFamilyRoot);

                        }
                        ImGui.TreePop();
                    }
                    if (selectedScene)
                    {
                        InspectorWindow.SelectScene(scene.Scene);
                    }
                }

                ImGui.TreePop();
            }
            //if (ImGui.TreeNodeEx(f.Node.Key))
            //{
            //    foreach (var g in f.Scene)
            //    {
            //        if (ImGui.TreeNodeEx(f.Node.Key))
            //        {
            //            ImGui.Checkbox($"##{g.Scene.Key}togg", ref g.Active);
            //        ImGui.SameLine();
            //        ImGui.Text(g.Scene.Key);
            //    }
            //    ImGui.TreePop();
            //}

        }

        public void OnReset(IProgramProject in_Renderer)
        {
            throw new NotImplementedException();
        }

        public void Render(IProgramProject in_Renderer)
        {
            var renderer = (KunaiProject)in_Renderer;
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, MenuBarWindow.MenuBarHeight), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(ImGui.GetWindowViewport().Size.X / 4.5f, (ImGui.GetWindowViewport().Size.Y - AnimationsWindow.AnimationButtonHeight) - MenuBarWindow.MenuBarHeight), ImGuiCond.Always);
            if (ImGui.Begin("Hierarchy", MainWindow.WindowFlags))
            {
                ImGui.BeginDisabled(true);
                ms_SearchBox.Render();
                ImGui.EndDisabled();
                ImKunai.ItemRowsBackground(new System.Numerics.Vector4(20, 20, 20, 64));
                //if (ImGui.BeginListBox("##hierarchylist", new System.Numerics.Vector2(-1, -1)))
                //{

                if (renderer.WorkProjectCsd != null)
                {
                    foreach (var f in renderer.VisibilityData.Nodes)
                    {
                        DrawSceneNode(f);
                    }

                }
                //ImGui.EndListBox();
                //}
            }
            ImGui.End();
        }
    }
}
