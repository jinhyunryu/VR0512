using UnityEngine;

namespace ArcadeRoom.Carrom
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class CarromPocketTrigger : MonoBehaviour
    {
        [SerializeField] private CarromBoardController boardController;

        private Collider _triggerCollider;

        private void Awake()
        {
            ResolveReferences();
        }

        public void Initialize(CarromBoardController board)
        {
            boardController = board;
            ResolveReferences();
        }

        private void OnTriggerEnter(Collider other)
        {
            var piece = other.attachedRigidbody != null
                ? other.attachedRigidbody.GetComponent<CarromPiece>()
                : other.GetComponentInParent<CarromPiece>();

            if (piece == null)
            {
                return;
            }

            if (piece.PieceType == CarromPieceType.PlayerCoin)
            {
                return;
            }

            boardController?.TryPocketPiece(piece, _triggerCollider != null ? _triggerCollider.bounds.center : transform.position);
        }

        private void ResolveReferences()
        {
            if (_triggerCollider == null)
            {
                _triggerCollider = GetComponent<Collider>();
            }

            if (_triggerCollider != null)
            {
                _triggerCollider.isTrigger = true;
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
