using UnityEngine;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine.Events;

using Assets.HomeGhost.Core.PlayerStateMachine.Detection;

using ALabs_Character_Stats;
using ALabs.GameControl;
using Assets.HomeGhost.Core.Static;
using Assets.HomeGhost.Core.CharacterComponents;
using System.Collections.Generic;
using Assets.HomeGhost.Core.Enemy.EnemyStateMachine;
using Assets.HomeGhost.Core.PlayerStateMachine.UI;
using Assets.HomeGhost.Core.Companions.Robot;
using Assets.HomeGhost.Core.PlayerStateMachine.PLayerVFX;
using Unity.Entities;


namespace Assets.HomeGhost.Core.PlayerStateMachine
{
    public class PlayerMachine : MonoBehaviour
    {

#region Serialized Variables

        private static PlayerMachine _instance;
        public static PlayerMachine Instance { get { return _instance; } set{ _instance = value; } }
        public Entity PlayerEntity;

        [Header("Reference Components")]
        private Health _playerHealth;
        [SerializeField] private Animator _anim;
        [SerializeField] private PlayerStats_Player _player;
        [SerializeField] private CanvasGroup _gameUICanvasGroup;
        [SerializeField] private Camera _mainCamera;
        [SerializeField] private StatsManager _playerStatsManager;
        [SerializeField] private bool _bowSpine;
        public PlayerGameUIObject GameUIObject;
        public RobotMachine RobotMachine;

        [Header("Reference Objects")]
        [SerializeField] private GameObject _detectionScriptHolder;
        [SerializeField] private GameObject _humanCharacterObject;
        [SerializeField] private GameObject _targetLockIcon;
        [SerializeField] private GameObject _holdArrowFire;

        [Header("Canvas Groups")]
        [SerializeField] private CanvasGroup _gameOverScreen;

        [Header("Materials")]
        [SerializeField] private SkinnedMeshRenderer[] _playerSkinnedMeshRenderers;
        [SerializeField] private Material _healthPotionMaterial, _damageMaterial;
        
        [Header("Bool Variables")]
        [SerializeField] private bool _playerHasGlider;
        [SerializeField] private bool _freeHang;

        [Header("Float Variables")]
        [SerializeField] private float _bulletTimeHeight = 1.5f;
        [SerializeField] private float _airBowTimeSpeed = 0.25f;
        [SerializeField] private float _startFallHeight = 3f;
        [SerializeField] private float _landingTriggerHeight = 0.15f;
        [SerializeField] private float _gliderResetDelay = 0.25f;
        [SerializeField] private float _gliderOpenHeight = 1.5f;
        
        [Header("Events")]
        public UnityEvent DeathEvent;
        public UnityEvent DamageEvent;

        [Header("Indoors")]
        public bool IsIndoors;
        public Collider IndoorCollider;

#endregion

#region Private Variables

        // State Machine
        private BaseState _currentState;
        private Factory _states;
        
        // Private Components
        private PlayerInputBridge _input;
        private PlayerMovement _movement;
        private PlayerGravity _gravitySetter;
        private Detection_Water _waterDetection;
        private Detection_Enemies _enemyDetect;
        private Detection_SurfaceAngle _wallAngleDetection;
        private Detection_Climbing _wallDetection;
        private List<List<Material>> _playerMaterials;
        private List<List<Material>> _damageMaterials;

        // Private Objects
        private GameObject _lockedObject;
        private EnemyMachine _lockedEnemy;
        private Transform _interactionTransform = null;
        private GameObject _climbingChildObject;

        // private bool values
        private int _framesHoldingSprintButton = 0;
        private bool _inventoryMenu;
        private bool _attacking = false, _preventAttack = false, _interacting = false, _dead = false;
        private bool _water, _swimming, _waterLevel;
        private bool _jumping, _doubleJumping,  _falling, _groundedDisable, _jumpReady = true;
        private bool _landingSet, _landingTrigger, _landingTriggered;
        private bool _blocking = false, _preventBlocking = false;
        public bool _hideWeapon, _bowActive, _bowHold = false, _weaponEquiped = false, _fireArrows;
        private bool _pushingObject = false, _ragdollEnabled = false;
        private bool _lockedOnEnemy, _lockedOnObject;
        private bool _gliderReset, _gliderReady = true;
        private bool _move, _sprint, _grounded, _climbing, _gliding, _downOnGround;
        private bool _climbUp, _jumpClimbUp;
        private bool _isSliding;
        private bool _hasBowObject = false;

