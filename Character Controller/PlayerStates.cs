using System.Collections.Generic;
using ALabs_Character_Stats;
using Assets.HomeGhost.Core.Audio;
using Assets.HomeGhost.Core.Enemy.EnemyStateMachine;
using Assets.HomeGhost.Core.PlayerStateMachine.Combat;
using Assets.HomeGhost.Core.Static;
using UnityEngine;

namespace Assets.HomeGhost.Core.PlayerStateMachine
{
    
#region Locomotion

    // Grounded
    public class PlayerState_Grounded_Root : BaseState
    {
        public PlayerState_Grounded_Root(PlayerMachine currentContext, Factory playerStateFactory) : base(currentContext, playerStateFactory)
        {
            IsRootState = true;
            InitializeSubState();
        }
        private bool _ragdoll;
        

        public override void EnterState()
        {
            if (StateMachine.Input.CaptureButton)
            {
                StateMachine.Input.RequireNewCapturePressed = true;
            }
            StateMachine.Jumping = false;
            StateMachine.DownOnGround = false;
            StateMachine.DoubleJumping = false;
            StateMachine.GravitySet.ResetLateralGravity();
        }

        public override void UpdateState()
        {
            CheckSwitchStates();
            GroundMovement();
            ActiveWeapon();
            Attack();
            
            if (StateMachine.Input.ZLButton)
            {
                StateMachine.LockedOnEnemy = true;
            }
            else
            {
                StateMachine.LockedOnEnemy = false;
            }

            if(StateMachine.RagdollEnabled)
            {
                SwitchState(StateFactory.FallingRoot());
                return;
            }

            // ROLLING
            if(StateMachine.Input.AButton && StateMachine.Input.LeftStickPressed && !StateMachine.Input.RequireNewAPressed)
            {
                StateMachine.Input.RequireNewAPressed = true;
                Vector3 movePos = PlayerMonitor.PlayerPosition + (PlayerMonitor.PlayerForward * 7f);
                List<Vector3> positions = new(){movePos};
                List<float> speeds = new(){StateMachine.Movement.Speeds.Rolling};
                List<Quaternion> rotations = new(){StateMachine.transform.rotation};
                StateMachine.Movement.ClimbingChildObject.transform.position = movePos;
                StateMachine.Movement.SetSlerp(positions,rotations,speeds);  
                StateMachine.AnimationTrigger("41");
                PlayerAudioBridge.Instance.Roll();
            }

            if(PlayerPower.Instance.Stamina.ClimbingDisabled)
            {
                StateMachine.Movement.Controller.slopeLimit = 50f;
            }
            else
            {
                StateMachine.Movement.Controller.slopeLimit = 180f;
            }

            if(StateMachine.Input.PlusButton && !StateMachine.Input.RequireNewPlusPressed)
            {
                StateMachine.Input.RequireNewPlusPressed = true;
                StateMachine.AnimationTriggerString("Scan");
            }
        }

        void Attack()
        {
            if (!StateMachine.PreventAttack && StateMachine.Input.YButton && !StateMachine.Input.RequireNewYPressed)
            {
                if(PlayerPower.Instance.RequestPower(PlayerWeaponStateMachine.Instance.CurrentWeapon))
                {
                    PlayerPower.Instance.UseAttachmentPower(PlayerWeaponStateMachine.Instance.CurrentWeapon);
                    StateMachine.Input.RequireNewYPressed = true;
                    EnemyCoreMachine.Instance.InitializePlayerAttack(PlayerWeaponStateMachine.Instance.CurrentWeapon);
                    StateMachine.Movement.AttackMovement();
                    PlayerAudioBridge.Instance.SwingWeapon();
                    StateMachine.AnimationTriggerString("AttackTrigger");
                }
                else
                {
                    StateMachine.Input.RequireNewYPressed = true;
                }
            }
        }

        private void GroundMovement()
        {
            if (!StateMachine.DownOnGround)
            {
                StateMachine.Movement.GroundMovement();
            }
            else
            {
                StateMachine.Movement.ResetMoveDirection();
            }
        }

        private void ActiveWeapon()
        {
            if(StateMachine.Input.RequireNewDPadPressed){return;}
            if(StateMachine.Input.DPad.y > 0.5f)
            {
                StateMachine.Input.RequireNewDPadPressed = true;
                PlayerWeaponStateMachine.Instance.SwitchAttachment(2);
            }
            else if(StateMachine.Input.DPad.x < -0.5f && !StateMachine.Input.RequireNewDPadPressed)
            {
                StateMachine.Input.RequireNewDPadPressed = true;
                PlayerWeaponStateMachine.Instance.SwitchAttachment(3);
            }
            else if(StateMachine.Input.DPad.x > 0.5f && !StateMachine.Input.RequireNewDPadPressed)
            {
                StateMachine.Input.RequireNewDPadPressed = true;
                PlayerWeaponStateMachine.Instance.SwitchAttachment(1);
            }
            else if(StateMachine.Input.DPad.y < -0.5f)
            {
                StateMachine.Input.RequireNewDPadPressed = true;
                PlayerWeaponStateMachine.Instance.SwitchAttachment(0);
            }
        }
        
        public override void ExitState()
        {
            StateMachine.Movement.GravityVelocity = Vector3.zero;
            StateMachine.Movement.Controller.slopeLimit = 180f;
        }

        public override void CheckSwitchStates()
        {
            if(StateMachine.Dead)
            {
                SwitchState(StateFactory.Dead());
            }
            else if (!StateMachine.Movement.EnvironmentDetection.IsGrounded && !StateMachine.Jumping && StateMachine.Movement.EnvironmentDetection.GroundHeight > 0.4f && !StateMachine.WallDetect._climbingUp)
            {
                StateMachine.AnimationTrigger("7");
                SwitchState(StateFactory.FallingRoot());
            }
            else if (_ragdoll)
            {
                StateMachine.RagdollEnabled = true;
                SwitchState(StateFactory.FallingRoot());
            }
            else if (!StateMachine.DownOnGround && StateMachine.Input.XButton && !StateMachine.Jumping && !StateMachine.Input.RequireNewXPressed && !StateMachine.Climbing && !StateMachine.LockedOnEnemy)
            {
                SwitchState(StateFactory.Jump());
            }
            else if (StateMachine.Climbing && !PlayerPower.Instance.Stamina.ClimbingDisabled)
            {
                SwitchState(StateFactory.ClimbingRoot());
            }
            else if (StateMachine.Water)
            {
                SwitchState(StateFactory.Water());
            }
            else if (StateMachine.LockedOnEnemy)
            {
                SwitchState(StateFactory.TargetLocked());
            }
            else if (StateMachine.Input.ZRButton && !StateMachine.Input.RequireNewZRPressed && PlayerPower.Instance.RequestPower(4))
            {
                StateMachine.Input.RequireNewZRPressed = true;
                SwitchState(StateFactory.BowRoot());
            }
        }

