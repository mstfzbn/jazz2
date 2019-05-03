﻿using System;
using System.Collections.Generic;
using System.Net;
using Duality;
using Duality.Audio;
using Duality.Components;
using Duality.Drawing;
using Duality.Resources;
using Jazz2.Game.Structs;

namespace Jazz2.Game.UI.Menu.InGame
{
    public partial class InGameMenu : Scene, IMenuContainer
    {
        private readonly App root;
        private readonly GameObject rootObject;
        private readonly LevelHandler levelHandler;

        private Stack<MenuSection> sectionStack;

        private Canvas canvas;
        private BitmapFont fontSmall, fontMedium;

        private Metadata metadata;

        public ContentRef<Material> TopLine, BottomLine, Dim;

        private Material finalMaterial;
        private float transition;

        public RefreshMode RefreshMode
        {
            get
            {
                return root.RefreshMode;
            }
            set
            {
                root.RefreshMode = value;
            }
        }

        public ScreenMode ScreenMode
        {
            get
            {
                return root.ScreenMode;
            }
            set
            {
                root.ScreenMode = value;
            }
        }

        public InGameMenu(App root, LevelHandler levelHandler)
        {
            this.root = root;
            this.levelHandler = levelHandler;

            root.Immersive = true;

            rootObject = new GameObject("InGameMenu");
            rootObject.AddComponent(new LocalController(this));
            AddObject(rootObject);

            // Setup camera
            GameObject camera = new GameObject("MainCamera");
            camera.AddComponent<Transform>();

            Camera cameraInner = camera.AddComponent<Camera>();
            cameraInner.Projection = ProjectionMode.Orthographic;
            cameraInner.NearZ = 10;
            cameraInner.FarZ = 1000;

            cameraInner.RenderingSetup = new MainMenuRenderSetup();
            camera.Parent = rootObject;

            canvas = new Canvas();

            // Load resources
            fontSmall = new BitmapFont(canvas, "UI/font_small", 17, 18, 15, 32, 256, -2);
            fontMedium = new BitmapFont(canvas, "UI/font_medium", 29, 31, 15, 32, 256, -1);

            metadata = ContentResolver.Current.RequestMetadata("UI/MainMenu");

            // Get game screen
            Camera cameraLevel = levelHandler.FindComponent<Camera>();
            LevelRenderSetup renderSetup = cameraLevel.ActiveRenderSetup as LevelRenderSetup;
            if (renderSetup != null) {
                finalMaterial = new Material(DrawTechnique.Solid, renderSetup.FinalTexture);
            }

            InitPlatformSpecific();

            // Show Begin section
            sectionStack = new Stack<MenuSection>();

            SwitchToSection(new InGameMenuBeginSection());

        }

        public void SwitchToSection(MenuSection section)
        {
            if (sectionStack.Count > 0) {
                sectionStack.Peek().OnHide(false);
            }

            sectionStack.Push(section);
            section.OnShow(this);
        }

        public void LeaveSection(MenuSection section)
        {
            if (sectionStack.Count > 0) {
                MenuSection activeSection = sectionStack.Pop();
                if (activeSection != section) {
                    throw new InvalidOperationException();
                }

                activeSection.OnHide(true);

                if (sectionStack.Count > 0) {
                    sectionStack.Peek().OnShow(this);
                }
            }
        }

        public void SwitchToCurrentGame()
        {
            root.Immersive = false;

            Scene.Current.DisposeLater();
            Scene.SwitchTo(levelHandler);
        }

        public void SwitchToMainMenu()
        {
            levelHandler.Dispose();

            root.ShowMainMenu();
        }

        public void DrawString(ref int charOffset, string text, float x,
            float y, Alignment align, ColorRgba? color, float scale = 1f, float angleOffset = 0f, float varianceX = 4f, float varianceY = 4f,
            float speed = 4f, float charSpacing = 1f, float lineSpacing = 1f)
        {
            fontSmall.DrawString(ref charOffset, text, x, y, align,
                color, scale, angleOffset, varianceX, varianceY, speed, charSpacing, lineSpacing);
        }

        public void DrawStringShadow(ref int charOffset, string text, float x,
            float y, Alignment align, ColorRgba? color, float scale = 1f, float angleOffset = 0f, float varianceX = 4f, float varianceY = 4f,
            float speed = 4f, float charSpacing = 1f, float lineSpacing = 1f)
        {
            int charOffsetShadow = charOffset;
            fontSmall.DrawString(ref charOffsetShadow, text, x, y + 2.8f * scale, align,
                new ColorRgba(0f, 0.29f), scale, angleOffset, varianceX, varianceY, speed, charSpacing, lineSpacing);
            fontSmall.DrawString(ref charOffset, text, x, y, align,
                color, scale, angleOffset, varianceX, varianceY, speed, charSpacing, lineSpacing);
        }

        public void DrawMaterial(string name, float x, float y, Alignment alignment, ColorRgba color, float scaleX = 1f, float scaleY = 1f)
        {
            GraphicResource res;
            if (metadata.Graphics.TryGetValue(name, out res)) {
                res.Draw(canvas, x, y, alignment, color, scaleX, scaleY);
            }
        }

        public void DrawMaterial(string name, float x, float y, Alignment alignment, ColorRgba color, float scaleX, float scaleY, Rect texRect)
        {
            GraphicResource res;
            if (metadata.Graphics.TryGetValue(name, out res)) {
                res.Draw(canvas, x, y, alignment, color, scaleX, scaleY, texRect);
            }
        }

        public void DrawMaterial(string name, int frame, float x, float y, Alignment alignment, ColorRgba color, float scaleX = 1f, float scaleY = 1f)
        {
            GraphicResource res;
            if (metadata.Graphics.TryGetValue(name, out res)) {
                res.Draw(canvas, frame, x, y, alignment, color, scaleX, scaleY);
            }
        }

