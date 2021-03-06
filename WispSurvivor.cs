using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Permissions;
using BepInEx;
using R2API;
using R2API.Utils;
using EntityStates;
using EntityStates.WispSurvivorStates;
using RoR2;
using RoR2.Skills;
using RoR2.Projectile;
using UnityEngine;
using UnityEngine.Networking;
using KinematicCharacterController;
using System.Collections;

namespace WispSurvivor
{

    [BepInDependency("com.bepis.r2api")]

    [BepInPlugin(MODUID, "WispSurvivor", "1.0.0")] // put your own name and version here
    [R2APISubmoduleDependency(nameof(PrefabAPI), nameof(SurvivorAPI), nameof(LoadoutAPI), nameof(EffectAPI), nameof(ItemAPI), nameof(DifficultyAPI), nameof(BuffAPI), nameof(DotAPI), nameof(LanguageAPI))] // need these dependencies for the mod to work properly


    public class WispSurvivor : BaseUnityPlugin
    {
        public const string MODUID = "com.docdino.Wisp"; // put your own names here

        public static GameObject characterPrefab; // the survivor body prefab
        public GameObject characterDisplay; // the prefab used for character select
        public GameObject doppelganger; // umbra shit

        public static GameObject spawnEffect;
        public static GameObject fireballProjectile;
        public static GameObject tetherPrefab;
        public static GameObject burstPrefab;
        public static GameObject burstSecondary;

        private static readonly Color characterColor = new Color(0.55f, 0.55f, 0.55f); // color used for the survivor

        private void Awake()
        {
            On.RoR2.Networking.GameNetworkManager.OnClientConnect += (self, user, t) => { };    //Debugging networking
            Assets.PopulateAssets(); // first we load the assets from our assetbundle
            CreatePrefab(); // then we create our character's body prefab
            RegisterNetworkedEffects();
            RegisterStates(); // register our skill entitystates for networking
            RegisterCharacter(); // and finally put our new survivor in the game
            CreateDoppelganger(); // not really mandatory, but it's simple and not having an umbra is just kinda lame
        }

        private static GameObject CreateModel(GameObject main)
        {
            Destroy(main.transform.Find("ModelBase").gameObject);
            Destroy(main.transform.Find("CameraPivot").gameObject);
            Destroy(main.transform.Find("AimOrigin").gameObject);

            // make sure it's set up right in the unity project
            string model_target = "Orb";
            GameObject model = Assets.MainAssetBundle.LoadAsset<GameObject>(model_target);           

            bool is_null = model == null;

            if(is_null)
            {
                Debug.LogError("Got null value searching for " + model_target);
                Debug.LogError("Valid objects are: ");

                UnityEngine.Object[] assets = Assets.MainAssetBundle.LoadAllAssets();
                for(int i = 0; i < assets.Length; i++) {
                    Debug.LogError(assets[i].name + ", " + assets[i].GetType());   
                }
            }

            GameObject wispReplacement = Resources.Load<GameObject>("prefabs/characterbodies/WispSoulBody");

            //Probably a better way to do this, but...
            Transform wispFire = wispReplacement.transform.Find("Model Base").Find("mdlWisp1Mouth").Find("WispArmature").Find("ROOT").Find("Base").Find("Fire");
            //Shrink the fire and put it above ground
            wispFire.localScale = new Vector3(.5f, .5f, .5f);
            wispFire.localPosition = new Vector3(0f, 1.25f, 0f);
            wispFire.SetParent(model.transform);
            //Debug.Log("Added fire to model");

            return model;
        }

        private static void printChildren(Transform t, int numTabs)
        {
            int numChildren = t.childCount;
            string indent = "";
            for(int i = 0; i < numTabs; i++)
            {
                indent = indent + "\t";
            }

            for(int i = 0; i < numChildren; i++)
            {
                Debug.Log(indent + t.GetChild(i).name);
                if(t.GetChild(i).childCount > 0)
                {
                    printChildren(t.GetChild(i), numTabs + 1);
                }
            }
        }