        public override void InitializeSubState()
        {
            if (StateMachine.Input.LeftStickPressed && !StateMachine.Input.BButton)
            {
                SetSubState(StateFactory.Run());
            }
            else if (StateMachine.Input.LeftStickPressed && StateMachine.Input.BButton && !PlayerPower.Instance.Stamina.SprintingDisabled)
            {
                SetSubState(StateFactory.Sprint());
            }
            // else if (StateMachine.Input.LeftStickButton && !StateMachine.Input.RequireNewLeftStickPressed)
            // {
            //     SetSubState(StateFactory.Crouch());
            // }
            else if (!StateMachine.Input.LeftStickPressed)
            {
                SetSubState(StateFactory.Idle());
            }
            // else if (StateMachine.LockedOnObject && StateMachine.PlayerStatsManager._playerStatApproval[ALabs_Character_Stats.PlayerStatType.StrengthStamina])
            // {
            //    SetSubState(StateFactory.Interact());
            // }
        }
        public override void FixedUpdateState(){}
    }

    public class PlayerState_Idle : BaseState
    {
        public PlayerState_Idle(PlayerMachine currentContext, Factory playerStateFactory) : base(currentContext, playerStateFactory) {}
        public override void EnterState()
        {
            if (StateMachine.Input.CaptureButton)
            {
                StateMachine.Input.RequireNewCapturePressed = true;
            }
        }
        public override void UpdateState()
        {
            CheckSwitchStates();
        }
        public override void CheckSwitchStates()
        {
            // if (StateMachine.Input.LeftStickButton && !StateMachine.Input.RequireNewLeftStickPressed)
            // {
            //     StateMachine.Input.RequireNewLeftStickPressed = true;
            //     SwitchState(StateFactory.Crouch());
            // }
            if (StateMachine.Input.BButton && StateMachine.Input.LeftStickPressed && StateMachine.PlayerStatsManager._playerStatApproval[ALabs_Character_Stats.PlayerStatType.SprintingStamina])
            {
                SwitchState(StateFactory.Sprint());
            }
            else if (StateMachine.Input.LeftStickPressed)
            {
                SwitchState(StateFactory.Run());
            }
            // else if (StateMachine.LockedOnObject && StateMachine.PlayerStatsManager._playerStatApproval[ALabs_Character_Stats.PlayerStatType.StrengthStamina])
            // {
            //    SetSubState(StateFactory.Interact());
            // }
        }

        public override void InitializeSubState(){}
        public override void FixedUpdateState(){}
        public override void ExitState(){}
        
    }

    public class PlayerState_Crouch : BaseState
    {
        public PlayerState_Crouch(PlayerMachine currentContext, Factory playerStateFactory) : base(currentContext, playerStateFactory){}
        public override void EnterState()
        {
            StateMachine.Animator.SetBool("Crouch", true);
        }

        public override void UpdateState()
        {
            CheckSwitchStates();
            if (StateMachine.Movement.CurrentSpeed != StateMachine.Player.PlayerStats[7].Value * 0.75f)
            {
                StateMachine.Movement.CurrentSpeed = StateMachine.Player.PlayerStats[7].Value * 0.75f;
            }
            
        }
        public override void ExitState()
        {

            StateMachine.Animator.SetBool("Crouch", false);
            if (StateMachine.Input.LeftStickButton)
            {
                StateMachine.Input.RequireNewLeftStickPressed = true;
            }
        }
        public override void CheckSwitchStates()
        {
            if (StateMachine.Input.LeftStickButton && !StateMachine.Input.LeftStickPressed && !StateMachine.Input.RequireNewLeftStickPressed)
            {
                SwitchState(StateFactory.Idle());
            }
            else if (StateMachine.Input.LeftStickButton && StateMachine.Input.LeftStickPressed && !StateMachine.Input.RequireNewLeftStickPressed)
            {
                SwitchState(StateFactory.Run());
            }
            else if (StateMachine.Input.BButton && StateMachine.Input.LeftStickPressed && StateMachine.PlayerStatsManager._playerStatApproval[PlayerStatType.SprintingStamina])
            {
                SwitchState(StateFactory.Sprint());
            }
            // else if (StateMachine.LockedOnObject && StateMachine.PlayerStatsManager._playerStatApproval[PlayerStatType.StrengthStamina])
            // {
            //    SetSubState(StateFactory.Interact());
            // }
        }
        public override void InitializeSubState(){}
        public override void FixedUpdateState(){}
    }

    public class PlayerState_Walk : BaseState
    {
        public PlayerState_Walk(PlayerMachine currentContext, Factory playerStateFactory) : base(currentContext, playerStateFactory) { }


        public override void EnterState()
        {
        }

        public override void UpdateState()
        {
            CheckSwitchStates();
        }

        public override void FixedUpdateState()
        {
            if (StateMachine.Input.LeftStickPressed)
            {
                StateMachine.Movement.CurrentSpeed = StateMachine.Movement.Speeds.Walk;
            }
            
        }

        public override void ExitState()
        {

        }

        public override void CheckSwitchStates()
        {
            // If Left Stick Input is less than .5f and more than .1f enter walk state
            if (StateMachine.Input.LeftStick.x > 0.5f || StateMachine.Input.LeftStick.y > 0.5f)
            {
                SwitchState(StateFactory.Run());
            }

            // If Left Stick Input is less than 0.1f enter grounded state
            if (StateMachine.Input.LeftStick.x < 0.1f && StateMachine.Input.LeftStick.y < 0.1f)
            {
                SwitchState(StateFactory.GroundedRoot());
            }

            // If Left Stick Input is greater than .5f B Button is Pressed enter Sprint State
            if (StateMachine.Input.LeftStick.x > 0.5f && StateMachine.Input.BButton  && !PlayerPower.Instance.Stamina.SprintingDisabled|| StateMachine.Input.LeftStick.y > 0.5f && StateMachine.Input.BButton && !PlayerPower.Instance.Stamina.SprintingDisabled)
            {
                SwitchState(StateFactory.Sprint());
            }

            // if not grounded and not jumping
            if (!StateMachine.Movement.EnvironmentDetection.IsGrounded && !StateMachine.Jumping)
            {
                SwitchState(StateFactory.FallingRoot());
            }

            // If X Button pressed and Jump Enabled in Movement script
            if (StateMachine.Input.XButton && StateMachine.JumpReady)
            {
                SwitchState(StateFactory.Jump());
            }
        }

        public override void InitializeSubState()
        {

        }
    }

    public class PlayerState_Run : BaseState
    {
        public PlayerState_Run(PlayerMachine currentContext, Factory playerStateFactory) : base(currentContext, playerStateFactory) { }

        public override void UpdateState()
        {
            CheckSwitchStates();

            if (PlayerPower.Instance.Stamina.SprintingDisabled)
            {
                if(StateMachine.Movement.CurrentSpeed != StateMachine.Movement.Speeds.Run * 0.75f)
                {
                    StateMachine.Movement.CurrentSpeed = StateMachine.Movement.Speeds.Run * 0.75f;
                }
            }
            else
            {
                if (StateMachine.Input.LeftStick.y > 0.75f || StateMachine.Input.LeftStick.y < -0.75f || StateMachine.Input.LeftStick.x > 0.75f || StateMachine.Input.LeftStick.x < -0.75f)
                {
                    if(StateMachine.Movement.CurrentSpeed != StateMachine.Movement.Speeds.Run)
                    {
                        StateMachine.Movement.CurrentSpeed = StateMachine.Movement.Speeds.Run;
                    }
                } else
                {
                    StateMachine.Movement.CurrentSpeed = StateMachine.Movement.Speeds.Walk;
                }
            }
            
            
        }

