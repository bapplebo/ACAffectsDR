using HarmonyLib;
using Kingmaker.Blueprints.Items.Armors;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.QA;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules.Damage;
using System;
using System.Runtime.CompilerServices;
using Owlcat.Runtime.Core.Logging;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.EntitySystem;

namespace MyNewMod
{
    internal static class ACAffectsDR
    {
        private static bool IncludeACFromNaturalArmour = false;
        private static bool IncludeACFromNonArmourSources = false;
        // AC to bonus DR convertion radio. Default 50%. Rounded down. Values are 0, 1, 2, 3, 4, with 0 being 0% and 4 being 100% AC to DR
        private static int ArmourACToDRRatio = 2;
        // Armor DR progression type. Default none; can be set to "+2.5% every level" and "+5% every level". Must have at least 1 point of armor DR to receive this effect. Does not affect enemies. These percentages are directly added to the bonus DR convertion radio.
        private static int ArmourACToDRProgressionType = 0;
        // Armor DR penalty type. Default none; can be set to "-1" and "-2". Does not affect enemies. Does not cause DR to become minus.
        private static int ArmorAC_to_DR_penalty_type = 0;
        // What kind of damage should the DR apply to. By default, physical and elemental damage only. Can be set to physical only, or apply to all damage, even force damage.
        private static int ArmorDR_protection_type = 1;

        private static LogChannel logChannel = LogChannelFactory.GetOrCreate("ACAffectsDR");

        private static int GetUnitArmourDRTotal(UnitEntityData unit, bool neglectProgression = false)
        {
            int AC = 0;
            foreach (ModifiableValue.Modifier modifier in unit.Stats.AC.Modifiers)
            {
                if (modifier.ModDescriptor is ModifierDescriptor.Armor or ModifierDescriptor.ArmorEnhancement or ModifierDescriptor.ArmorFocus)
                {
                    if (IncludeACFromNonArmourSources || modifier.ItemSource != null && modifier.ItemSource.Blueprint is BlueprintItemArmor)
                    {
                        AC += modifier.ModValue;
                    }
                }
                else if (IncludeACFromNaturalArmour && modifier.ModDescriptor is ModifierDescriptor.NaturalArmor or ModifierDescriptor.NaturalArmorEnhancement or ModifierDescriptor.NaturalArmorForm)
                {
                    AC += modifier.ModValue;
                }
            }

            return AC > 0 ? ConvertArmourACToDR(AC, unit, neglectProgression) : 0;
        }

        private static int ConvertArmourACToDR(int AC, UnitEntityData unit, bool neglectProgression = false)
        {
            var damageReduction = (int)Math.Floor(AC * ArmourACToDRRatio * 0.25);
            if (damageReduction > 0 && unit.IsPlayerFaction)
            {
                if (!neglectProgression && ArmourACToDRProgressionType > 0)
                {
                    var characterLevel = unit.Descriptor.Progression.CharacterLevel;
                    damageReduction = (int)Math.Floor(AC * ArmourACToDRRatio * 0.25 + characterLevel * 0.025 * ArmourACToDRProgressionType);
                }
            }

            return damageReduction > 0 ? damageReduction : 0;
        }

        [HarmonyPatch(typeof (RuleCalculateDamage), "OnTrigger")]
        private static class RuleCalculateDamage_OnTrigger_Patch
        {
            private static void Postfix(RuleCalculateDamage __instance)
            {
                if (!Main.Enabled)
                {
                    return;
                }

                int unitArmourDRTotal = GetUnitArmourDRTotal(__instance.Target);
                logChannel.Log($"unitArmourDRTotal: {unitArmourDRTotal}");
                if (unitArmourDRTotal > 0)
                {
                    for (int i = 0; i < __instance.CalculatedDamage.Count; i++)
                    {
                        var damage = __instance.CalculatedDamage[i];
                        logChannel.Log($"Old DR for {__instance.Target.CharacterName}: {damage.Reduction}");
                        logChannel.Log($"Total damage before new DR: {damage.FinalValue}");

                        var fact = new EntityFact();
                        // damage.Source.ReductionBecauseResistance.Add(new Modifier(damage.Source.ReductionBecauseResistance + unitArmourDRTotal, ModifierDescriptor.Other));
                        damage.Source.SetReductionBecauseResistance(damage.Source.ReductionBecauseResistance + unitArmourDRTotal, fact);
                        
                        logChannel.Log($"New DR for {__instance.Target.CharacterName}: {damage.Reduction}");
                        logChannel.Log($"Total damage: {damage.FinalValue}");
                    }
                }
            }
        }
    }
}