        internal static void CreatePrefab()
        {
            // first clone the commando prefab so we can turn that into our own survivor
            characterPrefab = PrefabAPI.InstantiateClone(Resources.Load<GameObject>("Prefabs/CharacterBodies/CommandoBody"), "WispPreview", true, "C:\\Users\\test\\Documents\\ror2mods\\ExampleSurvivor\\ExampleSurvivor\\ExampleSurvivor\\ExampleSurvivor.cs", "CreatePrefab", 151);
            //Debug.Log("Loaded prefab");
            characterPrefab.GetComponent<NetworkIdentity>().localPlayerAuthority = true;
            //Debug.Log("Set local player authority");

            #region charactermodel
            // create the model here, we're gonna replace commando's model with our own
            GameObject model = CreateModel(characterPrefab);

            GameObject gameObject = new GameObject("ModelBase");
            gameObject.transform.parent = characterPrefab.transform;
            gameObject.transform.localPosition = new Vector3(0f, -0.81f, 0f);
            gameObject.transform.localRotation = Quaternion.identity;
            gameObject.transform.localScale = new Vector3(1f, 1f, 1f);
            //Debug.Log("Created model base");

            GameObject gameObject2 = new GameObject("CameraPivot");
            gameObject2.transform.parent = gameObject.transform;
            gameObject2.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            gameObject2.transform.localRotation = Quaternion.identity;
            gameObject2.transform.localScale = Vector3.one;
            //Debug.Log("Created camera pivot");

            GameObject gameObject3 = new GameObject("AimOrigin");
            gameObject3.transform.parent = gameObject.transform;
            gameObject3.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            gameObject3.transform.localRotation = Quaternion.identity;
            gameObject3.transform.localScale = Vector3.one;
            //Debug.Log("Created aim origin");

            Transform transform = model.transform;
            transform.parent = gameObject.transform;
            transform.localPosition = Vector3.zero;
            transform.localScale = new Vector3(1f, 1f, 1f);
            transform.localRotation = Quaternion.identity;
            //Debug.Log("Created character transform");

            CharacterDirection characterDirection = characterPrefab.GetComponent<CharacterDirection>();
            characterDirection.moveVector = Vector3.zero;
            characterDirection.targetTransform = gameObject.transform;
            characterDirection.overrideAnimatorForwardTransform = null;
            characterDirection.rootMotionAccumulator = null;
            characterDirection.modelAnimator = model.GetComponentInChildren<Animator>();
            characterDirection.driveFromRootRotation = false;
            characterDirection.turnSpeed = 720f;
            //Debug.Log("Set character direction");
            #endregion

            #region basestats
            // set up the character body here
            CharacterBody bodyComponent = characterPrefab.GetComponent<CharacterBody>();
            bodyComponent.bodyIndex = -1;
            bodyComponent.baseNameToken = "WISP_NAME"; // name token
            bodyComponent.subtitleNameToken = "WISP_SUBTITLE"; // subtitle token- used for umbras
            bodyComponent.bodyFlags = CharacterBody.BodyFlags.ImmuneToExecutes;
            bodyComponent.rootMotionInMainState = false;
            bodyComponent.mainRootSpeed = 0;
            bodyComponent.baseMaxHealth = 82.5f;
            bodyComponent.levelMaxHealth = 24.75f;
            bodyComponent.baseRegen = 1f;
            bodyComponent.levelRegen = 0.25f;
            bodyComponent.baseMaxShield = 0;
            bodyComponent.levelMaxShield = 0f;
            bodyComponent.baseMoveSpeed = 7;
            bodyComponent.levelMoveSpeed = 0;
            bodyComponent.baseAcceleration = 80;
            bodyComponent.baseJumpPower = 15;
            bodyComponent.levelJumpPower = 0;
            bodyComponent.baseDamage = 6;
            bodyComponent.levelDamage = 1.2f;
            bodyComponent.baseAttackSpeed = 1;
            bodyComponent.levelAttackSpeed = 0;
            bodyComponent.baseCrit = 1;
            bodyComponent.levelCrit = 0;
            bodyComponent.baseArmor = 0;
            bodyComponent.levelArmor = 0;
            bodyComponent.baseJumpCount = 1;
            bodyComponent.sprintingSpeedMultiplier = 1.45f;
            bodyComponent.wasLucky = false;
            bodyComponent.hideCrosshair = false;
            bodyComponent.aimOriginTransform = gameObject3.transform;
            bodyComponent.hullClassification = HullClassification.Human;
            bodyComponent.portraitIcon = Assets.charPortrait;
            bodyComponent.isChampion = false;
            bodyComponent.currentVehicle = null;
            bodyComponent.skinIndex = 0U;
            //Debug.Log("Created character body");
            #endregion

            #region spawning-and-states
            //Replace the existing spawn animation
            EntityStateMachine entityStateMachine = characterPrefab.GetComponent<EntityStateMachine>();
            entityStateMachine.initialStateType = new SerializableEntityStateType(typeof(EntityStates.WispSurvivorStates.Spawn));
            bodyComponent.currentVehicle = null;
            bodyComponent.preferredPodPrefab = null;
            bodyComponent.preferredInitialStateType = entityStateMachine.initialStateType;
            

            //Change our main state to account for the float functionality
            entityStateMachine.mainStateType = new SerializableEntityStateType(typeof(EntityStates.WispSurvivorStates.WispCharacterMain));

            characterPrefab.AddComponent<TetherHandler>();
            #endregion

            #region movement-camera
            // the charactermotor controls the survivor's movement and stuff
            CharacterMotor characterMotor = characterPrefab.GetComponent<CharacterMotor>();
            characterMotor.walkSpeedPenaltyCoefficient = 1f;
            characterMotor.characterDirection = characterDirection;
            characterMotor.muteWalkMotion = false;
            characterMotor.mass = 100f;
            //characterMotor.airControl = 0.75f;
            characterMotor.airControl = 5f;
            characterMotor.disableAirControlUntilCollision = false;
            characterMotor.generateParametersOnAwake = true;
            //characterMotor.useGravity = true;
            //characterMotor.isFlying = false;
            //Debug.Log("Created character motor");

            InputBankTest inputBankTest = characterPrefab.GetComponent<InputBankTest>();
            inputBankTest.moveVector = Vector3.zero;

            CameraTargetParams cameraTargetParams = characterPrefab.GetComponent<CameraTargetParams>();
            cameraTargetParams.cameraParams = Resources.Load<GameObject>("Prefabs/CharacterBodies/CommandoBody").GetComponent<CameraTargetParams>().cameraParams;
            cameraTargetParams.cameraPivotTransform = null;
            cameraTargetParams.aimMode = CameraTargetParams.AimType.Standard;
            cameraTargetParams.recoil = Vector2.zero;
            cameraTargetParams.idealLocalCameraPos = Vector3.zero;
            cameraTargetParams.dontRaycastToPivot = false;
            //Debug.Log("Created camera target parameters");

            // this component is used to locate the character model(duh), important to set this up here
            ModelLocator modelLocator = characterPrefab.GetComponent<ModelLocator>();
            modelLocator.modelTransform = transform;
            modelLocator.modelBaseTransform = gameObject.transform;
            modelLocator.dontReleaseModelOnDeath = false;
            modelLocator.autoUpdateModelTransform = true;
            modelLocator.dontDetatchFromParent = false;
            modelLocator.noCorpse = false;
            modelLocator.normalizeToFloor = false; // set true if you want your character to rotate on terrain like acrid does
            modelLocator.preserveModel = false;
            //Debug.Log("Created model locator");
            #endregion


            // childlocator is something that must be set up in the unity project, it's used to find any child objects for things like footsteps or muzzle flashes
            // also important to set up if you want quality
            ChildLocator childLocator = model.GetComponent<ChildLocator>();
            //Debug.Log("Established child locator reference");

            // this component is used to handle all overlays and whatever on your character, without setting this up you won't get any cool effects like burning or freeze on the character
            // it goes on the model object of course
            CharacterModel characterModel = model.AddComponent<CharacterModel>();
            characterModel.body = bodyComponent;
            characterModel.baseRendererInfos = new CharacterModel.RendererInfo[]
            {
                // set up multiple rendererinfos if needed, but for this example there's only the one
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = model.GetComponentInChildren<SkinnedMeshRenderer>().material,
                    renderer = model.GetComponentInChildren<SkinnedMeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = true   //Disable overlays for incorrectly formatted character
                }
            };