        // private Vector Values
        private float _previousYPosition, _jumpYPosition;
        private readonly float _slopeSlideDirectionFloat;
        private Vector3 _slopeSlideVelocity;
        
#endregion

#region Getters and Setters

        // State Machine
        public BaseState CurrentState { get { return _currentState; } set { _currentState = value; } }
        public Health PlayerHealth { get { return _playerHealth; } set { _playerHealth = value; } }

        // Reference Components
        public PlayerStats_Player Player { get { return _player; } }
        public StatsManager PlayerStatsManager { get { return _playerStatsManager; } }
        public PlayerGravity GravitySet { get { return _gravitySetter; } }
        public PlayerInputBridge Input { get { return _input; } }
        public PlayerMovement Movement { get { return _movement; } }
        public Detection_Climbing WallDetect { get { return _wallDetection; } }
        public Animator Animator { get { return _anim; } }
        public Detection_Enemies EnemyDetection { get { return _enemyDetect; } }
        public Detection_SurfaceAngle WallAngleDetect { get { return _wallAngleDetection; } }   
        public bool BowSpine { get { return _bowSpine; } set { _bowSpine = value; } }
        
        
        // Objects
        public Transform InteractionTransform { get { return _interactionTransform; } set { _interactionTransform = value; } }
        public GameObject ClimbingChildObject { get { return _climbingChildObject; } set { _climbingChildObject = value; } }
        public GameObject LockedObject { get { return _lockedObject; } set { _lockedObject = value; } }
        public EnemyMachine LockedEnemy { get { return _lockedEnemy; } set { _lockedEnemy = value; } }

        // int values
        public int FramesHoldingSprintButton { get { return _framesHoldingSprintButton; } set { _framesHoldingSprintButton = value; } }

        // bool values
        public bool DoubleJumping { get { return _doubleJumping; } set { _doubleJumping = value; } }
        public bool FreeHang { get { return _freeHang; } set { _freeHang = value; } }
        public bool WeaponEquiped { get { return _weaponEquiped; } set { _weaponEquiped = value; } }
        public bool PlayerHasGlider { get { return _playerHasGlider; } }
        public bool InventoryMenu { get { return _inventoryMenu; } set { _inventoryMenu = value; } }
        public bool HideWeapon { get { return _hideWeapon; } set { _hideWeapon = value; } }
        public bool RagdollEnabled { get { return _ragdollEnabled; } set { _ragdollEnabled = value; } }
        public bool Interacting { get { return _interacting; } set { _interacting = value; } }
        public bool Dead { get { return _dead; } set { _dead = true; } }
        public bool DownOnGround { get { return _downOnGround; } set { _downOnGround = value; } }
        public bool BowHold { get { return _bowHold; } set { _bowHold = value; } }
        public bool PreventAttack { get { return _preventAttack; } }
        public bool LockedOnObject { get { return _lockedOnObject; } set { _lockedOnObject = value; } }
        public bool PushingObject { get { return _pushingObject; } set { _pushingObject = value; } }
        public bool Gliding { get { return _gliding; } set { _gliding = value; } }
        public bool BowActive { get { return _bowActive; } set { _bowActive = value; } }
        public bool HasBow { get { return _hasBowObject; } }
        public bool Jumping { get { return _jumping; } set { _jumping = value; } }
        public bool JumpReady { get { return _jumpReady; } set {_jumpReady = value; } }
        public bool Climbing { get { return _climbing; } set { _climbing = value; } }
        public bool LockedOnEnemy { get { return _lockedOnEnemy; } set { _lockedOnEnemy = value; } }
        public bool Falling { get { return _falling; } set { _falling = value; } }
        public bool GroundedDisable { get { return _groundedDisable; } }
        public bool Attacking { get { return _attacking; } set { _attacking = value; } }
        public bool WaterLevel { get { return _waterLevel; } set { _waterLevel = value; } }
        public bool Water { get { return _water; } set { _water = value; } }
        public bool Blocking { get { return _blocking; } set { _blocking = value; } }
        public bool PreventBlock { get { return _preventBlocking; } set { _preventBlocking = value; } }
        public bool Sprint { get { return _sprint; } set { _sprint = value; } }
        public bool GliderReady { get { return _gliderReady; } }
        public bool LandingTrigger { get { return _landingTrigger; } set { _landingTrigger = value; } }
        public bool ClimbUp { get { return _climbUp; } set { _climbUp = value; } }
        public bool JumpClimbUp { get { return _jumpClimbUp; } set { _jumpClimbUp = value; } }
        public bool Swimming { get { return _swimming; } set { _swimming = value; } }
        public bool FireArrows { get { return _fireArrows; } set { _fireArrows = value; } }
        public bool LandingSet { get { return _landingSet; } set { _landingSet = value; } }

