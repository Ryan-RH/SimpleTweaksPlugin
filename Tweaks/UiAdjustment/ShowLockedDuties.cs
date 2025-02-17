using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using Dalamud.Game.Gui.PartyFinder.Types;
using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.UI;
using System;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using System.Numerics;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Show Locked Duties")]
[TweakDescription("Reveals information about a locked duty in the party finder")]
[TweakAuthor("Treezy")]
public unsafe class ShowLockedDuties : UiAdjustments.SubTweak
{
    private static uint componentListId;
    private static uint descriptionId;
    private static uint elements;
    private static List<IPartyFinderListing> Listings = new List<IPartyFinderListing>();
    private static bool RetrievedAllListings = false;

    

    protected override void Enable()
    {
        Service.PartyFinderGui.ReceiveListing += ReceiveListing;
        base.Enable();
    }

    protected override void Disable()
    {
        Service.PartyFinderGui.ReceiveListing -= ReceiveListing;
        Listings.Clear();
        RetrievedAllListings = false;
        base.Disable();
    }

    private static void ReceiveListing(IPartyFinderListing listing, IPartyFinderListingEventArgs args)
    {
        Listings.Add(listing);
    }

    [FrameworkUpdate]
    private unsafe void FrameworkUpdate()
    {
        var addon = RaptureAtkUnitManager.Instance()->GetAddonByName("LookingForGroup");
        if (addon == null)
        {
            Listings.Clear();
            RetrievedAllListings = false;
        }
    }

    [AddonPostRefresh("LookingForGroup")]
    private unsafe void OnAddonRefresh(AtkUnitBase* addon)
    {
        var refreshed = addon->GetNodeById(47)->GetAsAtkComponentButton()->IsEnabled;
        if (refreshed)
        {
            if (!RetrievedAllListings)
            {
                RetrievedAllListings = true;
                Listings = Listings.OrderBy(listing =>
                {
                    var categorySortValue = (listing.Category == DutyCategory.None) ? 0 : (ushort)listing.Category;
                    return (IsUnlocked(listing.Duty.Value) ? 0 : 1, categorySortValue, -listing.Duty.Value.SortKey);
                }).ToList();
            }
        }
        else
        {
            if (RetrievedAllListings)
            {
                RetrievedAllListings = false;
                Listings.Clear();
            }
        }
    }

    [AddonPreDraw("LookingForGroup")]
    private unsafe void OnAddonDraw(AtkUnitBase* addon)
    {
        if (RetrievedAllListings)
        {
            if (addon->GetNodeById(39)->IsVisible())
            {
                componentListId = 39;
                descriptionId = 17;
                elements = 22;
            }
            else
            {
                componentListId = 38;
                descriptionId = 29;
                elements = 13;
            }
            AtkComponentList* componentList = addon->GetNodeById(componentListId)->GetAsAtkComponentList();

            for (int i = 0; i < Math.Min(Listings.Count, elements); i++)
            {
                var itemRendererComponent = componentList->GetItemRenderer(i);
                if (itemRendererComponent->ListItemIndex < Listings.Count)
                {
                    var UiListing = Listings[itemRendererComponent->ListItemIndex];
                    if (!itemRendererComponent->IsEnabled)
                    {
                        itemRendererComponent->GetTextNodeById(5)->GetAsAtkTextNode()->SetText(UiListing.Duty.Value.Name.ToString());
                        string description = "";
                        if (UiListing.Objective != ObjectiveFlags.None)
                            description += $"[{UiListing.Objective}]";
                        if (UiListing.Conditions != ConditionFlags.None)
                            description += $"[{UiListing.Conditions}]";
                        if (UiListing.SearchArea == SearchAreaFlags.OnePlayerPerJob)
                            description += $"[{UiListing.SearchArea}]";
                        description += $" {UiListing.Description}";
                        itemRendererComponent->GetTextNodeById(descriptionId)->GetAsAtkTextNode()->SetText(description);
                        itemRendererComponent->GetTextNodeById(descriptionId)->GetAsAtkTextNode()->TextColor = Vector4ToByteColor(new Vector4(0.9f, 0.9f, 0.9f, 1f));
                    }
                }
            }
        }
    }

    private static ByteColor Vector4ToByteColor(Vector4 vector)
    {
        return new ByteColor
        {
            R = (byte)Math.Clamp(vector.X * 255, 0, 255),
            G = (byte)Math.Clamp(vector.Y * 255, 0, 255),
            B = (byte)Math.Clamp(vector.Z * 255, 0, 255),
            A = (byte)Math.Clamp(vector.W * 255, 0, 255)
        };
    }

    private static bool IsUnlocked(ContentFinderCondition duty)
    {
        if (duty.Name == "")
            return true;

        var questIdWhich = duty.Unknown37;
        uint unlockQuestRowId;
        if (questIdWhich == 0)
            unlockQuestRowId = duty.UnlockQuest.RowId;
        else
            unlockQuestRowId = duty.Unknown31;

        if (unlockQuestRowId != 0)
            return QuestManager.IsQuestComplete(unlockQuestRowId);
        return UIState.IsInstanceContentUnlocked(duty.Content.RowId);
    }
}