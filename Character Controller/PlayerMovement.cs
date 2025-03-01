using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using HomeGhost.Core.Scriptables;
using Assets.HomeGhost.Core.PlayerStateMachine.Detection;
using Pathfinding.RVO;
using Assets.HomeGhost.CoreECS.Player.Movement;
using System;
using RaycastPro.RaySensors;
using Assets.HomeGhost.Core.Static;
using TMPro;

namespace Assets.HomeGhost.Core.PlayerStateMachine
{
    public class PlayerMovement : MonoBehaviour
    {

#region Variables

        public ClimbingParentObject _climbingParent;
        public ChainRay _climbingRay;
        private Vector3 _destinationPoint;
        Vector3 newMovePoint;
        public ClimbingDebug debug;
        public GameObject SlerpImage;

        [Header("Speed")]
        [SerializeField] private PlayerSpeedObject _speedDictionary;
        private float _speed, _activeSpeed, _currentSlopeSpeed, _slerpSpeed;
        private bool _onSlope;
        [SerializeField] public AnimationCurve RollingAnimationCurve; 

        [Header("References")]
        [SerializeField] Rigidbody _playerRigidbody;
        [SerializeField] CapsuleCollider _playerCollider;
        [SerializeField] RVOController _rvoController;
        [SerializeField] private Transform _mainCameraTransform;
        [SerializeField] private GameObject _characterObject;
        private PlayerMachine _stateMachine;
        private Transform _interactionTransform;
        public CharacterController _characterController;
        private PlayerGravity _playerGravityScript;
        private GameObject _climbingChildGameObject;
        private ClimbingChild _climbingChildScript;
        public PlayerMovementECS ECSMovement;
        private bool _slerping;
        private bool _preventMovement;
        public Vector3 _moveDirection, _finalMoveDirection;
        public Transform ForceDirectionObject;
        public float ForceAmount = 0;
        public float ForcePower = 0;
        private ParentForces _parentForces;
        private Transform _activeParent;
        private Vector3 _platformVelocity, _previousParentPosition;
        
        [Header("Detection")]
        [SerializeField] private GameObject _detectionScriptObject;
        private Detection_Ground _groundDetection;
        private Detection_SurfaceAngle _surfaceAngleDetection;
        [SerializeField] private LayerMask _environmentLayerMask, _enemyLayerMask;

        [Header("Animation Slerp")]
        private float _animationSlerpTime, _animationSlerpNormalizedTime, _animationSlerp;

        [Header("Rotation")]
        [SerializeField] private float _rotationSmoothing = 0.1f;
        [SerializeField] private float _glidingRotationSmoothing = 30f;
        [SerializeField] private float _climbingRotationSmoothing = .5f;
        [SerializeField] private float _swimRotationSpeed = 3f;
        private float _rotation;
        private float _turnSmoothVelocity;
        private readonly float _previousPlayerEulerY;
        private Quaternion _characterRotation, _characterClimbingRotation;
        
        [Header("Forces")]
        private Vector3 _glideVelocity, _gravityVelocity, _jumpVelocity, _slopeVelocity, _windForces, _forceVelocity;
        private Vector3 _nextParentMove;
        private bool _parentSet = false;
        private Vector3 _jumpDirection = Vector3.up;
        [SerializeField] private float _attackForce = 6f, _attackMoveDistance = 7f;
        private float _fallingWindYForce = 0f;
        [SerializeField] private float _fallingWindYForceLimit;

        [Header("Collisions")]
        private bool _bottomHit = false, _topHit = false, _sidesHit = false;

#endregion

#region Getters and Setters

        // References
        public PlayerSpeedObject Speeds { get { return _speedDictionary; } set { _speedDictionary = value; } }
        public Detection_Ground EnvironmentDetection { get { return _groundDetection; } }
        public CharacterController Controller { get { return _characterController; } }
        public GameObject Character { get { return _characterObject; } set { _characterObject = value; } }
        public LayerMask EnvironmentMask { get { return _environmentLayerMask; } }
        public LayerMask EnemyLayerMask { get { return _enemyLayerMask; } }
        public Transform InteractionTransform { get { return _interactionTransform; } set { _interactionTransform = value; } }


        // Game Objects
        public GameObject ClimbingChildObject { get { return _climbingChildGameObject; } }

        // Transforms
        public Transform ActiveParent { get { return _activeParent; } set { _activeParent = value; } }

