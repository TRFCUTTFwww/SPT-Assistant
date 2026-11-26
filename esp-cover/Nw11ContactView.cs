using System;
using System.Collections.Generic;
using System.IO;
using ChatShared;
using Comfort.Common;
using EFT;
using EFT.UI.Chat;
using HarmonyLib;
using Il2CppInterop.Runtime;
using LocalSpt.Shared;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using ContactsTask = Il2CppSystem.Threading.Tasks.Task<Comfort.Common.Result<ChatShared.ChatContacts>>;

namespace LocalSpt5Esp;

internal static class Nw11ContactView
{
    const string AvatarObject = "LocalSpt5.Nw11Avatar";
    const int AvatarSize = 256;
    static readonly List<GameObject> friendAvatars = new();
    static readonly Dictionary<IntPtr, (Image Image, Sprite? Previous)> dialogueAvatars = new();
    static Texture2D? texture;
    static Sprite? avatar;
    static ContactsTask? pendingContacts;
    static SocialNetwork? pendingNetwork;
    static float retryAt;

    internal static void Initialize(Plugin plugin)
    {
        var hooks = typeof(Nw11ContactView);
        plugin.Patches.Patch(AccessTools.Method(typeof(ChatMember), nameof(ChatMember.Show)),
            postfix: new HarmonyMethod(hooks, nameof(AfterMemberShow)));
        plugin.Patches.Patch(AccessTools.Method(typeof(DialogueView), "SetValuesByDialogueType"),
            postfix: new HarmonyMethod(hooks, nameof(AfterDialogueShow)));
        plugin.Patches.Patch(AccessTools.Method(typeof(ChatMemberContextInteractions), "IsActive"),
            prefix: new HarmonyMethod(hooks, nameof(BeforeMemberAction)));
        plugin.Patches.Patch(AccessTools.Method(typeof(ChatDialogueContextInteractions), "IsActive"),
            prefix: new HarmonyMethod(hooks, nameof(BeforeDialogueAction)));
        plugin.Patches.Patch(AccessTools.Method(typeof(ChatFriendsListPanel), nameof(ChatFriendsListPanel.Show)),
            postfix: new HarmonyMethod(hooks, nameof(AfterFriendsShown)));
    }

    static Sprite Avatar()
    {
        if (avatar != null)
            return avatar;
        using var stream = typeof(Nw11ContactView).Assembly.GetManifestResourceStream("LocalSpt5Esp.Assets.Nw11.png")
            ?? throw new FileNotFoundException("Embedded Nw11 avatar");
        using var png = new MemoryStream();
        stream.CopyTo(png);
        var image = new Texture2D(2, 2, TextureFormat.RGBA32, true);
        try
        {
            if (!ImageConversion.LoadImage(image, png.ToArray(), true)
                || image.width != AvatarSize || image.height != AvatarSize)
                throw new InvalidDataException("Invalid Nw11 avatar");
            image.filterMode = FilterMode.Trilinear;
            image.wrapMode = TextureWrapMode.Clamp;
            avatar = Sprite.Create(image, new Rect(0, 0, image.width, image.height), new Vector2(.5f, .5f));
            if (avatar == null)
                throw new InvalidDataException("Could not create Nw11 avatar sprite");
            texture = image;
            return avatar;
        }
        catch
        {
            Object.Destroy(image);
            throw;
        }
    }

    static void AfterMemberShow(ChatMember __instance, UpdatableChatMember __0)
    {
        var parent = __instance.transform;
        var existing = parent.Find(AvatarObject);
        if (!Nw11Contact.IsContact(__0?.Id))
        {
            if (existing != null)
                existing.gameObject.SetActive(false);
            return;
        }
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            return;
        }