            characterModel.autoPopulateLightInfos = true;
            characterModel.invisibilityCount = 0;
            characterModel.temporaryOverlays = new List<TemporaryOverlay>();
            //Debug.Log("Created character model");

            #region skin
            SkinnedMeshRenderer mainRenderer = (characterModel.baseRendererInfos[0].renderer as SkinnedMeshRenderer);
            if (!mainRenderer) Debug.LogError("No main renderer found!");
            if (!mainRenderer.sharedMesh) Debug.LogError("No shared mesh found for main renderer!");
            ModelSkinController modelSkinController = model.AddComponent<ModelSkinController>();
            LanguageAPI.Add("WISP_DEFAULT_SKIN", "Default");

            LoadoutAPI.SkinDefInfo skinDefInfo = default(LoadoutAPI.SkinDefInfo);
            skinDefInfo.BaseSkins = Array.Empty<SkinDef>();
            skinDefInfo.MinionSkinReplacements = new SkinDef.MinionSkinReplacement[0];
            skinDefInfo.ProjectileGhostReplacements = new SkinDef.ProjectileGhostReplacement[0];

            GameObject[] allObjects = new GameObject[0];
            skinDefInfo.GameObjectActivations = getActivations(allObjects);

            skinDefInfo.Icon = Assets.skin;
            skinDefInfo.MeshReplacements = new SkinDef.MeshReplacement[]
            {
                new SkinDef.MeshReplacement
                {
                    renderer = mainRenderer,
                    mesh = mainRenderer.sharedMesh
                }
            };
            skinDefInfo.Name = "WISP_DEFAULT_SKIN";
            skinDefInfo.NameToken = "WISP_DEFAULT_SKIN";
            skinDefInfo.RendererInfos = characterModel.baseRendererInfos;
            skinDefInfo.RootObject = model;
            skinDefInfo.UnlockableName = "";
            SkinDef defaultSkin = LoadoutAPI.CreateNewSkinDef(skinDefInfo);