        // floats
        public float CurrentSpeed { get { return _speed; } set { _speed = value; } }
        public float EffectiveSpeed { get { return _activeSpeed; } }
        public float CurrentSlopeSpeed { get { return _currentSlopeSpeed; } set { _currentSlopeSpeed = value; } }
        public float SlerpSpeed { get { return _slerpSpeed; } set { _slerpSpeed = value; } }
        public float ParentYMovement { get { return _parentForces._appliedParentMovement.y; } }
        public bool Slope { get { return _onSlope; } set { _onSlope = value; } }
        public bool Slerping { get { return _slerping; } set { _slerping = value; } }
        public bool PreventMovementWhileAttacking { get { return _preventMovement; } set { _preventMovement = value; } }

        // Vector3
        public Vector3 WindForces { get { return _windForces; } set { _windForces = value; } }
        public Vector3 GravityVelocity { get { return _gravityVelocity; } set { _gravityVelocity = value; } }
        public Vector3 JumpVelocity { get { return _jumpVelocity; } set { _jumpVelocity = value; } }
        public Vector3 JumpDirection { get { return _jumpDirection; } set { _jumpDirection = value; } }
        public Vector3 MoveDirection { get { return _moveDirection; } }
        public Vector3 NextParentMove { get { return _nextParentMove; } set { _nextParentMove = value; } }

        // Quaternion
        public Quaternion CharacterRotation { get { return _characterRotation; } set { _characterRotation = value; } }
        public Quaternion CharacterClimbingRotation { set { _characterClimbingRotation = value; } }
        public Vector3 AppliedParentMovement { get { return _parentForces._appliedParentMovement; } }
        public Vector3 ForceVelocity { get { return _forceVelocity; } set { _forceVelocity = value; } }
        public Vector3 ParentVelocity { get { return _platformVelocity; } set { _platformVelocity = value; } }

#endregion

#region Monobehaviour Functions

        private void Awake()
        {   
            _stateMachine = GetComponent<PlayerMachine>();
            _playerGravityScript = GetComponent<PlayerGravity>();
            _surfaceAngleDetection = _detectionScriptObject.GetComponent<Detection_SurfaceAngle>();
            _groundDetection = _detectionScriptObject.GetComponent<Detection_Ground>();
            _parentForces = new();
        }

        private void Start()
        {
            // Climbing Child Creation and Set
            _climbingChildGameObject = new("Climbing Child");
            _climbingChildScript = _climbingChildGameObject.AddComponent<ClimbingChild>();
            _climbingChildScript.SetMovementScript(this);
            PlayerStateMachine.Instance.WallDetect.ClimbingChildObject = _climbingChildGameObject;
            PlayerStateMachine.Instance.WallDetect.ClimbingChildScript = _climbingChildScript;
            PlayerStateMachine.Instance.ClimbingChildObject = _climbingChildGameObject;
            _activeParent = null;
        }

        private void Update()
        {
            if(!_characterController.enabled){return;}
            if(PlayerStateMachine.Instance.Interacting){ _characterController.Move(VectorMath.DirectionVector(transform.position,_interactionTransform.position)); transform.rotation = _interactionTransform.rotation; return; }
            _rvoController.velocity = _characterController.velocity;

            ParentUpdate();

            if(!PlayerMachine.Instance.Input.LeftStickPressed || !_groundDetection.IsGrounded)
            {
                PlayerMachine.Instance.Animator.SetFloat("ArmMove",0);
            }
            else
            {
                PlayerMachine.Instance.Animator.SetFloat("ArmMove",1);
            }

            if(_climbingChildScript == null)
            {
                _climbingChildGameObject = new("Climbing Child");
                _climbingChildScript = _climbingChildGameObject.AddComponent<ClimbingChild>();
                _climbingChildScript.SetMovementScript(this);
                PlayerStateMachine.Instance.WallDetect.ClimbingChildObject = _climbingChildGameObject;
                PlayerStateMachine.Instance.WallDetect.ClimbingChildScript = _climbingChildScript;
                PlayerStateMachine.Instance.ClimbingChildObject = _climbingChildGameObject;
            }

            if(_forceVelocity.y > 0)
            {
                if(_characterController.enabled)
                {
                    _characterController.Move(Time.deltaTime * _forceVelocity.y * Vector3.up);
                }
                _forceVelocity.y -= Time.deltaTime * -9.81f;
            }
            
            if (PlayerStateMachine.Instance.RagdollEnabled) 
            { 
                _characterController.Move(_gravityVelocity * Time.deltaTime);
                if(_groundDetection.IsGrounded)
                {
                   PlayerStateMachine.Instance.RagdollEnabled = false;
                } 
                if(_slerping)
                {
                    ClearSlerp();
                }
                return;
            }

            if(!PlayerStateMachine.Instance.Instance.Climbing)
            {
                _climbingChildScript._activeParent = null;
                transform.rotation = new(0,_characterRotation.y,0,_characterRotation.w);
            }
            else
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, _characterRotation, Time.deltaTime * _climbingRotationSmoothing);
            }
            
