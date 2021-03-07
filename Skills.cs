using RoR2;
using RoR2.Projectile;
using RoR2.Stats;
using R2API.Utils;
using R2API;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using static On.RoR2.DotController;
using System.Collections.Generic;

// the entitystates namespace is used to make the skills, i'm not gonna go into detail here but it's easy to learn through trial and error
namespace EntityStates.WispSurvivorStates
{
    public enum TETHER_TYPE
    {
        SIPHON,
        TETHER,
        ORB_REFERENCE,
        CLEAR
    }

    public class Spawn : BaseState
    {
        public static float duration = 2.5f;

        public override void OnEnter()
        {
            base.OnEnter();

            base.modelLocator.normalizeToFloor = true;

            EffectManager.SpawnEffect(WispSurvivor.WispSurvivor.spawnEffect, new EffectData
            {
                origin = base.characterBody.footPosition
            }, false);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if(base.fixedAge >= Spawn.duration && base.isAuthority)
            {
                this.outer.SetNextStateToMain();
            }
        }
    }

    //This is just to add the custom floating function. Code basically ripped from artificer
    //but it works
    public class WispCharacterMain : GenericCharacterMain
    {
        public static float hoverVelocity = 100f;
        public static float hoverAcceleration = 40f;
        private bool resetPosition;

        public override void OnEnter()
        {
            base.OnEnter();
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            //Scoot the position of the particle effects over juuuuust a tiny bit
            if(!resetPosition)
            {
                resetPosition = true;
                Transform fireBase = base.GetModelTransform().Find("Fire");
                if(fireBase)
                {
                    fireBase.localPosition = new Vector3(0f, 1.25f, 0f);
                }
            }
        }

        public override void ProcessJump()
        {
            base.ProcessJump();
  
            if(this.hasCharacterMotor && this.hasInputBank && base.isAuthority)
            {
                bool is_jumping = base.inputBank.jump.down;
                bool is_falling = base.characterMotor.velocity.y < 0f && !base.characterMotor.isGrounded;

                if(is_jumping && is_falling)
                {
                    float curVelocity = base.characterMotor.velocity.y;
                    float newVelocity = Mathf.MoveTowards(curVelocity, hoverVelocity, hoverAcceleration * Time.fixedDeltaTime);
                    base.characterMotor.velocity = new Vector3(base.characterMotor.velocity.x, newVelocity, base.characterMotor.velocity.z);
                }
            }
        }

        public override void OnExit()
        {
            base.OnExit();
        }
    }

    //Save grapple target data as a struct so we can keep multiple of them
    public struct GrappleTarget
    {
        public GameObject tether;
        public HurtBox grappleTarget;
        public HealthComponent grappleTargetHealth;
        public CharacterBody grappleTargetBody;
    }

    //Handle everything around being tethered.
    public class TetherHandler : NetworkBehaviour
    {
        //Core stuff
        public static TetherHandler instance;
        public TETHER_TYPE TETHER_TYPE;
        private List<GrappleTarget> GrappleTargets;
        public CharacterBody myBody;
        private bool hasTarget;
        
        //Movement stuff
        private bool flyToTarget;
        private Vector3 movementVector;
        private float moveSpeed = 10f;
        private float rotateRadius = 12f;   //How close we should be to the target before stopping
        private float distanceDampenFactor = 10f; //How much we should reduce our speed when moving
        private CharacterMotor motor;
        private CharacterDirection direction;
        
        //Siphon DoT effect
        private HealthComponent healthComponent;
        private float tickRate = .33f;
        private float tickTime;

        void Start()
        {
            Debug.LogWarning("Tether handler start function run on " + gameObject);
            instance = this;
            motor = transform.root.GetComponentInChildren<CharacterMotor>();
            direction = transform.root.GetComponentInChildren<CharacterDirection>();
            healthComponent = transform.root.GetComponentInChildren<HealthComponent>();

            GrappleTargets = new List<GrappleTarget>();
        }