        public override void CheckSwitchStates()
        {
            if (!StateMachine.Input.LeftStickPressed)
            {
                SwitchState(StateFactory.Idle());
            }
            // else if (StateMachine.Input.LeftStickButton && !StateMachine.Input.RequireNewLeftStickPressed)
            // {
            //     StateMachine.Input.RequireNewLeftStickPressed = true;
            //     SwitchState(StateFactory.Crouch());
            // }
            else if (StateMachine.Input.BButton && StateMachine.Input.LeftStickPressed && !PlayerPower.Instance.Stamina.SprintingDisabled)
            {
                SwitchState(StateFactory.Sprint());
            }
            // else if (StateMachine.LockedOnObject && StateMachine.PlayerStatsManager._playerStatApproval[PlayerStatType.StrengthStamina])
            // {
            //    SetSubState(StateFactory.Interact());
            // }
        }

        public override void InitializeSubState(){}
        public override void EnterState(){}
        public override void FixedUpdateState(){}
        public override void ExitState(){}
    }

    public class PlayerState_Sprint : BaseState
    {
        private bool _exitSprint = false;
        public PlayerState_Sprint(PlayerMachine currentContext, Factory playerStateFactory) : base(currentContext, playerStateFactory) {}

        public override void EnterState()
        {
            // StateMachine.PlayerStatsManager.ReduceStatByTime(10);
            StateMachine.Movement.CurrentSpeed = StateMachine.Movement.Speeds.Sprint;
            // PlayerVFXControl.Instance.Dash();
            if(PlayerPower.Instance.Stamina.SprintingDisabled){ _exitSprint = true; CheckSwitchStates();}
        }
        public override void UpdateState()
        {
            if(PlayerPower.Instance.Stamina.SprintingDisabled){ _exitSprint = true;}
            CheckSwitchStates();
            
            if(StateMachine.Movement.CurrentSpeed != StateMachine.Movement.Speeds.Sprint)
            {
                StateMachine.Movement.CurrentSpeed = StateMachine.Movement.Speeds.Sprint;
            }
            
        }
        public override void ExitState()
        {
            // StateMachine.PlayerStatsManager.ReplenishStatByTime(10);
            // PlayerVFXControl.Instance.DashEnd();
        }

        public override void CheckSwitchStates()
        {
            if (!StateMachine.Input.LeftStickPressed)
            {
                SwitchState(StateFactory.Idle());
            }
            else if (!StateMachine.Input.BButton || _exitSprint || PlayerPower.Instance.Stamina.SprintingDisabled)
            {
                SwitchState(StateFactory.Run());
            }
            // else if (StateMachine.Input.LeftStickButton && !StateMachine.Input.RequireNewLeftStickPressed)
            // {
            //     SwitchState(StateFactory.Crouch());
            // }
            // else if (StateMachine.LockedOnObject && StateMachine.PlayerStatsManager._playerStatApproval[PlayerStatType.StrengthStamina])
            // {
            //    SetSubState(StateFactory.Interact());
            // }
        }
        public override void InitializeSubState(){}
        public override void FixedUpdateState(){}
    }

    public class PlayerState_SlopeSliding : BaseState
    {
        public PlayerState_SlopeSliding(PlayerMachine currentContext, Factory playerStateFactory) : base(currentContext, playerStateFactory)
        {
            IsRootState = true;
            InitializeSubState();
        }
        public override void EnterState()
        {
            StateMachine.RagdollEnabled = true;
            StateMachine.Climbing = false;
            StateMachine.PlayerStatsManager.ReplenishStatByTime(9);
        }
        public override void UpdateState()
        {
            CheckSwitchStates();
        }
        public override void CheckSwitchStates()
        {
            if (!StateMachine.RagdollEnabled)
            {
                SwitchState(StateFactory.GroundedRoot());
            } 
        }
        public override void InitializeSubState()
        {
            SetSubState(StateFactory.Empty());
        }
        public override void FixedUpdateState(){}
        public override void ExitState(){}
    }

    // Climbing
    public class PlayerState_Climbing_Root : BaseState
    {
        private bool _exitState = false; 
        private float _climbingAttackTimer = 0;
        public PlayerState_Climbing_Root(PlayerMachine currentContext, Factory playerStateFactory) : base(currentContext, playerStateFactory)
        {
            IsRootState = true;
            InitializeSubState();
        }
        public override void EnterState()
        {
            if(PlayerPower.Instance.Stamina.ClimbingDisabled){ _exitState = true; return; }
            if(!PlayerPower.Instance.Stamina.RequestPowerTime(1)){ _exitState = true; return; }
            PlayerWeaponStateMachine.Instance.SwitchAttachment(5);
            PlayerPower.Instance.StaminaInUse[1] = true;
            StateMachine.DoubleJumping = false;
        }

        public override void UpdateState()
        {
            if(_exitState){ CheckSwitchStates(); return;}
            else
            {
                if(StateMachine.Input.LeftStickPressed)
                {
                    if(!PlayerPower.Instance.Stamina.RequestPowerTime(1))
                    {
                        _exitState = true;
                    }
                }
            }
            if (!StateMachine.WallDetect.IsJumping && !StateMachine.Movement.Slerping) //&& !StateMachine.Input.ZLButton)
            {
                StateMachine.Movement.ClimbingMovement();
            }
            else
            {
                // if(StateMachine.Input.ZLButton && !StateMachine.Attacking)
                // {
                //     _climbingAttackTimer += Time.deltaTime;
                //     if(StateMachine.Input.YButton && !StateMachine.Input.RequireNewYPressed)
                //     {
                //         StateMachine.Input.RequireNewYPressed = true;
                //         ClimbingWhipAttack();
                //     }
                // }
                StateMachine.Movement.ResetMoveDirection();
            }
            CheckSwitchStates();
        }

        private void ClimbingWhipAttack()
        {
            PlayerWeaponStateMachine.Instance.ClimbingWhipPlay(); 
        }
        public override void ExitState()
        {
            if (StateMachine.Input.XButton)
            {
                StateMachine.Input.RequireNewXPressed = true;
            }
            PlayerWeaponStateMachine.Instance.SwitchAttachment();
            PlayerPower.Instance.StaminaInUse[1] = false;
        }

        public override void CheckSwitchStates()
        {
            if(StateMachine.Dead)
            {
                SwitchState(StateFactory.Dead());
            }
            else if(StateMachine.RagdollEnabled)
            {
                StateMachine.Animator.SetFloat("WallAngle",0);
                SwitchState(StateFactory.FallingRoot());
            }
            else if (_exitState || !StateMachine.Climbing && !StateMachine.WallDetect.IsJumping && !StateMachine.WallDetect._climbingUp || !StateMachine.WallAngleDetect.WallWasHit)
            {
                if (StateMachine.Movement.EnvironmentDetection.IsGrounded)
                {
                    SwitchState(StateFactory.GroundedRoot());
                }
                else
                {
                    SwitchState(StateFactory.FallingRoot());
                }
            } 
        }
        public override void InitializeSubState()
        {
            SetSubState(StateFactory.Empty());
        }
        public override void FixedUpdateState(){}
    }

    // Off Ground