            if (PlayerStateMachine.Instance.Climbing)
            {
                if (!PlayerStateMachine.Instance.Input.LeftStickPressed || _surfaceAngleDetection.WallAngle > PlayerStateMachine.Instance.WallDetect.MaximumWallAngle)
                {
                    if (PlayerStateMachine.Instance.Animator.GetFloat("AnimationSpeed") > 0)
                    {
                        float animSpeed = PlayerStateMachine.Instance.Animator.GetFloat("AnimationSpeed");
                        animSpeed -= Time.deltaTime;
                        PlayerStateMachine.Instance.Animator.SetFloat("AnimationSpeed", animSpeed);
                    }
                }
                else
                {
                    if (PlayerStateMachine.Instance.Animator.GetFloat("AnimationSpeed") < 1)
                    {
                        float animSpeed = PlayerStateMachine.Instance.Animator.GetFloat("AnimationSpeed");
                        animSpeed += Time.deltaTime;
                        PlayerStateMachine.Instance.Animator.SetFloat("AnimationSpeed", animSpeed);
                    }
                }
            }
            else
            {
                if (PlayerStateMachine.Instance.Animator.GetFloat("AnimationSpeed") < 1)
                {
                    float animSpeed = PlayerStateMachine.Instance.Animator.GetFloat("AnimationSpeed");
                    animSpeed += Time.deltaTime;
                    PlayerStateMachine.Instance.Animator.SetFloat("AnimationSpeed", animSpeed);
                }
            }

            Vector3 MoveVector = Vector3.zero;

            if(WindMovement(out Vector3 wind))
            {
                MoveVector += wind;
            }

            ForcePower = PlayerMachine.Instance.Animator.GetFloat("ForcePower");
            if(ForcePower > 0)
            {
                Vector3 ForceDirection = ForceDirectionObject.forward;
                float power = ForceAmount * ForcePower;
                MoveVector += (ForceDirection * power);
            }

            if(!PlayerMachine.Instance.DownOnGround)
            {
                SpeedCheck();
                Movement(MoveVector);  
            }
            
            if (!PlayerStateMachine.Instance.WaterLevel)
            {
                _characterObject.transform.rotation = transform.rotation;
            }
        }

        void ParentUpdate()
        {
            if (ActiveParent == null)
            {
                _parentSet = false;
                return; 
            } 
            else
            {
                ParentForcesMovement();
            }
        }

#endregion

#region Speed

        // Sets players active speed based on state machine supplied speed / environment conditions
        void SpeedCheck()
        {
            
            if (PlayerStateMachine.Instance.Input.LeftStickPressed)
            {
                if (_surfaceAngleDetection.WallAngle > PlayerStateMachine.Instance.WallDetect.MaximumSlopeAngle)
                {
                    SetActiveSpeed(Speeds.Climbing);
                }
                else if (_surfaceAngleDetection.WallAngle > PlayerStateMachine.Instance.WallDetect.MidSlopeAngle)
                {
                    SetActiveSpeed(Speeds.SlopeTwo);
                    _activeSpeed *= PlayerStateMachine.Instance.Player.PlayerStats[6].Value;
                }
                else if (_surfaceAngleDetection.WallAngle > PlayerStateMachine.Instance.WallDetect.MinimumSlopeAngle && !PlayerStateMachine.Instance.Sprint)
                {
                    SetActiveSpeed(Speeds.SlopeOne);
                }
                else
                {
                    if(PlayerMachine.Instance.Attacking)
                    {
                        float attackSpeed = _speed * 1.4f;
                        SetActiveSpeed(attackSpeed);
                    }
                    else
                    {
                        if(PlayerMachine.Instance.WaterHeight > 0.4f && !PlayerMachine.Instance.Swimming)
                        {
                            _speed *= 0.6f;
                        }
                        SetActiveSpeed(_speed);
                    }
                }
            }
            else
            {
                SetActiveSpeed(0f);
            }

            if (_activeSpeed > 0)
            {
                PlayerStateMachine.Instance.Animator.SetFloat("Movement", _activeSpeed);
            }
            else
            {
                PlayerStateMachine.Instance.Animator.SetFloat("Movement", 0);
            }    
            
            if(_preventMovement)
            {
                SetActiveSpeed(0f);
            }
        }

        void SetActiveSpeed(float targetSpeed)
        {
            if (targetSpeed > 0)
            {
                if (_activeSpeed != targetSpeed)
                {
                    _activeSpeed = Mathf.Lerp(_activeSpeed, targetSpeed, Time.deltaTime * 8);
                    float slowFloat = PlayerStateMachine.Instance.Animator.GetFloat("Slow");
                    if(slowFloat < 1f)
                    {
                        _activeSpeed *= slowFloat;
                    }
                }
            }
            else
            {
                if(_activeSpeed < 0.05f)
                {
                    _activeSpeed = 0;
                    return;
                }
                _activeSpeed = Mathf.Lerp(_activeSpeed, 0, Time.deltaTime * 8);
            }
        }

