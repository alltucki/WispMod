using EntityStates;
using R2API;
using R2API.Utils;
using RoR2;
using RoR2.Skills;
using System;
using UnityEngine;

namespace WispSurvivor.Modules.Survivors
{
    internal static class Wisp
    {
        public static SkillDef primarySkillDef;
        public static SkillDef hasteSkillDef;
        public static SkillDef invigorateSkillDef;
        public static SkillDef burstSkillDef;

        public static void CreateSkills()
        {
            PrimarySetup();
            BuffsSetup();
            UtilitySetup();
            SpecialSetup();
        }

        private static void PrimarySetup()
        {
            SkillLocator component = WispSurvivor.characterPrefab.GetComponent<SkillLocator>();

            LanguageAPI.Add("WISP_PRIMARY_NAME", "Giddy Flame");
            LanguageAPI.Add("WISP_PRIMARY_DESCRIPTION", "Fire a pulse of energy, dealing <style=cIsDamage>200% damage</style>.");

            // set up your primary skill def here!

            SkillDef mySkillDef = ScriptableObject.CreateInstance<SkillDef>();
            mySkillDef.activationState = new SerializableEntityStateType(typeof(EntityStates.WispSurvivorStates.WispFireball));
            mySkillDef.activationStateMachineName = "Weapon";
            mySkillDef.baseMaxStock = 4;
            mySkillDef.baseRechargeInterval = 2f;
            mySkillDef.beginSkillCooldownOnSkillEnd = false;
            mySkillDef.canceledFromSprinting = false;
            mySkillDef.fullRestockOnAssign = true;
            mySkillDef.interruptPriority = InterruptPriority.Any;
            mySkillDef.isBullets = false;
            mySkillDef.isCombatSkill = true;
            mySkillDef.mustKeyPress = false;
            mySkillDef.noSprint = true;
            mySkillDef.rechargeStock = 4;
            mySkillDef.requiredStock = 1;
            mySkillDef.shootDelay = .2f;
            mySkillDef.stockToConsume = 1;
            mySkillDef.icon = Assets.icon1;
            mySkillDef.skillDescriptionToken = "WISP_PRIMARY_DESCRIPTION";
            mySkillDef.skillName = "WISP_PRIMARY_NAME";
            mySkillDef.skillNameToken = "WISP_PRIMARY_NAME";

            LoadoutAPI.AddSkillDef(mySkillDef);

            component.primary = WispSurvivor.characterPrefab.AddComponent<GenericSkill>();
            SkillFamily newFamily = ScriptableObject.CreateInstance<SkillFamily>();
            newFamily.variants = new SkillFamily.Variant[1];
            LoadoutAPI.AddSkillFamily(newFamily);
            component.primary.SetFieldValue("_skillFamily", newFamily);
            SkillFamily skillFamily = component.primary.skillFamily;

            skillFamily.variants[0] = new SkillFamily.Variant
            {
                skillDef = mySkillDef,
                unlockableName = "",
                viewableNode = new ViewablesCatalog.Node(mySkillDef.skillNameToken, false, null)
            };


            // add this code after defining a new skilldef if you're adding an alternate skill

            /*Array.Resize(ref skillFamily.variants, skillFamily.variants.Length + 1);
            skillFamily.variants[skillFamily.variants.Length - 1] = new SkillFamily.Variant
            {
                skillDef = newSkillDef,
                unlockableName = "",
                viewableNode = new ViewablesCatalog.Node(newSkillDef.skillNameToken, false, null)
            };*/
        }

