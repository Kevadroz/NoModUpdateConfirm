using System;
using System.Collections.Generic;
using System.Security.Permissions;
using BepInEx;
using BepInEx.Logging;
using Menu.Remix.MixedUI;
using UnityEngine;

#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace NMUC;

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]

public class NoModUpdateConfirm : BaseUnityPlugin {

	public const string PLUGIN_GUID = "kevadroz.no_mod_update_confirm";
	public const string PLUGIN_NAME = "No Mod Update Confirm";
	public const string PLUGIN_VERSION = "1.0.0";


	internal static RemixOptions options;
	public static new ManualLogSource Logger { get; private set; }

	private static List<WeakReference<Menu.DialogBoxNotify>> clicked = new List<WeakReference<Menu.DialogBoxNotify>>();

	private static bool WasClicked(Menu.DialogBoxNotify dialog) {

		foreach(WeakReference<Menu.DialogBoxNotify> reference in clicked) {
			Menu.DialogBoxNotify dialog2;
			if (reference.TryGetTarget(out dialog2) && dialog.Equals(dialog2))
				return true;
		}

		return false;
	}

	private static void AddToClicked(Menu.DialogBoxNotify dialog) {
		clicked.Add(new WeakReference<Menu.DialogBoxNotify>(dialog));
	}

	private static void PurgeClicked() {
		foreach(WeakReference<Menu.DialogBoxNotify> reference in clicked) {
	  if (!reference.TryGetTarget(out _) ) {
				clicked.Remove(reference);
			}
		}
	}

	private void OnEnable() {
		Logger = base.Logger;
		options = new RemixOptions(this);

		On.Menu.DialogBoxNotify.ctor += OnNewDialogBox;
		On.Menu.DialogBoxNotify.Update += OnDialogBoxUpdate;

		On.RainWorld.OnModsInit += OnModsInit;
	}

	private void OnModsInit( On.RainWorld.orig_OnModsInit orig, RainWorld self ) {
		MachineConnector.SetRegisteredOI(PLUGIN_GUID, options);
		orig(self);
	}

	private bool ShouldAutoConfirm( string signalText ) {
		return ( options.onModUpdate.Value && signalText == "REAPPLY" )
		|| ( options.onModReload.Value && signalText == "RESTART" )
		|| ( options.onGameUpdate.Value && signalText == "VERSIONPROMPT" );
	}

	private void OnNewDialogBox( On.Menu.DialogBoxNotify.orig_ctor orig, Menu.DialogBoxNotify self, Menu.Menu menu, Menu.MenuObject owner, string text, string signalText, Vector2 pos, Vector2 size, bool forceWrapping ) {
		orig(self, menu, owner, text, signalText, pos, size, forceWrapping);
		if ( ShouldAutoConfirm(signalText) ) {
			self.RemoveSprites();
			PurgeClicked();
		}
	}

	private void OnDialogBoxUpdate( On.Menu.DialogBoxNotify.orig_Update orig, Menu.DialogBoxNotify self ) {
		orig(self);
		if ( ShouldAutoConfirm(self.continueButton.signalText) && !WasClicked(self)) {
			self.continueButton.Clicked();
			AddToClicked(self);
		}
	}

}


internal class RemixOptions : OptionInterface {

	public Configurable<bool> onModUpdate;
	public Configurable<bool> onModReload;
	public Configurable<bool> onGameUpdate;


	public RemixOptions( NoModUpdateConfirm nmuc ) {
		onModUpdate = config.Bind("onModUpdate", true);
		onModReload = config.Bind("onModReload", true);
		onGameUpdate = config.Bind("onGameUpdate", false);
	}

	public override void Initialize() {

		OpTab opTab = new(this, "Tab");
		Tabs = new OpTab[1] { opTab };
		UIelement[] elements = new UIelement[6]
		{
				new OpLabel(40f, 550f, "Skip mod update confirm", bigText: true),
				new OpCheckBox(onModUpdate, new Vector2(10f, 550f)),
				new OpLabel(40f, 450f, "Skip mod reload confirm", bigText: true),
				new OpCheckBox(onModReload, new Vector2(10f, 450f)),
				new OpLabel(40f, 350f, "Skip game update confirm", bigText: true),
				new OpCheckBox(onGameUpdate, new Vector2(10f, 350f))
		};
		opTab.AddItems(elements);

	}

}

