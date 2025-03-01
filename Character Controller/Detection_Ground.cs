using UnityEngine;
using Assets.HomeGhost.Core.Static;

namespace Assets.HomeGhost.Core.PlayerStateMachine.Detection
{
    public class Detection_Ground : MonoBehaviour
    {

#region Variables

        [Header("References")]
        [SerializeField] private PlayerMovement _movement;
        [SerializeField] private PlayerMachine _stateMachine;

        [Header("Grounded Variables")]
        [SerializeField] private bool _isGrounded;
        [SerializeField] private float _groundedHeight = 0.05f;
        private float _groundAngle;
        private float _groundHeight;
        private Vector3 _groundHitNormal;

        [Header("Falling Variables")]
        [SerializeField] private float _startFallTimerHeight = 3f;
        [SerializeField] private float _fallDamageOne, _fallDamageTwo, _fallDamageThree;
        private float _fallTimer = 0;
        private float _fallTimerMonitor = 0;
        
        [Header("Slope Sliding Variables")]
        [SerializeField] float _slopeSlowingMultiplier = 7f;
        [SerializeField] float _slopeSpeedIncreaseMultiplier = 2f;
        [SerializeField] float _slopeOneSlideForce = -1f;
        [SerializeField] float _slopeTwoSlideForce = -3f;

#endregion

#region Getters And Setters

        public bool IsGrounded { get { return _isGrounded; } set { _isGrounded = value; } }
        public float GroundHeight { get { return _groundHeight; } }
        public float GroundAngle { get { return _groundAngle; } }
        public Vector3 GroundNormal { get { return _groundHitNormal; } }
        public float FallTimer { get { return _fallTimer; } set { _fallTimer = value; } }
        public float FallMonitor { get { return _fallTimerMonitor; } set { _fallTimerMonitor = value; } }

#endregion

#region Monobehaviour Functions

        private void Update()
        {
            GroundCheck();
            _isGrounded = _movement._characterController.isGrounded && !_stateMachine.GroundedDisable || _groundHeight <= _groundedHeight && !_stateMachine.GroundedDisable;

            if (!_stateMachine.Climbing || _isGrounded)
            {
                Invoke(nameof(ClearClimbingParent), 0.3f);
            }

            if (_isGrounded)
            {
                _groundHeight = 0;
                DetectGroundAngle();
                SlopeForceReduction();
            }
            else
            {
                if(_fallTimerMonitor < _fallTimer){ _fallTimerMonitor = _fallTimer; }
                if (_stateMachine.WallAngleDetect.WallWasHit)
                {
                    _groundAngle = _stateMachine.WallAngleDetect.WallAngle;
                }
                else
                {
                    _groundAngle = 0f;
                }
                if (!_stateMachine.WallDetect.ActiveClimbingParent && !_stateMachine.Climbing)
                {
                    _movement.ActiveParent = null;
                }
                _stateMachine.SlopeSlideVelocity = Vector3.zero;
                _stateMachine.Movement.ResetSlopeVelocity();
            }
        }

#endregion

#region Ground Check and Height

        private void GroundCheck()
        {
            if(Physics.SphereCast(transform.position + Vector3.up,0.25f,Vector3.down,out RaycastHit groundHit,float.MaxValue,LayerMaskCheck.EnvironmentMask,QueryTriggerInteraction.Ignore))
            {
                _groundHeight = groundHit.distance - 1f;
                _groundHitNormal = groundHit.normal;
                ParentCheck(groundHit);
                Vector3 flatRight = transform.right;
                flatRight.y = 0;
                _groundAngle = VectorMath.AngleSigned(groundHit.normal,Vector3.up,flatRight);
            }
            GroundHeightSet();
        }

        void GroundHeightSet()
        {
            if(_stateMachine.Climbing && _stateMachine.Gliding && _stateMachine.Jumping)
            {
                _groundHeight = 0;
                _fallTimer = 0;
            }
        }

#endregion

#region Ground Angle and Slope Forces