        private static void BuffsSetup()
        {

            SkillLocator component = WispSurvivor.characterPrefab.GetComponent<SkillLocator>();

            #region hasteslow
            LanguageAPI.Add("WISP_HASTE_NAME", "Haste");
            LanguageAPI.Add("WISP_HASTE_DESCRIPTION", "Increase attack and movespeed by <style=cIsUtility>150%</style>. Duplicate effect on <style=cIsHealing>partner</style>. Reduce " +
                "attack and movespeed of <style=cIsHealing>tethered</style> enemies.");

            // set up your primary skill def here!

            SkillDef mySkillDef = ScriptableObject.CreateInstance<SkillDef>();
            mySkillDef.activationState = new SerializableEntityStateType(typeof(EntityStates.WispSurvivorStates.WispHasteSkillState));
            mySkillDef.activationStateMachineName = "Weapon";
            mySkillDef.baseMaxStock = 1;
            mySkillDef.baseRechargeInterval = 9f;
            mySkillDef.beginSkillCooldownOnSkillEnd = true;
            mySkillDef.canceledFromSprinting = false;
            mySkillDef.fullRestockOnAssign = true;
            mySkillDef.interruptPriority = InterruptPriority.Skill;
            mySkillDef.isBullets = false;
            mySkillDef.isCombatSkill = false;
            mySkillDef.mustKeyPress = false;
            mySkillDef.noSprint = false;
            mySkillDef.rechargeStock = 1;
            mySkillDef.requiredStock = 1;
            mySkillDef.shootDelay = 0f;
            mySkillDef.stockToConsume = 1;
            mySkillDef.icon = Assets.icon2;
            mySkillDef.skillDescriptionToken = "WISP_HASTE_DESCRIPTION";
            mySkillDef.skillName = "WISP_HASTE_NAME";
            mySkillDef.skillNameToken = "WISP_HASTE_NAME";

            LoadoutAPI.AddSkillDef(mySkillDef);
            hasteSkillDef = mySkillDef;
            component.secondary = WispSurvivor.characterPrefab.AddComponent<GenericSkill>();
            SkillFamily newFamily = ScriptableObject.CreateInstance<SkillFamily>();
            newFamily.variants = new SkillFamily.Variant[1];
            LoadoutAPI.AddSkillFamily(newFamily);
            component.secondary.SetFieldValue("_skillFamily", newFamily);
            SkillFamily skillFamily = component.secondary.skillFamily;

            
            skillFamily.variants[0] = new SkillFamily.Variant
            {
                skillDef = mySkillDef,
                unlockableName = "",
                viewableNode = new ViewablesCatalog.Node(mySkillDef.skillNameToken, false, null)
            };
            #endregion

            /*
            // add this code after defining a new skilldef if you're adding an alternate skill
            Array.Resize(ref skillFamily.variants, skillFamily.variants.Length + 1);
            skillFamily.variants[skillFamily.variants.Length - 1] = new SkillFamily.Variant
            {
                skillDef = mySkillDef,
                unlockableName = "",
                viewableNode = new ViewablesCatalog.Node(mySkillDef.skillNameToken, false, null)
            };
            */

            LanguageAPI.Add("WISP_REGEN_NAME", "Invigorate");
            LanguageAPI.Add("WISP_REGEN_DESCRIPTION", "Increase healing rate and base damage. Duplicate effect on <style=cIsHealing>partner</style>. " +
                "Deal <style=cIsDamage>100%</style> damage per second to <style=cIsHealing>tethered</style> enemies.");

            // set up your primary skill def here!

            SkillDef secondarySkillDef = ScriptableObject.CreateInstance<SkillDef>();
            secondarySkillDef.activationState = new SerializableEntityStateType(typeof(EntityStates.WispSurvivorStates.WispInvigorateSkillState));
            secondarySkillDef.activationStateMachineName = "Weapon";
            secondarySkillDef.baseMaxStock = 1;
            secondarySkillDef.baseRechargeInterval = 9f;
            secondarySkillDef.beginSkillCooldownOnSkillEnd = true;
            secondarySkillDef.canceledFromSprinting = false;
            secondarySkillDef.fullRestockOnAssign = true;
            secondarySkillDef.interruptPriority = InterruptPriority.Skill;
            secondarySkillDef.isBullets = false;
            secondarySkillDef.isCombatSkill = false;
            secondarySkillDef.mustKeyPress = false;
            secondarySkillDef.noSprint = false;
            secondarySkillDef.rechargeStock = 1;
            secondarySkillDef.requiredStock = 1;
            secondarySkillDef.shootDelay = 0f;
            secondarySkillDef.stockToConsume = 1;
            secondarySkillDef.icon = Assets.icon2_invigorate;
            secondarySkillDef.skillDescriptionToken = "WISP_REGEN_DESCRIPTION";
            secondarySkillDef.skillName = "WISP_REGEN_NAME";
            secondarySkillDef.skillNameToken = "WISP_REGEN_NAME";

            invigorateSkillDef = secondarySkillDef;
            LoadoutAPI.AddSkillDef(secondarySkillDef);

            Array.Resize(ref skillFamily.variants, skillFamily.variants.Length + 1);
            skillFamily.variants[skillFamily.variants.Length - 1] = new SkillFamily.Variant
            {
                skillDef = secondarySkillDef,
                unlockableName = "",
                viewableNode = new ViewablesCatalog.Node(secondarySkillDef.skillNameToken, false, null)
            };
        }

