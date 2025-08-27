using UnityEngine;
using System;
using System.Collections.Generic;

namespace Platformer
{
    public class Actor : Mover
    {
        public const float Error = 0.0001f;

        protected eActorDirection m_directionType = eActorDirection.Invalid;
        protected eActorDirection m_nextDirectionType = eActorDirection.Invalid;
        protected eActorState m_nextInputStateType = eActorState.Invalid;
        protected eActorState m_nextStateType = eActorState.Invalid;
        protected object m_nextStateParameter = null;
        protected float m_prevGroundY = -1;
        protected Mover m_attachedMover = null;
        protected MoverStateMachine m_stateMachine = new MoverStateMachine();
        protected float m_speed;
        private const float m_wallDistance = 0.2f;
        private bool m_nextStateForced = false;

        public eActorDirection DirectionType { get { return m_directionType; } }
        public eActorState StateType { get { return m_stateMachine.CurrentStateType; } }
        public Mover BottomMover { get; private set; }

        public Mover UpdatePositionX()
        {
            m_position.x += m_vx + GetAdditionalVX();

            var cp = Position + ColliderOffset;
            var hCM = FindCollisionMover(cp, Radius);

            if (hCM != null)
            {
                var cmPos = hCM.ColliderPosition;
                var cr = hCM.Radius;

                if (cmPos.x < ColliderPosition.x)
                {
                    m_position.x = cmPos.x + cr.x + Radius.x + Error - ColliderOffset.x;
                }
                else
                {
                    m_position.x = cmPos.x - (cr.x + Radius.x + Error) - ColliderOffset.x;
                }
            }

            return hCM;
        }

        public void UpdatePositionYByAttachedMover()
        {
            var av = m_attachedMover;
            if(av == null)
            {
                return;
            }

            m_position.y = av.ColliderPosition.y + av.Radius.y + Radius.y + Actor.Error - ColliderOffset.y;
        }

        public override void CustomUpdate(float dt)
        {
            Move(dt);
        }

        public void PlaceAt(Vector2 position)
        {
            m_position = position;

            var layerMask = 1 << LayerMask.NameToLayer("Actor");
            layerMask = ~layerMask;

            var hitInfo = Physics2D.Raycast(position, new Vector2(0, -1), Mathf.Infinity, layerMask);
            if (hitInfo.collider != null)
            {
                m_position.y = hitInfo.point.y + Radius.y + Error - ColliderOffset.y;
            }

            transform.position = Position;
        }

        public static eActorDirection InverseDirectionType(eActorDirection type)
        {
            eActorDirection result = type;

            if (type == eActorDirection.Left)
            {
                result = eActorDirection.Right;
            }
            else if (type == eActorDirection.Right)
            {
                result = eActorDirection.Left;
            }

            return result;
        }

        public bool IsOnPlatformEdge()
        {
            var result = true;
            LinkedList<Mover> movers = GameController.Instance.Movers;
            LinkedListNode<Mover> node;
            LinkedListNode<Mover> nextNode;
            Mover mover;
            var cp = ColliderPosition;

            for (node = movers.First; node != null; node = nextNode)
            {
                nextNode = node.Next;
                mover = node.Value;

                if(!IsHorizontalCollisionType(mover.type))
                    continue;

                var mcp = mover.ColliderPosition;
                var mr = mover.Radius;

                if (Mathf.Abs(cp.y - mcp.y) > (mr.y + Radius.y + Error*2))
                    continue;

                // Check if player's left edge is include in the mover's rage
                if (cp.x - Radius.x >= mcp.x - mr.x &&
                    cp.x - Radius.x <= mcp.x + mr.x)
                {
                    result = false;
                    break;
                }

                // Check if player's right edge is include in the mover's rage
                if (cp.x + Radius.x >= mcp.x - mr.x &&
                    cp.x + Radius.x <= mcp.x + mr.x)
                {
                    result = false;
                    break;
                }
            }
            return result;
        }

        public void SetNextInputStateType(eActorState type)
        {
            m_nextInputStateType = type;

            EventManager.Send(EventActorNextInputStateChanged.Create(type));
        }

        public void SetNextDirectionType(eActorDirection type)
        {
            m_nextDirectionType = type;
        }