        void DetectGroundAngle()
        {
            _stateMachine.Animator.SetFloat("GroundAngle",_groundAngle);
            if (_groundAngle <= 80f && _groundAngle > 0 || PlayerPower.Instance.Stamina.ClimbingDisabled)
            {
                if (_groundAngle >= 40f)
                {
                    if (SlopeDirectionCheck(out float slope1))
                    {

                        _movement.Slope = true;
                        float newSpeed = Mathf.Lerp(_movement.CurrentSlopeSpeed, _movement.Speeds.SlopeTwo, Time.deltaTime * _slopeSpeedIncreaseMultiplier);
                        _movement.SetSlopeSpeed(newSpeed);
                        SlopeSlideForces(false);
                    }
                }
                else if (_groundAngle >= 60f)
                {
                    if (SlopeDirectionCheck(out float slope2))
                    {
                        _movement.Slope = true;
                        float newSpeed = Mathf.Lerp(_movement.CurrentSlopeSpeed, _movement.Speeds.SlopeOne, Time.deltaTime * _slopeSpeedIncreaseMultiplier);
                        _movement.SetSlopeSpeed(newSpeed);
                        SlopeSlideForces(true);
                    }
                }
                else
                {
                    _movement.Slope = false;
                    _movement.CurrentSlopeSpeed = _movement.Speeds.SlopeOne;
                    SlopeForceReduction();
                }
            }
            else
            {
                if(_groundAngle < -40f)
                {
                    if (!SlopeDirectionCheck(out float slope))
                    {
                        _movement.Slope = true;
                        float newSpeed = _movement.Speeds.Run;
                        if(_stateMachine.Input.LeftStickPressed)
                        {
                            newSpeed = Mathf.Lerp(_movement.Speeds.Run, _movement.Speeds.Run * 1.2f, Time.deltaTime);
                        }
                        _movement.SetSlopeSpeed(newSpeed);
                        _stateMachine.SlopeSlideVelocity = Vector3.ProjectOnPlane(new Vector3(0, _slopeOneSlideForce, 0), _groundHitNormal);
                    }
                }
                else if(_groundAngle < -60f)
                {
                        _movement.Slope = true;
                        float newSpeed = _movement.Speeds.Run;
                        if(_stateMachine.Input.LeftStickPressed)
                        {
                            newSpeed = Mathf.Lerp(_movement.Speeds.Run, _movement.Speeds.Run * 1.5f, Time.deltaTime);
                        }
                        _movement.SetSlopeSpeed(newSpeed);
                        _stateMachine.SlopeSlideVelocity = Vector3.ProjectOnPlane(new Vector3(0, _slopeTwoSlideForce, 0), _groundHitNormal);
                }
                else
                {
                    _movement.Slope = false;
                    SlopeForceReduction();
                }
            }
        }

        

        bool SlopeDirectionCheck(out float dot)
        {
            Vector3 slopeDirection = Vector3.ProjectOnPlane(new Vector3(0, 1, 0), _groundHitNormal);
            float slopeDot = Vector3.Dot(transform.forward, slopeDirection);
            if (slopeDot < 0.25)
            {
                dot = slopeDot;
                return false;
            }
            else
            {
                dot = slopeDot;
                return true;
            }
        }

        public void SlopeSlideForces(bool slopeOne)
        {
            if (slopeOne)
            {
                _stateMachine.SlopeSlideVelocity = Vector3.ProjectOnPlane(new Vector3(0, _slopeOneSlideForce, 0), _groundHitNormal);
            }
            else
            {
                _stateMachine.SlopeSlideVelocity = Vector3.ProjectOnPlane(new Vector3(0, _slopeTwoSlideForce, 0), _groundHitNormal);
            }
        }

        void SlopeForceReduction()
        {
            _stateMachine.SlopeSlideVelocity -= _slopeSlowingMultiplier * Time.fixedDeltaTime * _stateMachine.SlopeSlideVelocity;
            if (_stateMachine.SlopeSlideVelocity.magnitude < 0.1f)
            {
                _stateMachine.SlopeSlideVelocity = Vector3.zero;
                _stateMachine.Movement.ResetSlopeVelocity();
            }
        }

        #endregion

        #region Parent Forces

        void ParentCheck(RaycastHit hitInfo)
        {
            if (LayerMaskCheck.ClimbingParent(hitInfo.collider.gameObject.layer))
            {
                if (_stateMachine.Movement.ActiveParent != hitInfo.collider.gameObject.transform)
                {
                    SetActiveParent(hitInfo.collider.gameObject.transform);
                }
            }
            else
            {
                if (_movement.ActiveParent != null && !_stateMachine.WallDetect.ActiveClimbingParent)
                {
                    _movement.ActiveParent = null;
                }
            }
        }

        public void SetActiveParent(Transform parentTransform)
        {
            if (LayerMaskCheck.ClimbingParent(parentTransform.gameObject.layer))
            {
                _movement.ActiveParent = parentTransform;
                _movement.UpdateParent();
            }
        }

        void ClearClimbingParent()
        {
            _stateMachine.WallDetect.ClearClimbingParent();
        }
    }

#endregion

}