
using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using System.Reflection;
using HarmonyLib;
using System.Collections.Generic;
using SideLoader.SaveData;
using SideLoader;
using System.Linq;
using System.Linq.Expressions;
using NodeCanvas.BehaviourTrees;
using UnityEngine;
using Random = UnityEngine.Random;
using static MapMagic.Layout;
using static SideLoader.SL_Recipe;

namespace NewGamePlus
{
    public class Settings
    {
        public static string Main = "0. NGP Main";
        public static string TransfersE = "1. Transfer - Equipped";
        public static string Transfers = "2. Transfer - Main";
        public static string Limiters = "3. Limiters - Main";
        public static string LimitersT = "4. Limiters - Type";
        public static string Debuff = "5. Debuff - Streched Skin (Restart)";
        public static string BLists = "6. Blacklists";
        public static string Extras = "7. Extras";
    }
    public class NewGameExtension : PlayerSaveExtension
    {
        public int LegacyLevel;
        public int[] LegacySkills;

        public NewGameExtension() : base()
        {
            LegacyLevel = 0;
        }

        public override void Save(Character character, bool isWorldHost)
        {
            if (NewGamePlus.SaveData.TryGetValue(character.UID, out NewGameExtension save))
            {
                LegacyLevel = save.LegacyLevel;
                LegacySkills = save.LegacySkills;
            }
        }

        public override void ApplyLoadedSave(Character character, bool isWorldHost)
        {
            if (NewGamePlus.SaveData.ContainsKey(character.UID))
                NewGamePlus.logboy.Log(LogLevel.Error, "Save Data already exists for " + character.Name);
            NewGamePlus.SaveData[character.UID] = this;

            NewGamePlus.logboy.Log(LogLevel.Message, "Loaded Legacy Level for " + character.Name + ": " + LegacyLevel);
            if (LegacySkills?.Length > 0)
                NewGamePlus.logboy.Log(LogLevel.Message, "Loaded LegacySkills for " + character.Name + ": " + LegacySkills.Length);
        }


        public static NewGameExtension CreateExtensionFor(string UID)
        {
            if (!NewGamePlus.SaveData.TryGetValue(UID, out NewGameExtension nge))
            {
                nge = TryLoadExtension<NewGameExtension>(UID);
                if (nge == null)
                    nge = new NewGameExtension();
                NewGamePlus.SaveData[UID] = nge;
            }
            return nge;
        }


        public static int GetLegacyLevelFor(string UID)
        {
            if (NewGamePlus.SaveData.TryGetValue(UID, out NewGameExtension nge))
                return nge.LegacyLevel;

            int ret = 0;
            nge = TryLoadExtension<NewGameExtension>(UID);
            if (nge != null)
            {
                ret = nge.LegacyLevel;
                NewGamePlus.SaveData.Remove(UID);
            }
            return ret;
        }
    }

    [BepInPlugin(ID, NAME, VERSION)]
    public class NewGamePlus : BaseUnityPlugin
    {
        const string ID = "com.potatiums.newgameplus";
        const string NAME = "New Game Plus";
        const string VERSION = "1.2.0";

        public static List<string> FItemsN = new List<string>();
        public static List<string> FSkillsN = new List<string>();
        public static List<int> FItems = new List<int>();
        public static List<int> FSkills = new List<int>();

        public static ConfigEntry<bool> EnableNGP, DeleteKeys, TransferEquipped, TransferEquippedArmor, TransferEquippedMelee, TransferEquippedRanged, TransferEquippedAmmo, TransferMoney, TransferSkills, TransferRecipes, TransferFromPouch, TransferFromBag, TransferFromStash, EnableL, EnableLT, TransferExaltedAndLifeDrain, EnableDebuff, EnableBlacklists;
        public static ConfigEntry<float> MoneyL, SkillL, EquippedL, PouchL, BagL, StashL, FoodLT, Melee1LT, Melee2LT, RangedLT, AmmoLT, BagLT, ItemLT;
        public static ConfigEntry<int> RESValue, DMGValue, MAXLvl, MaxMoney;
        public static ConfigEntry<string> effIcon, MoneyTarget, ForbiddenItemsN, ForbiddenSkillsN, ForbiddenItemsID, ForbiddenSkillsID;

        const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static;

        public static Dictionary<string, NewGameExtension> SaveData = new Dictionary<string, NewGameExtension>();
        public static NewGamePlus Instance;
        public static NewGameExtension SaveExt;

