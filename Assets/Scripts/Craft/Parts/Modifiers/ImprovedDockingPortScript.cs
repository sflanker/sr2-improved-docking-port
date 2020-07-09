using System;
using System.Collections;
using Assets.Scripts.Flight.Sim;
using ModApi.Audio;
using ModApi.Craft;
using ModApi.Craft.Parts;
using ModApi.GameLoop;
using ModApi.GameLoop.Interfaces;
using ModApi.Math;
using ModApi.Ui.Inspector;
using UnityEngine;

namespace Assets.Scripts.Craft.Parts.Modifiers {
    public class ImprovedDockingPortScript : PartModifierScript<ImprovedDockingPortData>, IDockingPortScript, IFlightFixedUpdate, IFlightUpdate {
        /// <summary>The docking collider.</summary>
        private ImprovedDockingColliderScript _dockingCollider;

        /// <summary>
        /// The amount of time to wait before a docking port can dock after undocking.
        /// </summary>
        private const Single ResetTime = 10f;

        private const Single UndockForce = 10f;

        /// <summary>
        /// The amount of time the docking ports have been aligned correctly.
        /// </summary>
        private Single _alignmentTime;

        /// <summary>
        /// The timer that prevents a docking port from re-docking immediately after undocking.
        /// </summary>
        private Single _dockResetTimer;

        /// <summary>
        /// The percentage of docking complete shown in the inspector.
        /// </summary>
        private Single _inspectorDockingStatusPercentage;

        /// <summary>The current angular force of the magnetic joint.</summary>
        private Single _magneticAngularForce;

        /// <summary>
        /// The magnetic joint that pulls the docking ports together when docking.
        /// </summary>
        private ConfigurableJoint _magneticJoint;

        /// <summary>The current force of the magnetic joint.</summary>
        private Single _magneticJointForce;

        /// <summary>
        /// The other docking port that is currently in the process of being docked with.
        /// </summary>
        private IDockingPortScript _otherDockingPort;

        public Single DockResetTimer {
            get => this._dockResetTimer;
            set => this._dockResetTimer = value;
        }

        /// <summary>Gets the docking attach point.</summary>
        /// <value>The docking attach point.</value>
        public AttachPoint DockingAttachPoint => this.PartScript.Data.AttachPoints[1];