    public class PlayerState_Falling_Root : BaseState
    {
        public PlayerState_Falling_Root(PlayerMachine currentContext, Factory playerStateFactory) : base(currentContext, playerStateFactory)
        {
            IsRootState = true;
            InitializeSubState();
        }

        private float _fallingWindYForce = 0f;
        private float _fallingWindYForceLimit = 1f;
        private bool _inFreeFall = false;

        public override void EnterState()
        {
            StateMachine.Movement.EnvironmentDetection.FallTimer = 0;
            StateMachine.Animator.SetBool("Falling", true);
            if (StateMachine.Input.AButton)
            {
                StateMachine.Input.RequireNewAPressed = true;
            }
            if (StateMachine.Input.XButton)
            {
                StateMachine.Input.RequireNewXPressed = true;
            }
            StateMachine.LandingSet = false;
            StateMachine.LandingTrigger = false;
            StateMachine.Jumping = false;
        }

        public override void UpdateState()
        {
            CheckSwitchStates();
            StateMachine.Movement.EnvironmentDetection.FallTimer += Time.deltaTime;
            StateMachine.Movement.FallingMovement();
            StateMachine.FallingAnimation();
            StateMachine.LandingAnimation();
            ActiveWeapon();
            Attack();

            if(!_inFreeFall)
            {
                _fallingWindYForce = 0f;
                if(StateMachine.Input.RButton && !StateMachine.Input.RequireNewRPressed)
                {
                    StateMachine.Input.RequireNewRPressed = true;
                    _inFreeFall = true;
                    StateMachine.Animator.SetBool("FreeFall",true);
                }
            }
            else
            {
                if(_fallingWindYForce < _fallingWindYForceLimit)
                {
                    _fallingWindYForce += Time.deltaTime;
                }
                StateMachine.Movement.WindForces += _fallingWindYForce * Vector3.up;
                if(StateMachine.Movement.EnvironmentDetection.GroundHeight < 4 && StateMachine.Input.AButton && !StateMachine.Input.RequireNewAPressed)
                {
                    Ray ray = new(PlayerMonitor.PlayerPosition + (PlayerMonitor.PlayerForward * 6f),Vector3.down);
                    if(Physics.SphereCast(ray,0.25f,out RaycastHit hit,4f,LayerMaskCheck.EnvironmentMask,QueryTriggerInteraction.Ignore))
                    {
                        StateMachine.Movement.ClimbingChildObject.transform.SetPositionAndRotation(hit.point,StateMachine.transform.rotation);
                        List<Vector3> points = new(){ hit.point };
                        List<Quaternion> rotations = new(){ StateMachine.transform.rotation };
                        List<float> speeds = new(){ StateMachine.Movement.EnvironmentDetection.FallMonitor };
                        StateMachine.Input.RequireNewAPressed = true;
                        StateMachine.Movement.EnvironmentDetection.FallMonitor = 0;
                        StateMachine.Movement.EnvironmentDetection.FallTimer = 0;
                        StateMachine.AnimationTriggerString("FallingBoost");
                        StateMachine.Animator.SetBool("FreeFall",false);
                        StateMachine.Movement.SetSlerp(points,rotations,speeds);
                        StateMachine.Falling = false;
                    }
                }
            }

            
        }

        private void JumpAttackMove()
        {
            Ray ray = new(PlayerMonitor.PlayerPosition + (PlayerMonitor.PlayerForward * 4f),Vector3.down);
            if(Physics.SphereCast(ray,0.3f,out RaycastHit hit,5f,StateMachine.Movement.EnvironmentMask))
            {
                Vector3 movePos = hit.point;
                Vector3 posOne = new(movePos.x,StateMachine.transform.position.y,movePos.z);
                List<Vector3> positions = new(){posOne,movePos};
                List<float> speeds = new(){StateMachine.Movement.Speeds.JumpAttackMovement * 1.3f ,StateMachine.Movement.Speeds.JumpAttackMovement};
                List<Quaternion> rotations = new(){StateMachine.transform.rotation, StateMachine.transform.rotation};
                StateMachine.Movement.ClimbingChildObject.transform.position = movePos;
                StateMachine.Movement.SetSlerp(positions,rotations,speeds); 
            }
        }

        void Attack()
        {
            if (!StateMachine.PreventAttack && StateMachine.Input.YButton && !StateMachine.Input.RequireNewYPressed)
            {
                if(PlayerPower.Instance.RequestPower(PlayerWeaponStateMachine.Instance.CurrentWeapon))
                {
                    JumpAttackMove();
                    PlayerPower.Instance.UseAttachmentPower(PlayerWeaponStateMachine.Instance.CurrentWeapon);
                    StateMachine.Input.RequireNewYPressed = true;
                    EnemyCoreMachine.Instance.InitializePlayerAttack(PlayerWeaponStateMachine.Instance.CurrentWeapon);
                    StateMachine.Movement.AttackMovement();
                    StateMachine.AnimationTriggerString("AttackTrigger");
                }
                else
                {
                    StateMachine.Input.RequireNewYPressed = true;
                    // StateMachine.AnimationTriggerString("NoPower");
                }
            }
        }

        private void ActiveWeapon()
        {
            if(StateMachine.Input.RequireNewDPadPressed){return;}
            if(StateMachine.Input.DPad.y > 0.5f)
            {
                StateMachine.Input.RequireNewDPadPressed = true;
                PlayerWeaponStateMachine.Instance.SwitchAttachment(2);
            }
            else if(StateMachine.Input.DPad.x < -0.5f && !StateMachine.Input.RequireNewDPadPressed)
            {
                StateMachine.Input.RequireNewDPadPressed = true;
                PlayerWeaponStateMachine.Instance.SwitchAttachment(3);
            }
            else if(StateMachine.Input.DPad.x > 0.5f && !StateMachine.Input.RequireNewDPadPressed)
            {
                StateMachine.Input.RequireNewDPadPressed = true;
                PlayerWeaponStateMachine.Instance.SwitchAttachment(1);
            }
            else if(StateMachine.Input.DPad.y < -0.5f)
            {
                StateMachine.Input.RequireNewDPadPressed = true;
                PlayerWeaponStateMachine.Instance.SwitchAttachment(0);
            }
        }

        public override void FixedUpdateState()
        {

        }

        public override void ExitState()
        {
            StateMachine.Movement.WindForces = Vector3.zero;
            PlayerAudioBridge.Instance.Footstep();
            StateMachine.Falling = false;
            if(StateMachine.RagdollEnabled){ StateMachine.RagdollEnabled = false;}
            StateMachine.Animator.SetBool("Falling", false);
            StateMachine.Animator.SetFloat("FallingFloat", -1f);
            if(StateMachine.Movement.EnvironmentDetection.IsGrounded && StateMachine.Movement.EnvironmentDetection.FallMonitor >= 1)
            {
               StateMachine.Animator.SetInteger("LandingInt", Mathf.RoundToInt(StateMachine.Movement.EnvironmentDetection.FallMonitor * 2f)); 
            }
            StateMachine.Movement.EnvironmentDetection.FallMonitor = 0;
            StateMachine.Movement.EnvironmentDetection.FallTimer = 0;
            StateMachine.Movement.ResetMoveDirection();
        }

