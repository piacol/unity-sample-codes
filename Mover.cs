using UnityEngine;
using System.Collections.Generic;

namespace Platformer
{
    public class Mover : MonoBehaviour
    {
        public enum Type
        {
            Actor = 0,
            Item = 1,
            Loop = 2,
            Platform = 3,
            StampBoard = 4,
            Rope = 5,
            Ladder = 6,
            Trampoline = 7,
            RevolvingDoor = 8,
            Portal = 9,
            Thorn = 10,
            ShortThorn = 11,
            MovingPlatform = 12,
            DroppingFloor = 13,

            TypeEnd
        }

        public Vector3 Direction;
        public Type type;
        public GameObject meshPrefab;
        protected int m_uid;
        protected GameObject m_mesh;
        protected float m_vx = 0;
        protected float m_vy = 0;
        protected Vector3 m_position;
        protected Vector3 m_prevPosition;
        protected float m_angle;
        private BoxCollider2D m_collider;
        private float m_cachedAnimatorSpeed;

        public int UID { get { return m_uid; } }
        public float VX { get { return m_vx; } set { m_vx = value; } }
        public float VY { get { return m_vy; } set { m_vy = value; } }
        public Vector3 Position { get { return m_position; } set { m_position = value; } }
        public Quaternion Rotation { get; set; }
        public Vector3 PrevPosition { get { return m_prevPosition; } }
        public Vector2 Radius { get; private set; }
        public Vector2 ColliderPosition
        {
            get
            {
                return new Vector2(Position.x + ColliderOffset.x, Position.y + ColliderOffset.y);
            }
        }
        public Animator Animator { get; private set; }
        public static int AnimationIDHash { get; private set; }
        public Vector3 ColliderOffset { get; private set; }

        public void Init()
        {
            if(meshPrefab == null)
            {
                m_collider = GetComponent<BoxCollider2D>();
                ColliderOffset = Rotation * m_collider.offset;

                var localScale = transform.localScale;
                Radius = new Vector2(m_collider.size.x * localScale.x / 2f, m_collider.size.y * localScale.y / 2f);

                var r = Rotation * Radius;
                Radius = new Vector2(Mathf.Abs(r.x), Mathf.Abs(r.y));
            }
        }

        public virtual void CustomUpdate(float dt)
        {
            Move(dt);
        }

        public float GetGroundY()
        {
            float result = GetGroundY(new Vector2(Position.x, Position.y - Radius.y - 0.01f));

            return result;
        }

        public bool IsCollisionDetected(Mover dest)
        {
            if (Mathf.Abs(dest.ColliderPosition.x - ColliderPosition.x) < dest.Radius.x + Radius.x &&
                Mathf.Abs(dest.ColliderPosition.y - ColliderPosition.y) < dest.Radius.y + Radius.y)
            {
                return true;
            }

            return false;
        }

        public bool IsCollisionDetecteByYAxis(Mover dest)
        {
            if (Mathf.Abs(dest.ColliderPosition.y - ColliderPosition.y) < dest.Radius.y + Radius.y)
            {
                return true;
            }

            return false;
        }

        public Mover FindCollisionMover(Vector3 collisionPosition, Vector2 radius)
        {
            Mover result = null;
            var movers = GameController.Instance.Movers;
            LinkedListNode<Mover> nextNode;

            for (var node = movers.First; node != null; node = nextNode)
            {
                nextNode = node.Next;

                var mover = node.Value;
                if(mover.UID == UID)
                    continue;

                if (!IsHorizontalCollisionType(mover.type))
                    continue;

                if(MathUtils.IsCollisionDetected(collisionPosition, radius, mover.ColliderPosition, mover.Radius))
                {
                    result = mover;
                    break;
                }
            }

            return result;
        }

        protected virtual void Awake()
        {
            EventManager.RegisterHandler<EventGamePaused>(OnGamePaused);
            EventManager.RegisterHandler<EventGameResumed>(OnGameResumed);

            if (AnimationIDHash == 0)
            {
                AnimationIDHash = Animator.StringToHash("AnimationID");
            }

            if (GameController.Instance != null)
            {
                m_uid = GameController.Instance.AddMover(this);
            }

            m_position = transform.position;
            Rotation = transform.rotation;
            Direction = transform.forward;

            if (meshPrefab != null)
            {
                m_mesh = GameObject.Instantiate(meshPrefab);

                if (m_mesh != null)
                {
                    m_mesh.transform.parent = transform;
                    m_mesh.transform.localPosition = Vector3.zero;
                    m_mesh.transform.localRotation = Quaternion.identity;

                    Animator = m_mesh.GetComponent<Animator>();
                    m_collider = m_mesh.GetComponent<BoxCollider2D>();

                    ColliderOffset = Rotation * m_collider.offset;

                    var localScale = m_mesh.transform.localScale;
                    Radius = new Vector2(m_collider.size.x * localScale.x / 2f, m_collider.size.y * localScale.y / 2f);

                    var r = Rotation * Radius;
                    Radius = new Vector2(Mathf.Abs(r.x), Mathf.Abs(r.y));
                }
            }
        }

        protected virtual void OnDestroy()
        {
            EventManager.UnregisterHandler<EventGamePaused>(OnGamePaused);
            EventManager.UnregisterHandler<EventGameResumed>(OnGameResumed);

            if (GameController.Instance != null)
            {
                GameController.Instance.RemoveMover(this);
            }
        }

        protected virtual void Move(float dt)
        {
        }

        protected float GetGroundY(Vector2 position)
        {
            RaycastHit2D hitInfo = Physics2D.Raycast(position, new Vector2(0, -1), 100);

            if (hitInfo.collider != null)
            {
                return hitInfo.point.y + 0.01f;
            }

            return -Mathf.Infinity;
        }

        protected static bool IsHorizontalCollisionType(Type type)
        {
            return type is Type.Platform or Type.Trampoline or Type.Thorn or Type.MovingPlatform or Type.DroppingFloor;
        }

        private void OnGamePaused(EventGamePaused eventData)
        {
            if (Animator == null) return;
            m_cachedAnimatorSpeed = Animator.speed;
            Animator.speed = 0;
        }

        private void OnGameResumed(EventGameResumed eventData)
        {
            if (Animator == null) return;
            Animator.speed = m_cachedAnimatorSpeed;
        }
    }
}