        private static void UtilitySetup()
        {
            SkillLocator component = WispSurvivor.characterPrefab.GetComponent<SkillLocator>();

            LanguageAPI.Add("WISP_SIPHON_NAME", "Siphon");
            LanguageAPI.Add("WISP_SIPHON_DESCRIPTION", "<style=cIsHealing>Tether</style> to the nearest enemy. Drain their health and convert into <style=cShrine>barrier</style>");

            SkillDef mySkillDef = ScriptableObject.CreateInstance<SkillDef>();
            mySkillDef.activationState = new SerializableEntityStateType(typeof(EntityStates.WispSurvivorStates.WispSiphon));
            mySkillDef.activationStateMachineName = "Weapon";
            mySkillDef.baseMaxStock = 1;
            mySkillDef.baseRechargeInterval = 1f;
            mySkillDef.beginSkillCooldownOnSkillEnd = true;
            mySkillDef.canceledFromSprinting = false;
            mySkillDef.fullRestockOnAssign = true;
            mySkillDef.interruptPriority = InterruptPriority.PrioritySkill;
            mySkillDef.isBullets = false;
            mySkillDef.isCombatSkill = false;
            mySkillDef.mustKeyPress = true;
            mySkillDef.noSprint = false;
            mySkillDef.rechargeStock = 1;
            mySkillDef.requiredStock = 1;
            mySkillDef.shootDelay = 1f;
            mySkillDef.stockToConsume = 1;
            mySkillDef.icon = Assets.icon3_siphon;
            mySkillDef.skillDescriptionToken = "WISP_SIPHON_DESCRIPTION";
            mySkillDef.skillName = "WISP_SIPHON_NAME";
            mySkillDef.skillNameToken = "WISP_SIPHON_NAME";

            LoadoutAPI.AddSkillDef(mySkillDef);
            component.utility = WispSurvivor.characterPrefab.AddComponent<GenericSkill>();
            SkillFamily newFamily = ScriptableObject.CreateInstance<SkillFamily>();
            newFamily.variants = new SkillFamily.Variant[1];
            LoadoutAPI.AddSkillFamily(newFamily);
            component.utility.SetFieldValue("_skillFamily", newFamily);
            SkillFamily skillFamily = component.utility.skillFamily;

            skillFamily.variants[0] = new SkillFamily.Variant
            {
                skillDef = mySkillDef,
                unlockableName = "",
                viewableNode = new ViewablesCatalog.Node(mySkillDef.skillNameToken, false, null)
            };

            LanguageAPI.Add("WISP_TETHER_NAME", "Tether");
            LanguageAPI.Add("WISP_TETHER_DESCRIPTION", "<style=cIsHealing>Tether</style> to the nearest ally and increase regen rate. Gain <style=cShrine>barrier</style> when they deal damage.");

            mySkillDef = ScriptableObject.CreateInstance<SkillDef>();
            mySkillDef.activationState = new SerializableEntityStateType(typeof(EntityStates.WispSurvivorStates.WispTether));
            mySkillDef.activationStateMachineName = "Weapon";
            mySkillDef.baseMaxStock = 1;
            mySkillDef.baseRechargeInterval = 1f;
            mySkillDef.beginSkillCooldownOnSkillEnd = true;
            mySkillDef.canceledFromSprinting = false;
            mySkillDef.fullRestockOnAssign = true;
            mySkillDef.interruptPriority = InterruptPriority.PrioritySkill;
            mySkillDef.isBullets = false;
            mySkillDef.isCombatSkill = false;
            mySkillDef.mustKeyPress = true;
            mySkillDef.noSprint = false;
            mySkillDef.rechargeStock = 1;
            mySkillDef.requiredStock = 1;
            mySkillDef.shootDelay = 1f;
            mySkillDef.stockToConsume = 1;
            mySkillDef.icon = Assets.icon3_tether;
            mySkillDef.skillDescriptionToken = "WISP_TETHER_DESCRIPTION";
            mySkillDef.skillName = "WISP_TETHER_NAME";
            mySkillDef.skillNameToken = "WISP_TETHER_NAME";

            // add this code after defining a new skilldef if you're adding an alternate skill

            Array.Resize(ref skillFamily.variants, skillFamily.variants.Length + 1);
            skillFamily.variants[skillFamily.variants.Length - 1] = new SkillFamily.Variant
            {
                skillDef = mySkillDef,
                unlockableName = "",
                viewableNode = new ViewablesCatalog.Node(mySkillDef.skillNameToken, false, null)
            };
        }