        public override void CheckSwitchStates()
        {
            if(StateMachine.Dead)
            {
                SwitchState(StateFactory.Dead());
            }
            else if (StateMachine.Climbing && !PlayerPower.Instance.Stamina.ClimbingDisabled)
            {
                StateMachine.LandingTrigger = false;
                SwitchState(StateFactory.ClimbingRoot());
            }
            else if (StateMachine.Movement.EnvironmentDetection.IsGrounded)
            {
                SwitchState(StateFactory.GroundedRoot());
            }
            else if (StateMachine.PlayerHasGlider && StateMachine.Input.XButton && StateMachine.Movement.EnvironmentDetection.GroundHeight >= StateMachine.GliderOpenHeight && !StateMachine.Input.RequireNewXPressed && !PlayerPower.Instance.Stamina.GlidingDisabled) 
            {
                StateMachine.LandingTrigger = false;
                SwitchState(StateFactory.GlidingRoot());
            }
            else if (StateMachine.Water)
            {
                SwitchState(StateFactory.Water());
            }
            else if (StateMachine.Input.ZRButton && PlayerPower.Instance.RequestPower(4))
            {
                SwitchState(StateFactory.BowRoot());
            }
        }

        public override void InitializeSubState()
        {
            SetSubState(StateFactory.Empty());
        }
    }

    public class PlayerState_Jump_Root : BaseState
    {
        bool _movingJump = false;

        public PlayerState_Jump_Root(PlayerMachine currentContext, Factory playerStateFactory) : base(currentContext, playerStateFactory)
        {
            IsRootState = true;
            InitializeSubState();
        }

        public override void EnterState()
        {
            if (StateMachine.Input.XButton)
            {
                StateMachine.Input.RequireNewXPressed = true;
            }
            if (StateMachine.Input.LeftStickPressed)
            {
                _movingJump = true;
            }
            // StateMachine.AnimationTrigger("1");
            StateMachine.Animator.SetBool("Jumping",true);
            if (StateMachine.Input.XButton)
            {
                StateMachine.Input.RequireNewXPressed = true;
            }
            StateMachine.JumpYPosition = StateMachine.transform.position.y;
            
            StateMachine.GroundedDisableOn();
            StateMachine.Invoke(nameof(StateMachine.GroundedDisableOff), 0.3f);
            
            StateMachine.GravitySet.Jump(_movingJump);
            StateMachine.Jumping = true;
            StateMachine.GliderReset();
            StateMachine.Movement.ActiveParent = null;
            PlayerAudioBridge.Instance.Footstep();
        }

        public override void UpdateState()
        {
            CheckSwitchStates();
            if (_movingJump)
            {
                StateMachine.Movement.JumpMovement();
            }
            
            if (StateMachine.Movement.EnvironmentDetection.GroundHeight > StateMachine.StartFallHeight || StateMachine.transform.position.y < StateMachine.JumpYPosition)
            {
                StateMachine.Falling = true;
                CheckSwitchStates();
            } 
            // if (!StateMachine.PreventAttack && PlayerInventory.Instance.CurrentWeapon > 1 && StateMachine.Input.YButton && !StateMachine.Input.RequireNewYPressed)
            // {
            //     StateMachine.Input.RequireNewYPressed = true;
            //     StateMachine.AnimationTrigger("16");
            // }
            if(StateMachine.Input.DoubleJump && !StateMachine.Input.RequireNewDoubleJump && !StateMachine.DoubleJumping)
            {
                StateMachine.Input.RequireNewDoubleJump = true;
                StateMachine.AnimationTriggerString("DoubleJump");
            }
            ActiveWeapon();
            Attack();
        }

        private void JumpAttackMove()
        {
            Ray ray = new(PlayerMonitor.PlayerPosition + (PlayerMonitor.PlayerForward * 4f),Vector3.down);
            if(Physics.SphereCast(ray,0.3f,out RaycastHit hit,5f,StateMachine.Movement.EnvironmentMask))
            {
                Vector3 movePos = hit.point;
                Vector3 posOne = new(movePos.x,StateMachine.transform.position.y,movePos.z);
                List<Vector3> positions = new(){posOne,movePos};
                List<float> speeds = new(){StateMachine.Movement.Speeds.JumpAttackMovement * 1.3f ,StateMachine.Movement.Speeds.JumpAttackMovement};
                List<Quaternion> rotations = new(){StateMachine.transform.rotation, StateMachine.transform.rotation};
                StateMachine.Movement.ClimbingChildObject.transform.position = movePos;
                StateMachine.Movement.SetSlerp(positions,rotations,speeds); 
            }
        }

        void Attack()
        {
            if (!StateMachine.PreventAttack && StateMachine.Input.YButton && !StateMachine.Input.RequireNewYPressed)
            {
                if(PlayerPower.Instance.RequestPower(PlayerWeaponStateMachine.Instance.CurrentWeapon))
                {
                    JumpAttackMove();
                    PlayerPower.Instance.UseAttachmentPower(PlayerWeaponStateMachine.Instance.CurrentWeapon);
                    StateMachine.Input.RequireNewYPressed = true;
                    EnemyCoreMachine.Instance.InitializePlayerAttack(PlayerWeaponStateMachine.Instance.CurrentWeapon);
                    StateMachine.Movement.AttackMovement();
                    StateMachine.AnimationTriggerString("AttackTrigger");
                }
                else
                {
                    StateMachine.Input.RequireNewYPressed = true;
                    // StateMachine.AnimationTriggerString("NoPower");
                }
            }
        }

        private void ActiveWeapon()
        {
            if(StateMachine.Input.RequireNewDPadPressed){return;}
            if(StateMachine.Input.DPad.y > 0.5f)
            {
                StateMachine.Input.RequireNewDPadPressed = true;
                PlayerWeaponStateMachine.Instance.SwitchAttachment(2);
            }
            else if(StateMachine.Input.DPad.x < -0.5f && !StateMachine.Input.RequireNewDPadPressed)
            {
                StateMachine.Input.RequireNewDPadPressed = true;
                PlayerWeaponStateMachine.Instance.SwitchAttachment(3);
            }
            else if(StateMachine.Input.DPad.x > 0.5f && !StateMachine.Input.RequireNewDPadPressed)
            {
                StateMachine.Input.RequireNewDPadPressed = true;
                PlayerWeaponStateMachine.Instance.SwitchAttachment(1);
            }
            else if(StateMachine.Input.DPad.y < -0.5f)
            {
                StateMachine.Input.RequireNewDPadPressed = true;
                PlayerWeaponStateMachine.Instance.SwitchAttachment(0);
            }
        }


        public override void FixedUpdateState(){ }

        public override void ExitState()
        {
            StateMachine.Movement.ResetMoveDirection();
            StateMachine.Jumping = false;
        }

