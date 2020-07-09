using UnityEngine;

namespace Assets.Scripts.Craft.Parts.Modifiers {
    public class ImprovedDockingColliderScript : MonoBehaviour {
        /// <summary>Unity OnTriggerEnter method.</summary>
        /// <param name="other">The other.</param>
        private void OnTriggerEnter(Collider other) {
            var otherPart = other.GetComponentInParent<PartScript>();
            if (otherPart == null) {
                // The thing we're colliding with is not a part.
                return;
            }

            var thisDockingPort =
                this.GetComponentInParent<PartScript>().GetModifier<ImprovedDockingPortScript>();

            IDockingPortScript otherDockingPort = null;

            var otherNativeDockingPort = otherPart.GetModifier<DockingPortScript>();
            if (otherNativeDockingPort != null) {
                otherDockingPort = new DockingPortWrapper(otherNativeDockingPort);
            } else {
                var otherImprovedDockingPort = otherPart.GetModifier<ImprovedDockingPortScript>();
                if (otherImprovedDockingPort != null) {
                    otherDockingPort = otherImprovedDockingPort;
                }
            }

            if (otherDockingPort != null) {
                thisDockingPort.OnTouchDockingPort(otherDockingPort);
            }
        }
    }
}
