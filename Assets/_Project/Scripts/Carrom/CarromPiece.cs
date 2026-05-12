using ArcadeRoom.Core;
using UnityEngine;

namespace ArcadeRoom.Carrom
{
    public enum CarromPieceType
    {
        Striker = 0,
        Coin = 1,
        PlayerCoin = Striker,
        TargetCoin = Coin
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class CarromPiece : MonoBehaviour, IArcadeResettable
    {
        [SerializeField] private CarromPieceType pieceType;
        [SerializeField] private CarromBoardController boardController;
        [SerializeField] private Rigidbody pieceRigidbody;
        [SerializeField] private bool isPocketed;

        public CarromPieceType PieceType => pieceType;
        public Rigidbody Body
        {
            get
            {
                if (pieceRigidbody == null)
                {
                    pieceRigidbody = GetComponent<Rigidbody>();
                }

                return pieceRigidbody;
            }
        }
        public bool IsPocketed => isPocketed;

        private void Awake()
        {
            ResolveReferences();
        }

        public void Initialize(CarromBoardController board, CarromPieceType type)
        {
            boardController = board;
            pieceType = type;
            isPocketed = false;
            ResolveReferences();
        }

        public void Pocket(Vector3 pocketWorldPosition)
        {
            if (isPocketed)
            {
                return;
            }

            ResolveReferences();
            isPocketed = true;

            var colliders = GetComponentsInChildren<Collider>(true);
            for (var index = 0; index < colliders.Length; index++)
            {
                colliders[index].enabled = false;
            }

            if (pieceRigidbody != null)
            {
                var fallPosition = transform.position;
                fallPosition.x = pocketWorldPosition.x;
                fallPosition.z = pocketWorldPosition.z;
                transform.position = fallPosition;

                pieceRigidbody.linearVelocity = Vector3.zero;
                pieceRigidbody.angularVelocity = Vector3.zero;
                pieceRigidbody.isKinematic = false;
                pieceRigidbody.useGravity = true;
                pieceRigidbody.constraints &= ~RigidbodyConstraints.FreezePositionY;
                pieceRigidbody.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                pieceRigidbody.linearVelocity = Vector3.down * 0.35f;
            }

            boardController?.NotifyPiecePocketed(this);
        }

        public void ClearPocketedState()
        {
            isPocketed = false;
        }

        public void ResetState()
        {
            isPocketed = false;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (isPocketed)
            {
                return;
            }

            ResolveReferences();
            boardController?.NotifyPieceCollision(this, collision);
        }

        private void ResolveReferences()
        {
            if (pieceRigidbody == null)
            {
                pieceRigidbody = GetComponent<Rigidbody>();
            }

            if (boardController == null)
            {
                boardController = GetComponentInParent<CarromBoardController>();
            }

            if (boardController == null && transform.parent != null)
            {
                boardController = transform.parent.GetComponentInChildren<CarromBoardController>(true);
            }
        }
    }
}