#endregion

#region Movement

        // Main movement HUB function 
        void Movement(Vector3 moveVector)
        {
            if(!_characterController.enabled)
            { 
                return;
            }
            if (!PlayerStateMachine.Instance.RagdollEnabled && !PlayerStateMachine.Instance.DownOnGround) 
            {  
                if (PlayerStateMachine.Instance.Gliding)
                {
                    if(!_preventMovement)
                    {
                        moveVector += _glideVelocity;
                        _moveDirection = Vector3.zero;
                    }
                }
                else
                {
                    _glideVelocity = Vector3.zero;
                    if (PlayerStateMachine.Instance.Climbing && !PlayerPower.Instance.Stamina.ClimbingDisabled)
                    {
                        _slopeVelocity = Vector3.zero;
                        if(!_preventMovement)
                        {
                            moveVector += _moveDirection * _activeSpeed;
                        }
                    }
                    else
                    {
                        moveVector += _moveDirection * _activeSpeed;
                        _glideVelocity = Vector3.zero;
                    }
                }        
                  
            }
            
            moveVector += _slopeVelocity;
            moveVector += _gravityVelocity;

            if(!_characterController.enabled)
            {
                _characterController.enabled = true;
            }
            _destinationPoint = transform.position + (moveVector * Time.deltaTime);
            if(newMovePoint.magnitude > 0){ moveVector += newMovePoint;}
            newMovePoint = Vector3.zero;
            _characterController.Move(moveVector * Time.deltaTime);
        }

        public void BowMovement()
        {
            Vector3 mainRotation = _mainCameraTransform.forward;
            mainRotation.y = 0;
            _characterRotation = Quaternion.LookRotation(mainRotation);
            if (_groundDetection.IsGrounded || PlayerStateMachine.Instance.BowHold)
            {
                _moveDirection = (transform.forward * PlayerStateMachine.Instance.Input.LeftStick.y) + (transform.right * PlayerStateMachine.Instance.Input.LeftStick.x);
                _moveDirection.y = 0;
                _moveDirection.Normalize();
            }
        }

        public void GroundMovement()
        {
            if(ForcePower > 0 || PlayerMachine.Instance.DownOnGround){return;}
            if(!PlayerMachine.Instance._bowActive){ GroundedCharacterRotation(); }
            else
            {
                Vector3 mainRotation = _mainCameraTransform.forward;
                mainRotation.y = 0;
                _characterRotation = Quaternion.LookRotation(mainRotation);
            }
            Vector3 input = new (PlayerStateMachine.Instance.Input.LeftStick.x, 0, PlayerStateMachine.Instance.Input.LeftStick.y);
            if (input.sqrMagnitude > 1)
            {
                
                input.Normalize();
            }
            _moveDirection = Quaternion.AngleAxis(_mainCameraTransform.rotation.eulerAngles.y, Vector3.up) * input;
            _moveDirection.Normalize();
        }

        public void SurfaceSwimMovement()
        {
            GroundedCharacterRotation();
            _moveDirection = Quaternion.AngleAxis(_mainCameraTransform.rotation.eulerAngles.y, Vector3.up) * new Vector3(PlayerStateMachine.Instance.Input.LeftStick.x, 0, PlayerStateMachine.Instance.Input.LeftStick.y);
            _moveDirection.Normalize();
            if (PlayerStateMachine.Instance.Input.XButton)
            {
                List<Vector3> movementPoints = new()
            {
                transform.position + ((transform.forward * 7f) + transform.up)
            };
                _climbingChildGameObject.transform.SetPositionAndRotation(transform.position + (Vector3.down * 1.5f), transform.rotation);
                List<Quaternion> rotationPoints = new()
            {
                transform.rotation
            };
                List<float> movementSpeed = new()
            {
                Speeds.SwimSurface * 4
            };
                SetSlerp(movementPoints, rotationPoints, movementSpeed);
            }
        }

        public void SwimMovement()
        {
            SwimCharacterRotation();
            if (PlayerStateMachine.Instance.Input.LeftStickPressed)
            {
                _moveDirection = _characterObject.transform.forward;
            } else
            {
                _moveDirection = Vector3.zero;
            }
        }

        public void JumpMovement()
        {
            if (!_groundDetection.IsGrounded)
            {
                _activeSpeed = _speedDictionary.JumpMovement;
                MoveDirectionForward();
                _characterRotation = Quaternion.LookRotation(transform.forward);
            }
            else
            {
                ResetMoveDirection();
            }
        }

        public void FallingMovement()
        {
            if (!_groundDetection.IsGrounded && PlayerStateMachine.Instance.Input.LeftStickPressed)
            {
                GroundedCharacterRotation();
                _activeSpeed = _speedDictionary.JumpMovement;
                MoveDirectionForward();
            }
            else
            {
                ResetMoveDirection();
            }
        }

        public void GlidingMovement()
        {
            GlidingCharacterRotation();
        }

        public void AttackMovement(){}

        public void ChargedHoldMovement()
        {
            Vector3 raycastPosition = transform.position + (transform.forward * 1.5f) + transform.up;
            if (!Physics.Raycast(raycastPosition, Vector3.down, 2.5f)) { return; }
            if (!PlayerStateMachine.Instance.EnemyDetection.EnemyProximity) { return; }
            if (PlayerStateMachine.Instance.WallAngleDetect.WallAngle < -1) { return; }
            List<Vector3> movementPoints = new()
            {
                transform.position + ((transform.forward * 7f) + transform.up)
            };
            _climbingChildGameObject.transform.SetPositionAndRotation(transform.position + ((transform.forward * 3f) + (transform.up * .7f)), transform.rotation);
            List<Quaternion> rotationPoints = new()
            {
                transform.rotation
            };
            List<float> movementSpeed = new()
            {
                12f
            };
            SetSlerp(movementPoints, rotationPoints, movementSpeed);
        }

        public void ClimbingMovement()
        {
            if(!PlayerMachine.Instance.WallDetect._climbingUp && !PlayerMachine.Instance.WallDetect.IsJumping)
            {
                _moveDirection = Vector3.zero;
                _moveDirection += PlayerStateMachine.Instance.Input.LeftStick.x * transform.right;
                _moveDirection += Vector3.ProjectOnPlane(new(0, _stateMachine.Input.LeftStick.y, 0), -_surfaceAngleDetection.WallNormal); 
                _moveDirection.Normalize();
            }
            else
            {
                if(PlayerMachine.Instance.WallDetect.IsJumping){ return; }
                _moveDirection = Vector3.zero;
                _moveDirection += PlayerStateMachine.Instance.Input.LeftStick.x * transform.right;
                _moveDirection += PlayerStateMachine.Instance.Input.LeftStick.y * transform.forward; 
                _moveDirection.Normalize();
            }
            
        }

        public void LedgeDropMovement()
        {
            Vector3 forwardDirection;
            if(_slopeVelocity.magnitude > 0)
            {
                forwardDirection = _slopeVelocity.normalized;
                forwardDirection.y = 0;
            }
            else
            {
                forwardDirection = transform.forward;
            }
            _climbingChildGameObject.transform.SetPositionAndRotation(transform.position + forwardDirection + (Vector3.down * .25f), Quaternion.LookRotation(forwardDirection));
            List<Vector3> slerpPoints = new() { _climbingChildGameObject.transform.position };
            List<Quaternion> rotation = new() { transform.rotation };
            List<float> slerpSpeeds = new() { 3f };
            PlayerStateMachine.Instance.Movement.SetSlerp(slerpPoints, rotation, slerpSpeeds);
            PlayerStateMachine.Instance.AnimationTrigger("7");
            PlayerStateMachine.Instance.Movement.Slerp();
        }

        public void SlideMovement()
        {
            _characterController.SimpleMove(transform.forward * 6f);
        }

        bool WindMovement(out Vector3 windMove)
        {
            if (PlayerStateMachine.Instance.Gliding)
            {
                if (_windForces.magnitude > 0)
                {
                    windMove = WindForces;
                    return true;
                }
            }
            windMove = Vector3.zero;
            return false;
        }