        /// <summary>
        /// Gets the time the docking port was docked. If the docking port was docked initially, then this time will be zero.
        /// </summary>
        /// <value>The docking time.</value>
        public Single DockingTime { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the docking port's collider is ready for docking.
        /// </summary>
        /// <value>
        /// <c>true</c> if docking port's collider is ready for docking; otherwise, <c>false</c>.
        /// </value>
        public Boolean IsColliderReadyForDocking {
            get => this._dockingCollider.gameObject.activeSelf;
            set => this._dockingCollider.gameObject.SetActive(value);
        }

        /// <summary>
        /// Gets a value indicating whether the docking port is currently docked.
        /// </summary>
        /// <value>
        /// <c>true</c> if this docking port is docked; otherwise, <c>false</c>.
        /// </value>
        public Boolean IsDocked => this.DockingAttachPoint.PartConnections.Count > 0;

        /// <summary>
        /// Gets a value indicating whether this instance is docking.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is docking; otherwise, <c>false</c>.
        /// </value>
        public Boolean IsDocking => this._magneticJoint != null;

        /// <summary>
        /// Gets a value indicating whether this instance is ready for docking.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is ready for docking; otherwise, <c>false</c>.
        /// </value>
        public Boolean IsReadyForDocking => this.IsColliderReadyForDocking && this.PartScript.Data.Activated;

        // public PartScript PartScript { get; }

        void IFlightFixedUpdate.FlightFixedUpdate(in FlightFrameData frame) {
            if (this._otherDockingPort == null) {
                return;
            }

            var magnitude = (this._otherDockingPort.GetJointWorldPosition() - this.GetJointWorldPosition()).magnitude;
            if (magnitude > 1.5 || this._otherDockingPort.PartScript.Data.IsDestroyed) {
                this.DestroyMagneticJoint(true);
            } else {
                var num = Vector3.Dot(-this.transform.up, this._otherDockingPort.transform.up);
                if (num > 0.9999f && magnitude <= 0.009999f) {
                    this._alignmentTime += frame.DeltaTime;
                } else {
                    this._alignmentTime = 0.0f;
                }

                this._inspectorDockingStatusPercentage =
                    Mathf.Clamp01((num - 0.99f) / 0.009999999f *
                        Mathf.Lerp(1f, 0.0f, Mathf.Clamp01(magnitude * 20f)));

                if (this._alignmentTime > 1.0) {
                    this.CompleteDockConnection();
                } else {
                    this.SetMagneticJointForces(this._magneticJointForce, this._magneticAngularForce);
                    if (this._magneticJointForce < 50f) {
                        this._magneticJointForce += frame.DeltaTime * 500f * 0.009999999f;
                    }

                    if (this._magneticAngularForce < 100f) {
                        this._magneticAngularForce += frame.DeltaTime * 1000f * 0.009999999f;
                    }
                }
            }
        }

        void IFlightUpdate.FlightUpdate(in FlightFrameData frame) {
            if (this._dockResetTimer > 0.0f) {
                this._dockResetTimer -= frame.DeltaTime;
            } else if (!(this.IsDocked || this.IsDocking || this.IsColliderReadyForDocking)) {
                this.IsColliderReadyForDocking = true;
            }
        }

        /// <summary>Gets the status of the docking port.</summary>
        /// <returns>The status.</returns>
        public String GetStatus() {
            if (!this.PartScript.Data.Activated) {
                return "Disabled";
            } else if (this.IsColliderReadyForDocking) {
                return "Ready";
            } else if (this.IsDocking) {
                return $"Docking ({Units.GetPercentageString(this._inspectorDockingStatusPercentage)})";
            } else if (this.IsDocked) {
                return "Docked";
            } else if (this._dockResetTimer > 0.0) {
                return "Undocking";
            } else {
                return null;
            }
        }

        /// <summary>Called when a part is deactivated.</summary>
        public override void OnDeactivated() {
            base.OnDeactivated();
            if (this.IsDocked) {
                this.Undock();
            }
        }

        /// <summary>
        /// Called when the part is generating a model for an inspector panel. The modifier can optionally add
        /// its own functionality to the model.
        /// </summary>
        /// <param name="model">The inspector model.</param>
        public override void OnGenerateInspectorModel(PartInspectorModel model) {
            model.Add(new TextModel("Status", this.GetStatus));
            var button =
                new IconButtonModel(
                    "Ui/Sprites/Flight/IconPartInspectorUndock",
                    x => this.Undock(),
                    "Undock") {
                    UpdateAction = x => x.Visible = this.IsDocked
                };

            model.IconButtonRow.Add(button);
        }

        /// <summary>
        /// Called when physics has been enabled or disabled for the craft.
        /// </summary>
        /// <param name="enabled">if set to <c>true</c> enabled.</param>
        public override void OnPhysicsChanged(Boolean enabled) {
            base.OnPhysicsChanged(enabled);
            if (enabled || !(this._magneticJoint != null))
                return;
            this.DestroyMagneticJoint(true);
        }

        /// <summary>
        /// Called when this docking port trigger collider has touched another docking port's trigger.
        /// </summary>
        /// <param name="otherDockingPort">The other docking port.</param>
        public void OnTouchDockingPort(IDockingPortScript otherDockingPort) {
            if (this.IsReadyForDocking && otherDockingPort.IsReadyForDocking) {
                this.Dock(otherDockingPort);
            }
        }

        /// <summary>Undocks this docking port.</summary>
        public void Undock() {
            if (!this.PartScript.CraftScript.IsPhysicsEnabled || this.DockingAttachPoint.PartConnections.Count != 1) {
                return;
            }

            var partConnection = this.DockingAttachPoint.PartConnections[0];
            var vector3 = -this.transform.up;
            foreach (var joint in this.PartScript.BodyScript.Joints) {
                if (joint.PartConnection == partConnection) {
                    var otherPart = partConnection.GetOtherPart(this.PartScript.Data).PartScript;
                    var modifier =
                        otherPart.GetModifier<ImprovedDockingPortScript>() ??
                        otherPart.GetModifier<DockingPortScript>().AsIDockingPortScript();

                    if ((UnityEngine.Object)modifier != null) {
                        modifier.DockResetTimer = ResetTime;
                        modifier.DockingTime = 0.0f;
                    }

                    this._dockResetTimer = ResetTime;
                    this.IsColliderReadyForDocking = false;
                    this.DockingTime = 0.0f;
                    foreach (var componentsInChild in this.GetComponentsInChildren<Collider>()) {
                        if (componentsInChild.enabled) {
                            componentsInChild.enabled = false;
                            componentsInChild.enabled = true;
                        }
                    }

                    joint.Destroy();
                    this.PartScript.BodyScript.RigidBody.WakeUp();
                    this.PartScript.BodyScript.RigidBody.AddForceAtPosition(
                        vector3 * UndockForce,
                        this.PartScript.Transform.position,
                        ForceMode.Impulse
                    );

                    Game.Instance.AudioPlayer.PlaySound(AudioLibrary.Flight.DockDisconnect, this.transform.position);
                    break;
                }
            }
        }

        /// <summary>Called when the part modifier script is initialized.</summary>
        protected override void OnInitialized() {
            base.OnInitialized();

            // Because we're hijacking the stock Prefab, this comes with the wrong collider script, so we remove that and add our own.
            var legacyCollider = this.GetComponentInChildren<DockingColliderScript>();
            var legacyColliderParent = legacyCollider?.gameObject;
            if (legacyColliderParent != null) {
                this._dockingCollider = legacyColliderParent.AddComponent<ImprovedDockingColliderScript>();
                Destroy(legacyCollider);
            } else {
                Debug.LogWarning("IDC: DockingColliderScript Not Found");
            }

            this.IsColliderReadyForDocking = false;
        }

        /// <summary>Creates the joint.</summary>
        /// <param name="jointBody">The joint body.</param>
        /// <param name="jointPosition">The joint position in local space to the jointBody.</param>
        /// <param name="jointAxis">The joint axis in local space to the jointBody.</param>
        /// <param name="secondaryAxis">The secondary axis.</param>
        /// <param name="connectedBody">The connected body.</param>
        /// <param name="connectedPosition">The joint position in local space tot he connected body.</param>
        /// <returns>The configurable joint.</returns>
        private static ConfigurableJoint CreateJoint(
            IBodyScript jointBody,
            Vector3 jointPosition,
            Vector3 jointAxis,
            Vector3 secondaryAxis,
            Rigidbody connectedBody,
            Vector3 connectedPosition) {
            var configurableJoint = jointBody.GameObject.AddComponent<ConfigurableJoint>();
            configurableJoint.connectedBody = connectedBody;
            configurableJoint.autoConfigureConnectedAnchor = false;
            configurableJoint.axis = jointAxis;
            configurableJoint.secondaryAxis = secondaryAxis;
            configurableJoint.anchor = jointPosition;
            configurableJoint.connectedAnchor = connectedPosition;
            configurableJoint.xMotion = ConfigurableJointMotion.Free;
            configurableJoint.yMotion = ConfigurableJointMotion.Free;
            configurableJoint.zMotion = ConfigurableJointMotion.Free;
            configurableJoint.angularXMotion = ConfigurableJointMotion.Free;
            configurableJoint.angularYMotion = ConfigurableJointMotion.Free;
            configurableJoint.angularZMotion = ConfigurableJointMotion.Free;
            configurableJoint.enableCollision = true;
            return configurableJoint;
        }

        /// <summary>Completes the dock connection.</summary>
        private void CompleteDockConnection() {
            ICraftScript craftScript1 = this.PartScript.CraftScript;
            ICraftScript craftScript2 = this._otherDockingPort.PartScript.CraftScript;
            if (craftScript2.CraftNode.IsPlayer && !craftScript1.CraftNode.IsPlayer) {
                ICraftScript craftScript3 = craftScript1;
                craftScript1 = craftScript2;
                craftScript2 = craftScript3;
            }

            if (this._otherDockingPort.PartScript.CraftScript != this.PartScript.CraftScript) {
                this.StartCoroutine(this.OnDockingCompleteNextFrame(craftScript1.CraftNode.Name, craftScript2.CraftNode.Name));
                CraftSplitter.MergeCraftNode(craftScript2.CraftNode as CraftNode, craftScript1.CraftNode as CraftNode);
            } else {
                Debug.Log("Skipping craft merge because the docking ports are on the same craft.");
            }

            CraftBuilder.CreateBodyJoint(this.CreateDockingPartConnection(this._otherDockingPort, craftScript1));
            this.PartScript.PrimaryCollider.enabled = false;
            this.PartScript.PrimaryCollider.enabled = true;
            this.DockingTime = this._otherDockingPort.DockingTime = Time.time;
            this.DestroyMagneticJoint(false);
            Game.Instance.AudioPlayer.PlaySound(AudioLibrary.Flight.DockConnect, this.transform.position);
        }

        /// <summary>Creates the docking part connection.</summary>
        /// <param name="otherPort">The other port.</param>
        /// <param name="craftScript">The craft script.</param>
        /// <returns>The part connection.</returns>
        private PartConnection CreateDockingPartConnection(
            IDockingPortScript otherPort,
            ICraftScript craftScript) {
            var partConnection = new PartConnection(this.PartScript.Data, otherPort.PartScript.Data);
            partConnection.AddAttachment(this.DockingAttachPoint, otherPort.DockingAttachPoint);
            craftScript.Data.Assembly.AddPartConnection(partConnection);

            var bodyScript1 = this.PartScript.BodyScript;
            var bodyScript2 = otherPort.PartScript.BodyScript;
            partConnection.BodyJointData = new BodyJointData(partConnection) {
                Axis = Vector3.right,
                SecondaryAxis = Vector3.up,
                Position =
                    bodyScript1.Transform.InverseTransformPoint(
                        this.PartScript.Transform.TransformPoint(this.DockingAttachPoint.Position)),
                ConnectedPosition =
                    bodyScript2.Transform.InverseTransformPoint(
                        otherPort.PartScript.Transform.TransformPoint(otherPort.DockingAttachPoint.Position)),
                BreakTorque = 100000f,
                JointType = BodyJointData.BodyJointType.Docking,
                Body = bodyScript1.Data,
                ConnectedBody = bodyScript2.Data
            };

            return partConnection;
        }

        /// <summary>Destroys the magnetic joint.</summary>
        /// <param name="readyForDocking">if set to <c>true</c> then set the docking ports to ready for docking.</param>
        private void DestroyMagneticJoint(Boolean readyForDocking) {
            this.IsColliderReadyForDocking = readyForDocking;
            this._otherDockingPort.IsColliderReadyForDocking = readyForDocking;
            DestroyImmediate(this._magneticJoint);
            this._magneticJoint = null;
            this._otherDockingPort = null;
        }

        /// <summary>Docks this port with the other port.</summary>
        /// <param name="otherPort">The other port.</param>
        private void Dock(IDockingPortScript otherPort) {
            var thisCraft = this.PartScript.CraftScript as CraftScript;
            if (thisCraft?.CraftNode.IsPlayer != true) {
                // Only docking ports on the active craft do the docking
                return;
            }

            var thisBody = this.PartScript.BodyScript;
            var otherBody = otherPort.PartScript.BodyScript;
            if (thisBody == otherBody) {
                // Docking two ports on the same Body doesn't make sense, there should be some articulation in between them
                return;
            }

            this._otherDockingPort = otherPort;
            this.IsColliderReadyForDocking = false;
            otherPort.IsColliderReadyForDocking = false;
            this._alignmentTime = 0.0f;

            var jointPosition1 = this.GetJointPosition();
            var jointPosition2 = otherPort.GetJointPosition();
            this._magneticJoint =
                CreateJoint(
                    thisBody,
                    jointPosition1,
                    thisBody.Transform.InverseTransformDirection(this.transform.up),
                    thisBody.Transform.InverseTransformDirection(this.transform.right),
                    otherBody.RigidBody,
                    jointPosition2
                );

            this.SetMagneticJointForces(
                this._magneticJointForce = 0.0f,
                this._magneticAngularForce = 0.0f
            );

            CraftBuilder.SetJointTargetRotation(
                this._magneticJoint,
                Quaternion.FromToRotation(
                    thisBody.Transform.InverseTransformDirection(otherPort.transform.up),
                    thisBody.Transform.InverseTransformDirection(-this.transform.up))
            );
        }

        /// <summary>
        /// Gets the joint position in the body's local coordinates.
        /// </summary>
        /// <returns>The joint position.</returns>
        public Vector3 GetJointPosition() {
            return this.PartScript.BodyScript.Transform.InverseTransformPoint(this.GetJointWorldPosition());
        }

        /// <summary>Gets the joint position in world coordinates.</summary>
        /// <returns>The joint position.</returns>
        public Vector3 GetJointWorldPosition() {
            return this.PartScript.Transform.TransformPoint(this.DockingAttachPoint.Position);
        }

        /// <summary>Called the frame after docking has completed.</summary>
        /// <param name="playerCraftName">The name1.</param>
        /// <param name="otherCraftName">The name2.</param>
        /// <returns>The Unity enumerator.</returns>
        private IEnumerator OnDockingCompleteNextFrame(
            String playerCraftName,
            String otherCraftName) {
            yield return null;

            var craftScript =
                this.PartScript.CraftScript as CraftScript;

            if (craftScript != null) {
                craftScript.OnDockComplete(playerCraftName, otherCraftName);
            }
        }

        /// <summary>Sets the magnetic joint's forces.</summary>
        /// <param name="jointForce">The joint force.</param>
        /// <param name="angularForce">The angular force.</param>
        private void SetMagneticJointForces(Single jointForce, Single angularForce) {
            var jointDrive1 = new JointDrive {
                maximumForce = jointForce,
                positionSpring = jointForce,
                positionDamper = 10f
            };

            this._magneticJoint.xDrive = jointDrive1;
            this._magneticJoint.yDrive = jointDrive1;
            this._magneticJoint.zDrive = jointDrive1;
            this._magneticJoint.rotationDriveMode = RotationDriveMode.XYAndZ;
            var jointDrive2 = new JointDrive {
                maximumForce = angularForce,
                positionSpring = angularForce,
                positionDamper = 20f
            };

            this._magneticJoint.targetAngularVelocity = Vector3.zero;
            this._magneticJoint.slerpDrive = jointDrive2;
            this._magneticJoint.angularXDrive = jointDrive2;
            this._magneticJoint.angularYZDrive = jointDrive2;
        }
    }
}
