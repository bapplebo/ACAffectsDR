using HarmonyLib;
using UnityModManagerNet;

namespace MyNewMod
{
    public class Main
    {
        public static bool Enabled;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            new Harmony(modEntry.Info.Id).PatchAll();
            modEntry.OnToggle = OnToggle;
            return true;
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            Enabled = value;
            return true;
        }
    }
}
