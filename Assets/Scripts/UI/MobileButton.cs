using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Photon.Pun;

namespace GhostCatcher
{
    /// <summary>
    /// On-screen action button. Interact is hold-to-use; Scan/Blend are tap.
    /// Finds the local PlayerController at runtime.
    /// </summary>
    public class MobileButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public enum ButtonType { Interact, Scan, Blend }

        [SerializeField] private ButtonType buttonType;

        private Button           button;
        private PlayerController targetPlayer;

        private void Start()
        {
            button = GetComponent<Button>();
            if (button != null && buttonType != ButtonType.Interact)
                button.onClick.AddListener(OnButtonClicked);
        }

        private void Update()
        {
            if (targetPlayer == null)
                FindLocalPlayer();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (buttonType != ButtonType.Interact) return;
            EnsurePlayer();
            targetPlayer?.SetInteractHeld(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (buttonType != ButtonType.Interact) return;
            targetPlayer?.SetInteractHeld(false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (buttonType != ButtonType.Interact) return;
            targetPlayer?.SetInteractHeld(false);
        }

        private void OnButtonClicked()
        {
            EnsurePlayer();
            if (targetPlayer == null) return;

            switch (buttonType)
            {
                case ButtonType.Scan:  targetPlayer.OnScanButton();  break;
                case ButtonType.Blend: targetPlayer.OnBlendButton(); break;
            }
        }

        private void EnsurePlayer()
        {
            if (targetPlayer == null)
                FindLocalPlayer();
        }

        private void FindLocalPlayer()
        {
            foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                var pv = pc.GetComponent<PhotonView>();
                if (pv != null && pv.IsMine)
                {
                    targetPlayer = pc;
                    break;
                }
            }
        }
    }
}