        void FixedUpdate()
        {
            GrappleTarget[] grappleTargets = GrappleTargets.ToArray();
            foreach (GrappleTarget target in grappleTargets)
            {
                if (target.grappleTargetHealth != null && !target.grappleTargetHealth.alive)
                {
                    ClearGrappleTarget(target);
                }
            }
            if (hasTarget)
            {
                tickTime += Time.fixedDeltaTime;
                if(tickTime >= tickRate)
                {
                    tickTime = 0f;
                    DoTick();
                }
                if (flyToTarget && motor && direction)
                {
                    //Move towards our grapple target
                    this.movementVector = (GrappleTargets[0].grappleTarget.transform.position - transform.position).normalized;
                    float distance = Vector3.Distance(GrappleTargets[0].grappleTarget.transform.position, transform.position);

                    //Dampen our speed a bit so we don't rocket over
                    float moveSpeedAdjustment = distance / distanceDampenFactor;
                    //Then add the speed to our root motion for a little bit of control on the way
                    motor.rootMotion += this.movementVector * ((moveSpeed * moveSpeedAdjustment) * Time.fixedDeltaTime);

                    if (distance <= rotateRadius) flyToTarget = false;
                }
                UpdateTether();
            }
        }

        private void DoTick()
        {
            if (TETHER_TYPE == TETHER_TYPE.SIPHON)
            {
                foreach(GrappleTarget target in GrappleTargets) Siphon(target);
            }
        }

        private void Siphon(GrappleTarget target)
        {
            float fullHealth = target.grappleTargetHealth.fullCombinedHealth;
            float damageAmount = fullHealth * .0033f;   //.33% of max health per .33 seconds - 1% of max health / second

            DamageInfo damageInfo = new DamageInfo();
            damageInfo.position = target.grappleTarget.transform.position;
            damageInfo.attacker = this.gameObject;
            damageInfo.inflictor = this.gameObject;
            damageInfo.crit = false;
            //damageInfo.damage = base.damageStat * this.damageCoefficient;
            damageInfo.damage = damageAmount;
            damageInfo.damageColorIndex = DamageColorIndex.Default;
            damageInfo.damageType = DamageType.NonLethal;
            damageInfo.procCoefficient = 0f;

            target.grappleTargetHealth.TakeDamage(damageInfo);

            float barrierAmount = healthComponent.fullBarrier * .033f;
            healthComponent.AddBarrier(barrierAmount);
        }

        public static void Tether(On.RoR2.HealthComponent.orig_TakeDamage orig, HealthComponent self, DamageInfo damageInfo)
        {
            orig(self, damageInfo);

            //If there's no valid health component, or no valid attacker (eg environmental damage), bounce out of here
            if (!self) return;
            if (!damageInfo.attacker) return;

            //First check if the attacker is a player; monsters can't be tethered
            if (damageInfo.attacker.GetComponent<CharacterBody>().teamComponent.teamIndex == TeamIndex.Player)
            {
                //If the attacker is the same as the existing grapple target
                if (TetherHandler.instance.containsBody(damageInfo.attacker.GetComponent<CharacterBody>()))
                {
                    //If we are, in fact, tethered
                    if (TetherHandler.instance.TETHER_TYPE == TETHER_TYPE.TETHER)
                    {
                        //Add barrier based on the damage dealt
                        float barrierAmount = damageInfo.damage;
                        barrierAmount = barrierAmount * .5f;
                        TetherHandler.instance.myBody.healthComponent.AddBarrier(barrierAmount);
                    }
                }
            }
        }

        #region getters-setters
        public void SetGrappleTarget(HurtBox target)
        {
            Debug.Log("Attempting to set grapple target to " + target.name);
            myBody = GetComponent<CharacterBody>(); //This should probably go somewhere else
            GrappleTarget newTarget = new GrappleTarget();
            
            newTarget.grappleTarget = target;
            newTarget.grappleTargetHealth = newTarget.grappleTarget.healthComponent;
            newTarget.grappleTargetBody = newTarget.grappleTarget.healthComponent.body;
            newTarget.tether = GameObject.Instantiate(WispSurvivor.WispSurvivor.tetherPrefab);
            newTarget.tether.AddComponent<AnimateTether>();
            Debug.Log("Set grapple target to " + newTarget.grappleTargetBody);

            GrappleTargets.Add(newTarget);
            hasTarget = true;
            if (Vector3.Distance(base.transform.position, newTarget.grappleTarget.transform.position) > 20f) flyToTarget = true;
            if (newTarget.grappleTargetHealth == null) Debug.LogError("Could not add grapple target health!");
            UpdateTether();
        }

