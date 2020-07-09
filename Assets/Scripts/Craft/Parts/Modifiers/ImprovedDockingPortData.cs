using System;
using ModApi.Craft.Parts;
using ModApi.Craft.Parts.Attributes;

namespace Assets.Scripts.Craft.Parts.Modifiers {
    [Serializable]
    [DesignerPartModifier("ImprovedDockingPort")]
    [PartModifierTypeId("ImprovedDockingPort")]
    public class ImprovedDockingPortData : PartModifierData<ImprovedDockingPortScript> {
    }
}