        public override void CheckSwitchStates()
        {
            if(StateMachine.Dead)
            {
                SwitchState(StateFactory.Dead());
            }
            else if (StateMachine.Movement.EnvironmentDetection.IsGrounded)
            {
                PlayerAudioBridge.Instance.Footstep();
                SwitchState(StateFactory.GroundedRoot());
            }
            else if (StateMachine.Climbing && !PlayerPower.Instance.Stamina.ClimbingDisabled)
            {
                SwitchState(StateFactory.ClimbingRoot());
            }
            else if (StateMachine.Falling)
            {
                SwitchState(StateFactory.FallingRoot());
            }
            else if (StateMachine.PlayerHasGlider && StateMachine.Input.XButton && StateMachine.Movement.EnvironmentDetection.GroundHeight >= StateMachine.GliderOpenHeight && !StateMachine.Input.RequireNewXPressed && !PlayerPower.Instance.Stamina.GlidingDisabled) 
            {
                SwitchState(StateFactory.GlidingRoot());
            }
            else if (StateMachine.Water)
            {
                SwitchState(StateFactory.Water());
            }
            else if (StateMachine.Input.ZRButton && PlayerPower.Instance.RequestPower(4))
            {
                SwitchState(StateFactory.BowRoot());
            }

        }

        public override void InitializeSubState()
        {
            SetSubState(StateFactory.Empty());
        }
    }

    public class PlayerState_Gliding_Root : BaseState
    {
        private bool _exitGlide = false;
        public PlayerState_Gliding_Root(PlayerMachine currentContext, Factory playerStateFactory) : base(currentContext, playerStateFactory)
        {
            IsRootState = true;
            InitializeSubState();
        }
        public override void InitializeSubState()
        {
            SetSubState(StateFactory.Empty());
        }
        public override void EnterState()
        {
            if(!PlayerPower.Instance.Stamina.RequestPowerTime(2)){ _exitGlide = true; return; }
            PlayerWeaponStateMachine.Instance.SwitchAttachment(6);
            PlayerPower.Instance.StaminaInUse[2] = true;
            StateMachine.Animator.SetBool("Gliding", true);
            StateMachine.WeaponEquiped = false;
            StateMachine.Gliding = true;
            StateMachine.Movement.ActiveParent = null;
            StateMachine.GravitySet.ResetLateralGravity();
        }

        public override void UpdateState()
        {
            if(_exitGlide){ CheckSwitchStates(); return; }
            else
            {
                if(!PlayerPower.Instance.Stamina.RequestPowerTime(2)){ _exitGlide = true; CheckSwitchStates(); return; }
            }
            StateMachine.Movement.GlidingMovement();
            CheckSwitchStates();
        }

        public override void ExitState()
        {
            PlayerWeaponStateMachine.Instance.SwitchAttachment();
            PlayerPower.Instance.StaminaInUse[2] = false;
            StateMachine.Animator.SetBool("Gliding", false);
            StateMachine.Gliding = false;
            StateMachine.GravitySet.GliderForcesSet = false;
        }

        public override void CheckSwitchStates()
        {
            if(StateMachine.Dead)
            {
                SwitchState(StateFactory.Dead());
            }
            else if (_exitGlide)
            {
                if(!StateMachine.Movement.EnvironmentDetection.IsGrounded)
                {
                    SwitchState(StateFactory.FallingRoot());
                    return;
                } 
                else
                {
                    SwitchState(StateFactory.GroundedRoot());
                    return;
                } 
            }
            else if (StateMachine.Climbing && !PlayerPower.Instance.Stamina.ClimbingDisabled)
            {
                SwitchState(StateFactory.ClimbingRoot());
            }
            else if (StateMachine.Movement.EnvironmentDetection.IsGrounded)
            {
                SwitchState(StateFactory.GroundedRoot());
            }
            else if (StateMachine.Input.BButton)
            {
                SwitchState(StateFactory.FallingRoot());
            }
            else if (StateMachine.Water)
            {
                SwitchState(StateFactory.Water());
            }
        }

        public override void FixedUpdateState(){}
    }

    // Water
    public class PlayerState_Swim_Root : BaseState
    {
        public PlayerState_Swim_Root(PlayerMachine currentContext, Factory playerStateFactory) : base(currentContext, playerStateFactory)
        {
            IsRootState = true;
            InitializeSubState();
        }

        public override void EnterState()
        {
            StateMachine.Animator.SetBool("Swim", true);
            StateMachine.Swimming = true;
            StateMachine.DoubleJumping = false;
            // StateMachine.PlayerStatsManager.ReduceStatByTime(14);
        }

        public override void UpdateState()
        {
            CheckSwitchStates();
            if (!StateMachine.Movement.Slerping)
            {
                StateMachine.WallDetect.SwimMonitor();
            }
            // if(!StateMachine.PlayerStatsManager._playerStatApproval[ALabs_Character_Stats.PlayerStatType.SwimmingStamina])
            // {
            //     StateMachine.Player.PlayerStats[1].Value = 0;
            // }
        }
        public override void ExitState()
        {
            StateMachine.Animator.SetBool("Swim", false);
            StateMachine.Swimming = false;
            // StateMachine.PlayerStatsManager.ReplenishStatByTime(14);
        }

        public override void CheckSwitchStates()
        {
            if(StateMachine.Dead)
            {
                SwitchState(StateFactory.Dead());
            }
            else
            {
                if(!StateMachine.Water)
                {
                    if(StateMachine.Climbing && !PlayerPower.Instance.Stamina.ClimbingDisabled)
                    {
                        SwitchState(StateFactory.ClimbingRoot());
                    }
                    else
                    {
                        if (StateMachine.Movement.EnvironmentDetection.IsGrounded)
                        {
                            SwitchState(StateFactory.GroundedRoot());
                        }
                        else
                        {
                            SwitchState(StateFactory.FallingRoot());
                        }
                    }
                }
                if(StateMachine.Climbing && !PlayerPower.Instance.Stamina.ClimbingDisabled)
                {
                    SwitchState(StateFactory.ClimbingRoot());
                }
            }
        }

        public override void InitializeSubState()
        {
            if (StateMachine.WaterLevel)
            {
                SetSubState(StateFactory.Swim());
            }
            else
            {
                SetSubState(StateFactory.SurfaceSwim());
            }
        }
        public override void FixedUpdateState(){}
    }

    public class PlayerState_Swim_Depth : BaseState
    {
        private float _chargedSwimTime = 0f;
        private float _slerpCoolDownTime = 0f;
        public PlayerState_Swim_Depth(PlayerMachine currentContext, Factory playerStateFactory) : base(currentContext, playerStateFactory){}
        public override void UpdateState()
        {
            CheckSwitchStates();
            if (!StateMachine.Movement.Slerping)
            {
                StateMachine.Movement.SwimMovement();
            }
            if(StateMachine.Movement.CurrentSpeed != StateMachine.Movement.Speeds.SwimDepth)
            {
                StateMachine.Movement.CurrentSpeed = StateMachine.Movement.Speeds.SwimDepth;
            }
            SwimSlerpCheck();
        }