        var sprite = Avatar();
        var go = new GameObject(AvatarObject, new[] { Il2CppType.Of<RectTransform>() });
        go.transform.SetParent(parent, false);
        go.transform.SetAsFirstSibling();
        var rect = go.transform.Cast<RectTransform>();
        rect.sizeDelta = new Vector2(22, 22);
        var layout = go.AddComponent<LayoutElement>();
        layout.minWidth = layout.preferredWidth = 22;
        layout.minHeight = layout.preferredHeight = 22;
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        friendAvatars.RemoveAll(view => view == null);
        friendAvatars.Add(go);
    }

    static void AfterDialogueShow(DialogueView __instance)
    {
        var image = __instance._dialogueIcon;
        if (image == null)
            return;
        if (!Nw11Contact.IsContact(__instance._dialogue?._id))
        {
            if (dialogueAvatars.Remove(image.Pointer, out var previous))
                Restore(previous);
            return;
        }

        var sprite = Avatar();
        if (dialogueAvatars.TryGetValue(image.Pointer, out var existing) && existing.Image != null)
            image.overrideSprite = sprite;
        else
        {
            dialogueAvatars[image.Pointer] = (image, image.m_OverrideSprite);
            image.overrideSprite = sprite;
        }
    }

    static void Restore((Image Image, Sprite? Previous) original)
    {
        if (original.Image != null && original.Image.m_OverrideSprite == avatar)
            original.Image.overrideSprite = original.Previous;
    }

    static bool BeforeMemberAction(ChatMemberContextInteractions __instance, EFriendInteractionButton __0, ref bool __result)
    {
        if (__0 != EFriendInteractionButton.WatchProfile || !Nw11Contact.IsContact(__instance._selectedMember?.Id))
            return true;
        __result = false;
        return false;
    }

    static bool BeforeDialogueAction(ChatDialogueContextInteractions __instance, EDialogueInteractionButton __0, ref bool __result)
    {
        if (__0 != EDialogueInteractionButton.WatchProfile || !Nw11Contact.IsContact(__instance._selectedDialogue?._id))
            return true;
        __result = false;
        return false;
    }

    static bool HasContact(SocialNetwork network)
    {
        var entries = network.EftFriendsList;
        if (entries == null)
            return false;
        for (int i = 0; i < entries.Count; i++)
            if (Nw11Contact.IsContact(entries[i]?.Id))
                return true;
        return false;
    }

    static void AfterFriendsShown(SocialNetwork __0)
    {
        if (pendingContacts != null || Time.unscaledTime < retryAt || __0?.IsInitialized != true || HasContact(__0))
            return;
        var session = Singleton<TarkovApplication>.Instance?.GetClientBackEndSession();
        var current = session?.SocialNetwork;
        if (current == null || current.Pointer != __0.Pointer)
            return;
        retryAt = Time.unscaledTime + 10;
        pendingContacts = session!.GetFriendsList();
        pendingNetwork = __0;
    }

    internal static void Tick()
    {
        if (pendingContacts?.IsCompleted != true)
            return;
        var task = pendingContacts;
        var network = pendingNetwork;
        pendingContacts = null;
        pendingNetwork = null;
        var current = Singleton<TarkovApplication>.Instance?.GetClientBackEndSession()?.SocialNetwork;
        if (task.IsCanceled || task.IsFaulted || current == null || network == null || current.Pointer != network.Pointer
            || network.EftFriendsList == null || HasContact(network))
            return;
        var result = task.Result;
        var friends = result?.Value?.EftFriends;
        if (result?.Succeed != true || friends == null)
            return;
        foreach (var member in friends)
        {
            if (!Nw11Contact.IsContact(member?.Id))
                continue;
            network.EftFriendsList.Add(member);
            network.RebuildCombinedFriendsList();
            break;
        }
    }

    internal static void Reset()
    {
        pendingContacts = null;
        pendingNetwork = null;
        foreach (var go in friendAvatars)
            if (go != null)
                Object.Destroy(go);
        friendAvatars.Clear();

        foreach (var original in dialogueAvatars.Values)
            Restore(original);
        dialogueAvatars.Clear();
        if (avatar != null)
            Object.Destroy(avatar);
        if (texture != null)
            Object.Destroy(texture);
        avatar = null;
        texture = null;
    }
}