#endregion

#region Rotation

        void SwimCharacterRotation()
        {
            Vector3 mainRotation = _mainCameraTransform.forward;
            mainRotation.y = 0;
            _characterRotation = Quaternion.LookRotation(mainRotation);
            Quaternion newRotation = Quaternion.LookRotation(_mainCameraTransform.forward + (Vector3.up * 0.2f));
            if(PlayerMachine.Instance.Input.ZLButton){ return ;}
            _characterObject.transform.rotation = Quaternion.Slerp(_characterObject.transform.rotation, newRotation, Time.deltaTime * _swimRotationSpeed);
            
        }

        public void GroundedCharacterRotation()
        {
            if(ForcePower > 0 || PlayerMachine.Instance.DownOnGround){return;}
            float targetAngle = Mathf.Atan2(PlayerStateMachine.Instance.Input.LeftStick.x, PlayerStateMachine.Instance.Input.LeftStick.y) * Mathf.Rad2Deg + _mainCameraTransform.rotation.eulerAngles.y;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _turnSmoothVelocity, _rotationSmoothing);
            if (PlayerStateMachine.Instance.Input.LeftStickPressed)
            {
                _characterRotation = Quaternion.Euler(0f, angle, 0f).normalized;
                float rotation = transform.eulerAngles.y - targetAngle;
                float angleDifference = rotation - _previousPlayerEulerY;
                if (angleDifference > 180f) { angleDifference -= 360f; }
                if (angleDifference < -180f) { angleDifference += 360f; }
                if(angleDifference > 70)
                {
                    PlayerPivotAnimation(0);
                    PlayerStateMachine.Instance.Animator.SetFloat("Rotation",0);
                }
                else if(angleDifference < -70)
                {
                    PlayerPivotAnimation(1);
                    PlayerStateMachine.Instance.Animator.SetFloat("Rotation",0);
                }
                else
                {
                    if(angleDifference > 7 || angleDifference < -7)
                    {
                        _rotation = Mathf.Lerp(_rotation,-angleDifference,Time.deltaTime);
                    }
                    else
                    {
                        _rotation = Mathf.Lerp(_rotation,0,Time.deltaTime * 2);
                    }
                    Mathf.Clamp(_rotation,-5,5);
                    PlayerStateMachine.Instance.Animator.SetFloat("Rotation",_rotation);
                }
            } else
            {
                PlayerStateMachine.Instance.Animator.SetFloat("Rotation",_rotation);
            }
        }

        private void PlayerPivotAnimation(int direction)
        {
            if(direction > 0)
            {
                PlayerMachine.Instance.AnimationTriggerString("PivotRight180");
            }
            else
            {
                PlayerMachine.Instance.AnimationTriggerString("PivotLeft180");
            }
        }
        

        void GlidingCharacterRotation()
        {
            // Gliding Rotation
            float targetAngle = Mathf.Atan2(PlayerStateMachine.Instance.Input.LeftStick.x, PlayerStateMachine.Instance.Input.LeftStick.y) * Mathf.Rad2Deg + _mainCameraTransform.rotation.eulerAngles.y;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _turnSmoothVelocity, _rotationSmoothing);
            if (PlayerStateMachine.Instance.Input.LeftStickPressed)
            {
                float inpt = PlayerStateMachine.Instance.Input.LeftStick.x + 1 * 0.5f;
                float zLean = Mathf.Lerp(25f,-25f,inpt);
                _characterRotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, angle, zLean), Time.deltaTime * _glidingRotationSmoothing);
            }
            else
            {
                if (PlayerStateMachine.Instance.Input.RightStickPressed)
                {
                    _characterRotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, _mainCameraTransform.rotation.eulerAngles.y, 0f), Time.deltaTime * _glidingRotationSmoothing);
                }
            }
            transform.rotation = Quaternion.Lerp(transform.rotation, _characterRotation, Time.deltaTime * _glidingRotationSmoothing);
            Vector3 glideDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            if(PlayerStateMachine.Instance.Input.LeftStickPressed)
            {
                _glideVelocity = _speedDictionary.Gliding * glideDirection.normalized;
            } 
            else
            {
                _glideVelocity = Vector3.zero;
            }
        }