        private void SwimSlerpCheck()
        {
            if(StateMachine.Input.ZLButton && !StateMachine.Movement.Slerping)
            {
                _chargedSwimTime += Time.deltaTime;
                StateMachine.Movement.Character.transform.Rotate(new(0,0,1),360 * Time.deltaTime * 2.5f);
                if(StateMachine.Animator.GetInteger("SwimChargeInt") != 1)
                {
                    StateMachine.Animator.SetInteger("SwimChargeInt",1);
                }
                if(_chargedSwimTime > 1 && StateMachine.Input.XButton && !StateMachine.Input.RequireNewXPressed && _slerpCoolDownTime <= 0f || _chargedSwimTime > 5 && _slerpCoolDownTime <= 0f)
                {
                    if(StateMachine.Animator.GetInteger("SwimChargeInt") != 2)
                    {
                        StateMachine.Animator.SetInteger("SwimChargeInt",2);
                    }
                    StateMachine.Input.RequireNewXPressed = true;
                    float swimHold = _chargedSwimTime;
                    _chargedSwimTime = 0f;
                    StateMachine.GravitySet.SwimSlerp(swimHold);
                }
            }
            else
            {
                if(StateMachine.Animator.GetInteger("SwimChargeInt") != 0)
                {
                    StateMachine.Animator.SetInteger("SwimChargeInt",0);
                }
            }
        }
        public override void CheckSwitchStates()
        {
            if (!StateMachine.WaterLevel)
            {
                SwitchState(StateFactory.SurfaceSwim());
            }
        }
        public override void InitializeSubState(){}
        public override void FixedUpdateState(){}
        public override void ExitState(){}
        public override void EnterState(){}
    }

    public class PlayerState_SwimSurface : BaseState
    {   
        public PlayerState_SwimSurface(PlayerMachine currentContext, Factory playerStateFactory) : base(currentContext, playerStateFactory){}

        public override void UpdateState()
        {
            CheckSwitchStates();
            if (!StateMachine.Movement.Slerping)
            {
                StateMachine.Movement.SurfaceSwimMovement();
            }
            if(StateMachine.Movement.CurrentSpeed != StateMachine.Movement.Speeds.SwimSurface)
            {
                StateMachine.Movement.CurrentSpeed = StateMachine.Movement.Speeds.SwimSurface;
            }
        }
        public override void CheckSwitchStates()
        {
            if (StateMachine.WaterLevel)
            {
                SwitchState(StateFactory.Swim());
            }
        }
        public override void InitializeSubState(){}
        public override void FixedUpdateState(){}
        public override void EnterState(){}
        public override void ExitState(){}
        
    }

#endregion

#region Combat

    public class PlayerState_TargetLock_Root : BaseState
    {
        private bool _enemyInLock = false;
        public PlayerState_TargetLock_Root(PlayerMachine currentContext, Factory playerStateFactory) : base(currentContext, playerStateFactory)
        {
            IsRootState = true;
            InitializeSubState();
        }

        public override void EnterState()
        {
            if(PlayerEnemyMonitor.Instance.CameraPing(out EnemyMachine bestEnemy)){ StateMachine.LockedEnemy = bestEnemy; _enemyInLock = true; }
            StateMachine.LockedOnEnemy = true;
            // StateMachine.EnemyLocked();
        }

        public override void UpdateState()
        {
            if(_enemyInLock && StateMachine.LockedEnemy == null)
            {
                if(PlayerEnemyMonitor.Instance.CameraPing(out EnemyMachine bestEnemy)){ StateMachine.LockedEnemy = bestEnemy; _enemyInLock = true; }
                else{ _enemyInLock = false; }
            }

            CheckSwitchStates();
            StateMachine.Movement.TargetLockMovement(_enemyInLock);
            Attack();
        }

        void Attack()
        {
            if(StateMachine.Input.RButton && !StateMachine.Input.RequireNewRPressed)
            {
                StateMachine.RobotMachine.RobotAttack(UnityEngine.Random.Range(0,3));
            }

            if (!StateMachine.PreventAttack && StateMachine.Input.YButton && !StateMachine.Input.RequireNewYPressed)
            {
                if(PlayerPower.Instance.RequestPower(PlayerWeaponStateMachine.Instance.CurrentWeapon))
                {
                    StateMachine.Input.RequireNewYPressed = true;
                    if(!StateMachine.Movement.EnvironmentDetection.IsGrounded)
                    {
                        JumpAttackMove();
                    }
                    else
                    {
                        StateMachine.Movement.AttackMovement();
                    }
                    PlayerAudioBridge.Instance.SwingWeapon();
                    StateMachine.AnimationTriggerString("AttackTrigger");
                }
                else
                {
                    StateMachine.Input.RequireNewYPressed = true;
                    // StateMachine.AnimationTriggerString("NoPower");
                }
                
            }
        } 

        private void JumpAttackMove()
        {
            Ray ray = new(PlayerMonitor.PlayerPosition + (PlayerMonitor.PlayerForward * 4f),Vector3.down);
            if(Physics.SphereCast(ray,0.3f,out RaycastHit hit,5f,StateMachine.Movement.EnvironmentMask))
            {
                Vector3 movePos = hit.point;
                Vector3 posOne = new(movePos.x,StateMachine.transform.position.y,movePos.z);
                List<Vector3> positions = new(){posOne,movePos};
                List<float> speeds = new(){StateMachine.Movement.Speeds.JumpAttackMovement * 1.3f ,StateMachine.Movement.Speeds.JumpAttackMovement};
                List<Quaternion> rotations = new(){StateMachine.transform.rotation, StateMachine.transform.rotation};
                StateMachine.Movement.ClimbingChildObject.transform.position = movePos;
                StateMachine.Movement.SetSlerp(positions,rotations,speeds); 
            }
        }

        public override void ExitState()
        {
            StateMachine.LockedOnEnemy = false;
            // StateMachine.EnemyUnlocked();
            StateMachine.LockedEnemy = null;
            StateMachine.Animator.SetLayerWeight(6,0);
        }

        public override void CheckSwitchStates()
        {
            if (!StateMachine.Input.ZLButton)
            {
                SwitchState(StateFactory.GroundedRoot());
            }
            else if (!StateMachine.Movement.EnvironmentDetection.IsGrounded && !StateMachine.Jumping)
            {
                SwitchState(StateFactory.FallingRoot());
            }
            else if (StateMachine.Climbing && !PlayerPower.Instance.Stamina.ClimbingDisabled)
            {
                SwitchState(StateFactory.ClimbingRoot());
            }
            else if (StateMachine.Water)
            {
                SwitchState(StateFactory.Water());
            }
            else if(StateMachine.Input.ZRButton  && !PlayerPower.Instance.RequestPower(4))
            {
                SwitchState(StateFactory.BowRoot());
            }
        }

        public override void InitializeSubState()
        {
            SetSubState(StateFactory.Empty());
        }
        public override void FixedUpdateState(){}
    }

    public class PlayerState_Bow_Root : BaseState
    {
        private bool _exitState = false;
        public PlayerState_Bow_Root(PlayerMachine currentContext, Factory playerStateFactory) : base(currentContext, playerStateFactory)
        {
            IsRootState = true;
            InitializeSubState();
        }

        public override void EnterState()
        {
            if(!PlayerPower.Instance.RequestPower(4))
            {
                _exitState = true;
                return;
            }

            StateMachine.BowHold = true;
            StateMachine.Animator.SetBool("BowHold",true);
            PlayerPower.Instance.UseAttachmentPower(4);
        }

        public override void UpdateState()
        {
            if(_exitState){ CheckSwitchStates(); return; }
            CheckSwitchStates();
            StateMachine.Movement.CurrentSpeed = 4.5f;
        }

        public override void ExitState()
        {
            if(_exitState){ return; }
            StateMachine.BowHold = false;
            StateMachine.Animator.SetBool("BowHold",false);
        }

