using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.CameraControl;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace LocalSpt5Esp;

internal static partial class ScopeSticker
{
    private static readonly MenuInputGate editGate = new();
    private static ScopeStickerProfile? draft;
    private static string editScopeKey = "", notice = "";
    private static bool cursorOwned, oldCursorVisible, choosingImage, draggingSticker, resizingSticker;
    private static CursorLockMode oldCursorLock;
    private static int inputFrame = -1, editorSession;
    private static Vector2 dragStart, dragPivot, dragAnchor, imageScroll, settingsScroll;
    private static readonly Vector2[] editCorners = new Vector2[4];
    private static ScopeStickerProfile? dragValue;
    private static Task<ImportResult?>? importTask;
    private sealed record ImportResult(byte[] Bytes, string Name, int Session, string Scope);
    internal static bool Editing => draft != null;
    internal static bool InputBlocked => editGate.Blocked;

    internal static void CaptureInput()
    {
        try { CaptureInputCore(); }
        catch (Exception error) { CancelEdit(); Failed(error); }
    }

    private static void CaptureInputCore()
    {
        if (inputFrame == Time.frameCount || editKey == null) return;
        inputFrame = Time.frameCount;
        editGate.Tick(inputFrame, Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2));
        bool external = InterfaceMenu.Open || RaidChat.Open || FloatingStatus.Capturing || WorldEditor.ModalOpen ||
            ItemInventoryEditor.Open || DirectorStage.FreeActive || (!Application.isFocused && importTask == null);
        if (Editing)
        {
            if (external || Target() == null || !Context()) { CancelEdit(); return; }
            KeepCursor();
            if (importTask != null) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { EndEdit(false); return; }
            if (CycleShortcut()) return;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || ShortcutBindings.Take(editKey)) EndEdit(true);
            return;
        }
        if (external || Cursor.visible || editGate.Blocked || Target() == null || !Context()) return;
        if (enabled.Value && CycleShortcut()) return;
        if (!ShortcutBindings.Take(editKey)) return;
        if (store == null) { FeatureToasts.Show("无法读取倍镜贴纸方案，请查看日志。"); return; }
        WorldEditor.Suspend();
        editScopeKey = currentKey; editorSession++;
        draft = current with { Enabled = true, Name = currentName };
        notice = ""; choosingImage = false;
        editGate.SetOpen(true, inputFrame);
        oldCursorLock = Cursor.lockState; oldCursorVisible = Cursor.visible; cursorOwned = true;
        try { Singleton<GameWorld>.Instance?.MainPlayer?.HandsController?.TryCast<Player.FirearmController>()?.SetTriggerPressed(false); }
        catch (Exception error) { Plugin.Self.Log.LogWarning("Unable to release trigger for optic editor: " + error.Message); }
        KeepCursor();
    }

    private static bool CycleShortcut()
    {
        bool previous = ShortcutBindings.Take(previousKey), next = ShortcutBindings.Take(nextKey);
        if (previous == next || store == null) return false;
        string id = store.CycleImage(Value.Image, next ? 1 : -1);
        if (id.Length == 0) return true;
        if (id == Value.Image) return true;
        if (Editing) SelectImage(id);
        else
        {
            var value = store.ForImage(currentKey, id, Defaults) with { Name = currentName, Enabled = true };
            store.Save(currentKey, value);
            current = value;
            ClearFlash();
        }
        return true;
    }
    internal static void CancelEdit() { if (Editing) EndEdit(false); }

    private static void EndEdit(bool save)
    {
        if (draft == null) return;
        if (save)
        {
            // Recheck context at the moment of saving, including GUI events.
            if (Target() == null || currentKey != editScopeKey || !Context() || draft == null) { CancelEdit(); return; }
            try
            {
                var value = (draft with { Name = currentName }).Normalized();
                (store ?? throw new InvalidOperationException("图片库未就绪。")).Save(editScopeKey, value);
                current = value;
                enabled.Value = true;
                FeatureToasts.Show("已保存当前倍镜和图片的贴纸方案");
            }
            catch (Exception error) { notice = "保存失败，编辑尚未关闭：" + error.Message; return; }
        }
        ClearFlash();
        draft = null; editScopeKey = ""; draggingSticker = resizingSticker = choosingImage = false; dragValue = null;
        editGate.SetOpen(false, Time.frameCount);
        if (cursorOwned) { Cursor.lockState = oldCursorLock; Cursor.visible = oldCursorVisible; cursorOwned = false; }
    }

    internal static void KeepCursor()
    {
        if (Editing && cursorOwned) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }

    private static void SelectImage(string id)
    {
        if (draft == null || id == draft.Image) return;
        ClearFlash();
        // Unsaved changes stay local to this edit; switching restores the last saved variant.
        draft = (store?.ForImage(editScopeKey, id, Defaults) ?? Defaults with { Image = id }) with { Name = currentName, Enabled = true };
        draggingSticker = resizingSticker = false; choosingImage = false;
    }

    private static void BeginImport()
    {
        if (importTask != null || store == null) return;
        int session = editorSession; string scope = Editing ? editScopeKey : "";
        var picker = WindowsDialogs.PickOpenFile(I18n.Text("导入贴纸图片"), "PNG / JPEG (*.png;*.jpg;*.jpeg)\0*.png;*.jpg;*.jpeg\0\0");
        importTask = Task.Run(async () =>
        {
            string? path = await picker.ConfigureAwait(false);
            if (string.IsNullOrEmpty(path)) return null;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > ScopeStickerStore.MaximumImageBytes || stream.Length < 24)
                throw new InvalidDataException("请选择不超过 4096 × 4096、16 MB 的 PNG 或 JPG 图片。");
            var bytes = new byte[(int)stream.Length]; await stream.ReadExactlyAsync(bytes).ConfigureAwait(false);
            if (!ScopeStickerImage.TrySize(bytes, out _, out _, out _))
                throw new InvalidDataException("请选择不超过 4096 × 4096、16 MB 的 PNG 或 JPG 图片。");
            return new ImportResult(bytes, Path.GetFileName(path), session, scope);
        });
        notice = "正在选择图片…";
    }

    private static void CompleteImport()
    {
        if (importTask?.IsCompleted != true) return;
        var pending = importTask; importTask = null;
        try
        {
            var result = pending.GetAwaiter().GetResult();
            if (result == null) { notice = ""; return; }
            // Decode only on Unity's main thread; do not copy malformed compressed data to the library.
            var validation = Decode(result.Bytes); UnityEngine.Object.Destroy(validation);
            string id = (store ?? throw new InvalidOperationException("图片库未就绪。")).Import(result.Bytes, result.Name);
            if (Editing && result.Session == editorSession && result.Scope == editScopeKey) SelectImage(id);
            notice = "图片已导入；开镜编辑并保存后应用。";
        }
        catch (Exception error) { notice = "导入失败：" + error.Message; Plugin.Self.Log.LogWarning(notice); }
    }

    private static string ImageName(string id) => store?.Images.GetValueOrDefault(id) ?? id;

    // Bounds projection is used only for editor handles. The rendered sticker
    // itself never depends on this estimate or a world-space projection.
    private static bool LensRect(out Rect rect)
    {
        rect = default;
        var lens = CameraManager.Instance?.OpticCameraManager?.CurrentOpticSight?.LensRenderer;
        var main = OverlayProjection.MainCamera;
        if (lens == null || main == null) return false;
        var bounds = lens.bounds; var min = bounds.min; var max = bounds.max;
        float left = float.MaxValue, top = float.MaxValue, right = float.MinValue, bottom = float.MinValue;
        for (int i = 0; i < 8; i++)
        {
            var point = OverlayProjection.ScreenPoint(main, new Vector3((i & 1) == 0 ? min.x : max.x,
                (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z));
            if (point.z <= 0 || !float.IsFinite(point.x) || !float.IsFinite(point.y)) return false;
            left = Math.Min(left, point.x); right = Math.Max(right, point.x);
            top = Math.Min(top, Screen.height - point.y); bottom = Math.Max(bottom, Screen.height - point.y);
        }
        rect = Rect.MinMaxRect(left, top, right, bottom);
        return rect.width > 16 && rect.height > 16 && rect.width < Screen.width * 2 && rect.height < Screen.height * 2;
    }

    private static void Edge(Vector2 start, Vector2 end, Color color)
    {
        var oldColor = GUI.color; var oldMatrix = GUI.matrix;
        try
        {
            var direction = end - start;
            GUI.color = color;
            GUI.matrix = oldMatrix * Matrix4x4.TRS(new Vector3(start.x, start.y, 0),
                Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg), Vector3.one);
            GUI.DrawTexture(new Rect(0, -1, direction.magnitude, 2), Texture2D.whiteTexture);
        }
        finally { GUI.matrix = oldMatrix; GUI.color = oldColor; }
    }

    private static void DrawPreview(Rect lens)
    {
        GUI.BeginGroup(lens);
        var oldColor = GUI.color; var oldMatrix = GUI.matrix;
        try
        {
            var horizontal = editCorners[2] - editCorners[3];
            var vertical = editCorners[0] - editCorners[3];
            var transform = Matrix4x4.identity;
            transform.m00 = horizontal.x; transform.m01 = vertical.x; transform.m03 = editCorners[3].x - lens.x;
            transform.m10 = horizontal.y; transform.m11 = vertical.y; transform.m13 = editCorners[3].y - lens.y;
            GUI.matrix = oldMatrix * transform;
            bool flashing = IsFlashing(draft!);
            if (flashing && ScopeStickerRules.TryHex(draft!.HitColor, out var hitRgb))
                GUI.color = new Color(hitRgb.R / 255f, hitRgb.G / 255f, hitRgb.B / 255f, draft.Opacity);
            else if (draft!.ColorMode != 0)
            {
                var dyn = ScopeStickerRules.EvaluateDynamicColor(draft, Time.unscaledTime);
                GUI.color = new Color(dyn.R / 255f, dyn.G / 255f, dyn.B / 255f, draft.Opacity * dyn.AlphaFactor);
            }
            else GUI.color = new Color(1, 1, 1, draft.Opacity);
            GUI.DrawTexture(new Rect(0, 0, 1, 1), flashing || (draft.ColorMode != 0 && draft.ColorUseMask) ? WhiteMask() : texture);
        }
        finally { GUI.matrix = oldMatrix; GUI.color = oldColor; GUI.EndGroup(); }
    }

    internal static void DrawEditor()
    {
        if (draft == null || InterfaceMenu.Open) return;
        int oldDepth = GUI.depth; GUI.depth = -500;
        try
        {
            KeepCursor();
            float panelWidth = Math.Min(390, Screen.width - 24);
            var panel = new Rect(12, 12, panelWidth, Math.Min(Screen.height - 24, 680));
            bool hasLens = LensRect(out var lens);
            if (!hasLens)
            {
                float side = Math.Min(Screen.height * .6f, Math.Max(120, Screen.width - panelWidth - 48));
                lens = new Rect(Screen.width - side - 18, (Screen.height - side) / 2, side, side);
                InterfaceMenu.Panel(lens); InterfaceMenu.Label(new Rect(lens.x, lens.y - 28, lens.width, 24), "贴纸预览区");
            }
            var target = Target();
            DrawRangeEditorPreview(lens, panel);
            bool layoutValid = texture != null && target != null && ScopeStickerRules.TryLayout(texture.width, texture.height,
                target.pixelWidth, target.pixelHeight, draft.Width, draft.X, draft.Y, out _, draft.Rotation);
            if (layoutValid)
            {
                ScopeStickerRules.TryLayout(texture!.width, texture.height, target!.pixelWidth, target.pixelHeight,
                    draft.Width, draft.X, draft.Y, out var layout, draft.Rotation);
                for (int i = 0; i < 4; i++)
                {
                    var point = ScopeStickerRules.Vertex(layout, i, draft.Rotation, (float)target.pixelWidth / target.pixelHeight);
                    editCorners[i] = new Vector2(lens.x + point.X * lens.width, lens.yMax - point.Y * lens.height);
                }
                if (!hasLens) DrawPreview(lens);
                for (int i = 0; i < 4; i++) Edge(editCorners[i], editCorners[(i + 1) % 4], InterfaceMenu.Accent);
                var handle = new Rect(editCorners[2].x - 9, editCorners[2].y - 9, 18, 18);
                InterfaceMenu.Surface(handle, InterfaceMenu.Accent, 2);
                EditPointer(lens, handle, panel, (editCorners[0] + editCorners[2]) * .5f);
            }
            else EditPointer(lens, default, panel, lens.center);
            InterfaceMenu.Panel(panel);
            float px = panel.x + 14, py = panel.y + 10, w = panel.width - 28;
            InterfaceMenu.Label(new Rect(px, py, w, 26), "倍镜贴纸编辑", InterfaceMenu.Accent, 17); py += 30;
            InterfaceMenu.Label(new Rect(px, py, w, 25), currentName, null, 13, translate: false); py += 29;
            if (InterfaceMenu.Button(new Rect(px, py, w * .5f - 4, 30), "保存并退出")) { EndEdit(true); return; }
            if (InterfaceMenu.Button(new Rect(px + w * .5f + 4, py, w * .5f - 4, 30), "取消")) { EndEdit(false); return; }
            py += 38;
            DrawEditingControls(new Rect(px, py, w, Math.Max(60, panel.yMax - py - 12)));
        }
        catch (Exception error) { notice = error.Message; }
        finally
        {
            GUI.depth = oldDepth;
            var e = Event.current;
            if (e != null && (e.isMouse || e.type == EventType.ScrollWheel)) e.Use();
        }
    }

    private static float EditorSlider(float xPos, ref float yPos, float w, string title, float value, float low, float high, string unit = "%")
    {
        InterfaceMenu.Label(new Rect(xPos, yPos, 120, 24), I18n.Text(title) + $"  {value:F1}" + unit, null, 12, translate: false);
        float result = InterfaceMenu.Slider(new Rect(xPos + 126, yPos + 5, Math.Max(30, w - 126), 20), value, low, high);
        yPos += 29; return result;
    }

    private static void EditPointer(Rect lens, Rect handle, Rect panel, Vector2 center)
    {
        if (draft == null || importTask != null) return;
        var e = Event.current; var point = e.mousePosition;
        if (e.type == EventType.MouseDown && e.button == 0 && !panel.Contains(point) && (lens.Contains(point) || handle.Contains(point)))
        {
            resizingSticker = handle.width > 0 && handle.Contains(point); draggingSticker = !resizingSticker;
            dragStart = point; dragValue = draft; e.Use();
            dragPivot = center;
            dragAnchor = new Vector2((center.x - lens.x) / lens.width * 100, (lens.yMax - center.y) / lens.height * 100);
        }
        if (e.type == EventType.MouseDrag && dragValue != null && (draggingSticker || resizingSticker))
        {
            var delta = point - dragStart;
            draft = resizingSticker ? ScopeStickerRules.Scale(dragValue,
                    Math.Max(.01f, (point - dragPivot).magnitude / Math.Max(1, (dragStart - dragPivot).magnitude)), dragAnchor.x, dragAnchor.y)
                : (dragValue with { X = dragValue.X + delta.x / lens.width * 100, Y = dragValue.Y - delta.y / lens.height * 100 }).Normalized();
            e.Use();
        }
        if (e.type == EventType.MouseUp) { draggingSticker = resizingSticker = false; dragValue = null; }
        if (e.type == EventType.ScrollWheel && lens.Contains(point) && !panel.Contains(point))
        {
            draft = ScopeStickerRules.Scale(draft, MathF.Pow(1.08f, -e.delta.y),
                (point.x - lens.x) / lens.width * 100, (lens.yMax - point.y) / lens.height * 100); e.Use();
        }
    }

    private static void DrawImageChoices(Rect rect)
    {
        var images = store?.Images.ToArray() ?? Array.Empty<KeyValuePair<string, string>>();
        imageScroll = GUI.BeginScrollView(rect, imageScroll, new Rect(0, 0, rect.width - 20, images.Length * 34));
        try
        {
            for (int i = 0; i < images.Length; i++)
                if (InterfaceMenu.Button(new Rect(0, i * 34, rect.width - 24, 30), images[i].Value, draft?.Image == images[i].Key)) SelectImage(images[i].Key);
        }
        finally { GUI.EndScrollView(); }
    }

    internal static void DrawSettings(Rect rect)
    {
        float w = rect.width - 28;
        float offset = w >= 560 ? 0 : 94;
        float cardWidth = offset == 0 ? (w - 12) / 2 : w;
        var extraEntries = new ConfigEntryBase[] { rangefinder, rangeReadouts, rangeIronSights, autoZeroLive, autoZeroKey, editKey, previousKey, nextKey };
        int cols = w >= 560 ? 2 : 1;
        float extraRowsHeight = MathF.Ceiling(extraEntries.Length / (float)cols) * 94;
        float baseY = offset + 94 + extraRowsHeight;
        float colorHeight = MenuColorSettingsHeight();
        float height = baseY + 240 + colorHeight + (store?.Profiles.Count ?? 0) * 62;
        settingsScroll = GUI.BeginScrollView(rect, settingsScroll, new Rect(0, 0, rect.width - 20, Math.Max(height, rect.height - 1)));
        string? removeKey = null;
        ScopeStickerProfile? defaultSource = null;
        try
        {
            InterfaceMenu.DrawEntry(OpticZoom.Enabled, new Rect(0, 0, cardWidth, 86));
            InterfaceMenu.DrawEntry(enabled, new Rect(offset == 0 ? cardWidth + 12 : 0, offset, cardWidth, 86));
            for (int i = 0; i < extraEntries.Length; i++)
            {
                float ex = cols == 2 && (i % 2 == 1) ? cardWidth + 12 : 0;
                float ey = offset + 94 + (i / cols) * 94;
                InterfaceMenu.DrawEntry(extraEntries[i], new Rect(ex, ey, cols == 2 ? cardWidth : w, 86));
            }
            InterfaceMenu.Paragraph(new Rect(4, baseY, w - 8, 56), "开镜后按编辑快捷键，可拖动贴纸或测距窗位置、缩放、切换图片并保存。同倍镜的每张图片分别保存布局。", null, 13);
            bool old = GUI.enabled; GUI.enabled = old && store != null && importTask == null;
            if (InterfaceMenu.Button(new Rect(4, baseY + 62, 160, 32), "导入贴纸图片")) BeginImport();
            if (InterfaceMenu.Button(new Rect(172, baseY + 62, 168, 32), "清理未使用图片"))
            {
                int removed = store!.DeleteUnusedImages();
                notice = removed > 0 ? $"已清理 {removed} 张未使用的贴纸图片。" : "没有未使用的贴纸图片。";
            }
            GUI.enabled = old;
            if (InterfaceMenu.Button(new Rect(348, baseY + 62, 156, 32), "重置测距窗位置"))
            {
                rangeOffsetX.Value = 18f; rangeOffsetY.Value = 14f;
                notice = "测距窗已恢复默认偏移位置。";
            }
            InterfaceMenu.Label(new Rect(4, baseY + 102, w - 8, 24), $"PNG / JPG · ≤4096×4096 · ≤16 MB · 图库 {store?.Images.Count ?? 0} 张", null, 12, translate: false);
            InterfaceMenu.Paragraph(new Rect(4, baseY + 130, w - 8, 40), notice.Length > 0 ? notice : "导入图片会复制到本地图片库，保留原图透明度。", null, 12);

            float colorY = baseY + 176;
            DrawMenuColorSettings(4, ref colorY, w - 8);

            InterfaceMenu.Label(new Rect(4, colorY + 6, w - 8, 26), "已保存的倍镜方案", InterfaceMenu.Accent, 15);
            int index = 0;
            if (store != null) foreach (var pair in store.Profiles.OrderBy(p => p.Value.Name))
            {
                float row = colorY + 40 + index++ * 62;
                InterfaceMenu.Surface(new Rect(0, row, w, 56), Color.Lerp(InterfaceMenu.Background, InterfaceMenu.Text, .035f), 6);
                InterfaceMenu.Label(new Rect(10, row + 5, Math.Max(60, w - 210), 24), pair.Value.Name.Length > 0 ? pair.Value.Name : pair.Key, null, 13, translate: false);
                InterfaceMenu.Label(new Rect(10, row + 29, Math.Max(60, w - 210), 22), ImageName(pair.Value.Image), null, 12, translate: false);
                if (InterfaceMenu.Button(new Rect(w - 192, row + 13, 96, 30), "设为默认")) defaultSource = pair.Value;
                if (InterfaceMenu.Button(new Rect(w - 88, row + 13, 78, 30), "删除")) removeKey = pair.Key;
            }
        }
        finally { GUI.EndScrollView(); }
        if (defaultSource != null)
        {
            width.Value = defaultSource.Width; x.Value = defaultSource.X; y.Value = defaultSource.Y; opacity.Value = defaultSource.Opacity;
            defaultColorMode.Value = defaultSource.ColorMode;
            defaultColorPrimary.Value = defaultSource.ColorPrimary;
            defaultColorSecondary.Value = defaultSource.ColorSecondary;
            defaultColorSpeed.Value = defaultSource.ColorSpeed;
            defaultColorUseMask.Value = defaultSource.ColorUseMask;
            notice = "已将该倍镜布局设为新倍镜默认模板。";
        }
        if (removeKey != null && store != null && store.DeleteProfile(removeKey))
        {
            if (currentKey == removeKey) current = Defaults;
            notice = "已删除所选倍镜贴纸方案。";
        }
    }
}