#endregion

#region Target Lock

        public void TargetLockMovement(bool enemyLocked)
        {
            if(ForcePower > 0 || PlayerMachine.Instance.DownOnGround ){return;}
            // Rotate towards target or Camera forwards
            if(enemyLocked && PlayerMachine.Instance.LockedEnemy != null)
            {
                Vector3 lookRot = PlayerStateMachine.Instance.LockedEnemy.transform.position - transform.position;
                lookRot.y = 0;
                _characterRotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookRot), Time.deltaTime * 8);
            }
            else
            {
                Vector3 lookRot = _mainCameraTransform.forward;
                lookRot.y = 0;
                _characterRotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookRot), Time.deltaTime * 8);

            }
            // Reset Jump
            if (PlayerStateMachine.Instance.Jumping && _groundDetection.IsGrounded)
            {
                PlayerStateMachine.Instance.Jumping = false;
            }

            // Forward Jump
            if(PlayerStateMachine.Instance.Input.XButton && !PlayerStateMachine.Instance.Input.RequireNewXPressed && EnvironmentDetection.GroundHeight < PlayerMachine.Instance.LandingTriggerHeight && !_slerping)
            {
                if (PlayerStateMachine.Instance.Input.LeftStick.y > 0.75f)
                {
                    PlayerStateMachine.Instance.AnimationTrigger("26");
                    TargetLockJump();
                }

                // Backflip
                else if (PlayerStateMachine.Instance.Input.LeftStick.y < -0.75f)
                {
                    PlayerStateMachine.Instance.AnimationTrigger("13");
                    TargetLockJump();
                }

                // Side Hop
                else 
                {
                    PlayerStateMachine.Instance.AnimationTrigger("15");
                    TargetLockJump();
                }
            }
            

            // Movement
            else
            {
                if (!_slerping)
                {
                    if (PlayerStateMachine.Instance.Jumping)
                    {
                        _speed = _speedDictionary.JumpMovement;
                    }
                    else
                    {
                        _speed = _speedDictionary.Crouch;
                    }
                    _moveDirection = Vector3.zero;
                    if(PlayerMachine.Instance.Input.LeftStickPressed)
                    {
                        _moveDirection = Quaternion.AngleAxis(_mainCameraTransform.rotation.eulerAngles.y, Vector3.up) * new Vector3(PlayerStateMachine.Instance.Input.LeftStick.x, 0, _stateMachine.Input.LeftStick.y);
                    }
                    _moveDirection.Normalize();
                }
            }
        }

        void TargetLockJump()
        {
            _speed = _speedDictionary.TargetLockJumpMovement;
            PlayerStateMachine.Instance.GroundedDisableOn();
            _playerGravityScript.Jump(PlayerMachine.Instance.Input.LeftStickPressed);
            PlayerStateMachine.Instance.Jumping = true;
            PlayerStateMachine.Instance.Input.RequireNewXPressed = true;
            Invoke(nameof(GroundedDisableOff), 0.25f);
        }

