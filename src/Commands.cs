using System;
using System.Collections.Generic;
using Logger;
using Onyx_API;
using Onyx_API.Items;

static class Commands
{
    [Initializer([typeof(ItemCatalog)])]
    static void Init()
    {
        HashSet<string> itemNames = [];
        foreach(ItemDef itemDef in ItemCatalog.allItemDefs)
        {
            itemNames.Add(itemDef.name);
        }
        Console.RegisterCommand("Give_Item", GiveItem, [itemNames]);
        Console.RegisterCommand("DebugMode", GiveItem, [itemNames]);
    }

    static void DebugMode(object[] parameters)
    {
        GameScript.debugMode = !GameScript.debugMode;
        Log.Info("debugMode set to " + GameScript.debugMode);
    }

    static void GiveItem(object[] parameters)
    {
        //id, durability, array?, count, ?
        int itemIndex = 0;

        if (parameters.Length > 0)
        {
            if (parameters[0] is int paramIndedx)
            {
                itemIndex = paramIndedx;
            }
            if (parameters[0] is string itemName)
            {
                itemIndex = ItemCatalog.FindItemDef(itemName).Index;
            }
        } else
        {
            throw new ArgumentException("number of Arguments for command give_item has to be at least 1");
        }

        ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);

        int itemCount = 1;
        if (parameters.Length > 1)
        {
            if (parameters[1] is int paramIndedx)
            {
                itemCount = paramIndedx;
            } else
            {
                throw new ArgumentException("ItemCount argument must be a number");
            }
        }

        int itemDurability = itemDef.maxDurability > 0 ? itemDef.maxDurability : 0;
        if (parameters.Length > 2)
        {
            if (parameters[1] is int paramIndedx)
            {
                itemDurability = paramIndedx;
            } else
            {
                throw new ArgumentException("ItemCount argument must be a number");
            }
        }
        
        Singletons.GameScript.AddItem(new Item(itemIndex, itemCount, [0, 0, 0, 0], itemDurability, null));
    }
}
