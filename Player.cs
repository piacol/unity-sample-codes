using System.Collections.Generic;
using UnityEngine;

namespace Platformer
{
    public class Player : Actor
    {
        [SerializeField] private GameObject m_jumpEffectPrefab;
        [SerializeField] private GameObject m_wallSlideEffectPrefab;

        private List<Mover> m_cachedCollisionMovers;
        private float m_additionalSpeedRate;
        private float m_additionalSpeedTime;
        private GameObject m_jumpEffect;

        public float MinViewWorldPositionY { get; private set; }

        public void Init(eActorDirection direction, float minViewWorldPositionY)
        {
            gameObject.SetActive(true);
            MinViewWorldPositionY = minViewWorldPositionY;

            ChangeStateType(eActorState.Idle, null);

            SetNextDirectionType(direction);
            m_nextInputStateType = eActorState.Invalid;

            SetDirectionType(direction);

            m_additionalSpeedRate = 0;
            m_additionalSpeedTime = 0;
            m_attachedMover = null;
        }

        public void Play()
        {
            SetNextStateType(eActorState.Run);
        }

        public override float GetSpeed()
        {
            return m_speed * (1 + m_additionalSpeedRate);
        }

        public List<Mover> FindCollisionMovers(Vector3 position)
        {
            var movers = GameController.Instance.Movers;
            LinkedListNode<Mover> node;
            LinkedListNode<Mover> nextNode;
            Mover mover;
            var cp = position + ColliderOffset;

            m_cachedCollisionMovers.Clear();

            for (node = movers.First; node != null; node = nextNode)
            {
                nextNode = node.Next;

                mover = node.Value;

                if (!IsHorizontalCollisionType(mover.type))
                {
                    continue;
                }

                if (MathUtils.IsCollisionDetected(cp, Radius, mover.ColliderPosition, mover.Radius))
                {
                    m_cachedCollisionMovers.Add(mover);
                }
            }

            return m_cachedCollisionMovers;
        }

        public bool ContainsMover(Mover mover)
        {
            return m_attachedMover == mover;
        }

        protected override void Awake()
        {
            base.Awake();

            EventManager.RegisterHandler<EventThornTriggered>(OnThornTriggered);
            EventManager.RegisterHandler<EventItemAcquired>(OnItemAcquired);
            EventManager.RegisterHandler<EventActorDamaged>(OnActorDamaged);

            m_speed = 0.12f;
            Direction = new Vector3(1, 0, 0);
            Rotation = Quaternion.LookRotation(new Vector3(0, 0, 1));

            transform.rotation = Rotation;

            m_vx = GetSpeed();

            Direction.x = 1;

            m_prevGroundY = -Mathf.Infinity;
            m_prevPosition = Position;

            m_jumpEffect = Instantiate(m_jumpEffectPrefab, transform);

            m_stateMachine.AddState(eActorState.Idle, new PlayerIdleState(this, eActorState.Idle));
            m_stateMachine.AddState(eActorState.Run, new PlayerRunState(this, eActorState.Run));
            m_stateMachine.AddState(eActorState.JumpUp, new PlayerJumpUpState(this, eActorState.JumpUp));
            m_stateMachine.AddState(eActorState.JumpDown, new PlayerJumpDownState(this, eActorState.JumpDown, m_jumpEffect));
            m_stateMachine.AddState(eActorState.WallSlide, new PlayerWallSlideState(this, eActorState.WallSlide, m_wallSlideEffectPrefab));
            m_stateMachine.AddState(eActorState.Death, new PlayerDeathState(this, eActorState.Death));
            m_stateMachine.AddState(eActorState.GoInside, new PlayerGoInsideState(this, eActorState.GoInside));

            m_cachedCollisionMovers = new List<Mover>();
        }

        protected override void OnDestroy()
        {
            EventManager.UnregisterHandler<EventThornTriggered>(OnThornTriggered);
            EventManager.UnregisterHandler<EventItemAcquired>(OnItemAcquired);
            EventManager.UnregisterHandler<EventActorDamaged>(OnActorDamaged);

            foreach(MoverState state in m_stateMachine.States)
            {
                state.Destroy();
            }

            Destroy(m_jumpEffect);

            base.OnDestroy();
        }

        protected override void Move(float dt)
        {
            base.Move(dt);

            if(m_additionalSpeedTime > 0)
            {
                m_additionalSpeedTime -= dt;

                if(m_additionalSpeedTime < 0)
                {
                    m_additionalSpeedRate = 0;
                    m_additionalSpeedTime = 0;
                }
            }
        }

        private void OnThornTriggered(EventThornTriggered eventData)
        {
            Death();
        }

        private void OnItemAcquired(EventItemAcquired eventData)
        {
            if(eventData.itemType == ItemType.Shoes)
            {
                m_additionalSpeedRate = 0.5f;
                m_additionalSpeedTime = 10f;
            }
        }

        private void OnActorDamaged(EventActorDamaged eventData)
        {
            if(eventData.VictimUID != UID)
            {
                return;
            }

            Death();
        }

        private void Death() => SetNextStateType(eActorState.Death, true);
    }
}