#endregion

#region Slope Forces

        public void SetSlopeSpeed(float newSlopeSpeed)
        {
            _currentSlopeSpeed = newSlopeSpeed;
        }

        public void SlopeForces(Vector3 slopeForce)
        {
            _slopeVelocity = slopeForce;
        }

#endregion

#region Slerping

        public void Slerp()
        {
            _slerping = true;
        }

        // Sets slerp transform points list
        public void SetSlerp(List<Vector3> points, List<Quaternion> rotations, List<float> speeds)
        {
            if(_slerping) { return; }
            SlerpMove slerp = new()
            {
                _slerpPoints = new(),
                _slerpRotations = new(),
                _slerpSpeeds = new()
            };

            int i = 0;
            foreach(Vector3 point in points)
            {
                slerp._slerpPoints.Add(point);
                slerp._slerpRotations.Add(rotations[i]);
                slerp._slerpSpeeds.Add(speeds[i]);
                i++;
            }
            _animationSlerpNormalizedTime = points.Count - 1;
            if (_animationSlerpNormalizedTime < 1) { _animationSlerpNormalizedTime = 1f; }
            _animationSlerp = 0;
            _animationSlerpTime = 0;
            StartCoroutine(SlerpMoveRoutine(slerp));
        }

        private IEnumerator SlerpMoveRoutine(SlerpMove slerp)
        {
            _slerping = true;
            int speedIterator = 0;

            foreach (var endPosition in slerp._slerpPoints)
            {
                slerp._slerpTime = 0f;
                slerp._slerpStartPosition = transform.position;
                ResetMoveDirection();
                UpdateParent();

                if (!_characterController.enabled) break;

                if (speedIterator < slerp._slerpPoints.Count - 1)
                {
                    while (slerp._slerpTime < 1f)
                    {
                        slerp._slerpTime += Time.deltaTime * slerp._slerpSpeeds[speedIterator];
                        Vector3 movePos = Vector3.Slerp(slerp._slerpStartPosition, endPosition, slerp._slerpTime);
                        _characterRotation = Quaternion.Slerp(transform.rotation, slerp._slerpRotations[speedIterator], slerp._slerpTime);
                        if (!_characterController.enabled) break;
                        _characterController.Move(movePos - transform.position);
                        yield return null;
                    }
                    speedIterator++;
                }
                else
                {
                    while (slerp._slerpTime < 1f)
                    {
                        slerp._slerpTime += Time.deltaTime * slerp._slerpSpeeds[speedIterator];
                        Vector3 movePos = Vector3.Slerp(slerp._slerpStartPosition, _climbingChildGameObject.transform.position, slerp._slerpTime);
                        _characterRotation = Quaternion.Slerp(transform.rotation, slerp._slerpRotations[speedIterator], slerp._slerpTime);
                        if (!_characterController.enabled) break;
                        _characterController.Move(movePos - transform.position);
                        yield return null;
                    }
                }
            }
            ClearSlerp();
            _slerping = false;
        }


        public void ClearSlerp()
        {
            if(PlayerMachine.Instance.WallDetect._climbingUp)
            {
                PlayerMachine.Instance.WallDetect.ClimbUpOff();
            }
            else if(PlayerMachine.Instance.WallDetect._jumpingOff)
            {
                PlayerMachine.Instance.WallDetect.JumpingOff = false;
            }
            else if(PlayerMachine.Instance.WallDetect.IsJumping)
            {
                PlayerMachine.Instance.WallDetect.IsJumping = false;
            }

            _slerping = false;
            
            if(ActiveParent != null)
            {
                _characterController.Move(_climbingChildGameObject.transform.position - transform.position);
                _characterRotation = _climbingChildGameObject.transform.rotation;
            }
        }

