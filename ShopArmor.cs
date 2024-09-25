using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using ShopAPI;

namespace ShopArmor
{
    public class ShopArmor : BasePlugin
    {
        public override string ModuleName => "[SHOP] Armor";
        public override string ModuleDescription => "";
        public override string ModuleAuthor => "E!N";
        public override string ModuleVersion => "v1.0.1";

        private IShopApi? SHOP_API;
        private const string CategoryName = "Armor";
        public static JObject? JsonArmor { get; private set; }
        private readonly PlayerArmor[] playerArmor = new PlayerArmor[65];

        public override void OnAllPluginsLoaded(bool hotReload)
        {
            SHOP_API = IShopApi.Capability.Get();
            if (SHOP_API == null) return;

            LoadConfig();
            InitializeShopItems();
            SetupTimersAndListeners();
        }

        private void LoadConfig()
        {
            string configPath = Path.Combine(ModuleDirectory, "../../configs/plugins/Shop/Armor.json");
            if (File.Exists(configPath))
            {
                JsonArmor = JObject.Parse(File.ReadAllText(configPath));
            }
        }

        private void InitializeShopItems()
        {
            if (JsonArmor == null || SHOP_API == null) return;

            SHOP_API.CreateCategory(CategoryName, "Броня");

            var sortedItems = JsonArmor
                .Properties()
                .Select(p => new { Key = p.Name, Value = (JObject)p.Value })
                .OrderBy(p => (int)p.Value["armor"]!)
                .ToList();

            foreach (var item in sortedItems)
            {
                Task.Run(async () =>
                {
                    int itemId = await SHOP_API.AddItem(item.Key, (string)item.Value["name"]!, CategoryName, (int)item.Value["price"]!, (int)item.Value["sellprice"]!, (int)item.Value["duration"]!);
                    SHOP_API.SetItemCallbacks(itemId, OnClientBuyItem, OnClientSellItem, OnClientToggleItem);
                }).Wait();
            }
        }

        private void SetupTimersAndListeners()
        {
            RegisterListener<Listeners.OnClientDisconnect>(playerSlot => playerArmor[playerSlot] = null!);
        }

        public HookResult OnClientBuyItem(CCSPlayerController player, int itemId, string categoryName, string uniqueName, int buyPrice, int sellPrice, int duration, int count)
        {
            if (TryGetNumberOfArmor(uniqueName, out int Armor))
            {
                playerArmor[player.Slot] = new PlayerArmor(Armor, itemId);
            }
            else
            {
                Logger.LogError($"{uniqueName} has invalid or missing 'armor' in config!");
            }
            return HookResult.Continue;
        }

        public HookResult OnClientToggleItem(CCSPlayerController player, int itemId, string uniqueName, int state)
        {
            if (state == 1 && TryGetNumberOfArmor(uniqueName, out int Armor))
            {
                playerArmor[player.Slot] = new PlayerArmor(Armor, itemId);
            }
            else if (state == 0)
            {
                OnClientSellItem(player, itemId, uniqueName, 0);
            }
            return HookResult.Continue;
        }

        public HookResult OnClientSellItem(CCSPlayerController player, int itemId, string uniqueName, int sellPrice)
        {
            playerArmor[player.Slot] = null!;
            return HookResult.Continue;
        }

        [GameEventHandler]
        public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
        {
            var player = @event.Userid;
            if (player != null && !player.IsBot && playerArmor[player.Slot] != null)
            {
                GiveArmor(player);
            }
            return HookResult.Continue;
        }

        private void GiveArmor(CCSPlayerController player)
        {
            var playerPawn = player.PlayerPawn.Value;

            var ArmorValue = playerArmor[player.Slot].Armor;

            if (ArmorValue <= 0 || playerPawn == null) return;

            if (playerPawn.ItemServices != null)
                new CCSPlayer_ItemServices(playerPawn.ItemServices.Handle).HasHelmet = true;

            playerPawn.ArmorValue = ArmorValue;
            Utilities.SetStateChanged(playerPawn, "CCSPlayerPawn", "m_ArmorValue");
        }

        private static bool TryGetNumberOfArmor(string uniqueName, out int Armor)
        {
            Armor = 0;
            if (JsonArmor != null && JsonArmor.TryGetValue(uniqueName, out var obj) && obj is JObject jsonItem && jsonItem["armor"] != null && jsonItem["armor"]!.Type != JTokenType.Null)
            {
                Armor = (int)jsonItem["armor"]!;
                return true;
            }
            return false;
        }

        public record PlayerArmor(int Armor, int ItemID);
    }
}