        public void SetNextStateType(eActorState type, bool forced = false, object parameter = null)
        {
            if (m_nextStateForced) return;
            m_nextStateType = type;
            m_nextStateForced = forced;
            m_nextStateParameter = parameter;
        }

        public void AttachMover(Mover mover)
        {
            if(m_attachedMover != null)
            {
                throw new Exception($"Player.AttachMover() Failed - attached mover:{m_attachedMover.name} uid:{m_attachedMover.UID} already exsits. new mover:{mover.name} uid:{mover.UID}");
            }

            m_attachedMover = mover;
        }

        public void DetachMover()
        {
            m_attachedMover = null;
        }
        public virtual float GetSpeed()
        {
            return m_speed;
        }

        public bool IsValidPositionForTriangleJump()
        {
            LinkedList<Mover> movers = GameController.Instance.Movers;
            LinkedListNode<Mover> node;
            LinkedListNode<Mover> nextNode;
            Mover mover;
            var cp = ColliderPosition;

            for (node = movers.First; node != null; node = nextNode)
            {
                nextNode = node.Next;

                mover = node.Value;

                if(!IsHorizontalCollisionType(mover.type))
                {
                    continue;
                }

                if (IsCollisionDetecteByYAxis(mover))
                {
                    var mcp = mover.ColliderPosition;

                    if ((Direction.x < 0 && cp.x > mcp.x && (cp.x - Radius.x) < (mcp.x + mover.Radius.x + m_wallDistance) ||
                        Direction.x > 0 && cp.x < mcp.x && (cp.x + Radius.x) > (mcp.x - mover.Radius.x - m_wallDistance)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public void UpdateBottomMover()
        {
            var cp = ColliderPosition;
            cp.y -= Radius.y;
            BottomMover = FindCollisionMover(cp, Radius);
        }

        public void UpdateVXByDirection()
        {
            if(m_directionType == eActorDirection.Left)
            {
                m_vx = -GetSpeed();
            }
            else if(m_directionType == eActorDirection.Right)
            {
                m_vx = GetSpeed();
            }
        }

        protected override void Move(float dt)
        {
            if (m_nextInputStateType != eActorState.Invalid)
            {
                if (!m_nextStateForced && CanChangeStateType(m_nextInputStateType))
                {
                    m_nextStateType = m_nextInputStateType;

                    if ((StateType == eActorState.JumpUp || StateType == eActorState.WallSlide) &&
                        m_nextStateType == eActorState.JumpUp)
                    {
                        m_nextDirectionType = InverseDirectionType(m_directionType);
                    }
                }

                m_nextInputStateType = eActorState.Invalid;
            }

            if (m_nextDirectionType != eActorDirection.Invalid)
            {
                m_directionType = m_nextDirectionType;

                ChangeDirection(m_directionType);

                m_nextDirectionType = eActorDirection.Invalid;
            }

            if (m_nextStateType != eActorState.Invalid)
            {
                ChangeStateType(m_nextStateType, m_nextStateParameter);

                m_nextStateType = eActorState.Invalid;
                m_nextStateForced = false;
                m_nextStateParameter = null;
            }

            m_stateMachine.Update();

            transform.position = Position;
            transform.rotation = Rotation;

            m_prevPosition = Position;
        }

        protected void SetDirectionType(eActorDirection type)
        {
            Direction.x = type == eActorDirection.Right ? 1 : -1;

            transform.rotation = Quaternion.LookRotation(new Vector3(0, 0, Direction.x));
        }

        protected void ChangeDirection(eActorDirection type)
        {
            Direction.x = (type == eActorDirection.Right) ? 1 : -1;
        }

        protected void ChangeStateType(eActorState type, object parameter)
        {
            m_stateMachine.ChangeState(type, parameter);
        }

        private bool CanChangeStateType(eActorState nextStateType)
        {
            var stateType = StateType;

            if (stateType == eActorState.Idle || stateType == eActorState.Run)
            {
                return true;
            }
            else if (stateType == eActorState.JumpUp || stateType == eActorState.WallSlide)
            {
                if (nextStateType == eActorState.JumpUp)
                {
                    if (IsValidPositionForTriangleJump())
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private float GetAdditionalVX()
        {
            return m_attachedMover != null ? m_attachedMover.VX : 0;
        }
    }
}