        // vector values
        public float LandingTriggerHeight { get { return _landingTriggerHeight; } }
        public float AirBowTimeSpeed { get { return _airBowTimeSpeed; } }
        public float BulletTimeHeight { get { return _bulletTimeHeight; } }
        public float GliderOpenHeight { get { return _gliderOpenHeight; } }
        public float JumpYPosition { get { return _jumpYPosition; } set { _jumpYPosition = value; } }
        public Vector3 SlopeSlideVelocity { get { return _slopeSlideVelocity; } set { _slopeSlideVelocity = value; } }
        public float WaterHeight { get { return _waterDetection.WaterLevel; } }
        public float StartFallHeight { get { return _startFallHeight; } }

       
#endregion

#region Monobehaviour Functions

        private void Awake()
        {
            if(_instance != null){Destroy(this);}
            else{_instance = this;}

            _input = GetComponent<PlayerInputBridge>();
            _movement = GetComponent<PlayerMovement>();
            _gravitySetter = GetComponent<PlayerGravity>();
            _playerStatsManager = GetComponent<StatsManager>();
            _waterDetection = _detectionScriptHolder.GetComponent<Detection_Water>();
            _enemyDetect = _detectionScriptHolder.GetComponent<Detection_Enemies>();
            _wallAngleDetection = _detectionScriptHolder.GetComponent<Detection_SurfaceAngle>();
            _wallDetection = _detectionScriptHolder.GetComponent<Detection_Climbing>();
            SetupPlayerHealth();

            _states = new Factory(this);
            _currentState = _states.GroundedRoot();
            _currentState.EnterState();

            SetupDamageMaterials();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }

        private void Update()
        {
            _currentState.UpdateStates();
            _attacking = _anim.GetFloat("Attack") > 0;
            _preventAttack = _downOnGround;
            IndoorsCheck();
            if(GameUIObject.Health != _playerHealth.health){ GameUIObject.Health = _playerHealth.health; }
            if(GameUIObject.MaxHealth != _playerHealth.maxHealth){GameUIObject.MaxHealth = _playerHealth.maxHealth; }
            Shader.SetGlobalVector("PlayerPosition",PlayerMonitor.PlayerPosition);
            
            if(_attacking || _downOnGround || _dead)
            {
                if(_downOnGround || _dead)
                {
                    
                }
            } else 
            {

                if(_movement.PreventMovementWhileAttacking)
                {
                    _movement.PreventMovementWhileAttacking = false;
                }
                
            }
            
            if (Movement.EnvironmentDetection.GroundHeight > _startFallHeight && !Climbing && !_jumping && !_gliding)
            {
                _falling = true;
            }
            AnimationParameters();
            UserInterface();
            SlopeSliding();
             
            if (!_jumping && !_falling && _landingTriggered)
            {
                _landingTriggered = false;
            }
            if(transform.position != PlayerMonitor.PlayerPosition){PlayerMonitor.PlayerPosition = transform.position; }
            if(transform.forward != PlayerMonitor.PlayerForward){ PlayerMonitor.PlayerForward = transform.forward; }
        }

        private void FixedUpdate()
        {
            _currentState.FixedUpdateStates();
        }

        private void LateUpdate()
        {
            _previousYPosition = transform.position.y;

        }

#endregion

#region Camera Switching Interaction

        public void DisablePlayerCameraAndUI()
        {
            _mainCamera.enabled = false;
            _gameUICanvasGroup.alpha = 0;
        }

        public void EnablePlayerCameraAndUI()
        {
            _gameUICanvasGroup.alpha = 1;
            _mainCamera.enabled = true;
        }

#endregion

#region Target Locking

        public void EnemyLocked(){}
        public void EnemyUnlocked(){}
        public void EnemyChanged(){}

#endregion


#region Slope Sliding