            var skinDefs = new List<SkinDef>() { defaultSkin };
            modelSkinController.skins = skinDefs.ToArray();
            //Debug.Log("Created skin");
            #endregion

            #region team-health
            TeamComponent teamComponent = null;
            if (characterPrefab.GetComponent<TeamComponent>() != null) teamComponent = characterPrefab.GetComponent<TeamComponent>();
            else teamComponent = characterPrefab.GetComponent<TeamComponent>();
            teamComponent.hideAllyCardDisplay = false;
            teamComponent.teamIndex = TeamIndex.None;

            HealthComponent healthComponent = characterPrefab.GetComponent<HealthComponent>();
            healthComponent.health = 82.5f;
            healthComponent.shield = 0f;
            healthComponent.barrier = 0f;
            healthComponent.magnetiCharge = 0f;
            healthComponent.body = null;
            healthComponent.dontShowHealthbar = false;
            healthComponent.globalDeathEventChanceCoefficient = 1f;
            //Debug.Log("Created components");
            #endregion

            characterPrefab.GetComponent<Interactor>().maxInteractionDistance = 3f;
            characterPrefab.GetComponent<InteractionDriver>().highlightInteractor = true;

            // this disables ragdoll since the character's not set up for it, and instead plays a death animation
            CharacterDeathBehavior characterDeathBehavior = characterPrefab.GetComponent<CharacterDeathBehavior>();
            characterDeathBehavior.deathStateMachine = characterPrefab.GetComponent<EntityStateMachine>();
            characterDeathBehavior.deathState = new SerializableEntityStateType(typeof(GenericCharacterDeath));

            // edit the sfxlocator if you want different sounds
            SfxLocator sfxLocator = characterPrefab.GetComponent<SfxLocator>();
            sfxLocator.deathSound = "Play_ui_player_death";
            sfxLocator.barkSound = "";
            sfxLocator.openSound = "";
            sfxLocator.landingSound = "Play_char_land";
            sfxLocator.fallDamageSound = "Play_char_land_fall_damage";
            sfxLocator.aliveLoopStart = "";
            sfxLocator.aliveLoopStop = "";
            //Debug.Log("Created sfx");

            Rigidbody rigidbody = characterPrefab.GetComponent<Rigidbody>();
            rigidbody.mass = 50f;
            rigidbody.drag = 0f;
            rigidbody.angularDrag = 0f;
            rigidbody.useGravity = false;
            rigidbody.isKinematic = true;
            rigidbody.interpolation = RigidbodyInterpolation.None;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rigidbody.constraints = RigidbodyConstraints.None;
            //Debug.Log("Created rigidbody");

            CapsuleCollider capsuleCollider = characterPrefab.GetComponent<CapsuleCollider>();
            capsuleCollider.isTrigger = false;
            capsuleCollider.material = null;
            capsuleCollider.center = new Vector3(0f, 0f, 0f);
            capsuleCollider.radius = 0.5f;
            capsuleCollider.height = 1.82f;
            capsuleCollider.direction = 1;
            //Debug.Log("Created capsule collider");

            KinematicCharacterMotor kinematicCharacterMotor = characterPrefab.GetComponent<KinematicCharacterMotor>();
            kinematicCharacterMotor.CharacterController = characterMotor;
            kinematicCharacterMotor.Capsule = capsuleCollider;
            kinematicCharacterMotor.Rigidbody = rigidbody;

            capsuleCollider.radius = 0.5f;
            capsuleCollider.height = 1.82f;
            capsuleCollider.center = new Vector3(0, 0, 0);
            capsuleCollider.material = null;

            kinematicCharacterMotor.DetectDiscreteCollisions = false;
            kinematicCharacterMotor.GroundDetectionExtraDistance = 0f;
            kinematicCharacterMotor.MaxStepHeight = 0.2f;
            kinematicCharacterMotor.MinRequiredStepDepth = 0.1f;
            kinematicCharacterMotor.MaxStableSlopeAngle = 55f;
            kinematicCharacterMotor.MaxStableDistanceFromLedge = 0.5f;
            kinematicCharacterMotor.PreventSnappingOnLedges = false;
            kinematicCharacterMotor.MaxStableDenivelationAngle = 55f;
            kinematicCharacterMotor.RigidbodyInteractionType = RigidbodyInteractionType.None;
            kinematicCharacterMotor.PreserveAttachedRigidbodyMomentum = true;
            kinematicCharacterMotor.HasPlanarConstraint = false;
            kinematicCharacterMotor.PlanarConstraintAxis = Vector3.up;
            kinematicCharacterMotor.StepHandling = StepHandlingMethod.None;
            kinematicCharacterMotor.LedgeHandling = true;
            kinematicCharacterMotor.InteractiveRigidbodyHandling = true;
            kinematicCharacterMotor.SafeMovement = false;
            //Debug.Log("Set physics");