        public override void CheckSwitchStates()
        {
            if (!StateMachine.Input.ZRButton || _exitState)
            {
                if (StateMachine.Movement.EnvironmentDetection.IsGrounded)
                {
                    StateMachine.LandingTrigger = false;
                    SwitchState(StateFactory.GroundedRoot());
                }
                else
                {
                    SwitchState(StateFactory.FallingRoot());
                }
            }
            else if (StateMachine.Climbing && !PlayerPower.Instance.Stamina.ClimbingDisabled)
            {
                StateMachine.LandingTrigger = false;
                SwitchState(StateFactory.ClimbingRoot());
            }
            else if (StateMachine.Water)
            {
                StateMachine.LandingTrigger = false;
                SwitchState(StateFactory.Water());
            }
            else if (StateMachine.PlayerHasGlider && StateMachine.Input.XButton && StateMachine.Movement.EnvironmentDetection.GroundHeight >= StateMachine.GliderOpenHeight && !StateMachine.Input.RequireNewXPressed && !PlayerPower.Instance.Stamina.GlidingDisabled)
            {
                StateMachine.LandingTrigger = false;
                SwitchState(StateFactory.GlidingRoot());
            }
        }
        public override void InitializeSubState()
        {
            if (StateMachine.Movement.EnvironmentDetection.GroundHeight >= StateMachine.BulletTimeHeight)
            {
                SetSubState(StateFactory.BowBulletTime());
            }
            else
            {
                SetSubState(StateFactory.BowGrounded());
            }
        }
        public override void FixedUpdateState(){}
    }

    public class PlayerState_BowGrounded : BaseState
    {
        public PlayerState_BowGrounded(PlayerMachine currentContext, Factory playerStateFactory) : base(currentContext, playerStateFactory)
        {
        }

        public override void EnterState()
        {

        }

        public override void UpdateState()
        {
            CheckSwitchStates();
            StateMachine.Movement.BowMovement();
        }

        public override void FixedUpdateState()
        {

        }

        public override void ExitState()
        {

        }

        public override void CheckSwitchStates()
        {
            if (!StateMachine.Movement.EnvironmentDetection.IsGrounded && StateMachine.Movement.EnvironmentDetection.GroundHeight >= StateMachine.BulletTimeHeight)
            {
                SwitchState(StateFactory.BowBulletTime());
            }
        }

        public override void InitializeSubState()
        {

        }
    }

    public class PlayerState_BowFalling : BaseState
    {
        public PlayerState_BowFalling(PlayerMachine currentContext, Factory playerStateFactory) : base(currentContext, playerStateFactory){}

        public override void EnterState()
        {
            StateMachine.Animator.SetBool("Falling", true);
            if (StateMachine.Input.AButton)
            {
                StateMachine.Input.RequireNewAPressed = true;
            }
            if (StateMachine.Input.XButton)
            {
                StateMachine.Input.RequireNewXPressed = true;
            }
            StateMachine.LandingSet = false;
            StateMachine.LandingTrigger = false;
            StateMachine.Jumping = false;
        }

        public override void UpdateState()
        {
            CheckSwitchStates();
            StateMachine.Movement.FallingMovement();
            StateMachine.FallingAnimation();
            StateMachine.LandingAnimation();
        }

        public override void FixedUpdateState()
        {

        }

        public override void ExitState()
        {
        }

        public override void CheckSwitchStates()
        {
            if (StateMachine.Movement.EnvironmentDetection.IsGrounded)
            {
                PlayerAudioBridge.Instance.Footstep();
                StateMachine.Falling = false;
                StateMachine.Animator.SetBool("Falling", false);
                StateMachine.Animator.SetFloat("FallingFloat", -1f);
                StateMachine.Animator.SetFloat("LandingFloat", -1f);
                StateMachine.Movement.ResetMoveDirection();
                SwitchState(StateFactory.BowGrounded());
            }
            else if (!StateMachine.Movement.EnvironmentDetection.IsGrounded && StateMachine.Movement.EnvironmentDetection.GroundHeight >= StateMachine.BulletTimeHeight && StateMachine.Input.ZRButton)
            {
                SwitchState(StateFactory.BowBulletTime());
            }
        }

        public override void InitializeSubState()
        {
        }
    }

    public class PlayerState_BowBulletTime : BaseState
    {
        public PlayerState_BowBulletTime(PlayerMachine currentContext, Factory playerStateFactory) : base(currentContext, playerStateFactory)
        {
        }

        private float _releaseFloat = 0f;
        public override void EnterState()
        {
            Time.timeScale = StateMachine.AirBowTimeSpeed;
        }

        public override void UpdateState()
        {
            CheckSwitchStates();
        }

        public override void FixedUpdateState()
        {

        }

        public override void ExitState()
        {
            Time.timeScale = 1;
        }

        public override void CheckSwitchStates()
        {
            if (StateMachine.Movement.EnvironmentDetection.IsGrounded)
            {
                SwitchState(StateFactory.BowGrounded());
            }
            if (!StateMachine.Input.ZRButton)
            {
                _releaseFloat += Time.deltaTime;
                if(_releaseFloat > 1f)
                {
                    SwitchState(StateFactory.BowFalling());
                } else
                {
                    _releaseFloat = 0f;
                }
                
            }
        }

        public override void InitializeSubState()
        {

        }
    }

#endregion

#region Special States

    public class PlayerState_Interacting : BaseState
    {
        public PlayerState_Interacting(PlayerMachine currentContext, Factory playerStateFactory) : base(currentContext, playerStateFactory)
        {
            IsRootState = true;
            IsInteracting = true;
            InitializeSubState();
        }

        public override void CheckSwitchStates()
        {
            if (!StateMachine.Interacting)
            {
                SwitchState(StateFactory.GroundedRoot());
            }
        }

        public override void EnterState()
        {
            // StateMachine.Input.MenuActive();
        }

        public override void ExitState()
        {
            // StateMachine.Input.GameActive();
        }

        public override void FixedUpdateState()
        {
        }

        public override void InitializeSubState()
        {
            SetSubState(StateFactory.Empty());
        }

        public override void UpdateState()
        {
            CheckSwitchStates();
        }
    }

    public class PlayerDeathState : BaseState
    {
        public PlayerDeathState(PlayerMachine currentContext, Factory playerStateFactory) : base(currentContext, playerStateFactory)
        {
            IsRootState = true;
            InitializeSubState();
        }

        public override void CheckSwitchStates()
        {
        }

        public override void EnterState()
        {
            StateMachine.DeathEffect();
        }

        public override void ExitState()
        {
        }

        public override void FixedUpdateState()
        {
        }

        public override void InitializeSubState()
        {
            SetSubState(StateFactory.Empty());
        }

        public override void UpdateState()
        {
        }
    }

    public class EmptySubState : BaseState
    {
        public EmptySubState(PlayerMachine currentContext, Factory playerStateFactory) : base(currentContext, playerStateFactory)
        {
        }
        public override void CheckSwitchStates()
        {
        }

        public override void EnterState()
        {
        }

        public override void ExitState()
        {
        }

        public override void FixedUpdateState()
        {
        }

        public override void InitializeSubState()
        {
        }

        public override void UpdateState()
        {
        }
    }

#endregion

}
