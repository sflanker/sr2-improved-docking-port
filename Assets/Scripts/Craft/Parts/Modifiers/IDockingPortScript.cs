using System;
using ModApi.Craft.Parts;
using UnityEngine;

namespace Assets.Scripts.Craft.Parts.Modifiers {
    public interface IDockingPortScript {
        Boolean IsReadyForDocking { get; }
        IPartScript PartScript { get; }
        Transform transform { get; }
        Single DockingTime { get; set; }
        Single DockResetTimer { get; set; }
        AttachPoint DockingAttachPoint { get; }
        Boolean IsColliderReadyForDocking { get; set; }
        Vector3 GetJointWorldPosition();
        Vector3 GetJointPosition();
    }
}