        void SlopeSliding()
        {
            if (_slopeSlideVelocity != Vector3.zero && _movement.EnvironmentDetection.IsGrounded && !_climbing)
            {
                _isSliding = true;
            }
            else
            {
                _isSliding = false;
                _slopeSlideVelocity = Vector3.zero;
            }
            if (_isSliding)
            {
                Movement.SlopeForces(_slopeSlideVelocity);
            }
        }

#endregion

       

#region Misc and Reset Functions

        // Ground disable functions are meant to create a pause in detecting the ground
        // long enough for player to get off the ground when jumping
        // without exiting the jump animation from being grounded

        public void GroundedDisableOn()
        {
            _groundedDisable = true;
        }

        public void GroundedDisableOff()
        {
            _groundedDisable = false;
        }

        public void GliderReset()
        {
            
            if (!_gliderReady && !_gliderReset)
            {
                _gliderReset = true;
                Invoke(nameof(GliderReset), _gliderResetDelay);
            }
            else
            {
                _gliderReady = true;
                _gliderReset = false;
            }
        }

        public void WeaponEquip()
        {
            _weaponEquiped = true;
        }

        public void WeaponDeEquip()
        {
            AnimationTrigger("19");
            _weaponEquiped = false;
        }

        public void StartInteraction(Transform interactionTransform)
        {
            _movement.InteractionTransform = interactionTransform;
            _interacting = true;
        }

        public void StartInteraction(Transform interactionTransform, string animationString)
        {
            _movement.InteractionTransform = interactionTransform;
            _interacting = true;
            AnimationTriggerString(animationString);
        }

        public void EndInteraction()
        {
            _movement.InteractionTransform = null;
            _interacting = false;
        }

#endregion

#region Animation

        public void AnimationTrigger(string trigger)
        {
            _anim.SetInteger("TriggerInt",int.Parse(trigger));
            StartCoroutine(TriggerGo());
        }

        public void AnimationTriggerString(string trigger)
        {
            StartCoroutine(Trigger(trigger));
        }

        private IEnumerator Trigger(string trigger)
        {
            _anim.SetTrigger(trigger);
            yield return null;
            _anim.ResetTrigger(trigger);
        }

        private IEnumerator TriggerGo()
        {
            _anim.SetTrigger("TriggerGo");
            yield return null;
            _anim.ResetTrigger("TriggerGo");
        }

        void AnimationParameters()
        {
            bool target = false;
            if(_lockedOnEnemy || _lockedOnObject)
            {
                target = true;
            }
            _anim.SetBool("TargetLocked", target);
            _move = Input.LeftStickPressed;
            _anim.SetBool("Push",_pushingObject);
            _anim.SetFloat("SlopeSlideVelocity", _slopeSlideVelocity.magnitude);
            if (_anim.GetBool("Move") != _move)
            {
                _anim.SetBool("Move", _move);
            }
            _sprint = _framesHoldingSprintButton > 10 && _input.LeftStickPressed;
            if (_anim.GetBool("Sprint") != _sprint)
            {
                _anim.SetBool("Sprint", _sprint);
            }
            _grounded = Movement.EnvironmentDetection.IsGrounded && !_groundedDisable;
            if (_anim.GetBool("Grounded") != _grounded)
            {
                _anim.SetBool("Grounded", _grounded);
            }
            if (_anim.GetFloat("SlideDirectionFloat") != _slopeSlideDirectionFloat)
            {
                _anim.SetFloat("SlopeDirectionFloat", _slopeSlideDirectionFloat);
            }
            _anim.SetBool("Climbing", _climbing);
            _anim.SetBool("Swim", _swimming);

            if(_anim.GetBool("PreventAttack") != _preventAttack)
            {
                _anim.SetBool("PreventAttack",_preventAttack);
            }
            if (!_climbing)
            {
                if(WaterHeight < 0.4f || Swimming)
                {
                    _anim.SetFloat("AnimationSpeed", 1f);
                }
                else
                {
                    _anim.SetFloat("AnimationSpeed", 0.6f);
                }
            }
        }

        public void FallingAnimation()
        {
            _anim.SetFloat("FallingFloat", Movement.EnvironmentDetection.FallTimer);
        }

        public void LandingAnimation(){}

#endregion

#region UI Interface

        public void ShieldBlockAnim()
        {
            Vector3 movePos = PlayerMonitor.PlayerPosition + (-PlayerMonitor.PlayerForward * 2f);
            List<Vector3> positions = new(){movePos};
            List<float> speeds = new(){ 6f };
            List<Quaternion> rotations = new(){transform.rotation};
            Movement.ClimbingChildObject.transform.position = movePos;
            Movement.SetSlerp(positions,rotations,speeds);
            AnimationTriggerString("ShieldTrigger");
        }



#endregion

#region Damage and Death