            // this sets up the character's hurtbox, kinda confusing, but should be fine as long as it's set up in unity right
            HurtBoxGroup hurtBoxGroup = model.AddComponent<HurtBoxGroup>();
            //Debug.Log("Set reference to hurtBoxGroup");
            if (model.GetComponentInChildren<CapsuleCollider>() == null) Debug.LogError("Could not find capsule collider!");
            HurtBox componentInChildren = model.GetComponentInChildren<CapsuleCollider>().gameObject.AddComponent<HurtBox>();
            //Debug.Log("Added hurtbox component to capsule collider");
            componentInChildren.gameObject.layer = LayerIndex.entityPrecise.intVal;
            componentInChildren.healthComponent = healthComponent;
            componentInChildren.isBullseye = true;
            componentInChildren.damageModifier = HurtBox.DamageModifier.Normal;
            componentInChildren.hurtBoxGroup = hurtBoxGroup;
            componentInChildren.indexInGroup = 0;

            hurtBoxGroup.hurtBoxes = new HurtBox[]
            {
                componentInChildren
            };

            hurtBoxGroup.mainHurtBox = componentInChildren;
            hurtBoxGroup.bullseyeCount = 1;

            //Debug.Log("Set components");

            // this is for handling footsteps, not needed but polish is always good
            FootstepHandler footstepHandler = model.AddComponent<FootstepHandler>();
            footstepHandler.baseFootstepString = "Play_player_footstep";
            footstepHandler.sprintFootstepOverrideString = "";
            footstepHandler.enableFootstepDust = true;
            footstepHandler.footstepDustPrefab = Assets.footstepPrefab;

            // ragdoll controller is a pain to set up so we won't be doing that here..
            RagdollController ragdollController = model.AddComponent<RagdollController>();
            ragdollController.bones = null;
            ragdollController.componentsToDisableOnRagdoll = null;

            // this handles the pitch and yaw animations, but honestly they are nasty and a huge pain to set up so i didn't bother
            AimAnimator aimAnimator = model.AddComponent<AimAnimator>();
            aimAnimator.inputBank = inputBankTest;
            aimAnimator.directionComponent = characterDirection;
            aimAnimator.pitchRangeMax = 55f;
            aimAnimator.pitchRangeMin = -50f;
            aimAnimator.yawRangeMin = -44f;
            aimAnimator.yawRangeMax = 44f;
            aimAnimator.pitchGiveupRange = 30f;
            aimAnimator.yawGiveupRange = 10f;
            aimAnimator.giveupDuration = 8f;
            //Debug.Log("Finished setup");
        }

        private void RegisterCharacter()
        {
            // now that the body prefab's set up, clone it here to make the display prefab
            characterDisplay = PrefabAPI.InstantiateClone(characterPrefab.GetComponent<ModelLocator>().modelBaseTransform.gameObject, "ExampleSurvivorDisplay", true, "C:\\Users\\test\\Documents\\ror2mods\\ExampleSurvivor\\ExampleSurvivor\\ExampleSurvivor\\ExampleSurvivor.cs", "RegisterCharacter", 153);
            characterDisplay.AddComponent<NetworkIdentity>();

            // write a clean survivor description here!
            string desc = "Wisp is a supportive survivor who can rejuvenate their allies, or sap the strength of their enemies <color=#CCD3E0>" + Environment.NewLine + Environment.NewLine;
            desc = desc + "< ! > Your tether will pull you to your target when cast - use it to escape from danger" + Environment.NewLine + Environment.NewLine;
            desc = desc + "< ! > Burst deals damage around both you and your partner. Enemies close enough to be affected by both will take double damage" + Environment.NewLine + Environment.NewLine;
            desc = desc + "< ! > Siphon deals non-lethal damage - you'll need to use other skills or items to finish enemies off" + Environment.NewLine + Environment.NewLine;
            desc = desc + "< ! > Tethering to an ally will increase their regen rate based on your own regen rate at the time of the tether.</color>" + Environment.NewLine + Environment.NewLine;

            // add the language tokens
            LanguageAPI.Add("WISP_NAME", "Wisp");
            LanguageAPI.Add("WISP_DESCRIPTION", desc);
            LanguageAPI.Add("WISP_SUBTITLE", "The Phantasm");

            // add our new survivor to the game~
            SurvivorDef survivorDef = new SurvivorDef
            {
                name = "WISP_NAME",
                unlockableName = "",
                descriptionToken = "WISP_DESCRIPTION",
                primaryColor = characterColor,
                bodyPrefab = characterPrefab,
                displayPrefab = characterDisplay
            };

            // set up the survivor's skills here
            SkillSetup();

            SurvivorAPI.AddSurvivor(survivorDef);

            // gotta add it to the body catalog too
            BodyCatalog.getAdditionalEntries += delegate (List<GameObject> list)
            {
                list.Add(characterPrefab);
            };
        }

