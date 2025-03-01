using System.Collections.Generic;
using UnityEngine;
using RaycastPro.RaySensors;
using Assets.HomeGhost.Core.Static;
using UnityEditorInternal;

namespace Assets.HomeGhost.Core.PlayerStateMachine.Detection
{
    public class Detection_Climbing : MonoBehaviour
    {

#region Variables
        [Header("References")]
        
        private Detection_SurfaceAngle _wallAngleDetection;
        private Detection_LedgeRayCollision _ledgeRayCollider;
        private bool _activeClimbingParent;
        public bool _climbingUp;
        public bool _jumpingOff;
        private Vector3 previousNormal;

        [Header("Raycast Check")]
        [SerializeField] private float _raycastCheckDistanceMax = 5f;

        [Header("Climbing Child Object")]
        private ClimbingChild _climbingChild;
        private GameObject _climbingChildObject;

        [Header("Raycasters")]
        [SerializeField] private ChainRay _wallAngleRay;
        [SerializeField] private BoxRay _rotationRay;
        [SerializeField] private BasicRay _ledgeRay;
        [SerializeField] private BasicRay _overpassRay;
        [SerializeField] private BoxRay _frontBoxRay;
        [SerializeField] private ChainRay _climbingRay;
        [SerializeField] private ChainRay _climbingJumpRay;
        [SerializeField] private ChainRay _wallClimbRay;

        [Header("Climb Jumping")]
        private Quaternion _endJumpRotation;

        [Header("Slerping Bools")]
        private bool _isJumping;
        [SerializeField] private Vector2 _wallJumpOffSpeed = new(1.5f,1.5f);
        [SerializeField] private Vector4 _fullClimbJumpSpeed = new(4f,3f,4f,2f);
        [SerializeField] private Vector2 _climbUpSpeed = new(3f,2f);
        [SerializeField] private Vector2 _jumpingClimbUpSpeed = new(4f,2f);

        [Header("Wall Angle Limits")]
        private readonly float _minimumSlopeAngle = 25f;
        private readonly float _midSlopeAngle = 40f;
        private readonly float _maximumSlopeAngle = 60f;
        private readonly float _maximumWallAngle = 140f;

        [Header("Transform Points")]
        [SerializeField] private Transform _jumpPoint1, _jumpPoint2, _jumpPoint3, _jumpPoint4, _jumpPoint5;
        [SerializeField] private Transform _climbPoint1, _climbPoint2, _climbPoint3;
        private int _exitFrames = 0;

#endregion

#region Getters and Setters
        public GameObject ClimbingChildObject { get { return _climbingChildObject; } set { _climbingChildObject = value; } }
        public ClimbingChild ClimbingChildScript { get { return _climbingChild; } set { _climbingChild = value; } }
        public float MaximumWallAngle { get { return _maximumWallAngle; } }
        public float MaximumSlopeAngle { get { return _maximumSlopeAngle; } }
        public float MinimumSlopeAngle { get { return _minimumSlopeAngle; } }
        public float MidSlopeAngle { get { return _maximumWallAngle; } }
        public bool IsJumping { get { return _isJumping; } set { _isJumping = value; } }
        public bool ActiveClimbingParent { get { return _activeClimbingParent; } set { _activeClimbingParent = value; } }
        public bool JumpingOff { get { return _jumpingOff; } set { _jumpingOff = value; } }
        public bool ClimbingUp { get { return _climbingUp; } }
#endregion

#region Monobehaviour Functions

        // Cache references
        private void Start()
        {
            _wallAngleDetection = PlayerMachine.Instance.WallAngleDetect;
            _ledgeRayCollider = _ledgeRay.GetComponent<Detection_LedgeRayCollision>();
        }

        // if player isn't climbing, detect climbing conditions
        // if player is climbing, monitor surroundings / input for exit conditions
        private void Update()
        {
            if(!PlayerMachine.Instance.Climbing)
            {
                DetectNewClimb();
                if(_exitFrames > 0){ _exitFrames = 0; }
            }
            else
            {
                if(_exitFrames < 5){_exitFrames++;}
                if (PlayerMachine.Instance.RagdollEnabled || PlayerPower.Instance.Stamina.ClimbingDisabled) { PlayerMachine.Instance.Climbing = false; return; }
                if (PlayerMachine.Instance.Movement.Controller.enabled || _climbingUp || _jumpingOff || _isJumping) { return; }
                ClimbingMonitor();
            }
        }
    