        private void SetupPlayerHealth()
        {
            _playerHealth = new();
            float maxHealth = 100f;
            _playerHealth.SetMaxHealth(maxHealth);
            _playerHealth.DeathEvent = new();
            _playerHealth.DeathEvent.AddListener(PlayerDeath);
        }

        public void HealthPotion()
        {
            StartCoroutine(HealthCo());
        }

        public void TakeDamage(float damageAmount)
        {
            if(PlayerHealth.dead){return;}
            if (_climbing || _gliding || _wallDetection._climbingUp)
            {
                _climbing = false;
                _gliding = false;
                _ragdollEnabled = true;
            }
            
            float damageResponse = Mathf.InverseLerp(0,PlayerHealth.maxHealth,PlayerHealth.health - damageAmount);
            _anim.SetFloat("DamageAmountFloat",damageResponse);

            _playerHealth.TakeDamage(damageAmount);
            
            if(!_playerHealth.dead)
            {
                DamageEvent.Invoke();
                AnimationTriggerString("DamageTrigger");
                StartCoroutine(DamageMaterialFlash());
            }
        }

        public void PlayerDeath()
        {
            if(!_dead)
            {
                _anim.SetBool("Dead",true);
                _dead = true;
            }
            
        }

        private IEnumerator DamageMaterialFlash()
        {
            PlayerVFXControl.Instance.PlayerDamageFlash();
            int iteratorOne = 0;
            foreach(var renderer in _playerSkinnedMeshRenderers)
            {
                renderer.SetSharedMaterials(_damageMaterials[iteratorOne]);
                iteratorOne++;
            }

            yield return new WaitForSeconds(.6f);

            int iteratorTwo = 0;
            foreach(var renderer in _playerSkinnedMeshRenderers)
            {
                renderer.SetSharedMaterials(_playerMaterials[iteratorTwo]);
                iteratorTwo++;
            }
        }

#endregion

        public void CollectGlider()
        {
            _playerHasGlider = true;
        }

        public void CollectBow()
        {
            _hasBowObject = true;
        }

        public void Intro2()
        {
            PlayerMonitorInstance.Instance.FadeOut();
            _anim.SetBool("Intro",false);
            _anim.SetTrigger("Intro2");
        }

        public void DeathEffect()
        {
            DeathEvent.Invoke();
        }

        public void GetFireArrows()
        {
            _fireArrows = true;
            _holdArrowFire.SetActive(true);
        }

        public void Teleport(Vector3 respawnPoint)
        {
            _movement.Controller.enabled = false;
            transform.position = respawnPoint;
            _anim.SetBool("Falling",false);
            _movement.Controller.enabled = true;
            _movement.Controller.Move(respawnPoint - transform.position);
        }


#region Setup Functions

        private void SetupDamageMaterials()
        {
            int total = 0;
            foreach(var renderer in _playerSkinnedMeshRenderers)
            {
                foreach(var material in renderer.materials)
                {
                    total++;
                }
            }

            _playerMaterials = new();
            _damageMaterials = new();

            int iterator = 0;
            foreach(var renderer in _playerSkinnedMeshRenderers)
            {
                _playerMaterials.Add(new());
                renderer.GetMaterials(_playerMaterials[iterator]);
                List<Material> flashMat = new();
                foreach(var material in renderer.sharedMaterials)
                {
                    flashMat.Add(_damageMaterial);
                }
                _damageMaterials.Add(flashMat);
                iterator++;
            }
        }

#endregion

#region Indoors Detection

        private void IndoorsCheck()
        {
            
            Ray ray = new(transform.position,Vector3.up);
            if(Physics.SphereCast(ray,0.25f,out RaycastHit hit,200f,LayerMaskCheck.IndoorsMask,QueryTriggerInteraction.Collide))
            {
                if(IsIndoors){return;}
                if(IndoorCollider != hit.collider){ IsIndoors = true; IndoorCollider = hit.collider; }
            }
            else
            {
                if(IsIndoors){IsIndoors = false; IndoorCollider = null; }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if(IsIndoors && IndoorCollider == other){ IsIndoors = false; IndoorCollider = null; }
        }

#endregion


    }
}
