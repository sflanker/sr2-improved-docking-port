namespace Assets.Scripts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using ModApi;
    using ModApi.Common;
    using ModApi.Mods;
    using UnityEngine;

    /// <summary>
    /// A singleton object representing this mod that is instantiated and initialize when the mod is loaded.
    /// </summary>
    public class Mod : ModApi.Mods.GameMod
    {
        /// <summary>
        /// Prevents a default instance of the <see cref="Mod"/> class from being created.
        /// </summary>
        private Mod() : base()
        {
        }

        /// <summary>
        /// Gets the singleton instance of the mod object.
        /// </summary>
        /// <value>The singleton instance of the mod object.</value>
        public static Mod Instance { get; } = GetModInstance<Mod>();

        public override void OnModLoaded() {
            base.OnModLoaded();

            var success=
                this.TryLoadAsset("Assets/Content/Craft/Parts/DesignerParts/icon") ||
                this.TryLoadAsset("Assets/Content/Craft/Parts/DesignerParts/icon.png") ||
                this.TryLoadAsset("Content/Craft/Parts/DesignerParts/icon") ||
                this.TryLoadAsset("Content/Craft/Parts/DesignerParts/icon.png") ||
                this.TryLoadAsset("Craft/Parts/DesignerParts/icon") ||
                this.TryLoadAsset("Craft/Parts/DesignerParts/icon.png");

            if (!success) {
                Debug.Log("Shite!");
            }
        }

        private Boolean TryLoadAsset(String asset) {
            if (this.ResourceLoader.LoadAsset<Sprite>(asset) != null) {
                Debug.Log($"Hooray! {asset}");
                return true;
            } else {
                Debug.Log($"Nope: {asset}");
                return false;
            }
        }

    }
}