        private void UpdateTether()
        {
            foreach (GrappleTarget target in GrappleTargets)
            {
                if (!target.tether) return;
                Vector3[] positions = new Vector3[2];
                positions[0] = transform.position;
                positions[1] = target.grappleTarget.transform.position;
                //TODO: Change this so it's not GetComponent
                target.tether.GetComponent<LineRenderer>().SetPositions(positions);
            }
        }

        public void ClearGrappleTarget(GrappleTarget target)
        {
            Destroy(target.tether);
            if (TETHER_TYPE == TETHER_TYPE.TETHER)
            {
                myBody.RemoveBuff(WispSurvivor.Modules.Buffs.sustainSelf);
                target.grappleTargetBody.RemoveBuff(WispSurvivor.Modules.Buffs.sustainTarget);
            }
            else if(TETHER_TYPE == TETHER_TYPE.SIPHON)
            {
                GetComponent<CharacterBody>().RemoveBuff(WispSurvivor.Modules.Buffs.siphonSelf);
                target.grappleTargetBody.RemoveBuff(WispSurvivor.Modules.Buffs.siphonTarget);
            }
            GrappleTargets.Remove(target);
            if(GrappleTargets.Count <= 0) hasTarget = false;
        }

        public List<GrappleTarget> GetGrappleTargets()
        {
            return GrappleTargets;
        }

        public bool containsBody(CharacterBody characterBody)
        {
            for(int i = 0; i < GrappleTargets.Count; i++)
            {
                if (GrappleTargets[i].grappleTargetBody == characterBody) return true;
            }
            return false;
        }

        public bool hasGrappleTarget()
        {
            return hasTarget;
        }
        #endregion
    }

    public class WispFireball : BaseSkillState
    {
        public float damageCoefficient = 2f;
        public float baseDuration = .25f;
        public float recoil = 1f;
        public static GameObject tracerEffectPrefab = Resources.Load<GameObject>("Prefabs/Effects/Tracers/TracerToolbotRebar");

        private float duration;
        private float fireDuration;
        private bool hasFired;
        private Animator animator;
        private string muzzleString;
        private TetherHandler grappleHandler;

        public override void OnEnter()
        {
            base.OnEnter();

            if (grappleHandler == null)
            {
                grappleHandler = base.GetComponent<TetherHandler>();
                if (grappleHandler == null)
                {
                    grappleHandler = base.gameObject.AddComponent<TetherHandler>();
                    Debug.LogWarning("Added grapple handler via fireball function");
                }
            }

            this.duration = this.baseDuration / this.attackSpeedStat;
            if (this.grappleHandler && this.grappleHandler.hasGrappleTarget())
            {
                //this.damageStat = this.grappleHandler.GetGrappleTarget().healthComponent.body.damage;
                //this.duration = this.baseDuration / this.grappleHandler.GetGrappleTarget().healthComponent.body.attackSpeed;
            }
            
            this.fireDuration = 0.25f * this.duration;
            base.characterBody.SetAimTimer(2f);
            this.animator = base.GetModelAnimator();
            this.muzzleString = "Muzzle";


            base.PlayAnimation("Gesture, Override", "FireArrow", "FireArrow.playbackRate", this.duration);
        }

        public override void OnExit()
        {
            base.OnExit();
        }

