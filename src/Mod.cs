using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;
using SkyFrost.Base;

namespace ContactsNoLaggy;

// Opening the Contacts tab the first time froze the game for seconds. Cause (profiled): inside a UIX mask
// (the contacts scroll area) every graphics chunk clones its materials via GraphicsChunk.RenderData.CloneMaterial
// -> Slot.DuplicateComponent -> DuplicateComponents -> Slot.GenerateHierarchy(material's slot). The shared UI
// materials live on the userspace ROOT slot (World.GetSharedComponentOrCreate attaches to RootSlot), so every
// clone walks the entire userspace hierarchy (~28k slots, ~13 ms) and the contact list needs hundreds of clones.
//
// That hierarchy set only decides which references count as "internal" to the copy. For a single copied
// component, references to anything not copied are preserved either way (TransferReferences(preserveMissingTargets:
// true)), and CloneMaterial then drives the clone's fields from the source. So while cloning a UIX material,
// GenerateHierarchy only needs the material's own slot.
public class ContactsNoLaggyMod : ResoniteMod
{
    public override string Name => "ContactsNoLaggy";
    public override string Author => "Winter";
    public override string Version => "1.0.0";
    public override string Link => "";

    public override void OnEngineInit() => new Harmony("com.winter.ContactsNoLaggy").PatchAll();

    [HarmonyPatch]
    private static class CloneMaterialPatch
    {
        [ThreadStatic] internal static bool Cloning;

        private static MethodBase TargetMethod() =>
            AccessTools.Method(AccessTools.Inner(typeof(FrooxEngine.UIX.GraphicsChunk), "RenderData"), "CloneMaterial");

        private static void Prefix() => Cloning = true;
        private static void Finalizer() => Cloning = false; // runs even if CloneMaterial throws
    }

    [HarmonyPatch(typeof(Slot), nameof(Slot.GenerateHierarchy))]
    private static class GenerateHierarchyPatch
    {
        private static bool Prefix(Slot __instance, System.Collections.Generic.HashSet<Slot> set)
        {
            if (!CloneMaterialPatch.Cloning) return true;
            set.Add(__instance);
            return false;
        }
    }

    // The stock tab builds every contact row in one frame on first open (~600 rows, ~600 ms). Build it in the
    // background instead, once the contact list has loaded: a dash screen that isn't shown has an inactive
    // ScreenRoot, so its canvas does no work while rows are added.
    [HarmonyPatch(typeof(UserspaceRadiantDash), "OnCommonUpdate")]
    private static class PrebuildPatch
    {
        private static readonly MethodInfo OnShow = AccessTools.Method(typeof(LegacyRadiantScreenWrapper<ContactsDialog>), "OnShow");
        private static bool done;

        private static void Postfix(UserspaceRadiantDash __instance)
        {
            if (done || !__instance.Engine.Cloud.Contacts.ContactListLoaded) return;
            var screen = __instance.Dash?.Screens.OfType<LegacyRadiantScreenWrapper<ContactsDialog>>().FirstOrDefault();
            if (screen == null) return;
            done = true;
            if (!screen.IsShown) OnShow.Invoke(screen, null); // builds the tab hidden; no-op if already built
        }
    }

    // ...and spread the row creation over frames (~2 ms each) so the background build doesn't hitch either.
    // Only while the tab is hidden (on a visible canvas every added row forces a relayout, so trickling rows in
    // would be worse than stock), and only creating a new row; updates and removals stay immediate.
    [HarmonyPatch(typeof(ContactsDialog), "UpdateContactItem")]
    private static class UpdateContactItemPatch
    {
        private const double BudgetMs = 2;
        private static readonly AccessTools.FieldRef<ContactsDialog, Dictionary<string, ContactItem>> Items =
            AccessTools.FieldRefAccess<ContactsDialog, Dictionary<string, ContactItem>>("_contactItems");
        // Calls the patched method, so a deferred row passes the budget check again.
        private static readonly Action<ContactsDialog, ContactData> UpdateContactItem =
            AccessTools.MethodDelegate<Action<ContactsDialog, ContactData>>(AccessTools.Method(typeof(ContactsDialog), "UpdateContactItem"));
        private static int frame = -1;
        private static long spent;

        private static bool Prefix(ContactsDialog __instance, ContactData data, out long __state)
        {
            __state = 0;
            if (__instance.Slot.IsActive || data.Contact.ShouldBeHidden || Items(__instance).ContainsKey(data.UserId)) return true;
            int now = __instance.Time.LocalUpdateIndex;
            if (now != frame) { frame = now; spent = 0; }
            if (spent < BudgetMs * Stopwatch.Frequency / 1000) { __state = Stopwatch.GetTimestamp(); return true; }
            __instance.RunInUpdates(1, () =>
            {
                if (!__instance.IsRemoved && __instance.Engine.Cloud.Contacts.GetContact(data.UserId) != null)
                    UpdateContactItem(__instance, data);
            });
            return false;
        }

        private static void Postfix(long __state) { if (__state != 0) spent += Stopwatch.GetTimestamp() - __state; }
    }
}