        public void PlaySound(string name, float volume = 1f)
        {
            SoundResource res;
            if (metadata.Sounds.TryGetValue(name, out res)) {
                SoundInstance instance = DualityApp.Sound.PlaySound(res.Sound);
                instance.Volume = volume * SettingsCache.SfxVolume;
            }
        }

        private void OnUpdate()
        {
            if (sectionStack.Count > 0) {
                sectionStack.Peek().OnUpdate();
            }

            if (transition < 1f) {
                transition += Time.TimeMult * 0.14f;
            } else {
                transition = 1f;
            }
        }

        private void OnRender(IDrawDevice device)
        {
            Vector2 size = device.TargetSize;

            Rect view = new Rect(size);
            AdjustVisibleZone(ref view);

            Vector2 center = size * 0.5f;

            canvas.Begin(device);

            int charOffset = 0;
            int charOffsetShadow = 0;

            // Dark background
            canvas.State.SetMaterial(finalMaterial);
            canvas.State.ColorTint = new ColorRgba(1f - transition * 0.5f);
            canvas.State.TextureCoordinateRect = new Rect(0, 0, 1, 1);
            canvas.FillRect(0, 0, device.TargetSize.X, device.TargetSize.Y);

            // Title
            DrawMaterial("MenuCarrot", -1, center.X - 76f, 64f + 2f, Alignment.Center, new ColorRgba(0f, 0.3f), 0.8f, 0.8f);
            DrawMaterial("MenuCarrot", -1, center.X - 76f, 64f, Alignment.Center, ColorRgba.White, 0.8f, 0.8f);

            fontMedium.DrawString(ref charOffsetShadow, "Jazz", center.X - 63f, 70f + 2f, Alignment.Left,
                new ColorRgba(0f, 0.35f), 0.75f, 1.63f, 3f, 3f, 0f, 0.92f);
            fontMedium.DrawString(ref charOffsetShadow, "2", center.X - 19f, 70f - 8f + 2f, Alignment.Left,
                new ColorRgba(0f, 0.35f), 0.5f, 0f, 0f, 0f, 0f);
            fontMedium.DrawString(ref charOffsetShadow, "Resurrection", center.X - 10f, 70f + 4f + 2.5f, Alignment.Left,
                new ColorRgba(0f, 0.33f), 0.5f, 0.4f, 1.2f, 1.2f, 7f, 0.8f);

            fontMedium.DrawString(ref charOffset, "Jazz", center.X - 63f, 70f, Alignment.Left,
                new ColorRgba(0.54f, 0.44f, 0.34f, 0.5f), 0.75f, 1.63f, 3f, 3f, 0f, 0.92f);
            fontMedium.DrawString(ref charOffset, "2", center.X - 19f, 70f - 8f, Alignment.Left,
                new ColorRgba(0.54f, 0.44f, 0.34f, 0.5f), 0.5f, 0f, 0f, 0f, 0f);
            fontMedium.DrawString(ref charOffset, "Resurrection", center.X - 10f, 70f + 4f, Alignment.Left,
                new ColorRgba(0.6f, 0.42f, 0.42f, 0.5f), 0.5f, 0.4f, 1.2f, 1.2f, 7f, 0.8f);

            // Version
            Vector2 bottomRight = device.TargetSize;
            bottomRight.X = view.X + view.W - 24f;
            bottomRight.Y -= 10f;
            DrawStringShadow(ref charOffset, "v" + App.AssemblyVersion, bottomRight.X, bottomRight.Y, Alignment.BottomRight,
                new ColorRgba(0.45f, 0.5f), 0.7f, 0.4f, 1.2f, 1.2f, 7f, 0.8f);

            // Copyright
            Vector2 bottomLeft = bottomRight;
            bottomLeft.X = view.X + 24f;
            DrawStringShadow(ref charOffset, "(c) 2016-" + DateTime.Now.Year + "  Dan R.", bottomLeft.X, bottomLeft.Y, Alignment.BottomLeft,
                new ColorRgba(0.45f, 0.5f), 0.7f, 0.4f, 1.2f, 1.2f, 7f, 0.8f);

            // Current section
            if (sectionStack.Count > 0) {
                sectionStack.Peek().OnPaint(canvas, view);
            }

            DrawPlatformSpecific(device.TargetSize);

            canvas.End();
        }

        partial void InitPlatformSpecific();

        partial void DrawPlatformSpecific(Vector2 size);

        partial void AdjustVisibleZone(ref Rect view);

        public void BeginFadeOut(Action action)
        {
            throw new NotImplementedException();
        }

        public void SwitchToLevel(LevelInitialization data)
        {
            throw new NotImplementedException();
        }

        public bool IsAnimationPresent(string name)
        {
            return metadata.Graphics.ContainsKey(name);
        }

        public void SwitchToServer(IPEndPoint endPoint)
        {
            throw new NotSupportedException();
        }

        private class LocalController : Component, ICmpUpdatable, ICmpRenderer
        {
            private readonly InGameMenu menu;

            public LocalController(InGameMenu menu)
            {
                this.menu = menu;
            }

            void ICmpUpdatable.OnUpdate()
            {
                menu.OnUpdate();
            }

            void ICmpRenderer.GetCullingInfo(out CullingInfo info)
            {
                info.Position = Vector3.Zero;
                info.Radius = float.MaxValue;
                info.Visibility = VisibilityFlag.Group0 | VisibilityFlag.ScreenOverlay;
            }

            void ICmpRenderer.Draw(IDrawDevice device)
            {
                menu.OnRender(device);
            }
        }
    }
}