        void SkillSetup()
        {
            // get rid of the original skills first, otherwise we'll have commando's loadout and we don't want that
            foreach (GenericSkill obj in characterPrefab.GetComponentsInChildren<GenericSkill>())
            {
                BaseUnityPlugin.DestroyImmediate(obj);
            }

            Modules.Buffs.RegisterBuffs();
            //PassiveSetup();
            Modules.Survivors.Wisp.CreateSkills();

            Hook();
        }

        private void RegisterNetworkedEffects()
        {
            //TOOD: Replace with custom effect
            spawnEffect = Resources.Load<GameObject>("prefabs/effects/CrocoSpawnEffect").InstantiateClone("WispSpawn", true);
            spawnEffect.AddComponent<NetworkIdentity>();

            //fireballProjectile = Assets.MainAssetBundle.LoadAsset<GameObject>("WispFire");
            fireballProjectile = Resources.Load<GameObject>("Prefabs/Projectiles/WispCannon").InstantiateClone("WispFireball", true);
            fireballProjectile.GetComponent<ProjectileController>().ghostPrefab.transform.Find("Particles").Find("FireSphere").transform.localScale = new Vector3(.4f, .4f, .4f);
            fireballProjectile.GetComponent<ProjectileDamage>().force = 0f;
            // just setting the numbers to 1 as the entitystate will take care of those
            fireballProjectile.GetComponent<ProjectileController>().procCoefficient = 1f;
            fireballProjectile.GetComponent<ProjectileDamage>().damage = 1f;
            fireballProjectile.GetComponent<ProjectileDamage>().damageType = DamageType.Generic;

            tetherPrefab = Assets.MainAssetBundle.LoadAsset<GameObject>("Tether");

            //burstPrefab = Assets.MainAssetBundle.LoadAsset<GameObject>("SmallExplosionEffect");
            burstPrefab = PrefabAPI.InstantiateClone(Resources.Load<GameObject>("prefabs/effects/WilloWispExplosion"), "Prefabs/Projectiles/burstPrefab", true, "C:\\Users\\test\\Documents\\ror2mods\\ExampleSurvivor\\ExampleSurvivor\\ExampleSurvivor\\ExampleSurvivor.cs", "RegisterCharacter", 155);
            burstSecondary = PrefabAPI.InstantiateClone(Resources.Load<GameObject>("prefabs/effects/ShieldBreakEffect"), "Prefabs/Projectiles/burstPrefab", true, "C:\\Users\\test\\Documents\\ror2mods\\ExampleSurvivor\\ExampleSurvivor\\ExampleSurvivor\\ExampleSurvivor.cs", "RegisterCharacter", 155);
            
            //Scale the explosion sizes
            Vector3 burstPrimaryScale = new Vector3(.75f, .75f, .75f);
            burstPrefab.transform.Find("Flash").localScale = burstPrimaryScale;
            burstPrefab.transform.Find("Flames, Tube").localScale = burstPrimaryScale;
            burstPrefab.transform.Find("Flames, Radial").localScale = burstPrimaryScale;

            Vector3 burstSecondaryScale = new Vector3(2f, 2f, 2f);
            burstSecondary.transform.Find("Lightning").localScale = burstSecondaryScale;
            burstSecondary.transform.Find("Shards").localScale = burstSecondaryScale;
            burstSecondary.transform.Find("Flash").localScale = burstSecondaryScale;


            // register it for networking
            //fireballProjectile.AddComponent<NetworkIdentity>();
            if (fireballProjectile) PrefabAPI.RegisterNetworkPrefab(fireballProjectile);
            tetherPrefab.AddComponent<NetworkIdentity>();
            if (tetherPrefab) PrefabAPI.RegisterNetworkPrefab(tetherPrefab);
            burstPrefab.AddComponent<NetworkIdentity>();
            if (burstPrefab) PrefabAPI.RegisterNetworkPrefab(burstPrefab);
            burstSecondary.AddComponent<NetworkIdentity>();
            if (burstSecondary) PrefabAPI.RegisterNetworkPrefab(burstSecondary);

            // add it to the projectile catalog or it won't work in multiplayer
            ProjectileCatalog.getAdditionalEntries += list =>
            {
                list.Add(fireballProjectile);
            };

            //Do the same for the effects
            EffectAPI.AddEffect(spawnEffect);
            EffectAPI.AddEffect(burstPrefab);
            EffectAPI.AddEffect(burstSecondary);
        }