        internal void Awake()
        {
            Instance = this;
            logboy = Logger;

            //load configs
            //Main
            EnableNGP = Config.Bind<bool>(Settings.Main, "New Game Plus - On/Off", true, new ConfigDescription("Enable New Game Plus", null, new ConfigurationManagerAttributes { Order = 2 }));
            DeleteKeys = Config.Bind<bool>(Settings.Main, "Delete Keys", true, new ConfigDescription("Delete Keys on Creation", null, new ConfigurationManagerAttributes { Order = 1 }));
            
            //Transfers - Equipped
            TransferEquipped = Config.Bind<bool>(Settings.TransfersE, "Transfer Equipped - On/Off", true, new ConfigDescription("Enables -Equipped- Section", null, new ConfigurationManagerAttributes { Order = 5 }));
            TransferEquippedArmor = Config.Bind<bool>(Settings.TransfersE, "Transfer - Equipped Armor", true, new ConfigDescription("Transfer Equipped Armor", null, new ConfigurationManagerAttributes { Order = 4 }));
            TransferEquippedMelee = Config.Bind<bool>(Settings.TransfersE, "Transfer - Equipped Melee", true, new ConfigDescription("Transfer Equipped Melee Weapon/Shield", null, new ConfigurationManagerAttributes { Order = 3 }));
            TransferEquippedRanged = Config.Bind<bool>(Settings.TransfersE, "Transfer - Equipped Ranged", true, new ConfigDescription("Transfer Equipped Ranged Weapon", null, new ConfigurationManagerAttributes { Order = 2 }));
            TransferEquippedAmmo = Config.Bind<bool>(Settings.TransfersE, "Transfer - Equipped Ammo", true, new ConfigDescription("Transfer Equipped Ammo", null, new ConfigurationManagerAttributes { Order = 1 }));
            
            //Transfers - Main
            TransferMoney = Config.Bind<bool>(Settings.Transfers, "Transfer - Money", true, new ConfigDescription("Transfer Money", null, new ConfigurationManagerAttributes { Order = 6 }));
            TransferSkills = Config.Bind<bool>(Settings.Transfers, "Transfer - Skills", true, new ConfigDescription("Transfer Skills", null, new ConfigurationManagerAttributes { Order = 5 }));
            TransferRecipes = Config.Bind<bool>(Settings.Transfers, "Transfer - Recipes", true, new ConfigDescription("Transfer Recipes", null, new ConfigurationManagerAttributes { Order = 4 }));
            TransferFromPouch = Config.Bind<bool>(Settings.Transfers, "Transfer - Pouch", true, new ConfigDescription("Transfer Pouch Items", null, new ConfigurationManagerAttributes { Order = 3 }));
            TransferFromBag = Config.Bind<bool>(Settings.Transfers, "Transfer - Bag", true, new ConfigDescription("Transfer Backpack Items", null, new ConfigurationManagerAttributes { Order = 2 }));
            TransferFromStash = Config.Bind<bool>(Settings.Transfers, "Transfer - Stash", true, new ConfigDescription("Transfer Stash Items", null, new ConfigurationManagerAttributes { Order = 1 }));
            
            //Limiters Main
            EnableL = Config.Bind<bool>(Settings.Limiters, "Limiters - On/Off", true, new ConfigDescription("Enable Main Limiters", null, new ConfigurationManagerAttributes { Order = 7 }));
            MoneyL = Config.Bind<float>(Settings.Limiters, "% - Money", 1f, new ConfigDescription("Limit Money %", new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = 6 }));
            SkillL = Config.Bind<float>(Settings.Limiters, "% - Skills", 0.75f, new ConfigDescription("Limit Skill %", new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = 5 }));
            EquippedL = Config.Bind<float>(Settings.Limiters, "% - Equipped", 1f, new ConfigDescription("Limit Equipped Items %", new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = 4 }));
            PouchL = Config.Bind<float>(Settings.Limiters, "% - Pouch", 0.75f, new ConfigDescription("Limit Pouch Items %", new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = 3 }));
            BagL = Config.Bind<float>(Settings.Limiters, "% - Backpack", 0.50f, new ConfigDescription("Limit Backpack Items %", new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = 2 }));
            StashL = Config.Bind<float>(Settings.Limiters, "% - Stash", 0.20f, new ConfigDescription("Limit Stash Items %", new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = 1 }));

            //Limiters - Type
            EnableLT = Config.Bind<bool>(Settings.LimitersT, "Limiters - On/Off", true, new ConfigDescription("Enable Type Limiters", null, new ConfigurationManagerAttributes { Order = 8 }));
            FoodLT = Config.Bind<float>(Settings.LimitersT, "% - Food", 0.5f, new ConfigDescription("Limit Food %", new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = 7 }));
            Melee1LT = Config.Bind<float>(Settings.LimitersT, "% - Melee - One Handed", 0.5f, new ConfigDescription("Limit One-Handed Melee Weapons %", new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = 6 }));
            Melee2LT = Config.Bind<float>(Settings.LimitersT, "% - Melee - Two Handed", 0.5f, new ConfigDescription("Limit Two-Handed Melee Weapons %", new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = 6 }));
            RangedLT = Config.Bind<float>(Settings.LimitersT, "% - Ranged", 0.5f, new ConfigDescription("Limit Ranged Weapons %", new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = 4 }));
            AmmoLT = Config.Bind<float>(Settings.LimitersT, "% - Ammo", 0.5f, new ConfigDescription("Limit Ammo %", new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = 3 }));
            BagLT = Config.Bind<float>(Settings.LimitersT, "% - Bags", 0.5f, new ConfigDescription("Limit Bags %", new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = 2 }));
            ItemLT = Config.Bind<float>(Settings.LimitersT, "% - Other Items", 0.5f, new ConfigDescription("Limit Other Items %", new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = 1 }));

            //Debuff Settings (needs restart)
            EnableDebuff = Config.Bind<bool>(Settings.Debuff, "Debuff - On/Off", true, new ConfigDescription("Enable Streched Skin", null, new ConfigurationManagerAttributes { Order = 6 }));
            TransferExaltedAndLifeDrain = Config.Bind<bool>(Settings.Debuff, "Debuff - Transfer Exalted", false, new ConfigDescription("Transfer Exalted & Life Drain", null, new ConfigurationManagerAttributes { Order = 5 }));
            RESValue = Config.Bind<int>(Settings.Debuff, "Debuff - Resistance", 25, new ConfigDescription("Debuff Resistance %", new AcceptableValueRange<int>(0, 200), new ConfigurationManagerAttributes { Order = 4 }));
            DMGValue = Config.Bind<int>(Settings.Debuff, "Debuff - Damage", 15, new ConfigDescription("Debuff Damage %", new AcceptableValueRange<int>(0, 200), new ConfigurationManagerAttributes { Order = 3 }));
            MAXLvl = Config.Bind<int>(Settings.Debuff, "Debuff - Max Level", 100, new ConfigDescription("Maximum Level for Debuff", new AcceptableValueRange<int>(0, 100), new ConfigurationManagerAttributes { Order = 2 }));
            effIcon = Config.Bind<string>(Settings.Debuff, "Debuff - Icon", "Orange", new ConfigDescription("Streched Skin Icon", new AcceptableValueList<string>("Orange", "Red", "Transparent"), new ConfigurationManagerAttributes { Order = 1 }));

            //Blacklists
            EnableBlacklists = Config.Bind<bool>(Settings.BLists, "Blacklists - On/Off", true, new ConfigDescription("Enable Blacklists", null, new ConfigurationManagerAttributes { Order = 5 }));
            ForbiddenItemsN = Config.Bind<string>(Settings.BLists, "Blacklist - Items - Name(s)", "", new ConfigDescription("Write the Item Name(s), separated by commas | eg: Fang Sword,Fang Shield", null, new ConfigurationManagerAttributes { Order = 4 }));
            ForbiddenItemsID = Config.Bind<string>(Settings.BLists, "Blacklist - Items - ID(s)", "3100090,3100092,3100091,5100080,2100150,2100030,3100490,3100031,3100030,3100040,3100042,3100041,2000031,2200100,5110097", new ConfigDescription("Write the Item ID(s), separated by commas | Default: All Faction-Exclusive Items", null, new ConfigurationManagerAttributes { Order = 3 }));
            ForbiddenSkillsN = Config.Bind<string>(Settings.BLists, "Blacklist - Skills - Name(s)", "", new ConfigDescription("Write the Skill Name(s), separated by commas", null, new ConfigurationManagerAttributes { Order = 2 }));
            ForbiddenSkillsID = Config.Bind<string>(Settings.BLists, "Blacklist - Skills - ID(s)", "8205340,8205330,8200105,8205350,8202005,8205280,8200100,8205270,8205240,8205311,8205310,8205300,8202004,8205998,8205997,8200104", new ConfigDescription("Write the Skill ID(s), separated by commas | Default: All Faction-Exclusive Skills", null, new ConfigurationManagerAttributes { Order = 1 }));

            //Extras
            MaxMoney = Config.Bind<int>(Settings.Extras, "Extras - Max Money", 0, new ConfigDescription("Maximum Amount of Silver to Transfer (0 = Unlimited)", null, new ConfigurationManagerAttributes { Order = 2 }));
            MoneyTarget = Config.Bind<string>(Settings.Extras, "Extras - Money Target", "Stash", new ConfigDescription("Where silver will be transferred to", new AcceptableValueList<string>("Pouch", "Bag", "Stash"), new ConfigurationManagerAttributes { Order = 1 }));


            //Add Blacklisted
            if (EnableBlacklists.Value)
            {
                //Convert and Add Forbidden Items by Name to List
                if (!string.IsNullOrEmpty(ForbiddenItemsN.Value))
                {
                    string[] BItemsnL = ForbiddenItemsN.Value.Split(',');
                    foreach (string BItemsn in BItemsnL)
                    {
                        FItemsN.Add(BItemsn);
                    }
                }

                //Convert and Add Forbidden Skills by Name to List
                if (!string.IsNullOrEmpty(ForbiddenSkillsN.Value))
                {
                    string[] BSkillsnL = ForbiddenSkillsN.Value.Split(',');
                    foreach (string BSkillsn in BSkillsnL)
                    {
                        FSkillsN.Add(BSkillsn);
                    }
                }


                //Convert and Add Forbidden Items by ID to List
                if (!string.IsNullOrEmpty(ForbiddenItemsID.Value))
                {
                    string[] BItemsL = ForbiddenItemsID.Value.Split(',');
                    foreach (string BItems in BItemsL)
                    {
                        FItems.Add(int.Parse(BItems));
                    }
                }

                //Convert and Add Forbidden Skills by ID to List
                if (!string.IsNullOrEmpty(ForbiddenSkillsID.Value))
                {
                    string[] BSkillsL = ForbiddenSkillsID.Value.Split(',');
                    foreach (string BSkills in BSkillsL)
                    {
                        FSkills.Add(int.Parse(BSkills));
                    }
                }
            }


            //initialize NG_StatusManager Debuff
            NG_StatusManager.InitializeEffects();
            SL.OnPacksLoaded += NG_StatusManager.UpdateLevelData;

            var harmony = new Harmony(ID);
            harmony.PatchAll();
            Log("New Game Plus starting...");
        }

        public static bool setMaxStats = false;
        public static bool skillisForbidden = false;
        public static bool itemisForbidden = false;

        public static void CreateNewCharacter()
        {
            if (itemList != null)
            {
                Character player = CharacterManager.Instance.GetFirstLocalCharacter();
                NewGameExtension player_nge = NewGameExtension.CreateExtensionFor(player.UID);

                // Two Options
                //    Option1: 0.X Character with LegacyLevel set, but no PSE
                if (m_legacy.CharSave.PSave.LegacyLevel > 0)
                    player_nge.LegacyLevel = m_legacy.CharSave.PSave.LegacyLevel + 1;
                //    Option2: 1.0+ Character with PSE
                else
                    player_nge.LegacyLevel = NewGameExtension.GetLegacyLevelFor(m_legacy.CharSave.CharacterUID) + 1;

                logboy.Log(LogLevel.Message, "Loading Legacy Gear from " + m_legacy.CharSave.PSave.Name);
                List<int> legacySkills = new List<int>();

                //Transfer Money (To Pouch or Stash
                if (TransferMoney.Value == true)
                {
                    float moneysaved = m_legacy.CharSave.PSave.Money;
                    if (EnableL.Value)
                    {
                        moneysaved *= MoneyL.Value;
                    }

                    if (MaxMoney.Value == 0 || MaxMoney.Value >= moneysaved)
                    {
                        if (MoneyTarget.Value.ToLower() == "pouch")
                            player.Inventory.Pouch.SetSilverCount((int)moneysaved);
                        if (MoneyTarget.Value.ToLower() == "stash")
                            player.Inventory.Stash.SetSilverCount((int)moneysaved);
                    }
                    else
                    {
                        if (MoneyTarget.Value.ToLower() == "pouch")
                            player.Inventory.Pouch.SetSilverCount(MaxMoney.Value);
                        if (MoneyTarget.Value.ToLower() == "stash")
                            player.Inventory.Stash.SetSilverCount(MaxMoney.Value);
                    }
                }

                //Transfer Recipes
                if (TransferRecipes.Value == true)
                {
                    typeof(CharacterRecipeKnowledge).GetMethod("LoadLearntRecipe", FLAGS).Invoke(player.Inventory.RecipeKnowledge, new object[] { m_legacy.CharSave.PSave.RecipeSaves });
                }


                //Add Push Kick, Throw Lantern, Fire/Reload, or Dagger Slash (tutorial event will give duplicates) to Forbidden Skills List
                FSkills.Add(8100120);
                FSkills.Add(8100010);
                FSkills.Add(8100072);
                FSkills.Add(8200600);

                //Add Alternate Start related Skills to Forbidden Skills List
                for (int s = -2222; s <= -2200; s++)
                {
                    FSkills.Add(s);
                }

                foreach (BasicSaveData data in itemList)
                {
                    Item item = ItemManager.Instance.GetItem(data.m_saveIdentifier.ToString());
                    if (item != null)
                    {
                        int loc = data.SyncData.IndexOf("<Hierarchy>");
                        if (loc != -1)
                        {
                            char type = data.SyncData.Substring(loc + 11, 1)[0];
                            if (type == '2')
                            {
                                //Initialize Item Clone
                                item.ChangeParent(player.Inventory.Pouch.transform);
                                Equipment clone = (Equipment)ItemManager.Instance.CloneItem(item);

                                //Transfer Backpack
                                if (TransferFromBag.Value && clone.GetType() == typeof(Bag))
                                {
                                    clone.OnContainerChangedOwner(player);
                                    clone.SetIsntNew();
                                    clone.ChangeParent(player.Inventory.Pouch.transform);
                                    player.Inventory.EquipItem(clone);
                                    clone.ForceStartInit();

                                    //Transfer Money (to Bag)
                                    if (TransferMoney.Value && MoneyTarget.Value.ToLower() == "bag")
                                    {
                                        float moneysaved = m_legacy.CharSave.PSave.Money;
                                        if (EnableL.Value)
                                        {
                                            moneysaved *= MoneyL.Value;
                                        }
                                        if (MaxMoney.Value == 0 || MaxMoney.Value >= moneysaved)
                                            player.Inventory.EquippedBag.Container.SetSilverCount((int)moneysaved);
                                        else
                                            player.Inventory.EquippedBag.Container.SetSilverCount(MaxMoney.Value);
                                    }
                                }

                                //Check Blacklists
                                if (EnableBlacklists.Value)
                                {
                                    itemisForbidden = false;
                                    //Check Forbidden Items by Name
                                    if (FItemsN != null)
                                    {
                                        foreach (string blockeditem in FItemsN)
                                        {
                                            if (item.Name.Contains(blockeditem))
                                            {
                                                itemisForbidden = true;
                                                break;
                                            }
                                            else { continue; }
                                        }
                                    }

                                    //Check Forbidden Items by ID
                                    if (FItems != null)
                                    {
                                        foreach (int blockeditem in FItems)
                                        {
                                            if (item.ItemID == blockeditem)
                                            {
                                                itemisForbidden = true;
                                                break;
                                            }
                                            else { continue; }
                                        }
                                    }

                                    //Block Item
                                    if (itemisForbidden)
                                        continue;
                                }

                                //Transfer Equipped Items
                                if (clone.GetType() != typeof(Bag) && TransferEquipped.Value)
                                {
                                    float percent = 1f;
                                    if (EnableL.Value)
                                    {
                                        percent = EquippedL.Value;
                                    }

                                    if (Random.Range(0f, 1f) <= percent)
                                    {
                                        if (TransferEquippedArmor.Value && clone.GetType() == typeof(Armor))
                                        {
                                            clone.OnContainerChangedOwner(player);
                                            clone.SetIsntNew();
                                            clone.ChangeParent(player.Inventory.Pouch.transform);
                                            player.Inventory.EquipItem(clone);
                                            clone.ForceStartInit();
                                        }
                                        if (TransferEquippedMelee.Value && clone.GetType() == typeof(MeleeWeapon) || TransferEquippedMelee.Value && clone.GetType() == typeof(DualMeleeWeapon))
                                        {
                                            clone.OnContainerChangedOwner(player);
                                            clone.SetIsntNew();
                                            clone.ChangeParent(player.Inventory.Pouch.transform);
                                            player.Inventory.EquipItem(clone);
                                            clone.ForceStartInit();
                                        }
                                        if (TransferEquippedRanged.Value && clone.GetType() == typeof(ProjectileWeapon))
                                        {
                                            clone.OnContainerChangedOwner(player);
                                            clone.SetIsntNew();
                                            clone.ChangeParent(player.Inventory.Pouch.transform);
                                            player.Inventory.EquipItem(clone);
                                            clone.ForceStartInit();
                                        }
                                        if (TransferEquippedAmmo.Value && clone.GetType() == typeof(Ammunition))
                                        {
                                            clone.OnContainerChangedOwner(player);
                                            clone.SetIsntNew();
                                            clone.ChangeParent(player.Inventory.Pouch.transform);
                                            player.Inventory.EquipItem(clone);
                                            clone.ForceStartInit();
                                        }
                                    }
                                }
                                else { continue; }

                                continue;
                            }
                        }
                    }
                }



                foreach (BasicSaveData data in itemList)
                {
                    Item item = ItemManager.Instance.GetItem(data.m_saveIdentifier.ToString());

                    if (item == null)
                        logboy.Log(LogLevel.Error, "Couldn't get Item -- " + data.m_saveIdentifier.ToString());
                    else if (!(item is Quest) && (!DeleteKeys.Value || !(item.GetType() == typeof(Item)) || !item.Name.Contains("Key")))
                    {
                        int loc = data.SyncData.IndexOf("<Hierarchy>");
                        if (loc != -1)
                        {
                            //logboy.Log(LogLevel.Message, "Item: " + item.GetType() + " - " + item.Name + " - " + item.name + " - " + item.DisplayName + " - " + data.SyncData);
                            int len = data.SyncData.IndexOf("<", loc + 11) - (loc + 11);
                            string type = data.SyncData.Substring(loc + 11, len);
                            //logboy.Log(LogLevel.Message, "NGP - Item TYPE:" + type);


                            //Initialize Item Clone
                            Item clone = ItemManager.Instance.CloneItem(item);


                            //Check Blacklists
                            if (EnableBlacklists.Value)
                            {
                                itemisForbidden = false;
                                //Check Forbidden Items by Name
                                if (FItemsN != null)
                                {
                                    foreach (string blockeditem in FItemsN)
                                    {
                                        if (item.Name.Contains(blockeditem))
                                        {
                                            itemisForbidden = true;
                                            break;
                                        }
                                        else { continue; }
                                    }
                                }

                                //Check Forbidden Items by ID
                                foreach (int blockeditem in FItems)
                                {
                                    if (item.ItemID == blockeditem)
                                    {
                                        itemisForbidden = true;
                                        break;
                                    }
                                    else { continue; }
                                }

                                //Block Item
                                if (itemisForbidden)
                                    continue;
                            }


                            //Limiter Init
                            bool percentL = true;
                            bool percentLT = true;
                            //Limiter - Main Calculations
                            if (EnableL.Value)
                            {
                                percentL = false;
                                if (type.StartsWith("1Pouch") && Random.Range(0f, 1f) <= PouchL.Value) { percentL = true; }
                                if (type.Contains("_Content") && Random.Range(0f, 1f) <= BagL.Value) { percentL = true; }
                                if (type.StartsWith("1Stash") && Random.Range(0f, 1f) <= StashL.Value) { percentL = true; }
                            }
                            //Limiter - Type Calculations
                            if (EnableLT.Value)
                            {
                                percentLT = false;
                                if (clone.GetType() == typeof(Food) && Random.Range(0f, 1f) <= FoodLT.Value) { percentLT = true; }
                                if (clone.GetType() == typeof(MeleeWeapon) && Random.Range(0f, 1f) <= Melee1LT.Value) { percentLT = true; }
                                if (clone.GetType() == typeof(DualMeleeWeapon) && Random.Range(0f, 1f) <= Melee2LT.Value) { percentLT = true; }
                                if (clone.GetType() == typeof(ProjectileWeapon) && Random.Range(0f, 1f) <= RangedLT.Value) { percentLT = true; }
                                if (clone.GetType() == typeof(Ammunition) && Random.Range(0f, 1f) <= AmmoLT.Value) { percentLT = true; }
                                if (clone.GetType() == typeof(Bag) && Random.Range(0f, 1f) <= BagLT.Value) { percentLT = true; }
                                if (clone.GetType() == typeof(Item) && Random.Range(0f, 1f) <= ItemLT.Value) { percentLT = true; }
                            }

                            //Transfer Pouch Inventory
                            if (type.StartsWith("1Pouch") && TransferFromPouch.Value && percentL && percentLT)
                            {
                                item.ChangeParent(player.Inventory.Pouch.transform);
                                if (item.RemainingAmount != clone.RemainingAmount)
                                    clone.RemainingAmount = item.RemainingAmount;
                                clone.OnContainerChangedOwner(player);
                                clone.SetIsntNew();
                                clone.ChangeParent(player.Inventory.Pouch.transform);
                                clone.ForceStartInit();
                            }

                            //Transfer Backpack Inventory
                            else if (type.Contains("_Content") && TransferFromBag.Value && percentL && percentLT)
                            {
                                item.ChangeParent(player.Inventory.EquippedBag.Container.transform);
                                if (item.RemainingAmount != clone.RemainingAmount)
                                    clone.RemainingAmount = item.RemainingAmount;
                                clone.OnContainerChangedOwner(player);
                                clone.SetIsntNew();
                                clone.ChangeParent(player.Inventory.EquippedBag.Container.transform);
                                clone.ForceStartInit();
                            }

                            //Transfer Stash Inventory
                            else if (type.StartsWith("1Stash") && TransferFromStash.Value && percentL && percentLT)
                            {
                                item.ChangeParent(player.Inventory.Stash.transform);
                                if (item.RemainingAmount != clone.RemainingAmount)
                                    clone.RemainingAmount = item.RemainingAmount;
                                clone.OnContainerChangedOwner(player);
                                clone.SetIsntNew();
                                clone.ChangeParent(player.Inventory.Stash.transform);
                                clone.ForceStartInit();
                            }
                            else
                            {
                                switch (type[0])
                                {
                                    case '2':
                                        //Do nothing, cause already equipped
                                        break;
                                    case '3':
                                        if (!(item is Skill))
                                        {
                                            logboy.Log(LogLevel.Error, "Can't learn a non-skill: " + item.Name);
                                            break;
                                        }

                                        skillisForbidden = false;

                                        //Check Forbidden Skills by Name
                                        if (FSkillsN != null)
                                        {
                                            foreach (string blockedskill in FSkillsN)
                                            {
                                                if (item.Name.Contains(blockedskill))
                                                {
                                                    skillisForbidden = true;
                                                    break;
                                                }
                                                else { continue; }
                                            }
                                        }

                                        //Check Forbidden Skills by ID
                                        foreach (int blockedskill in FSkills)
                                        {
                                            if (item.ItemID == blockedskill)
                                            {
                                                skillisForbidden = true;
                                                break;
                                            }
                                            else { continue; }
                                        }

                                        //Block Forbidden Skill
                                        if (skillisForbidden)
                                            break;

                                        //Check for Exalted, and remove it or add LifeDrain
                                        if (item.ItemID == 8205999)
                                        {
                                            if (!TransferExaltedAndLifeDrain.Value)
                                                break;
                                            foreach (BasicSaveData status in m_legacy.CharSave.PSave.StatusList)
                                            {
                                                if (status != null && !string.IsNullOrEmpty(status.SyncData))
                                                {
                                                    string _statusPrefabName = status.Identifier.ToString();
                                                    if (_statusPrefabName.Contains("Life Drain"))
                                                    {
                                                        string[] _splitData = StatusEffect.SplitNetworkData(status.SyncData);
                                                        StatusEffect statusEffectPrefab = ResourcesPrefabManager.Instance.GetStatusEffectPrefab(_statusPrefabName);
                                                        player.StatusEffectMngr.AddStatusEffect(statusEffectPrefab, null, _splitData);
                                                        break;
                                                    }
                                                }
                                            }
                                        }

                                        //Limiter - Main Calculations
                                        bool percentSL = true;
                                        if (EnableL.Value)
                                        {
                                            percentSL = false;
                                            if (Random.Range(0f, 1f) <= SkillL.Value) { percentSL = true; }
                                        }

                                        //Transfer Skills
                                        if (TransferSkills.Value && percentSL)
                                        {
                                            player.Inventory.TryUnlockSkill((Skill)item);
                                        }
                                        if (!player.Inventory.LearnedSkill(item))
                                            logboy.Log(LogLevel.Error, "Failed to learn skill: " + item.Name);
                                        else
                                            legacySkills.Add(item.ItemID);

                                        break;
                                    default:
                                        logboy.Log(LogLevel.Error, "Unknown Item Detected: " + item.Name + " - " + data.SyncData);
                                        break;
                                }
                            }
                        }
                        else
                        {
                            logboy.Log(LogLevel.Error, "Hierarchy not found -- " + item.Name + " - " + data.SyncData);
                        }
                    }
                }

                player.ApplyQuicklots(m_legacy.CharSave.PSave);

                PropertyInfo pi_DropBag = typeof(Character).GetProperty("HelpDropBagCount");
                pi_DropBag.SetValue(player, m_legacy.CharSave.PSave.HelpDropBagCount);

                PropertyInfo pi_UseBandage = typeof(Character).GetProperty("HelpUseBandageCount");
                pi_UseBandage.SetValue(player, m_legacy.CharSave.PSave.HelpBandageCount);

                player.TargetingSystem.SetHelpLockCount(m_legacy.CharSave.PSave.HelpLockCount);

                player_nge.LegacySkills = legacySkills.ToArray();
                logboy.Log(LogLevel.Message, "Increasing Legacy Level to " + player_nge.LegacyLevel);
                logboy.Log(LogLevel.Message, "Giving Character " + legacySkills.Count + " Legacy Skills");

                SaveData[player.UID] = player_nge;

                itemList.Clear();
                itemList = null;
                m_legacy = null;

                setMaxStats = true;
            }
        }

        // Copied from ItemManager
        private static string ItemSavesToString(BasicSaveData[] _itemSaves)
        {
            string text = "";
            for (int i = 0; i < _itemSaves.Length; i++)
            {
                string syncDataFromSaveData = Item.GetSyncDataFromSaveData(_itemSaves[i].SyncData);
                if (!string.IsNullOrEmpty(syncDataFromSaveData))
                {
                    text = text + syncDataFromSaveData + "~";
                }
            }
            return text;
        }

        public static void Log(string message)
        {
            logboy.Log(LogLevel.Message, message);
        }

        private static bool CharacterHasLegacySkill(Character character, Skill skill)
        {
            return SaveData.TryGetValue(character.UID, out NewGameExtension nge) && nge.LegacySkills != null && nge.LegacySkills.Contains(skill.ItemID);
        }

        public static ManualLogSource logboy;
        public static SaveInstance m_legacy;
        public static List<BasicSaveData> itemList;

        // To gain access to the legacy character save & to init item load
        [HarmonyPatch(typeof(CharacterCreationPanel), "OnConfirmSave")]
        public class CharacterCreationPanel_OnConfirmSave
        {
            [HarmonyPrefix]
            public static void Prefix(CharacterCreationPanel __instance, ref SaveInstance ___m_legacy)
            {
                if (EnableNGP.Value && ___m_legacy != null)
                {
                    m_legacy = ___m_legacy;
                    itemList = new List<BasicSaveData>();
                    foreach (BasicSaveData data in ___m_legacy.CharSave.ItemList)
                        itemList.Add(data);

                    ItemManager.Instance.OnReceiveItemSync(ItemSavesToString(itemList.ToArray()), ItemManager.ItemSyncType.Character);
                }
            }
        }

        // To change character creation settings to match Legacy Character
        [HarmonyPatch(typeof(CharacterCreationPanel), "OnLegacySelected", new Type[] { typeof(SaveInstance) })]
        public class CharacterCreationPanel_OnLegacySelected
        {
            [HarmonyPostfix]
            public static void Postfix(CharacterCreationPanel __instance, ref SaveInstance _instance)
            {
                // Character Creation Settings:
                //    Gender
                //    Race
                //    Face Style
                //    Hair Style
                //    Hair Color
                if (EnableNGP.Value && _instance != null)
                {
                    CharacterVisualData legacy = _instance.CharSave.PSave.VisualData;
                    int legacyLevel = _instance.CharSave.PSave.LegacyLevel;
                    if (legacyLevel == 0)
                        legacyLevel = NewGameExtension.GetLegacyLevelFor(_instance.CharSave.CharacterUID);

                    string legacyName = _instance.CharSave.PSave.Name;
                    string[] splits = legacyName.Split(' ');
                    if (splits[splits.Length - 1] == ToRoman(legacyLevel + 1))
                    {
                        legacyName = legacyName.Substring(0, legacyName.Length - (splits[splits.Length - 1].Length + 1));
                    }

                    __instance.OnCharacterNameEndEdit(legacyName + " " + ToRoman(legacyLevel + 2));

                    __instance.OnSexeChanged((int)legacy.Gender);
                    __instance.OnSkinChanged(legacy.SkinIndex);
                    __instance.OnHeadVariationChanged(legacy.HeadVariationIndex + 1);
                    __instance.OnHairStyleChanged(legacy.HairStyleIndex + 1);
                    __instance.OnHairColorChanged(legacy.HairColorIndex + 1);

                    FieldInfo fi_ccp;
                    CharacterCreationDisplay selector;

                    fi_ccp = typeof(CharacterCreationPanel).GetField("m_sexeSelector", FLAGS);
                    selector = (CharacterCreationDisplay)fi_ccp.GetValue(__instance);
                    selector.SetSelectedIndex((int)legacy.Gender);

                    fi_ccp = typeof(CharacterCreationPanel).GetField("m_skinSelector", FLAGS);
                    selector = (CharacterCreationDisplay)fi_ccp.GetValue(__instance);
                    selector.SetSelectedIndex(legacy.SkinIndex);

                    fi_ccp = typeof(CharacterCreationPanel).GetField("m_faceSelector", FLAGS);
                    selector = (CharacterCreationDisplay)fi_ccp.GetValue(__instance);
                    selector.SetValue(legacy.HeadVariationIndex + 1);

                    fi_ccp = typeof(CharacterCreationPanel).GetField("m_hairStyleSelector", FLAGS);
                    selector = (CharacterCreationDisplay)fi_ccp.GetValue(__instance);
                    selector.SetValue(legacy.HairStyleIndex + 1);

                    fi_ccp = typeof(CharacterCreationPanel).GetField("m_hairColorSelector", FLAGS);
                    selector = (CharacterCreationDisplay)fi_ccp.GetValue(__instance);
                    selector.SetValue(legacy.HairColorIndex + 1);

                    typeof(CharacterCreationPanel).GetMethod("RefreshCharPreview", FLAGS).Invoke(__instance, new object[0]);
                }
            }
        }

        // Shamelessly stolen from https://stackoverflow.com/a/11749642 and modified
        public static string ToRoman(int number)
        {
            if (number >= 500) return "D" + ToRoman(number - 500);
            if (number >= 400) return "CD" + ToRoman(number - 400);
            if (number >= 100) return "C" + ToRoman(number - 100);
            if (number >= 90) return "XC" + ToRoman(number - 90);
            if (number >= 50) return "L" + ToRoman(number - 50);
            if (number >= 40) return "XL" + ToRoman(number - 40);
            if (number >= 10) return "X" + ToRoman(number - 10);
            if (number >= 9) return "IX" + ToRoman(number - 9);
            if (number >= 5) return "V" + ToRoman(number - 5);
            if (number >= 4) return "IV" + ToRoman(number - 4);
            if (number >= 1) return "I" + ToRoman(number - 1);
            return string.Empty;
        }

        // To fix issue with one of the 4 starter skills being in a quickslot when starting new game plus
        //     would cause the message of "skill not in inventory"
        public static void FixQuickSlots(Character player)
        {
            for (int i = 0; i < player.QuickSlotMngr.QuickSlotCount; i++)
            {
                QuickSlot slot = player.QuickSlotMngr.GetQuickSlot(i);
                if (slot.ActiveItem is Skill && !player.Inventory.OwnsItem(slot.ActiveItem.UID))
                {
                    Item skill = player.Inventory.SkillKnowledge.GetItemFromItemID(slot.ActiveItem.ItemID);
                    if (skill != null)
                        slot.SetQuickSlot(skill);
                }
            }
        }

        /*
        [HarmonyPatch(typeof(QuickSlot), "Activate")]
        public class QuickSlot_Activate
        {
            [HarmonyPrefix]
            public static void Prefix(QuickSlot __instance, ref Character ___m_owner)
            {
                //logboy.Log(LogLevel.Message, "QuickSlot Activation for " + __instance.ActiveItem.Name + " - " + (__instance.ItemAsSkill == null));
                if (__instance.ActiveItem != null && !__instance.ItemIsSkill && __instance.ActiveItem is Skill)
                {
                    Item skill = ___m_owner.Inventory.SkillKnowledge.GetItemFromItemID(__instance.ActiveItem.ItemID);
                    if (skill != null)
                        __instance.SetQuickSlot(skill);
                    else
                        __instance.Clear();
                }
            }
        }
        */

        // LEGACY METHOD OF LOADING LegacyLevel
        //    Can't remove due to breaking older games
        [HarmonyPatch(typeof(Character), "LoadPlayerSave", new Type[] { typeof(PlayerSaveData) })]
        public class Character_LoadPlayerSave
        {
            [HarmonyPostfix]
            public static void Postfix(ref PlayerSaveData _save)
            {
                int level = _save.LegacyLevel;
                if (level > 0)
                {
                    if (SaveData.TryGetValue(_save.UID, out NewGameExtension nge))
                        nge.LegacyLevel = level;
                    else
                        logboy.Log(LogLevel.Error, "Missing NewGameExtension for Loaded Save");
                }
            }
        }

        /*
         * Legacy Method of Saving LegacyLevel
         * 
        [HarmonyPatch(typeof(CharacterSave), "PrepareSave")]
        public class CharacterSave_PrepareSave
        {
            [HarmonyPostfix]
            public static void Postfix(ref CharacterSave __instance)
            {
                if(SaveData.TryGetValue(__instance.CharacterUID,out NewGameExtension nge))
                    __instance.PSave.LegacyLevel = nge.LegacyLevel;
            }
        }
         */

        public static void MarkAllSkillsAsNotNew(Character player)
        {
            foreach (Item item in player.Inventory.SkillKnowledge.GetLearnedItems())
                item.SetIsntNew();
        }

        // To set health to max when loading character in
        [HarmonyPatch(typeof(NetworkLevelLoader), "OnReportLoadingProgress", new Type[] { typeof(float) })]
        public class NetworkLevelLoader_OnReportLoadingProgress
        {
            [HarmonyPrefix]
            public static void Prefix(NetworkLevelLoader __instance, ref float _progress)
            {
                if (_progress > 0.6f)
                    CreateNewCharacter();

                if (_progress >= 1f)
                {
                    Character player = CharacterManager.Instance.GetFirstLocalCharacter();
                    if (setMaxStats)
                    {
                        logboy.Log(LogLevel.Message, "Resetting Stats");
                        player.Stats.RestoreAllVitals();
                        setMaxStats = false;
                        FixQuickSlots(player);
                        MarkAllSkillsAsNotNew(player);
                    }
                    // Do special stuff for legacy characters
                    if (SaveData.TryGetValue(player.UID, out NewGameExtension nge) && nge.LegacyLevel > 0)
                    {
                        // Check if LegacySkills is blank or empty
                        if (nge.LegacySkills == null || nge.LegacySkills.Length == 0)
                        {
                            List<int> skills = new List<int>();
                            foreach (Item item in player.Inventory.SkillKnowledge.GetLearnedItems())
                            {
                                if (!skills.Contains(item.ItemID))
                                    skills.Add(item.ItemID);
                            }
                            SaveData[player.UID].LegacySkills = skills.ToArray();
                            Log("Loaded LegacySkills for " + player.Name + ": " + skills.Count);
                        }


                        if (EnableDebuff.Value)
                        {
                            // Check if debuffs exist, if not apply them
                            if (nge.LegacyLevel > 0)
                            {
                                LevelStatusEffect temp = (LevelStatusEffect)player.StatusEffectMngr.GetStatusEffectOfName("Stretched Thin");
                                if (temp == null)
                                    temp = (LevelStatusEffect)player.StatusEffectMngr.AddStatusEffect("Stretched Thin");
                                temp.IncreaseLevel(nge.LegacyLevel - temp.CurrentLevel);
                            }
                        }

                    }
                }
            }
        }

        // to allow purchasing of all skills
        [HarmonyPatch(typeof(SkillSlot), "IsBlocked", new Type[] { typeof(Character), typeof(bool) })]
        public class SkillSlot_IsBlocked
        {
            [HarmonyPrefix]
            public static bool Prefix(SkillSlot __instance, ref bool __result, Character _character, ref bool _notify)
            {
                // If you gained this skill as a legacy skill, then show it as blocked
                if (NewGamePlus.CharacterHasLegacySkill(_character, __instance.Skill))
                {
                    __result = true;
                    return false;
                }

                if (__instance.SiblingSlot != null && NewGamePlus.CharacterHasLegacySkill(_character, __instance.SiblingSlot.Skill))
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }
    }
}