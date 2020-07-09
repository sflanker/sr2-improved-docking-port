using System;
using System.Reflection;
using ModApi.Craft.Parts;
using UnityEngine;

namespace Assets.Scripts.Craft.Parts.Modifiers {
    public class DockingPortWrapper : IDockingPortScript {
        private static readonly FieldInfo DockResetTimerField =
            typeof(DockingPortScript).GetField("_dockResetTimer", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly PropertyInfo DockingTimeProperty =
            typeof(DockingPortScript).GetProperty(
                nameof(DockingPortScript.DockingTime),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly MethodInfo GetJointWorldPositionMethod =
            typeof(DockingPortScript).GetMethod("GetJointWorldPosition", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo GetJointPositionMethod =
            typeof(DockingPortScript).GetMethod("GetJointPosition", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly DockingPortScript _nativeDockingPort;

        public Single DockingTime {
            get => this._nativeDockingPort.DockingTime;
            set => DockingTimeProperty.SetValue(this._nativeDockingPort, value);
        }
        public Single DockResetTimer {
            get => (Single)DockResetTimerField.GetValue(this._nativeDockingPort);
            set => DockResetTimerField.SetValue(this._nativeDockingPort, value);
        }

        public AttachPoint DockingAttachPoint => this._nativeDockingPort.DockingAttachPoint;
        public Boolean IsColliderReadyForDocking {
            get => this._nativeDockingPort.IsColliderReadyForDocking;
            set => this._nativeDockingPort.IsColliderReadyForDocking = value;
        }

        public Boolean IsReadyForDocking => this._nativeDockingPort.IsReadyForDocking;
        public IPartScript PartScript => this._nativeDockingPort.PartScript;
        public Transform transform => this._nativeDockingPort.transform;

        public DockingPortWrapper(DockingPortScript nativeDockingPort) {
            this._nativeDockingPort = nativeDockingPort;
        }

        public Vector3 GetJointPosition() {
            return (Vector3)GetJointPositionMethod.Invoke(this._nativeDockingPort, new System.Object[0]);
        }

        public Vector3 GetJointWorldPosition() {
            return (Vector3)GetJointWorldPositionMethod.Invoke(this._nativeDockingPort, new System.Object[0]);
        }
    }

    internal static class DockingPortScriptExtensions {
        public static IDockingPortScript AsIDockingPortScript(this DockingPortScript dockingPort) {
            return dockingPort != null ? new DockingPortWrapper(dockingPort) : null;
        }
    }
}