        private static void SpecialSetup()
        {
            SkillLocator component = WispSurvivor.characterPrefab.GetComponent<SkillLocator>();

            LanguageAPI.Add("EXAMPLESURVIVOR_SPECIAL_BURST_NAME", "Burst");
            LanguageAPI.Add("EXAMPLESURVIVOR_SPECIAL_BURST_DESCRIPTION", "Shatter your barrier for <style=cIsDamage>700-800%</style> damage. " +
                "If tethered, duplicate explosion on target.");

            // set up your primary skill def here!

            SkillDef mySkillDef = ScriptableObject.CreateInstance<SkillDef>();
            mySkillDef.activationState = new SerializableEntityStateType(typeof(EntityStates.WispSurvivorStates.WispBurst));
            mySkillDef.activationStateMachineName = "Weapon";
            mySkillDef.baseMaxStock = 1;
            mySkillDef.baseRechargeInterval = 7f;
            mySkillDef.beginSkillCooldownOnSkillEnd = true;
            mySkillDef.canceledFromSprinting = false;
            mySkillDef.fullRestockOnAssign = true;
            mySkillDef.interruptPriority = InterruptPriority.Skill;
            mySkillDef.isBullets = false;
            mySkillDef.isCombatSkill = true;
            mySkillDef.mustKeyPress = true;
            mySkillDef.noSprint = false;
            mySkillDef.rechargeStock = 1;
            mySkillDef.requiredStock = 1;
            mySkillDef.shootDelay = 1f;
            mySkillDef.stockToConsume = 1;
            mySkillDef.icon = Assets.icon4;
            mySkillDef.skillDescriptionToken = "EXAMPLESURVIVOR_SPECIAL_BURST_DESCRIPTION";
            mySkillDef.skillName = "EXAMPLESURVIVOR_SPECIAL_BURST_NAME";
            mySkillDef.skillNameToken = "EXAMPLESURVIVOR_SPECIAL_BURST_NAME";

            LoadoutAPI.AddSkillDef(mySkillDef);
            component.special = WispSurvivor.characterPrefab.AddComponent<GenericSkill>();
            SkillFamily newFamily = ScriptableObject.CreateInstance<SkillFamily>();
            newFamily.variants = new SkillFamily.Variant[1];
            LoadoutAPI.AddSkillFamily(newFamily);
            component.special.SetFieldValue("_skillFamily", newFamily);
            SkillFamily skillFamily = component.special.skillFamily;

            skillFamily.variants[0] = new SkillFamily.Variant
            {
                skillDef = mySkillDef,
                unlockableName = "",
                viewableNode = new ViewablesCatalog.Node(mySkillDef.skillNameToken, false, null)
            };


            // add this code after defining a new skilldef if you're adding an alternate skill

            /*Array.Resize(ref skillFamily.variants, skillFamily.variants.Length + 1);
            skillFamily.variants[skillFamily.variants.Length - 1] = new SkillFamily.Variant
            {
                skillDef = newSkillDef,
                unlockableName = "",
                viewableNode = new ViewablesCatalog.Node(newSkillDef.skillNameToken, false, null)
            };*/
        }
    }
}
