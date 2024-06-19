using ProtoBuf;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace SimpleCommands {
    public class SimpleCommands : ModSystem {
        public static AssetLocation TeleportSound = new AssetLocation("game", "sounds/effect/translocate-breakdimension");
        public static AssetLocation SetHomeSound = new AssetLocation("game", "sounds/tutorialstepsuccess");


        public Dictionary<string, PersistentData> pData;
        [ProtoContract] public class PersistentData {
            [ProtoMember(10)] public bool HomeSet;
            [ProtoMember(11)] public Vec3d HomePosition;

            [ProtoMember(20)] public bool HasDied;
            [ProtoMember(21)] public Vec3d DeathPosition;
        }
        public void EnsureEntryExists (string uid) {
            if (!pData.ContainsKey(uid)) {
                pData.Add(uid, new PersistentData());
            }
        }


        public override void StartServerSide(ICoreServerAPI API) {
            base.StartServerSide(API);


            API.Event.SaveGameLoaded += () => OnSaveGameLoading(API);
            API.Event.GameWorldSave += () => OnSaveGameSaving(API);
            API.Event.PlayerDeath += OnPlayerDeath;


            API.ChatCommands.Create("sethome")
            .WithDescription("Set your home location")
            .RequiresPrivilege(Privilege.chat)
            .RequiresPlayer()
            .HandleWith((args) => {
                var player = args.Caller.Player;
                var uid = player.PlayerUID;
                var position = player.Entity.Pos.XYZ;

                EnsureEntryExists(uid);
                pData[uid].HomeSet = true;
                pData[uid].HomePosition = position;

                API.World.PlaySoundAt(SetHomeSound, player);
                return TextCommandResult.Success($"Home set. ({position})");
            });

            API.ChatCommands.Create("home")
            .WithDescription("Return to your home location")
            .RequiresPrivilege(Privilege.chat)
            .RequiresPlayer()
            .HandleWith((args) => {
                var player = args.Caller.Player;
                var uid = player.PlayerUID;

                EnsureEntryExists(uid);

                if (pData[uid].HomeSet) {
                    var pos = pData[uid].HomePosition;

                    API.World.PlaySoundAt(TeleportSound, player);
                    API.World.PlaySoundAt(TeleportSound, pos.X, pos.Y, pos.Z);
                    player.Entity.TeleportTo(pos);

                    return TextCommandResult.Success("Returning to home...");
                } else {
                    return TextCommandResult.Error("Home not set!");
                }
            });

            API.ChatCommands.Create("back")
            .WithDescription("Return to the place of your last death")
            .RequiresPrivilege(Privilege.chat)
            .RequiresPlayer()
            .HandleWith((args) => {
                var player = args.Caller.Player;
                var uid = player.PlayerUID;

                EnsureEntryExists(uid);

                if (!pData[uid].HasDied) {
                    return TextCommandResult.Error("No death position found.");
                } else {
                    var pos = pData[uid].DeathPosition;

                    API.World.PlaySoundAt(TeleportSound, player);
                    API.World.PlaySoundAt(TeleportSound, pos.X, pos.Y, pos.Z);
                    player.Entity.TeleportTo(pos);

                    // Reset again
                    pData[uid].HasDied = false;

                    return TextCommandResult.Success("Returning to last death location...");
                }
            });
        }

        protected void OnSaveGameLoading(ICoreServerAPI API) {
            var bytes = API.WorldManager.SaveGame.GetData("SimpleCommandsPersistent");

            if (bytes != null) {
                pData = SerializerUtil.Deserialize<Dictionary<string, PersistentData>>(bytes);
            } else {
                pData = new Dictionary<string, PersistentData>();
            }
        }
        protected void OnSaveGameSaving(ICoreServerAPI API) {
            var bytes = SerializerUtil.Serialize(pData);
            API.WorldManager.SaveGame.StoreData("SimpleCommandsPersistent", bytes);
        }


        public void OnPlayerDeath (IServerPlayer player, DamageSource source) {
            var uid = player.PlayerUID;

            EnsureEntryExists(uid);

            pData[uid].DeathPosition = player.Entity.Pos.XYZ;
            pData[uid].HasDied = true;
        }
    }
}