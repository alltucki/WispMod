using System;
using R2API;
using RoR2;
using UnityEngine;
using static On.RoR2.DotController;

namespace WispSurvivor.Modules
{
    public static class Buffs
    {
        internal static BuffIndex siphonSelf, siphonTarget;
        internal static BuffIndex sustainSelf, sustainTarget;

        internal static BuffIndex haste;
        internal static BuffIndex slow;
        internal static BuffIndex invigorate, regenMinus;
        internal static DotController.DotIndex degenDot;

        internal static void RegisterBuffs()
        {
            siphonSelf = AddNewBuff("WispSiphonSelf", "textures/bufficons/texBuffBodyArmorIcon", Color.blue, false, false);
            siphonTarget = AddNewBuff("WispSiphonTarget", "textures/bufficons/texBuffBodyArmorIcon", Color.clear, false, true);
            Debug.Log("Registered siphon buffs");

            sustainSelf = AddNewBuff("WispSustainSelf", "textures/bufficons/texBuffPowerIcon", Color.clear, false, false);
            sustainTarget = AddNewBuff("WispSustainTarget", "textures/bufficons/texBuffPowerIcon", Color.clear, false, false);
            Debug.Log("Registered sustain buffs");

            haste = AddNewBuff("WispHaste", "Textures/BuffIcons/texBuffTempestSpeedIcon", Color.red, true, false);

            slow = AddNewBuff("WispSlow", "Textures/BuffIcons/texBuffCrippleIcon", Color.cyan, true, false);

            invigorate = AddNewBuff("WispInvigorate", "Textures/BuffIcons/texBuffRegenBoostIcon", Color.green, true, false);
            regenMinus = AddNewBuff("WispRegenMinus", "Textures/BuffIcons/texBuffWeakIcon", Color.magenta, true, true);

            degenDot = DotAPI.RegisterDotDef(.8f, 2f, DamageColorIndex.Bleed, regenMinus);
        }

        internal static BuffIndex AddNewBuff(string buffName, string iconPath, Color buffColor, bool canStack, bool isDebuff)
        {
            CustomBuff customBuff = new CustomBuff(new BuffDef
            {
                name = buffName,
                iconPath = iconPath,
                buffColor = buffColor,
                canStack = canStack,
                isDebuff = isDebuff,
                eliteIndex = EliteIndex.None
            });

            return BuffAPI.Add(customBuff);
        }
    }
}