        // Detect whether player should enter climbing state based on wall angles in its forward direction
        // Reverts to false if player is exiting climb to prevent re-entry 
        // If conditions are met player enters climbing state 
        // Checks whether object we are climbing is a "Moving Parent" and if we should inherit its movement and relative rotation
        private void DetectNewClimb()
        {
            bool climbingBool;
            if (PlayerMachine.Instance.Movement.EnvironmentDetection.GroundHeight < 3)
            {
                climbingBool = _wallAngleDetection.WallAngle > _maximumSlopeAngle && !_stateMachine.PushingObject && _frontBoxRay.Performed;
            }
            else
            {
                climbingBool = _wallAngleDetection.WallAngle > _maximumSlopeAngle;
            }
            if (_climbingUp || _jumpingOff || PlayerMachine.Instance.Movement.Slerping || _wallAngleDetection.LedgeHeight > 0 && _wallAngleDetection.LedgeHeight < 0.75f || PlayerMachine.Instance.WaterLevel) 
            { 
                climbingBool = false; 
            }
            if (climbingBool && !PlayerMachine.Instance.Climbing)
            {
                InitiateFullClimb();
                ParentCheck(_wallClimbRay.Hit);
                ParentCheck(_wallAngleRay.Hit);
                PlayerMachine.Instance.Climbing = true;
            }
        }