        void RegisterStates()
        {
            // register the entitystates for networking reasons
            LoadoutAPI.AddSkill(typeof(WispFireball));
            LoadoutAPI.AddSkill(typeof(WispHasteSkillState));
            LoadoutAPI.AddSkill(typeof(WispInvigorateSkillState));
            LoadoutAPI.AddSkill(typeof(WispSiphon));
            LoadoutAPI.AddSkill(typeof(WispBurst));
            LoadoutAPI.AddSkill(typeof(Spawn));
        }

        private void Hook()
        {
            On.RoR2.CharacterBody.RecalculateStats += CharacterBody_RecalculateStats;
            On.RoR2.HealthComponent.TakeDamage += EntityStates.WispSurvivorStates.TetherHandler.Tether;
        }

        private string timestamp()
        {
            string returnString = "[" + System.DateTime.Now.Hour + ":" + System.DateTime.Now.Minute + ":" + System.DateTime.Now.Millisecond + "]";
            return returnString;
        }

        private void CharacterBody_RecalculateStats(On.RoR2.CharacterBody.orig_RecalculateStats orig, CharacterBody self)
        {
            orig(self);

            if(self)
            {
                if(self.HasBuff(Modules.Buffs.siphonSelf))
                {

                }
                if(self.HasBuff(Modules.Buffs.siphonTarget))
                {

                }

                if(self.HasBuff(Modules.Buffs.sustainSelf))
                {

                }
                if(self.HasBuff(Modules.Buffs.sustainTarget))
                {
                    float bonusRegen = TetherHandler.instance.myBody.regen;
                    bonusRegen = bonusRegen * .5f;
                    Reflection.SetPropertyValue<float>(self, "regen", self.regen + bonusRegen);
                }

                if(self.HasBuff(Modules.Buffs.haste))
                {
                    int hasteStacks = self.GetBuffCount(Modules.Buffs.haste);
                    //Add 50% of base movespeed per stack
                    Reflection.SetPropertyValue<float>(self, "moveSpeed", self.moveSpeed + (self.baseMoveSpeed * .5f * hasteStacks));
                    //Add 75% of base attack speed per stack
                    Reflection.SetPropertyValue<float>(self, "attackSpeed", self.attackSpeed + (self.baseAttackSpeed * .75f * hasteStacks));

                    SkillLocator skillLocator = self.skillLocator;
                    Reflection.SetPropertyValue<float>(skillLocator.primary, "flatCooldownReduction", skillLocator.primary.flatCooldownReduction + 1f);
                    Reflection.SetPropertyValue<float>(skillLocator.secondary, "flatCooldownReduction", skillLocator.secondary.flatCooldownReduction + 1f);
                    Reflection.SetPropertyValue<float>(skillLocator.utility, "flatCooldownReduction", skillLocator.utility.flatCooldownReduction + 1f);
                    Reflection.SetPropertyValue<float>(skillLocator.special, "flatCooldownReduction", skillLocator.special.flatCooldownReduction + 1f);
                }

                if(self.HasBuff(Modules.Buffs.slow))
                {
                    int slowStacks = self.GetBuffCount(Modules.Buffs.slow);
                    Reflection.SetPropertyValue<float>(self, "moveSpeed", self.moveSpeed * .5f * slowStacks);
                    Reflection.SetPropertyValue<float>(self, "attackSpeed", self.attackSpeed * .5f * slowStacks);

                    SkillLocator skillLocator = self.skillLocator;
                    if(skillLocator.primary) Reflection.SetPropertyValue<float>(
                        skillLocator.primary, "flatCooldownReduction", skillLocator.primary.flatCooldownReduction - 1f);
                    if (skillLocator.secondary) Reflection.SetPropertyValue<float>(
                        skillLocator.secondary, "flatCooldownReduction", skillLocator.secondary.flatCooldownReduction - 1f);
                    if (skillLocator.utility) Reflection.SetPropertyValue<float>(
                        skillLocator.utility, "flatCooldownReduction", skillLocator.utility.flatCooldownReduction - 1f);
                    if (skillLocator.special) Reflection.SetPropertyValue<float>(
                        skillLocator.special, "flatCooldownReduction", skillLocator.special.flatCooldownReduction - 1f);
                }
                if(self.HasBuff(Modules.Buffs.invigorate))
                {
                    Reflection.SetPropertyValue<float>(self, "regen", self.regen + (self.levelRegen * 8f));
                    Reflection.SetPropertyValue<float>(self, "damage", self.damage + (self.levelDamage * 2f));
                }
                if(self.HasBuff(Modules.Buffs.regenMinus))
                {
                    Reflection.SetPropertyValue<float>(self, "regen", 0f);
                    
                }
            }
        }

