using Assets.HomeGhost.Core.Static;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Assets.HomeGhost.Core.PlayerStateMachine
{
    public class FootIK : MonoBehaviour
    {

#region Variables
        private static FootIK _instance;
        public static FootIK Instance { get { return _instance; } set { _instance = value; } }

        [Header("Rigging References")]
        public Transform HipTarget;
        public Transform LeftFootFlamingoTarget, RightFootFlamingoTarget;
        public MultiPositionConstraint HipRigComponent;
        public float HipYOffset;
        public float FootYOffset = 0.1f;
        public float FootYLimiter = 0.5f;
        public float OutwardFootIdleAngle = 15f;

        private Animator _anim;
        private Transform _leftFootTransform, _rightFootTransform, _hipTransform;
        private Vector3 _leftFootPosition, _rightFootPosition;
        private bool _inIdle, _leftFootFlamingo, _rightFootFlamingo;
        private float _leftFootWeight, _rightFootWeight;
        private bool _inFlamingo = false;


#endregion

#region Monobehaviour Functions

        private void Awake()
        {
            if(_instance == null){ _instance = this; }
            else{ Destroy(this); }
        }
        private void Start()
        { 
            _anim = PlayerMachine.Instance.Animator; 
            _leftFootTransform = _anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            _rightFootTransform = _anim.GetBoneTransform(HumanBodyBones.RightFoot);
            _hipTransform = _anim.GetBoneTransform(HumanBodyBones.Hips);
        }

        private void Update()
        {
            for(int i = 1; i < PlayerMachine.Instance.Animator.layerCount; i++)
            {
                if(PlayerMachine.Instance.Animator.GetLayerWeight(i) > 0)
                {
                    LeftFootReset(); RightFootReset();
                    return;
                }
            }
            if(PlayerMachine.Instance.Dead){ LeftFootReset(); RightFootReset(); return; }
            FlamingoAnimation();
            _inIdle = !PlayerMachine.Instance.Interacting && !PlayerMachine.Instance.WallDetect._climbingUp && PlayerMachine.Instance.Movement.EnvironmentDetection.IsGrounded && !PlayerMachine.Instance.Input.LeftStickPressed && !PlayerMachine.Instance.Climbing && !PlayerMachine.Instance.Attacking && !PlayerMachine.Instance.DownOnGround;
            HipIK();
        }

        private void OnAnimatorIK(int layer)
        {
            LeftFootIK();
            RightFootIK();
        }

#endregion

#region Helper Functions

        private void FlamingoAnimation()
        {
            if(_leftFootFlamingo || _rightFootFlamingo)
            { 
                if(PlayerMachine.Instance.Input.LeftStickPressed || !PlayerMachine.Instance.Movement.EnvironmentDetection.IsGrounded){ _leftFootFlamingo = false; _rightFootFlamingo = false; return; }
                if(!_inFlamingo)
                {
                    _inFlamingo = true;
                    if(_leftFootFlamingo){ PlayerMachine.Instance.Animator.SetFloat("Flamingo",-1); }
                    else{ PlayerMachine.Instance.Animator.SetFloat("Flamingo",1); }
                }
            }
            else
            { 
                if(_inFlamingo)
                {
                    _inFlamingo = false;
                    PlayerMachine.Instance.Animator.SetFloat("Flamingo",0);
                }
            }
        }

        private void HipIK()
        {
            if(!_inIdle || PlayerMachine.Instance.LockedOnEnemy || _inFlamingo)
            {
                HipTarget.SetPositionAndRotation(_hipTransform.position,_hipTransform.rotation);
                if(HipRigComponent.weight > 0)
                { 
                    HipRigComponent.weight = 0; 
                }
                return;
            }

            float hipYPosition = _leftFootTransform.position.y <= _rightFootTransform.position.y ? _leftFootTransform.position.y + HipYOffset : _rightFootTransform.position.y + HipYOffset;
            if(hipYPosition < transform.position.y)
            { 
                if(HipRigComponent.weight > 0)
                { 
                    HipRigComponent.weight = 0; 
                }
                return; 
            }
            Vector3 hipTargetPosition = Vector3.Lerp(_hipTransform.position,Vector3.Lerp(_leftFootTransform.position,_rightFootTransform.position,0.5f),Time.deltaTime * 8);

            hipTargetPosition.y = hipYPosition;
            HipTarget.SetPositionAndRotation(hipTargetPosition,_hipTransform.rotation);
            if(HipRigComponent.weight < 1){ HipRigComponent.weight = 1; }
        }

        private void LeftFootIK()
        {
            if(_inFlamingo){  return; }
            if(_inIdle)
            {
                Ray leftFootRay = new(_leftFootTransform.position + (0.2f * Vector3.up),Vector3.down);
                if(Physics.SphereCast(leftFootRay,0.1f,out RaycastHit leftFootHit,float.MaxValue,LayerMaskCheck.EnvironmentMask,QueryTriggerInteraction.Ignore))
                {
                    if(leftFootHit.point.y < transform.position.y - FootYLimiter)
                    {
                        if(!_rightFootFlamingo)
                        {
                            _leftFootFlamingo = true;
                        }
                    }
                    else
                    {
                        if(!_rightFootFlamingo)
                        {
                            _leftFootFlamingo = false;
                            _leftFootPosition = leftFootHit.point + (leftFootHit.normal * FootYOffset);
                            _anim.SetIKPosition(AvatarIKGoal.LeftFoot,_leftFootPosition);
                            Vector3 footForward = _anim.transform.forward;
                            Quaternion footYawOffset = Quaternion.Euler(0, -OutwardFootIdleAngle, 0);
                            Vector3 adjustedForward = footYawOffset * footForward;
                            Vector3 projectedForward = Vector3.ProjectOnPlane(adjustedForward, leftFootHit.normal);
                            Quaternion targetRotation = Quaternion.LookRotation(projectedForward, leftFootHit.normal);
                            _anim.SetIKRotation(AvatarIKGoal.LeftFoot,targetRotation);
                            if(_leftFootWeight < 1){ _leftFootWeight += Time.deltaTime; }
                            _anim.SetIKPositionWeight(AvatarIKGoal.LeftFoot,_leftFootWeight);
                            _anim.SetIKRotationWeight(AvatarIKGoal.LeftFoot,_leftFootWeight);
                        }
                    }
                } 
                else 
                { 
                   LeftFootReset();
                }
            }
            else 
            { 
                LeftFootReset();
            }
        }

        private void LeftFootReset()
        {
            if(_leftFootWeight == 0 && _leftFootFlamingo == false){ return; }
            _leftFootFlamingo = false;
            _leftFootWeight = 0;
            _anim.SetIKPositionWeight(AvatarIKGoal.LeftFoot,0);
            _anim.SetIKRotationWeight(AvatarIKGoal.LeftFoot,0);
        }

        private void RightFootIK()
        {
            if(_inFlamingo){ return; }
            if(_inIdle)
            {
                Ray rightFootRay = new(_rightFootTransform.position + (0.2f * Vector3.up),Vector3.down);
                
                if(Physics.SphereCast(rightFootRay,.1f,out RaycastHit rightFootHit,float.MaxValue,LayerMaskCheck.EnvironmentMask,QueryTriggerInteraction.Ignore))
                {
                    if(rightFootHit.point.y < transform.position.y - FootYLimiter)
                    {
                        if(!_leftFootFlamingo)
                        {
                            _rightFootFlamingo = true;
                        }
                    }
                    else
                    {
                        if(!_leftFootFlamingo)
                        {
                            _rightFootFlamingo = false;
                            _rightFootPosition = rightFootHit.point + (rightFootHit.normal * FootYOffset);
                            _anim.SetIKPosition(AvatarIKGoal.RightFoot,_rightFootPosition);
                            Vector3 footForward = _anim.transform.forward;
                            Quaternion footYawOffset = Quaternion.Euler(0, OutwardFootIdleAngle, 0);
                            Vector3 adjustedForward = footYawOffset * footForward;
                            Vector3 projectedForward = Vector3.ProjectOnPlane(adjustedForward, rightFootHit.normal);
                            Quaternion targetRotation = Quaternion.LookRotation(projectedForward, rightFootHit.normal);
                            _anim.SetIKRotation(AvatarIKGoal.RightFoot,targetRotation);
                            if(_rightFootWeight < 1){ _rightFootWeight += Time.deltaTime; }
                            _anim.SetIKPositionWeight(AvatarIKGoal.RightFoot,_rightFootWeight);
                            _anim.SetIKRotationWeight(AvatarIKGoal.RightFoot,_rightFootWeight);
                        }
                    }
                } 
                else 
                { 
                    RightFootReset();
                }
            }
            else 
            { 
                RightFootReset();
            }
        }

        private void RightFootReset()
        {
            if(_rightFootWeight == 0 && _rightFootFlamingo == false){ return; }
            _rightFootFlamingo = false;
            _rightFootWeight = 0;
            _anim.SetIKPositionWeight(AvatarIKGoal.RightFoot,0);
            _anim.SetIKRotationWeight(AvatarIKGoal.RightFoot,0);
        }

#endregion

    }   
}