        // This function monitors our climbing raycast detection
        // If a detected hit is at zero, or too far from the player, we discard it 
        private bool RaycastPointCheck(Vector3 hitPoint)
        {
            if (hitPoint != Vector3.zero && Vector3.Distance(PlayerMonitor.PlayerPosition, hitPoint) < _raycastCheckDistanceMax)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

#endregion

#region Moving Parent Check

        // Checks whether the surface we are climbing is on our "Climbing Parent" layer
        // If true, set the objects Transform as our "Active Parent" in PlayerMovement script
        void ParentCheck(RaycastHit hit)
        {
            if(hit.collider && LayerMaskCheck.ClimbingParent(hit.collider.gameObject.layer))
            {
                _activeClimbingParent = true;
                if (PlayerMachine.Instance.Movement.ActiveParent != hit.collider.transform)
                {
                    SetActiveParent(hit.collider.transform);
                }
            }
        }

        // Sets moving parent object as Active Parent in our movement script
        // Updates our Active Parent 
        public void SetActiveParent(Transform parentTransform)
        {
            PlayerMachine.Instance.Movement.ActiveParent = parentTransform;
            PlayerMachine.Instance.Movement.UpdateParent();
        }

        // Clears our Active Parent if we are no longer detecting it via our raycasts
        // Climbing Parent is stored as seperate variable do prevent us from clearing an Active Parent we are standing on after exiting climb
        public void ClearClimbingParent()
        {
            if (_activeClimbingParent)
            {
                PlayerMachine.Instance.Movement.ActiveParent = null;
            }
            _activeClimbingParent = false;
        }
#endregion

#region Trajectory Point Set

        // Sets the trajectory points for our Climbing Jump raycasts based on controller input and player transform
        void SetJumpTrajectory()
        {
            Vector3 inputDirection = new();
            Quaternion endRotation;
            if (PlayerMachine.Instance.Input.LeftStickPressed && _climbingJumpRay.Performed)
            {
                endRotation = Quaternion.LookRotation(-_climbingJumpRay.Normal);
                inputDirection += transform.right * _input.LeftStick.x;
                if (PlayerMachine.Instance.Input.LeftStick.y > -0.5f)
                {
                    inputDirection += transform.up * PlayerMachine.Instance.Input.LeftStick.y;
                }
            } else 
            {
                endRotation = transform.rotation;
                inputDirection += transform.up;
            }
            inputDirection.Normalize();
            
            _jumpPoint1.SetPositionAndRotation(transform.position + transform.up + -transform.forward, transform.rotation);
            _jumpPoint2.SetPositionAndRotation(transform.position + transform.up + -transform.forward + (inputDirection * 4f), Quaternion.Slerp(transform.rotation, endRotation, .25f));
            _jumpPoint3.SetPositionAndRotation(transform.position + transform.up + (inputDirection * 4f) + (transform.forward * 1f), Quaternion.Slerp(transform.rotation, endRotation, .5f));
            _jumpPoint4.SetPositionAndRotation(transform.position + transform.up + (transform.forward * 1f), endRotation);
            _jumpPoint5.SetPositionAndRotation(transform.position + transform.up + (transform.forward * 0.4f), endRotation);
        }

        // Sets the trajectory points for our normal climbing move raycasts based on controller input and player transform
        void SetClimbTrajectory()
        {
            Vector3 inputDirection = new();
            inputDirection += transform.up * _input.LeftStick.y;
            inputDirection += _stateMachine.Input.LeftStick.x * transform.right;
            inputDirection.Normalize();
            _climbPoint1.position = transform.position + transform.up + (inputDirection * 1f);
            _climbPoint2.position = transform.position + transform.up + (inputDirection * 1f) + (transform.forward * 1f);
            _climbPoint3.position = transform.position + transform.up + (transform.forward * 1f) + (-_input.LeftStick.x * 0.5f * transform.right);
        }

#endregion


#region Wall Climbing

        // Initiates a new climbing state based on which of our detection raycasts were triggered
        // Sets interpolation points in movement script positioning us on wall at detection point
        void InitiateFullClimb()
        {
            previousNormal = Vector3.zero;
            if (_frontBoxRay.Performed)
            {
                PlayerMachine.Instance.Climbing = true;
                ParentCheck(_frontBoxRay.Hit);
                Quaternion rotation = Quaternion.LookRotation(-_frontBoxRay.Normal);
                _climbingChild.UpdatePosition(_frontBoxRay.HitPoint, rotation);
                List<Vector3> slerpPoints = new() { _climbingChildObject.transform.position };
                List<float> slerpSpeeds = new() { 4f };
                List<Quaternion> slerpRotation = new() { rotation };
                PlayerMachine.Instance..Movement.SetSlerp(slerpPoints, slerpRotation, slerpSpeeds);
            }
            else if (_rotationRay.Performed && RaycastPointCheck(_rotationRay.HitPoint))
            {
                ParentCheck(_rotationRay.Hit);
                Quaternion rotation = Quaternion.LookRotation(-_rotationRay.Normal);
                _climbingChild.UpdatePosition(_rotationRay.HitPoint, rotation);
                PlayerMachine.Instance.Climbing = true;
                List<Vector3> slerpPoints = new() { _climbingChildObject.transform.position };
                List<float> slerpSpeeds = new() { 4f };
                List<Quaternion> slerpRotation = new() { rotation };
                PlayerMachine.Instance.Movement.SetSlerp(slerpPoints, slerpRotation, slerpSpeeds);
            }
            else if (_wallAngleRay.Performed)
            {
                ParentCheck(_wallAngleRay.Hit);
                Quaternion rotation = Quaternion.LookRotation(-_wallAngleRay.Normal);
                _climbingChild.UpdatePosition(_wallAngleRay.HitPoint, rotation);
                PlayerMachine.Instance.Climbing = true;
                List<Vector3> slerpPoints = new() { _climbingChildObject.transform.position };
                List<float> slerpSpeeds = new() { 4f };
                List<Quaternion> slerpRotation = new() { rotation };
                PlayerMachine.Instance.Movement.SetSlerp(slerpPoints, slerpRotation, slerpSpeeds);

            }

            // If we detect a ledge, climb over ledge rather than entering climbing state
            if(_ledgeRay.Performed && _wallAngleDetection.LedgeHeight < 2 && VectorMath.AngleSigned(DetectionPacket.LedgeHitInfo.Normal, Vector3.up, transform.right) < _maximumSlopeAngle)
            {
                int animInt;
                if(_wallAngleDetection.LedgeHeight < 0.75f){ animInt = 1; }
                else if(_wallAngleDetection.LedgeHeight < 2f){ animInt = 2; }
                else{ animInt = 3;}
                PlayerMachine.Instance.Animator.SetInteger("ClimbUp",animInt);
                PlayerMachine.Instance.AnimationTrigger("2");
            }
            else
            { 
                PlayerMachine.Instance.AnimationTriggerString("EnterClimb"); 
            }
        }

        // Main Hub Function for monitoring climbing conditions and player input
        public void ClimbingMonitor()
        {
            ParentCheck(_rotationRay.Hit);
            ParentCheck(_climbingRay.Hit);
            if(_jumpingOff || _isJumping){ return; }
            SetClimbTrajectory();
            SetJumpTrajectory();
            if(_climbingUp){ return; }
            ClimbingJumpMonitor();
            ClimbingAngleDetect();
            SetClimbingRotation();
        }

        // Monitors our environment raycasts to determine the character rotation based on surface normals
        private void SetClimbingRotation()
        {
            if(_climbingUp){ return; }
            if(_rotationRay.Performed)
            {
                if(Vector3.Dot(-_rotationRay.Hit.Normal,-transform.forward) > 0.5){return;}
                if(Vector3.Dot(-_rotationRay.Hit.Normal,Vector3.up) > 0.5f){return;}
                if(previousNormal == Vector3.zero)
                { 
                    previousNormal = -_rotationRay.Hit.Normal; 
                    PlayerMachine.Instance.Movement.CharacterRotation = Quaternion.LookRotation(-DetectionPacket.ClimbingRotationHitInfo.Normal); 
                    return; 
                }

                Vector3 rotationNormal = Vector3.Lerp(previousNormal,-_rotationRay.Hit.Normal,0.5f);
                rotationNormal.y = 0;
                PlayerMachine.Instance.Movement.CharacterRotation = Quaternion.LookRotation(rotationNormal);
                previousNormal = rotationNormal;
            }
            else if(_climbingRay.Performed)
            {
                if(Vector3.Dot(-_climbingRay.Hit.Normal,-transform.forward) > 0.5){return;}
                if(Vector3.Dot(-_climbingRay.Hit.Normal,Vector3.up) > 0.5f){return;}
                if(previousNormal == Vector3.zero)
                { 
                    previousNormal = -_climbingRay.Hit.Normal;
                    PlayerMachine.Instance.Movement.CharacterRotation = Quaternion.LookRotation(-_climbingRay.Hit.Normal);
                    return;
                }

                Vector3 rotationNormal = Vector3.Lerp(previousNormal,-_climbingRay.Hit.Normal,0.5f);
                rotationNormal.y = 0;
                PlayerMachine.Instance.Movement.CharacterRotation = Quaternion.LookRotation(rotationNormal);
                previousNormal = rotationNormal;
            }
        }

        // Monitors our player input, as well as climbing jump rays to determine a climbing jump
        // If player is holding Left Trigger and hits Jump we exit wall by jumping in left stick relative direction
        // If player is pressing down on left stick and hits jump, we exit wall with a 180 jump
        void ClimbingJumpMonitor()
        {
            // Launch Off Wall
            if(PlayerMachine.Instance.Input.ZLButton)
            {
                if (PlayerMachine.Instance.Input.XButton && !PlayerMachine.Instance.Input.RequireNewXPressed)
                {
                    PlayerMachine.Instance.Input.RequireNewXPressed = true;
                    Vector3 x = (PlayerMachine.Instance.Input.LeftStick.x * transform.right).normalized;
                    Vector3 y = (PlayerMachine.Instance.Input.LeftStick.y * transform.up).normalized;
                    Vector3 jumpDirection = x + y + (-PlayerMonitor.PlayerForward * 0.2f);
                    PlayerMachine.Instance.GravitySet.ClimbingDoubleJump(jumpDirection);
                    _jumpingOff = true;
                    PlayerMachine.Instance.Climbing = false;
                }
            }

            // Wrap Around Wall with jump or 180 jump off wall
            else
            {
                if (PlayerMachine.Instance.Input.XButton && !PlayerMachine.Instance.Input.RequireNewXPressed)
                {
                    if (!PlayerMachine.Instance.Input.LeftStickPressed || PlayerMachine.Instance.Input.LeftStick.y < -0.5f)
                    {
                        ClearClimbingParent();
                        JumpOffWall();
                    }
                    else
                    {
                        if (_climbingJumpRay.Performed && RaycastPointCheck(_climbingJumpRay.Hit.Point))
                        {
                            _climbingChildObject.transform.forward = -_climbingJumpRay.Hit.Normal;
                            Vector3 flatRight = _climbingChildObject.transform.right;
                            flatRight.y = 0;
                            float jumpHitAngle = VectorMath.AngleSigned(_climbingJumpRay.Normal, Vector3.up, flatRight);
                            if (jumpHitAngle < _maximumSlopeAngle)
                            {
                                ClearClimbingParent();
                                JumpingClimbUp();
                            }
                            else
                            {
                                SetClimbingJump();
                            }
                        }
                    }
                }
            }
        }


        // Monitors our climbing raycast detection to determine whether we should be exiting a climb and how
        bool ClimbingAngleDetect()
        {
            Vector3 flatRight = transform.right;
            flatRight.y = 0;

            if(!_wallAngleDetection.WallWasHit && !_isJumping && !_climbingUp && !_jumpingOff){ PlayerMachine.Instance.Climbing = false; }
            // GROUND CHECK EXIT
            if(PlayerMachine.Instance.Movement.EnvironmentDetection.GroundHeight < 2 && PlayerMachine.Instance.Movement.EnvironmentDetection.GroundAngle < _maximumSlopeAngle && _exitFrames > 4)
            {
                if(PlayerMachine.Instance.Input.LeftStick.y < -0.5f)
                {
                    ClearClimbingParent();
                    JumpOffWall();
                    return true;
                }
            }
            // CLIMBING HIT PERFORMED
            if (_climbingRay.Performed && RaycastPointCheck(_climbingRay.Hit.Point) && _exitFrames > 4)
            {
                float hitAngle = VectorMath.AngleSigned(_climbingRay.Hit.Normal, Vector3.up, flatRight);
                if (hitAngle < _maximumSlopeAngle && _wallAngleDetection.WallAngle < _maximumSlopeAngle)
                {
                    if (!_isJumping)
                    {
                        ClearClimbingParent();
                        ClimbingClimbUp(_climbingRay.Hit);
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                return false;
            }
            // NO CLIMBING HIT PERFORMED
            else
            {
                // LEDGE HIT PERFORMED
                if (_ledgeRay.Performed && !_ledgeRayCollider.IsColliding && RaycastPointCheck(_ledgeRay.Hit.Point) && _exitFrames > 4)
                {
                    float ledgeAngle = VectorMath.AngleSigned(_ledgeRay.Hit.Normal, Vector3.up, flatRight);
                    if (ledgeAngle < _maximumSlopeAngle)
                    {
                        if(Physics.OverlapCapsule(_ledgeRay.Hit.Point + Vector3.up * 0.02f,_ledgeRay.Hit.Point + (Vector3.up * 0.5f),0.05f,LayerMaskCheck.EnvironmentMask,QueryTriggerInteraction.Ignore).Length > 0)
                        {
                            return false;
                        }
                        if (_ledgeRay.Hit.Point.y - transform.position.y < 3f && _ledgeRay.Hit.Point.y > transform.position.y)
                        {
                            ClearClimbingParent();
                            ClimbingClimbUp(_ledgeRay.Hit);
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
                // NO LEDGE HIT PERFORMED
                else
                {
                    if(_wallAngleDetection.WallWasHit && _wallAngleDetection.WallAngle < _maximumSlopeAngle && _exitFrames > 4 || PlayerMachine.Instance.Movement.ActiveParent == null && _wallAngleDetection.WallWasHit && _wallAngleDetection.WallAngle < _maximumSlopeAngle && _exitFrames > 4)
                    {
                        _stateMachine.Climbing = false;
                        return true;
                    }
                }
                return false;
            }
        }

#endregion

#region Climbing Exit and Slerping
        public void BoolsOff()
        {
            Invoke(nameof(ClimbingJumpOff), 0.5f);
        }

        void ClimbingJumpOff()
        {
            _isJumping = false;
            _climbingUp = false;
        }

        public void ClimbUpOff()
        {
            PlayerMachine.Instance.Climbing = false;
            _climbingUp = false;
        }

       // Tells our movement script to climb over ledge 
        void ClimbingClimbUp(RaycastHit ledgeHit)
        {
            ParentCheck(ledgeHit);
            Vector3 flatForward = transform.forward;
            flatForward.y = 0;
            Vector3 ledgePoint = transform.position;
            ledgePoint.y = ledgeHit.Point.y;
            Vector3 endPoint = ledgeHit.Point;
            Quaternion rot = Quaternion.LookRotation(VectorMath.DirectionVectorNormalized(transform.position,ledgeHit.point));
            _climbingChild.UpdatePosition(endPoint, rot);
            // Set Slerp Points
            List<Vector3> slerpPoints = new() { ledgePoint, endPoint };
            List<Quaternion> slerpRotation = new() { rot, rot };
            List<float> slerpSpeeds = new() { _climbUpSpeed.x,_climbUpSpeed.y };
            PlayerMachine.Instance.Movement.SetSlerp(slerpPoints, slerpRotation, slerpSpeeds);
            int animInt;
            if(_wallAngleDetection.LedgeHeight < 0.75f){ animInt = 1; }
            else if(_wallAngleDetection.LedgeHeight < 2f){ animInt = 2; }
            else{ animInt = 3;}
            PlayerMachine.Instance.Animator.SetInteger("ClimbUp",animInt);
            PlayerMachine.Instance.AnimationTrigger("2");
            PlayerMachine.Instance.Movement.CharacterRotation = rot;
            _climbingUp = true;
            PlayerMachine.Instance.Climbing = false;
        }

        // Tells our movement script to jump then climb ledge
        void JumpingClimbUp()
        {
            ParentCheck(_climbingJumpRay.Hit);
            Vector3 flatForward = transform.forward;
            flatForward.y = 0;
            Quaternion endRotation = Quaternion.LookRotation(transform.forward);
            Vector3 ledgePoint = transform.position;
            ledgePoint.y = _climbingJumpRay.HitPoint.y + 0.2f;
            Vector3 endPoint = ledgePoint + (transform.forward * 0.2f);
            _climbingChild.UpdatePosition(endPoint, endRotation);

            // Set Slerp Points
            List<Vector3> slerpPoints = new() {ledgePoint, _climbingChildObject.transform.position };
            List<float> slerpSpeeds = new() { _jumpingClimbUpSpeed.x, _jumpingClimbUpSpeed.y };
            List<Quaternion> slerpRotation = new() { transform.rotation, transform.rotation };
            PlayerMachine.Instance.Movement.SetSlerp(slerpPoints, slerpRotation, slerpSpeeds);
            PlayerMachine.Instance.AnimationTrigger("10");
            _climbingUp = true;
            PlayerMachine.Instance.Climbing = false;
            
        }

        // Tells our movement script to jump off wall
        public void JumpOffWall()
        {
            Quaternion endRotation = Quaternion.LookRotation(-transform.forward);
            Vector3 jumpPointOne = transform.position + (-transform.forward * 1.5f);
            Vector3 endPoint = transform.position + (-transform.forward * 2.5f);
            _climbingChild.UpdatePosition(endPoint, endRotation);

            // Set Slerp Points
            List<Vector3> slerpPoints = new() { jumpPointOne, endPoint };
            List<float> slerpSpeeds = new() { _wallJumpOffSpeed.x, _wallJumpOffSpeed.y };
            List<Quaternion> slerpRotation = new() { Quaternion.Slerp(transform.rotation,endRotation,0.5f), endRotation };
            PlayerMachine.Instance.Movement.SetSlerp(slerpPoints, slerpRotation, slerpSpeeds);
            if(PlayerMachine.Instance.Movement.EnvironmentDetection.GroundHeight > 2)
            {
                PlayerMachine.Instance.AnimationTrigger("6");
            }
            ClearClimbingParent();
            _jumpingOff = true;
            PlayerMachine.Instance.Climbing = false;
        }

        // Tells our movement script to drop off wall when we run out of climbing stamina
        public void JumpOffWallStamina()
        {
            
            Quaternion endRotation = Quaternion.LookRotation(-transform.forward);
            Vector3 jumpPointOne = transform.position + (-transform.forward * 3f);
            Vector3 endPoint = transform.position + (-transform.forward * 4f);
            _climbingChild.UpdatePosition(endPoint, endRotation);

            // Set Slerp Points
            List<Vector3> slerpPoints = new() { jumpPointOne, endPoint };
            List<float> slerpSpeeds = new() { _wallJumpOffSpeed.x, _wallJumpOffSpeed.y };
            List<Quaternion> slerpRotation = new() { Quaternion.Slerp(transform.rotation,endRotation,0.5f), endRotation };
            PlayerMachine.Instance.Movement.SetSlerp(slerpPoints, slerpRotation, slerpSpeeds);
            PlayerMachine.Instance.AnimationTrigger("6");
            PlayerMachine.Instance.Climbing = false;
            ClearClimbingParent();
        }
#endregion

#region Climbing Jump

        // Detects the chain ray index of where the Climbing Jump ray hit, and then adds jump points 
        // to a list before slerping the player between them
        private void SetClimbingJump()
        {
            // Parent check and index set
            if(!_climbingJumpRay.Performed){return;}
            ParentCheck(_climbingJumpRay.Hit);
            int index = _climbingJumpRay.DetectIndex;

            // Set Slerp Points
            Vector3 endRotationVector = -_climbingJumpRay.Normal;
            endRotationVector.y = 0;
            _endJumpRotation = Quaternion.LookRotation(endRotationVector);
            _climbingChild.UpdatePosition(_climbingJumpRay.HitPoint + (-_climbingJumpRay.Normal * 0.2f), _endJumpRotation);
            List<Vector3> slerpPoints = new();
            List<Quaternion> slerpRotations = new();
            List<float> slerpSpeeds = new();

            Vector3 centerOrigin = Vector3.Lerp(transform.position,_climbingJumpRay.Hit.point,0.5f);

            // POINT ONE
            Ray ray = new(_jumpPoint2.position,VectorMath.DirectionVectorNormalized(_jumpPoint2.position,centerOrigin));
            if(Physics.Raycast(ray,out RaycastHit hit))
            {
                slerpPoints.Add(hit.point + (hit.normal * 0.5f));
            }
            else
            {
                slerpPoints.Add(_jumpPoint1.position);
            }
            slerpRotations.Add(Quaternion.Slerp(transform.rotation, _endJumpRotation, 0.25f));
            slerpSpeeds.Add(_fullClimbJumpSpeed.x);

            // POINT TWO
            if (index > 1)
            {
                ray = new(_jumpPoint2.position,VectorMath.DirectionVectorNormalized(_jumpPoint2.position,centerOrigin));
                if(Physics.Raycast(ray,out hit))
                {
                    slerpPoints.Add(hit.point + (hit.normal * 0.5f));
                }
                else
                {
                    slerpPoints.Add(_jumpPoint2.position);
                }
                slerpRotations.Add(Quaternion.Slerp(transform.rotation, _endJumpRotation, 0.25f));
                slerpSpeeds.Add(_fullClimbJumpSpeed.y);
            }

            // POINT THREE
            if (index > 2)
            {
                ray = new(_jumpPoint3.position,VectorMath.DirectionVectorNormalized(_jumpPoint3.position,centerOrigin));
                if(Physics.Raycast(ray,out hit))
                {
                    slerpPoints.Add(hit.point + (hit.normal * 0.5f));
                }
                else
                {
                    slerpPoints.Add(_jumpPoint3.position);
                }
                slerpRotations.Add(Quaternion.Slerp(transform.rotation, _endJumpRotation, 0.5f));
                slerpSpeeds.Add(_fullClimbJumpSpeed.z);
            }

            // POINT FOUR
            if (index > 3)
            {
                ray = new(_jumpPoint4.position,VectorMath.DirectionVectorNormalized(_jumpPoint4.position,centerOrigin));
                if(Physics.Raycast(ray,out hit))
                {
                    slerpPoints.Add(hit.point + (hit.normal * 0.5f));
                }
                else
                {
                    slerpPoints.Add(_jumpPoint4.position);
                }
                slerpRotations.Add(Quaternion.Slerp(transform.rotation, _endJumpRotation, 0.75f));
                slerpSpeeds.Add(_fullClimbJumpSpeed.w);
            }

            // Slerp Set 
            PlayerMachine.Instance.Movement.SetSlerp(slerpPoints, slerpRotations, slerpSpeeds);
            _isJumping = true;
            PlayerMachine.Instance.AnimationTrigger("10");
        }

#endregion
    }
}