#endregion

#region Parent Forces

        void ParentForcesMovement()
        {
            Vector3 newGlobalPlatformPoint = _activeParent.TransformPoint(_parentForces._activeLocalParentPoint);
            _parentForces._appliedParentMovement = newGlobalPlatformPoint - _parentForces._activeGlobalParentPoint;
            _characterController.Move(_parentForces._appliedParentMovement);
            Quaternion newGlobalPlatformRotation = _activeParent.rotation * _parentForces._activeLocalParentRotation;
            Quaternion rotationDiff = newGlobalPlatformRotation * Quaternion.Inverse(_parentForces._activeGlobalParentRotation);
            rotationDiff = Quaternion.FromToRotation(rotationDiff * Vector3.up, Vector3.up) * rotationDiff;
            _characterRotation = rotationDiff * _characterRotation; 
            UpdateParent();
        }

        public void UpdateParent()
        {
            if (_activeParent != null)
            {
                _parentForces._activeGlobalParentPoint = transform.position;
                _parentForces._activeLocalParentPoint = _activeParent.InverseTransformPoint(transform.position);
                _parentForces._activeGlobalParentRotation = transform.rotation;
                _parentForces._activeLocalParentRotation = Quaternion.Inverse(_activeParent.rotation) * transform.rotation;
            }
        }

#endregion

#region Reset Functions

        public void ResetMoveDirection()
        {
            _moveDirection = Vector3.zero;
        }

        public void MoveDirectionForward()
        {
            _moveDirection = transform.forward;
        }

        public void ResetSlopeVelocity()
        {
            _slopeVelocity = Vector3.zero;
        }

        // called by target lock jump functions, enables player to get off ground before
        // animator grounded parameter is triggered
        void GroundedDisableOff()
        {
            PlayerStateMachine.Instance.GroundedDisableOff();
        }

        // Called when character capsule detects a collision in the middle of a move
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {

            if ((_characterController.collisionFlags & CollisionFlags.Below) != 0){ _bottomHit = true; } else { _bottomHit = false; }
            if ((_characterController.collisionFlags & CollisionFlags.Above) != 0){ _topHit = true; } else { _topHit = false; }
            if ((_characterController.collisionFlags & CollisionFlags.Sides) != 0){ _sidesHit = true; } else { _sidesHit = false; }
            if(PlayerStateMachine.Instance.Climbing)
            {
                if(_topHit){_characterController.height -= Time.deltaTime; _characterController.radius -= Time.deltaTime; } else { _characterController.height = 2f;}
                if(_sidesHit){ _characterController.radius -= Time.deltaTime; } else { _characterController.radius = .25f;}
            }
            else { _characterController.radius = .25f; _characterController.height = 2f; }
            if(PlayerStateMachine.Instance.Climbing && _characterController.velocity.magnitude > 0 && PlayerStateMachine.Instance.Input.LeftStick.y > 0)
            {
                Vector3 planeNormal = transform.position - hit.point;
                planeNormal.Normalize();
                float distance = Vector3.Distance(hit.point,_destinationPoint);
                Vector3 mov = _destinationPoint - distance * hit.normal;
                mov -= transform.position;
                newMovePoint = mov;
            }
        }

#endregion

    }

    [Serializable]
    public struct SlerpMove
    {
        public float _slerpTime;
        public Vector3 _slerpStartPosition, _slerpEndPosition;
        public Quaternion _slerpRotation;
        public List<Vector3> _slerpPoints;
        public List<Quaternion> _slerpRotations;
        public List<float> _slerpSpeeds;
        public bool _slerpReset, _slerping, _childObjectSlerp;
        public int _slerpCount;
    }

    [Serializable]
    public struct ParentForces
    {
        public Vector3 _appliedParentMovement;
        public Vector3 _activeGlobalParentPoint, _activeLocalParentPoint;
        public Quaternion _activeGlobalParentRotation, _activeLocalParentRotation;
    }
}