        private void CreateDoppelganger()
        {
            // set up the doppelganger for artifact of vengeance here
            // quite simple, gets a bit more complex if you're adding your own ai, but commando ai will do

            doppelganger = PrefabAPI.InstantiateClone(Resources.Load<GameObject>("Prefabs/CharacterMasters/CommandoMonsterMaster"), "ExampleSurvivorMonsterMaster", true, "C:\\Users\\test\\Documents\\ror2mods\\ExampleSurvivor\\ExampleSurvivor\\ExampleSurvivor\\ExampleSurvivor.cs", "CreateDoppelganger", 159);

            MasterCatalog.getAdditionalEntries += delegate (List<GameObject> list)
            {
                list.Add(doppelganger);
            };

            CharacterMaster component = doppelganger.GetComponent<CharacterMaster>();
            component.bodyPrefab = characterPrefab;
        }

        private static SkinDef.GameObjectActivation[] getActivations(GameObject[] allObjects, params GameObject[] activatedObjects)
        {
            List<SkinDef.GameObjectActivation> GameObjectActivations = new List<SkinDef.GameObjectActivation>();

            for (int i = 0; i < allObjects.Length; i++)
            {

                bool activate = activatedObjects.Contains(allObjects[i]);

                GameObjectActivations.Add(new SkinDef.GameObjectActivation
                {
                    gameObject = allObjects[i],
                    shouldActivate = activate
                });
            }

            return GameObjectActivations.ToArray();
        }
    }

    // get the assets from your assetbundle here
    // if it's returning null, check and make sure you have the build action set to "Embedded Resource" and the file names are right because it's not gonna work otherwise
    public static class Assets
    {
        public static AssetBundle MainAssetBundle = null;
        public static AssetBundleResourcesProvider Provider;

        public static Texture charPortrait;

        public static Sprite skin;

        public static Sprite iconP;
        public static Sprite icon1;
        public static Sprite icon2;
        public static Sprite icon2_invigorate;
        public static Sprite icon3_siphon;
        public static Sprite icon3_tether;
        public static Sprite icon4;

        public static GameObject footstepPrefab;

        public static void PopulateAssets()
        {
            if (MainAssetBundle == null)
            {
                //Should probably change this away from ExampleSurvivor but that sounds like it might break things
                using (var assetStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ExampleSurvivor.examplesurvivorbundle"))
                {
                    MainAssetBundle = AssetBundle.LoadFromStream(assetStream);
                    Provider = new AssetBundleResourcesProvider("@ExampleSurvivor", MainAssetBundle);
                }
            }

            // include this if you're using a custom soundbank
            /*using (Stream manifestResourceStream2 = Assembly.GetExecutingAssembly().GetManifestResourceStream("ExampleSurvivor.ExampleSurvivor.bnk"))
            {
                byte[] array = new byte[manifestResourceStream2.Length];
                manifestResourceStream2.Read(array, 0, array.Length);
                SoundAPI.SoundBanks.Add(array);
            }*/

            // and now we gather the assets

            Texture2D skinText = MainAssetBundle.LoadAsset<Texture2D>("Skin");
            skin = TextToSprite(skinText);

            Texture2D iconPText = MainAssetBundle.LoadAsset<Texture2D>("Passive");
            iconP = TextToSprite(iconPText);

            Texture2D icon1Text = MainAssetBundle.LoadAsset<Texture2D>("Skill1");
            icon1 = TextToSprite(icon1Text);

            Texture2D icon2Text = MainAssetBundle.LoadAsset<Texture2D>("Skill2");
            icon2 = TextToSprite(icon2Text);

            Texture2D icon2RegenText = MainAssetBundle.LoadAsset<Texture2D>("Skill2_Heal");
            icon2_invigorate = TextToSprite(icon2RegenText);

            Texture2D icon3SiphonText = MainAssetBundle.LoadAsset<Texture2D>("Skill3_Siphon");
            icon3_siphon = TextToSprite(icon3SiphonText);

            Texture2D icon3TetherText = MainAssetBundle.LoadAsset<Texture2D>("Skill3_Tether");
            icon3_tether = TextToSprite(icon3TetherText);

            Texture2D icon4Text = MainAssetBundle.LoadAsset<Texture2D>("Skill4");
            icon4 = TextToSprite(icon4Text);

            charPortrait = MainAssetBundle.LoadAsset<Texture2D>("WispPreview");

            footstepPrefab = MainAssetBundle.LoadAsset<GameObject>("OrbFootstep");
        }


        private static Sprite TextToSprite(Texture2D input)
        {
            return Sprite.Create(input, new Rect(0, 0, input.width, input.height), new Vector2(0, 0));
        }
    }

}