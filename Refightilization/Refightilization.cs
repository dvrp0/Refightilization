﻿using BepInEx;
using RoR2;
using RoR2.Navigation;
using R2API.Utils;
using MonsterVariants;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Networking;

namespace Wonda
{
    [NetworkCompatibility(CompatibilityLevel.NoNeedForSync)]
    [BepInDependency("com.bepis.r2api", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("com.rob.MonsterVariants", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.ThinkInvisible.TILER2", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.ThinkInvisible.ClassicItems", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.amogus_lovers.StandaloneAncientScepter", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(guid, modName, version)]
    public class Refightilization : BaseUnityPlugin
    {
        // Setting up variables and waking the program up.
        //

        // Cool info B)
        const string guid = "com.Wonda.Refightilization";
        const string modName = "Refightilization";
        const string version = "1.0.3";

        // Config
        private RefightilizationConfig _config;

        // Additional mods to hook into for balance reasons.
        private PluginInfo _monsterVariants;
        private PluginInfo _classicItems;
        private PluginInfo _standaloneAncientScepter;

        // Class to make players easier to manage
        public class PlayerStorage {
            public NetworkUser user;
            public CharacterMaster master;
            public GameObject origPrefab;
            public bool isDead;
            public Inventory inventory;
            public bool giftedAffix;
        }

        // The actual class to use.
        public List<PlayerStorage> playerStorage = new List<PlayerStorage>();

        // Misc variables
        public float respawnTime; // For an added penalty per death.
        public bool metamorphosIsEnabled; // For tracking the artifact of the same name.

        public void Awake()
        {
            _config = new RefightilizationConfig(Config);
            SetupHooks();
            Logger.LogInfo("Loaded Refightilization!");
        }

        public void Start()
        {
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("com.rob.MonsterVariants", out var monsterVariantsPlugin)) _monsterVariants = monsterVariantsPlugin;
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("com.ThinkInvisible.ClassicItems", out var classicItemsPlugin)) _classicItems = classicItemsPlugin;
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("com.amogus_lovers.StandaloneAncientScepter", out var standaloneAncientScepterPlugin)) _standaloneAncientScepter = standaloneAncientScepterPlugin;
        }

        // Hook setup.
        //

        // See!
        private void SetupHooks()
        {
            On.RoR2.Run.Start += Run_Start;
            On.RoR2.GlobalEventManager.OnPlayerCharacterDeath += GlobalEventManager_OnPlayerCharacterDeath;
            On.RoR2.Run.OnServerSceneChanged += Run_OnServerSceneChanged;
            On.RoR2.Run.OnUserAdded += Run_OnUserAdded;
            On.RoR2.TeleporterInteraction.OnInteractionBegin += TeleporterInteraction_OnInteractionBegin;
            On.RoR2.GenericPickupController.AttemptGrant += GenericPickupController_AttemptGrant;
        }

        private void Run_Start(On.RoR2.Run.orig_Start orig, Run self)
        {
            orig(self);
            if (!_config.EnableRefightilization) return;
            respawnTime = _config.RespawnDelay; // Making sure that this function exists on a per run basis.
            metamorphosIsEnabled = RunArtifactManager.instance.IsArtifactEnabled(RoR2Content.Artifacts.randomSurvivorOnRespawnArtifactDef);
            SetupPlayers(); // Gotta make sure players are properly stored once the run begins. (todo: What if players join after the run starts?)
        }

        private void GlobalEventManager_OnPlayerCharacterDeath(On.RoR2.GlobalEventManager.orig_OnPlayerCharacterDeath orig, GlobalEventManager self, DamageReport damageReport, NetworkUser victimNetworkUser)
        {
            orig(self, damageReport, victimNetworkUser);
            if(!_config.EnableRefightilization) return;
            StartCoroutine(RespawnCheck(victimNetworkUser.master.transform.position)); // Spawning in players shortly after a delay.
            respawnTime += _config.AdditionalRespawnTime;
        }

        private void Run_OnServerSceneChanged(On.RoR2.Run.orig_OnServerSceneChanged orig, Run self, string sceneName)
        {
            orig(self, sceneName);
            if (self.stageClearCount == 0 || !_config.EnableRefightilization) return; // Kinda pointless to reset prefabs before the stage begins.
            metamorphosIsEnabled = RunArtifactManager.instance.IsArtifactEnabled(RoR2Content.Artifacts.randomSurvivorOnRespawnArtifactDef);
            respawnTime = _config.RespawnDelay;
            ResetPrefabs(); // Gotta make sure players respawn as their desired class.
        }

        private void Run_OnUserAdded(On.RoR2.Run.orig_OnUserAdded orig, Run self, NetworkUser user)
        {
            orig(self, user);
            if(Run.instance.time > 1f && _config.EnableRefightilization)
                SetupPlayers(false); // For players who enter the game late.
        }

        private void TeleporterInteraction_OnInteractionBegin(On.RoR2.TeleporterInteraction.orig_OnInteractionBegin orig, TeleporterInteraction self, Interactor activator)
        {
            if (_config.EnableRefightilization) { 
                foreach (PlayerStorage player in playerStorage)
                {
                    if (self.isCharged && _monsterVariants != null) RemoveMonsterVariantItems(player.master);
                    if (player.master.GetBody().gameObject == activator.gameObject && player.isDead && playerStorage.Count > 1) return; // If there's multiple players, then dead ones won't be able to activate the teleporter.
                }
            }
            orig(self, activator);
        }

        private void GenericPickupController_AttemptGrant(On.RoR2.GenericPickupController.orig_AttemptGrant orig, GenericPickupController self, CharacterBody body)
        {
            if (_config.EnableRefightilization)
            {
                foreach (PlayerStorage player in playerStorage)
                {
                    if (player.master.GetBody() && player.master.GetBody().gameObject == body.gameObject && player.isDead && _config.ItemPickupToggle)
                        player.master.GetBody().teamComponent.teamIndex = TeamIndex.Player; // Apparently you have to be on the player team to pick up items???
                }
                orig(self, body);
                foreach (PlayerStorage player in playerStorage)
                {
                    if (player.master.GetBody() && player.master.GetBody().gameObject == body.gameObject && player.isDead && _config.ItemPickupToggle)
                        player.master.GetBody().teamComponent.teamIndex = (TeamIndex)_config.RespawnTeam; // Set them back for posterity.
                }
            }
            else orig(self, body);
        }

        // Beginning the *actual* custom code.
        //

        // Adding every player into the playerStorage list so we can easily refer to them and their original prefabs.
        private void SetupPlayers(bool StageUpdate = true)
        {
            Logger.LogInfo("Setting up players...");
            if(StageUpdate) playerStorage.Clear();
            foreach (PlayerCharacterMasterController playerCharacterMaster in PlayerCharacterMasterController.instances)
            {
                // If this is ran mid-stage, just skip over existing players and add anybody who joined.
                if(!StageUpdate && playerStorage != null)
                {
                    bool flag = false;
                    foreach (PlayerStorage player in playerStorage)
                    {
                        if (player.master == playerCharacterMaster.master)
                            flag = true;
                    }
                    if(flag) continue;
                }
                PlayerStorage newPlayer = new PlayerStorage();
                if(playerCharacterMaster.networkUser) newPlayer.user = playerCharacterMaster.networkUser;
                if(playerCharacterMaster.master) newPlayer.master = playerCharacterMaster.master;
                if(playerCharacterMaster.master.bodyPrefab) newPlayer.origPrefab = playerCharacterMaster.master.bodyPrefab;
                if(playerCharacterMaster.master.inventory) newPlayer.inventory = new Inventory();
                playerStorage.Add(newPlayer);
                Logger.LogInfo(newPlayer.user.userName + " added to PlayerStorage!");
            }
            Logger.LogInfo("Setting up players finished.");
            if(!StageUpdate) StartCoroutine(RespawnCheck(new Vector3(0, 0, 0)));
        }

        // Checking for dead players, and respawning them if it seems like they're respawnable.
        IEnumerator RespawnCheck(Vector3 deathPos = new Vector3())
        {
            Logger.LogInfo("Respawn called! Waiting " + respawnTime + " seconds!");

            yield return new WaitForSeconds(respawnTime);

            if(!Run.instance.isActiveAndEnabled) yield break;

            Logger.LogInfo("Checking players...");

            // Wait... Yea disable functionality if the mod is disabled.
            if (!_config.EnableRefightilization)
            {
                Logger.LogInfo("Nevermind. Mod is disabled via config.");
                yield break;
            }
            
            // Okay, prep a flag for if everybody has died and kill the function if PlayerStorage is null for some reason.
            bool isEverybodyDead = true;
            if (playerStorage == null)
            {
                Logger.LogError("PlayerStorage is null!");
                yield break;
            }

            // Iterate through each player and confirm whether or not they've died.
            foreach (PlayerStorage player in playerStorage)
            {
                if (player == null)
                {
                    playerStorage.Remove(player); // This was quickly spledged in and is untested. It'll probably break *everything* if a player leaves mid-game... It probably does already.
                    continue;
                }

                if (player.master.IsDeadAndOutOfLivesServer())
                {
                    Logger.LogInfo(player.user.userName + " passed spawn check!");
                    if(!player.isDead)
                    {
                        player.isDead = true;
                        player.master.teamIndex = (TeamIndex)_config.RespawnTeam;
                        player.inventory.CopyItemsFrom(player.master.inventory);
                        if (_config.RemoveAllItems) player.master.inventory.CopyItemsFrom(new Inventory());
                    }
                    Respawn(player.master, deathPos); // Begin respawning the player.
                }
                else
                {
                    Logger.LogInfo(player.user.userName + " failed spawn check.");
                    if(!player.isDead) isEverybodyDead = false; // Alright, cool. We don't have to kill everybody.
                }
            }

            Logger.LogInfo("Checking players complete.");
            if (isEverybodyDead && _config.EndGameWhenEverybodyDead)
            {
                Logger.LogInfo("Everybody is dead. Forcibly ending the game.");
                ResetPrefabs();
                Run.instance.BeginGameOver(RoR2Content.GameEndings.StandardLoss);
            }

            yield break;
        }

        // Respawning that player.
        private void Respawn(CharacterMaster player, Vector3 deathPos)
        {
            Logger.LogInfo("Attempting player respawn!");

            // Apparently there's an NRE that can happen within this method, so I'm prepping for that possible event.
            if(player == null)
            {
                Logger.LogError("Player is null!? Aborting Respawn.");
                return;
            }

            // Was player previously a monster variant? Gotta take away those items if the server owner wants that.
            if(_monsterVariants != null)
                RemoveMonsterVariantItems(player);

            // Was this player assigned an affix by us?
            if(FindPlayerStorage(player).giftedAffix)
            {
                player.inventory.SetEquipmentIndex(EquipmentIndex.None);
                FindPlayerStorage(player).giftedAffix = false;
            }

            // Grabbing a random spawncard from the monster selection.
            SpawnCard monsterCard = ClassicStageInfo.instance.monsterSelection.Evaluate(Random.Range(0f, 1f)).spawnCard;
            if(monsterCard == null)
            {
                Logger.LogError("Spawn card is null! Retrying Respawn.");
                Respawn(player, deathPos);
                return;
            }

            // Grabbing the prefab from that monster card.
            GameObject randomMonster = BodyCatalog.FindBodyPrefab(monsterCard.prefab.name.Replace("Master", "Body"));
            if (randomMonster == null)
            {
                Logger.LogError("Random monster is null! Retrying Respawn.");
                Respawn(player, deathPos);
                return;
            }

            // Checking to see if the configuration has disabled the selected monster.
            if ((randomMonster.GetComponent<CharacterBody>().isChampion && !_config.AllowBosses) || (randomMonster.name == "ScavengerBody" && !_config.AllowScavengers) || (CheckBlacklist(randomMonster.name) && ClassicStageInfo.instance.monsterSelection.Count > 1))
            {
                Logger.LogInfo(randomMonster.name + " is disabled! Retrying Respawn.");
                Respawn(player, deathPos);
                return;
            }

            // To prevent players from spawning in as the same monster twice in a row.
            if (randomMonster.name == player.bodyPrefab.name)
            {
                Logger.LogInfo(player.playerCharacterMasterController.networkUser.userName + " was already " + randomMonster.name + ". Retrying Respawn.");
                Respawn(player, deathPos);
                return;
            }

            Logger.LogInfo("Found body " + randomMonster.name + ".");

            // Is the Artifact of Metamorphosis enabled?
            if(metamorphosIsEnabled && player.inventory.GetItemCount(RoR2Content.Items.InvadingDoppelganger) <= 0) player.inventory.GiveItem(RoR2Content.Items.InvadingDoppelganger);

            // Does the body have an AncientScepter skill?
            /*
            if (_classicItems != null)
                FixScepterSkillCI(randomMonster);
            if (_standaloneAncientScepter != null)
                FixScepterSkillSAS(randomMonster);
            */

            // Assigning the player to the selected monster prefab.
            player.bodyPrefab = randomMonster;
            Logger.LogInfo(player.playerCharacterMasterController.networkUser.userName + " was assigned to " + player.bodyPrefab.name + ".");

            // Grabbing a viable position for the player to spawn in.
            Vector3 newPos = deathPos;
            if(TeleporterInteraction.instance) newPos = TeleporterInteraction.instance.transform.position + new Vector3(Random.Range(-5, 5), Random.Range(-5, 5), Random.Range(-2, 2));
            player.Respawn(GrabNearestNodePosition(newPos), Quaternion.identity);
            Logger.LogInfo("Respawned " + player.playerCharacterMasterController.networkUser.userName + "!");

            // Stat changes to allow players to not die instantly when they get into the game.
            player.GetBody().baseMaxHealth *= _config.RespawnHealthMultiplier;
            player.GetBody().baseDamage *= _config.RespawnDamageMultiplier;
            if(player.GetBody().GetComponent<DeathRewards>()) player.GetBody().GetComponent<DeathRewards>().goldReward *= (uint)_config.RespawnMoneyMultiplier;
            player.GetBody().baseRegen = 1f;
            player.GetBody().levelRegen = 0.2f;

            // Some fun stuff to allow players to easily get back into combat.
            player.GetBody().AddTimedBuff(RoR2Content.Buffs.ArmorBoost, 15f);
            player.GetBody().AddTimedBuff(RoR2Content.Buffs.CloakSpeed, 30f);

            // And Affixes if the player is lucky.
            if(Util.CheckRoll(_config.RespawnAffixChance, player.playerCharacterMasterController.master) || RunArtifactManager.instance.IsArtifactEnabled(RoR2Content.Artifacts.eliteOnlyArtifactDef))
            {
                if (player.inventory.GetEquipmentIndex() != EquipmentIndex.None) return;

                int i = Random.Range(0, 5);
                EquipmentIndex selectedBuff;
                switch(i)
                {
                    case 0:
                        selectedBuff = RoR2Content.Equipment.AffixRed.equipmentIndex;
                        break;
                    case 1:
                        selectedBuff = RoR2Content.Equipment.AffixBlue.equipmentIndex;
                        break;
                    case 2:
                        selectedBuff = RoR2Content.Equipment.AffixWhite.equipmentIndex;
                        break;
                    case 3:
                        if(Run.instance.loopClearCount > 0)
                            selectedBuff = RoR2Content.Equipment.AffixPoison.equipmentIndex;
                        else
                            selectedBuff = RoR2Content.Equipment.AffixRed.equipmentIndex;
                        break;
                    case 4:
                        if(Run.instance.loopClearCount > 0)
                            selectedBuff = RoR2Content.Equipment.AffixHaunted.equipmentIndex;
                        else
                            selectedBuff = RoR2Content.Equipment.AffixWhite.equipmentIndex;
                        break;
                    case 5:
                        if(Run.instance.loopClearCount > 0)
                            selectedBuff = RoR2Content.Equipment.AffixLunar.equipmentIndex;
                        else
                            selectedBuff = RoR2Content.Equipment.AffixBlue.equipmentIndex;
                        break;
                    default:
                        selectedBuff = RoR2Content.Equipment.AffixRed.equipmentIndex;
                        break;
                }
                player.inventory.SetEquipmentIndex(selectedBuff);
                FindPlayerStorage(player).giftedAffix = true;
            }

            // Broadcasting it to everyone.
            Chat.SendBroadcastChat(new Chat.SimpleChatMessage{ baseToken = "<style=cWorldEvent><sprite name=\"Skull\" tint=1> " + player.playerCharacterMasterController.networkUser.userName + " has inhabited the body of a " + player.GetBody().GetDisplayName() + "! <sprite name=\"Skull\" tint=1></style>" });
        }

        // Making sure that players are set back to their original prefabs.
        private void ResetPrefabs()
        {
            Logger.LogInfo("Resetting player prefabs...");

            // Same as always. Never know what'll cause an NRE.
            if(playerStorage == null)
            {
                Logger.LogError("PlayerStorage is null!");
                return;
            }

            // Iterating through every player and set all the things we've changed back.
            foreach (PlayerStorage player in playerStorage)
            {
                if(player.isDead)
                {
                    player.master.bodyPrefab = player.origPrefab;
                    player.master.preventGameOver = true;
                    player.master.teamIndex = TeamIndex.Player;

                    if (player.giftedAffix)
                    {
                        player.master.inventory.SetEquipmentIndex(EquipmentIndex.None);
                        player.giftedAffix = false;
                    }

                    if (metamorphosIsEnabled && player.inventory.GetItemCount(RoR2Content.Items.InvadingDoppelganger) > 0) player.inventory.RemoveItem(RoR2Content.Items.InvadingDoppelganger);

                    if (_config.RemoveAllItems && _config.ReturnItemsOnStageChange) player.master.inventory.AddItemsFrom(player.inventory);
                    if (_config.ForceItemRestoration) player.master.inventory.CopyItemsFrom(player.inventory);

                    player.isDead = false;
                }
                Logger.LogInfo(player.user.userName + "'s prefab reset.");
            }
            SetupPlayers();
            Logger.LogInfo("Reset player prefabs!");
        }


        // Utility Methods
        //

        // Mooooore stolen coooode. Using RoR's ambush generation code to grab the spawn position for players.
        private Vector3 GrabNearestNodePosition(Vector3 startPos)
        {
            NodeGraph groundNodes = SceneInfo.instance.groundNodes;
            NodeGraph.NodeIndex nodeIndex = groundNodes.FindClosestNode(startPos, HullClassification.BeetleQueen);
            NodeGraphSpider nodeGraphSpider = new NodeGraphSpider(groundNodes, HullMask.BeetleQueen);
            nodeGraphSpider.AddNodeForNextStep(nodeIndex);

            List<NodeGraphSpider.StepInfo> list = new List<NodeGraphSpider.StepInfo>();
            int num = 0;
            List<NodeGraphSpider.StepInfo> collectedSteps = nodeGraphSpider.collectedSteps;
            while (nodeGraphSpider.PerformStep() && num < 8)
            {
                num++;
                for (int i = 0; i < collectedSteps.Count; i++)
                {
                    list.Add(collectedSteps[i]);
                }
                collectedSteps.Clear();
            }
            groundNodes.GetNodePosition(list[Random.Range(0, list.Count - 1)].node, out Vector3 outPos);
            return outPos;
        }

        // A cheap and dirty way of checking to see if a string is in the blacklist.
        private bool CheckBlacklist(string name)
        {
            bool flag = false;
            foreach(string enemy in _config.BlacklistedEnemies)
            {
                if (name == enemy) flag = true;
            }
            return flag;
        }

        private PlayerStorage FindPlayerStorage(CharacterMaster cMaster)
        {
            foreach(PlayerStorage player in playerStorage)
            {
                if (player.master == cMaster)
                    return player;
            }

            return null;
        }

        // Code for handling other mods.
        //

        // Stolen coooode. It takes the AddItems function from MonsterVariants and does everything in reverse.
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void RemoveMonsterVariantItems(CharacterMaster player)
        {
            if (_monsterVariants == null) return;

            if (player != null && player.GetBody().GetComponent<MonsterVariants.Components.VariantHandler>() && player.GetBody().GetComponent<MonsterVariants.Components.VariantHandler>().isVariant && _config.RemoveMonsterVariantItems)
            {
                Logger.LogInfo(player.playerCharacterMasterController.networkUser.userName + " is a Monster Variant. Attempting to remove their items.");

                MonsterVariants.Components.VariantHandler inv = player.GetBody().GetComponent<MonsterVariants.Components.VariantHandler>();

                if (inv.customInventory == null) inv.customInventory = new ItemInfo[0];

                if (inv.customInventory.Length > 0)
                {
                    for (int i = 0; i < inv.customInventory.Length; i++)
                    {
                        player.inventory.GiveItemString(inv.customInventory[i].itemString, -inv.customInventory[i].count); // Possible edge case where it eats up a DIOs if a player spawns as a Jellyfish?
                        Logger.LogInfo("Removing " + player.playerCharacterMasterController.networkUser.userName + "'s " + inv.customInventory[i].count + " " + inv.customInventory[i].itemString + "(s).");
                    }
                }

                if (inv.tier == MonsterVariantTier.Uncommon  || inv.tier == MonsterVariantTier.Rare)
                {
                    player.inventory.RemoveItem(RoR2Content.Items.Infusion);
                    Logger.LogInfo("Removing " + player.playerCharacterMasterController.networkUser.userName + "'s spare Infusion.");
                }

                Destroy(inv);
            }
        }

        /*
        // This one is for Classic Items. Squeezes in a fake skill that halves recharge time.
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void FixScepterSkillCI(GameObject monster)
        {
            Logger.LogInfo("Generating skill for " + monster.name);

            GenericSkill baseSkill = monster.GetComponent<GenericSkill>();

            if(baseSkill)
            {
                Logger.LogInfo("Monster Skill located!");

                if (_classicItems != null)
                {
                    ThinkInvisible.ClassicItems.Scepter.instance.RegisterScepterSkill(baseSkill.skillDef, monster.name, SkillSlot.Primary, 0);
                    Logger.LogInfo("Skill for " + monster.name + " registered!");
                }                
            }
            else
            {
                Logger.LogError("Monster missing Skill Locator!");
            }            
        }

        // This one is for Standalone AS. Same as before.
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void FixScepterSkillSAS(GameObject monster)
        {
            RoR2.Skills.SkillDef newSkill = monster.GetComponent<CharacterBody>().skillLocator.GetSkill(SkillSlot.Primary).skillDef;
            newSkill.baseRechargeInterval /= 2;

            if (_standaloneAncientScepter != null)
                AncientScepter.AncientScepterItem.instance.RegisterScepterSkill(newSkill, monster.name, SkillSlot.Primary, 0);
        
        }*/
    }    
}