        private void FireShot()
        {
            if (!this.hasFired)
            {
                this.hasFired = true;

                base.characterBody.AddSpreadBloom(0.75f);
                Ray aimRay = base.GetAimRay();
                EffectManager.SimpleMuzzleFlash(Commando.CommandoWeapon.FirePistol.effectPrefab, base.gameObject, this.muzzleString, false);

                if (base.isAuthority)
                {
                    ProjectileManager.instance.FireProjectile(WispSurvivor.WispSurvivor.fireballProjectile, aimRay.origin, Util.QuaternionSafeLookRotation(aimRay.direction), base.gameObject, this.damageCoefficient * this.damageStat, 0f, Util.CheckRoll(this.critStat, base.characterBody.master), DamageColorIndex.Default, null, -1f);
                }
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (base.fixedAge >= this.fireDuration)
            {
                FireShot();
            }

            if (base.fixedAge >= this.duration && base.isAuthority)
            {
                this.outer.SetNextStateToMain();
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Skill;
        }
    }

    public class WispHasteSkillState : BaseSkillState
    {
        public float damageCoefficient = 0f;
        public float baseDuration = .5f;
        public float recoil = 1f;
        public static GameObject tracerEffectPrefab = Resources.Load<GameObject>("Prefabs/Effects/Tracers/TracerToolbotRebar");

        private float duration;
        private float fireDuration;
        private bool hasFired;
        private Animator animator;
        private string muzzleString;
        private TetherHandler grappleHandler;

        public override void OnEnter()
        {
            base.OnEnter();
            this.duration = this.baseDuration / this.attackSpeedStat;
            this.fireDuration = 0f;
            base.characterBody.SetAimTimer(2f);
            this.animator = base.GetModelAnimator();
            this.muzzleString = "Muzzle";

            if (grappleHandler == null)
            {
                grappleHandler = base.GetComponent<TetherHandler>();
                if (grappleHandler == null)
                {
                    grappleHandler = base.gameObject.AddComponent<TetherHandler>();
                    Debug.LogWarning("Added grapple handler via haste skill");
                }
            }

            base.PlayAnimation("Gesture, Override", "FireGrenade", "FireGrenade.playbackRate", this.duration);
        }

        public override void OnExit()
        {
            base.OnExit();
        }

        private void DoBuff()
        {
            if (!this.hasFired)
            {
                this.hasFired = true;

                base.characterBody.AddSpreadBloom(0.75f);
                Ray aimRay = base.GetAimRay();
                EffectManager.SimpleMuzzleFlash(Commando.CommandoWeapon.FirePistol.effectPrefab, base.gameObject, this.muzzleString, false);

                if(NetworkServer.active) base.characterBody.AddTimedBuff(WispSurvivor.Modules.Buffs.haste, 5f);
                if (grappleHandler.hasGrappleTarget())
                {
                    List<GrappleTarget> targets = grappleHandler.GetGrappleTargets();
                    foreach (GrappleTarget target in targets)
                    {
                        if (target.grappleTarget.teamIndex == TeamIndex.Player)
                        {
                            if (NetworkServer.active) target.grappleTargetBody.AddTimedBuff(WispSurvivor.Modules.Buffs.haste, 5f);
                        }
                        else if (target.grappleTarget.teamIndex == TeamIndex.Monster)
                        {
                            if (NetworkServer.active) target.grappleTargetBody.AddTimedBuff(WispSurvivor.Modules.Buffs.slow, 5f);
                        }
                    }

                }
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (base.fixedAge >= this.fireDuration)
            {
                DoBuff();
            }

            if (base.fixedAge >= this.duration && base.isAuthority)
            {
                this.outer.SetNextStateToMain();
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Skill;
        }
    }

    public class WispInvigorateSkillState : BaseSkillState
    {
        public float damageCoefficient = 0f;
        public float baseDuration = .5f;
        public float recoil = 1f;
        public static GameObject tracerEffectPrefab = Resources.Load<GameObject>("Prefabs/Effects/Tracers/TracerToolbotRebar");

        private float duration;
        private float fireDuration;
        private bool hasFired;
        private Animator animator;
        private string muzzleString;
        private TetherHandler grappleHandler;

        public override void OnEnter()
        {
            base.OnEnter();
            this.duration = this.baseDuration / this.attackSpeedStat;
            this.fireDuration = 0f;
            base.characterBody.SetAimTimer(2f);
            this.animator = base.GetModelAnimator();
            this.muzzleString = "Muzzle";

            if (grappleHandler == null)
            {
                grappleHandler = base.GetComponent<TetherHandler>();
                if (grappleHandler == null)
                {
                    grappleHandler = base.gameObject.AddComponent<TetherHandler>();
                    Debug.LogWarning("Added grapple handler via invigorate skill");
                }
            }

            base.PlayAnimation("Gesture, Override", "FireGrenade", "FireGrenade.playbackRate", this.duration);
        }

        public override void OnExit()
        {
            base.OnExit();
        }

        private void DoBuff()
        {
            if (!this.hasFired)
            {
                this.hasFired = true;

                base.characterBody.AddSpreadBloom(0.75f);
                Ray aimRay = base.GetAimRay();
                EffectManager.SimpleMuzzleFlash(Commando.CommandoWeapon.FirePistol.effectPrefab, base.gameObject, this.muzzleString, false);


                if (NetworkServer.active)
                {
                    base.characterBody.AddTimedBuff(WispSurvivor.Modules.Buffs.invigorate, 5f);


                    if (grappleHandler.hasGrappleTarget())
                    {
                        List<GrappleTarget> targets = grappleHandler.GetGrappleTargets();
                        foreach (GrappleTarget target in targets)
                        {
                            if (target.grappleTarget.teamIndex == TeamIndex.Monster)
                            {
                                if (NetworkServer.active)
                                {
                                    target.grappleTargetBody.AddTimedBuff(WispSurvivor.Modules.Buffs.regenMinus, 5f);
                                    DotController.InflictDot(target.grappleTargetHealth.gameObject, base.healthComponent.gameObject, WispSurvivor.Modules.Buffs.degenDot);
                                }
                            }
                            else if (target.grappleTarget.teamIndex == TeamIndex.Player)
                            {
                                if (NetworkServer.active) target.grappleTargetBody.AddTimedBuff(WispSurvivor.Modules.Buffs.invigorate, 5f);
                            }
                        }
                    }
                }

            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (base.fixedAge >= this.fireDuration)
            {
                DoBuff();
            }

            if (base.fixedAge >= this.duration && base.isAuthority)
            {
                this.outer.SetNextStateToMain();
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Skill;
        }
    }

    public class WispSiphon : BaseSkillState
    {
        public float damageCoefficient = 0f;
        public float baseDuration = 1f;
        public float recoil = 1f;
        public float maxDistanceFilter = 100f;

        private TetherHandler tetherHandler;
        private float duration;
        private float fireDuration;
        private bool hasFired;
        private Animator animator;
        private string muzzleString;


        public override void OnEnter()
        {
            Debug.Log("Entering siphon skillstate");
            base.OnEnter();
            this.duration = baseDuration;
            this.fireDuration = this.duration * .01f;
        }

        private void DoSiphon()
        {
            if (this.hasFired) return;
            this.hasFired = true;
            
            //if (base.isAuthority)
            //{
                tetherHandler = base.GetComponent<TetherHandler>();
                HurtBox closestHurtbox = null;

                //First check to see if we're aiming at a valid target
                Ray aimRay = base.GetAimRay();
                RaycastHit raycastHit = new RaycastHit();
                bool foundTarget = Physics.Raycast(aimRay, out raycastHit, maxDistanceFilter);

                if (foundTarget && raycastHit.collider.GetComponent<HurtBox>())
                {
                    TeamIndex targetTeamIndex = raycastHit.collider.GetComponent<HurtBox>().healthComponent.body.teamComponent.teamIndex;
                    //Make sure we're not targeting the same team
                    if (base.GetTeam() != targetTeamIndex)
                    {
                        closestHurtbox = raycastHit.collider.GetComponent<HurtBox>();
                        Debug.Log("Found aimed at object " + closestHurtbox.transform.root.name);
                    }
                }
                //If we weren't aiming at something, just search for a valid nearby target
                else
                {
                    //Search for all nearby targets
                    BullseyeSearch bullseyeSearch = new BullseyeSearch();
                    bullseyeSearch.searchOrigin = base.transform.position;
                    bullseyeSearch.searchDirection = UnityEngine.Random.onUnitSphere;
                    bullseyeSearch.maxDistanceFilter = maxDistanceFilter;
                    bullseyeSearch.teamMaskFilter = TeamMask.GetEnemyTeams(base.GetTeam());

                    //Sort by distance
                    bullseyeSearch.sortMode = BullseyeSearch.SortMode.Distance;
                    bullseyeSearch.RefreshCandidates();
                    //Remove ourselves from the search results
                    //(shouldn't be there in the first place, but hey)
                    bullseyeSearch.FilterOutGameObject(base.gameObject);

                    //Get the closest hurtbox
                    closestHurtbox = bullseyeSearch.GetResults().FirstOrDefault<HurtBox>();

                    Debug.Log("Found object " + closestHurtbox.transform.root.name);
                    if (closestHurtbox == default(HurtBox)) Debug.LogError("Default value!");
                    if (closestHurtbox == null) Debug.LogError("Null value!");
                }

                //Set up our grapple handler
                if (tetherHandler == null)
                {
                    tetherHandler = base.gameObject.AddComponent<TetherHandler>();
                    Debug.LogWarning("Added grapple handler via siphon");
                }

                //Then establish our grapple target
                if (closestHurtbox == null)
                {
                    Debug.LogError("Null hurtbox");
                    return;
                }


                //If we've successfully established a tether
                if (closestHurtbox)
                {
                    Debug.Log("Attempting to establish tether");

                    //If adding a new grapple target would go beyond our max stock
                    int curNumGrappled = tetherHandler.GetGrappleTargets().Count;
                    if (curNumGrappled + 1 > base.activatorSkillSlot.maxStock)
                    {
                        //Remove the oldest grapple target
                        tetherHandler.ClearGrappleTarget(tetherHandler.GetGrappleTargets()[0]);
                    }

                    tetherHandler.SetGrappleTarget(closestHurtbox);
                    tetherHandler.TETHER_TYPE = TETHER_TYPE.SIPHON;
                    base.characterBody.AddBuff(WispSurvivor.Modules.Buffs.siphonSelf);
                    closestHurtbox.healthComponent.body.AddBuff(WispSurvivor.Modules.Buffs.siphonTarget);
                }

            //}

            this.animator = base.GetModelAnimator();
            this.muzzleString = "Muzzle";


            base.PlayAnimation("Gesture, Override", "FireGrapple", "FireGrapple.playbackRate", this.duration);
        }

        public override void OnExit()
        {
            base.OnExit();
        }


        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if(base.fixedAge >= this.fireDuration)
            {
                DoSiphon();
            }

            if (base.fixedAge >= this.duration && base.isAuthority)
            {
                this.outer.SetNextStateToMain();
            }

        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Any;
        }
    }

    public class WispTether : BaseSkillState
    {
        public float damageCoefficient = 0f;
        public float baseDuration = 1f;
        public float recoil = 1f;
        public float maxDistanceFilter = 100f;

        private TetherHandler tetherHandler;
        private float duration;
        private float fireDuration;
        private bool hasFired;
        private Animator animator;
        private string muzzleString;


        public override void OnEnter()
        {
            short tetherIndex = EntityStates.StateIndexTable.TypeToIndex(typeof(WispTether));
            Debug.Log("Entering tether base state. Index on this machine: " + tetherIndex); 
            base.OnEnter();
            this.duration = baseDuration;
            this.fireDuration = this.duration * .01f;
            Debug.Log("Ran on enter for tether");
        }

        private void DoTether()
        {
            if (this.hasFired) return;
            this.hasFired = true;
            Debug.Log("Setting tether handler!");

            //base.isAuthority -> will only run on local machine
            //if (base.isAuthority)
            //{
                tetherHandler = base.GetComponent<TetherHandler>();

                HurtBox closestHurtbox = null;

                Debug.Log("Raycasting!");
                //First check to see if we're aiming at a valid target
                Ray aimRay = base.GetAimRay();
                RaycastHit raycastHit = new RaycastHit();
                bool foundTarget = Physics.Raycast(aimRay, out raycastHit, maxDistanceFilter);

                if (foundTarget && raycastHit.collider.GetComponent<HurtBox>())
                {
                    TeamIndex targetTeamIndex = raycastHit.collider.GetComponent<HurtBox>().healthComponent.body.teamComponent.teamIndex;
                    //Make sure we're not targeting the enemy team
                    if (base.GetTeam() == targetTeamIndex)
                    {
                        closestHurtbox = raycastHit.collider.GetComponent<HurtBox>();
                        Debug.Log("Found aimed at object " + closestHurtbox.transform.root.name);
                    }
                }
                //If we weren't aiming at something, just search for a valid nearby target
                else
                {
                    //Search for all nearby targets
                    BullseyeSearch bullseyeSearch = new BullseyeSearch();
                    bullseyeSearch.searchOrigin = base.transform.position;
                    bullseyeSearch.searchDirection = UnityEngine.Random.onUnitSphere;
                    bullseyeSearch.maxDistanceFilter = maxDistanceFilter;
                    TeamMask sameTeamMask = new TeamMask();
                    sameTeamMask.AddTeam(base.GetTeam());
                    bullseyeSearch.teamMaskFilter = sameTeamMask;

                    //Sort by distance
                    bullseyeSearch.sortMode = BullseyeSearch.SortMode.Distance;
                    bullseyeSearch.RefreshCandidates();
                    //Remove ourselves from the search results
                    //(shouldn't be there in the first place, but hey)
                    bullseyeSearch.FilterOutGameObject(base.gameObject);

                    //Get the closest hurtbox
                    closestHurtbox = bullseyeSearch.GetResults().FirstOrDefault<HurtBox>();

                    Debug.Log("Found local object " + closestHurtbox.transform.root.name);
                    if (closestHurtbox == default(HurtBox)) Debug.LogError("Default value!");
                    if (closestHurtbox == null) Debug.LogError("Null value!");
                }

                //Set up our grapple handler
                if (tetherHandler == null)
                {
                    tetherHandler = base.gameObject.AddComponent<TetherHandler>();
                    Debug.LogWarning("Added grapple handler via tether function");
                    return;
                }

                //Then establish our grapple target
                if (closestHurtbox == null)
                {
                    Debug.LogError("Null hurtbox");
                    return;
                }
                //If we've successfully established a tether
                else if (closestHurtbox)
                {
                    Debug.Log("Attempting to establish tether");
                    //If adding a new grapple target would go beyond our max stock
                    int curNumGrappled = tetherHandler.GetGrappleTargets().Count;
                    if (curNumGrappled + 1 > base.activatorSkillSlot.maxStock)
                    {
                        //Remove the oldest grapple target
                        tetherHandler.ClearGrappleTarget(tetherHandler.GetGrappleTargets()[0]);
                    }

                    tetherHandler.SetGrappleTarget(closestHurtbox);
                    tetherHandler.TETHER_TYPE = TETHER_TYPE.TETHER;
                    Debug.Log("Set grapple target");

                    base.characterBody.AddBuff(WispSurvivor.Modules.Buffs.sustainSelf);
                    closestHurtbox.healthComponent.body.AddBuff(WispSurvivor.Modules.Buffs.sustainTarget);
                }
            //}
            /*
                this.animator = base.GetModelAnimator();
                this.muzzleString = "Muzzle";


                base.PlayAnimation("Gesture, Override", "FireGrapple", "FireGrapple.playbackRate", this.duration);
                */
        }

        public override void OnExit()
        {
            base.OnExit();
        }


        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if(base.fixedAge >= this.fireDuration && !hasFired)
            {
                Debug.Log("Running DoTether");
                DoTether();
            }

            if (base.fixedAge >= this.duration && base.isAuthority)
            {
                this.outer.SetNextStateToMain();
            }

        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Any;
        }
    }

    public class WispBurst : BaseSkillState
    {
        public float damageCoefficient = 7f;
        public float baseDuration = 1f;
        public float recoil = 1f;
        private float burstRadius = 15f;

        private float duration;
        private float fireDuration;
        private bool hasFired;
        private string muzzleString;
        private TetherHandler grappleHandler;

        public override void OnEnter()
        {
            base.OnEnter();

            if (grappleHandler == null)
            {
                grappleHandler = base.GetComponent<TetherHandler>();
                if (grappleHandler == null)
                {
                    grappleHandler = base.gameObject.AddComponent<TetherHandler>();
                    Debug.LogWarning("Added grapple handler via burst function");
                }
            }

            this.duration = this.baseDuration / this.attackSpeedStat;
            this.fireDuration = 0.25f * this.duration;
            base.characterBody.SetAimTimer(2f);
            this.muzzleString = "Muzzle";

            base.PlayAnimation("Gesture, Override", "FireArrow", "FireArrow.playbackRate", this.duration);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (base.fixedAge >= this.fireDuration)
            {
                DoExplode();
            }

            if (base.fixedAge >= this.duration && base.isAuthority)
            {
                this.outer.SetNextStateToMain();
            }
        }

        private void DoExplode()
        {
            if (!this.hasFired)
            {
                this.hasFired = true;

                base.characterBody.AddSpreadBloom(1.25f);
                EffectManager.SimpleMuzzleFlash(Commando.CommandoWeapon.FirePistol.effectPrefab, base.gameObject, this.muzzleString, false);

                if (NetworkServer.active)
                {
                    EffectManager.SimpleEffect(WispSurvivor.WispSurvivor.burstPrefab, transform.position, transform.rotation, true);
                    EffectManager.SimpleEffect(WispSurvivor.WispSurvivor.burstSecondary, transform.position, transform.rotation, true);

                    //Get all nearby hurtboxes
                    BullseyeSearch selfSearch = new BullseyeSearch();
                    selfSearch.searchOrigin = base.transform.position;
                    selfSearch.maxDistanceFilter = burstRadius;
                    selfSearch.teamMaskFilter = TeamMask.GetEnemyTeams(base.teamComponent.teamIndex);
                    selfSearch.RefreshCandidates();

                    HurtBox[] hurtboxes = selfSearch.GetResults().ToArray();
                    //Debug.Log("Found " + hurtboxes.Length + " hurtboxes within " + burstRadius + " of self");

                    //Set up damage, add bonus based on percent of full barrier
                    DamageInfo damageInfo = new DamageInfo();
                    damageInfo.attacker = base.characterBody.gameObject;
                    damageInfo.crit = base.RollCrit();
                    
                    float bonusDamage = base.healthComponent.barrier / base.healthComponent.fullBarrier;
                    damageInfo.damage = base.damageStat * (this.damageCoefficient + bonusDamage);

                    //Drain barrier
                    base.healthComponent.barrier = 0f;
                    damageInfo.damageColorIndex = DamageColorIndex.Default;
                    damageInfo.damageType = DamageType.AOE;
                    damageInfo.procCoefficient = 0f;

                    //Boom
                    for (int i = 0; i < hurtboxes.Length; i++)
                    {
                        hurtboxes[i].healthComponent.TakeDamage(damageInfo);
                    }

                    if (!grappleHandler.hasGrappleTarget()) return;

                    //Now do an explosion on each grappled target
                    List<GrappleTarget> targets = grappleHandler.GetGrappleTargets();
                    foreach (GrappleTarget target in targets)
                    {
                        Transform grappleTransform = target.grappleTarget.transform;
                        EffectManager.SimpleEffect(WispSurvivor.WispSurvivor.burstPrefab, grappleTransform.position, grappleTransform.rotation, true);
                        EffectManager.SimpleEffect(WispSurvivor.WispSurvivor.burstSecondary, grappleTransform.position, grappleTransform.rotation, true);


                        BullseyeSearch grappleSearch = new BullseyeSearch();
                        grappleSearch.searchOrigin = target.grappleTarget.transform.position;
                        grappleSearch.maxDistanceFilter = burstRadius;
                        grappleSearch.teamMaskFilter = TeamMask.GetEnemyTeams(base.teamComponent.teamIndex);
                        grappleSearch.RefreshCandidates();
                        hurtboxes = grappleSearch.GetResults().ToArray();
                        //Debug.Log("Found " + hurtboxes.Length + " hurtboxes within " + burstRadius + " of grapple target");

                        for (int i = 0; i < hurtboxes.Length; i++)
                        {
                            hurtboxes[i].healthComponent.TakeDamage(damageInfo);
                        }
                    }
                }
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Frozen;
        }

        public override void OnExit()
        {
            base.OnExit();
        }
    }
}
