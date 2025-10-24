using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Permissions;
using BepInEx;
using BepInEx.Logging;
using Menu;
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

	private static readonly Dictionary<WeakReference<DialogBoxNotify>, float> trackedDialogs = [];

	private static bool ShouldClickNow( DialogBoxNotify dialog ) {

		List<WeakReference<DialogBoxNotify>> referencesToRemove = [];

		foreach ( KeyValuePair<WeakReference<DialogBoxNotify>, float> entry in trackedDialogs ) {
			WeakReference<DialogBoxNotify> reference = entry.Key;
			if ( reference.TryGetTarget(out DialogBoxNotify dialog2) ) {
				if ( dialog.Equals(dialog2) ) {
					foreach ( WeakReference<DialogBoxNotify> reference2 in referencesToRemove )
						trackedDialogs.Remove(reference2);

					float dialogTime = entry.Value;
					if ( float.IsNegativeInfinity(dialogTime) )
						return false;

					float timeUntilClick = dialogTime - 1f / 40f;

					if ( timeUntilClick <= 0.0f ) {
						trackedDialogs[reference] = float.NegativeInfinity;
						return true;
					} else {
						trackedDialogs[reference] = timeUntilClick;
						return false;
					}
				}
			} else
				referencesToRemove.Add(reference);
		}

		foreach ( WeakReference<DialogBoxNotify> reference in referencesToRemove )
			trackedDialogs.Remove(reference);

		float delay = GetSkipDelay();
		if ( delay > 0.0f ) {
			trackedDialogs.Add(new WeakReference<DialogBoxNotify>(dialog), delay);
			return false;
		} else {
			trackedDialogs.Add(new WeakReference<DialogBoxNotify>(dialog), float.NegativeInfinity);
			return true;
		}


	}

	private static bool isEarlyConfig = true;
	private static bool earlySkipModUpdate = true;
	private static bool earlySkipModReload = true;
	private static float earlyDelay = 0.0f;

	private static bool ShouldSkipModUpdate() {
		if ( isEarlyConfig )
			return earlySkipModUpdate;
		return options.onModUpdate.Value;
	}

	private static bool ShouldSkipModReload() {
		if ( isEarlyConfig )
			return earlySkipModReload;
		return options.onModReload.Value;
	}

	private static float GetSkipDelay() {
		if ( isEarlyConfig )
			return earlyDelay;
		return options.delay.Value;
	}

#pragma warning disable IDE0051
	private void OnEnable() {
		Logger = base.Logger;
		options = new RemixOptions(this);

		On.Menu.DialogBoxNotify.Update += OnDialogBoxUpdate;

		On.RainWorld.OnModsInit += OnModsInit;
		On.RainWorld.PostModsInit += OnPostModsInit;

		On.Menu.InitializationScreen.ctor += OnInitializationScreen;
		// DEBUG
		// {
		// 	On.Menu.InitializationScreen.Update +=
		// 		delegate ( On.Menu.InitializationScreen.orig_Update orig, InitializationScreen self ) {
		// 			self.needsRelaunch = true;
		// 			self.filesInBadState = true;

		// 			orig(self);
		// 		};
		// }
	}
#pragma warning restore IDE0051

	private void OnModsInit( On.RainWorld.orig_OnModsInit orig, RainWorld self ) {
		MachineConnector.SetRegisteredOI(PLUGIN_GUID, options);
		orig(self);
	}

	private void OnPostModsInit( On.RainWorld.orig_PostModsInit orig, RainWorld self ) {
		orig(self);
		isEarlyConfig = false;
	}

	private void OnInitializationScreen( On.Menu.InitializationScreen.orig_ctor orig, InitializationScreen self, ProcessManager manager ) {
		orig(self, manager);
		options.config.GetConfigPath();
		string path = Path.Combine(OptionInterface.ConfigHolder.configDirPath, PLUGIN_GUID + ".txt");
		if ( File.Exists(path) ) {
			var lines = File.ReadLines(path);
			foreach ( string rawLine in lines ) {
				string line = rawLine.Trim();
				if ( line.StartsWith("#") )
					continue;

				string[] words = line.Split(['='], 2);
				if ( words.Length != 2 )
					continue;

				string key = words[0].Trim();
				string value = words[1].Trim();
				switch ( key ) {
					case "onModUpdate":
						try {
							earlySkipModUpdate = bool.Parse(value);
						} catch { }
						break;
					case "onModReload":
						try {
							earlySkipModReload = bool.Parse(value);
						} catch { }
						break;
					case "delay":
						try {
							earlyDelay = float.Parse(value);
						} catch { }
						break;
				}
			}
		}
	}

	private bool ShouldAutoConfirm( DialogBoxNotify self ) {
		return !self.continueButton.buttonBehav.greyedOut &&
				( self.menu is InitializationScreen || self.menu is ModdingMenu ) &&
				 ( ShouldSkipModUpdate() && self.continueButton.signalText == "REAPPLY"
				|| ( ShouldSkipModReload() && self.continueButton.signalText == "RESTART" ) );
	}

	private void OnDialogBoxUpdate( On.Menu.DialogBoxNotify.orig_Update orig, DialogBoxNotify self ) {
		orig(self);
		if ( ShouldAutoConfirm(self) && ShouldClickNow(self) ) {
			self.continueButton.Clicked();
		}
	}

}


internal class RemixOptions : OptionInterface {

	public Configurable<bool> onModUpdate;
	public Configurable<bool> onModReload;
	public Configurable<float> delay;


#pragma warning disable IDE0060
	public RemixOptions( NoModUpdateConfirm nmuc ) {
		onModUpdate = config.Bind("onModUpdate", true);
		onModReload = config.Bind("onModReload", true);
		delay = config.Bind("delay", 0.0f, new ConfigAcceptableRange<float>(0.0f, 5.0f));
	}
#pragma warning restore IDE0060

	public override void Initialize() {

		OpTab opTab = new(this, "Tab");
		Tabs = [opTab];
		UIelement[] elements =
		[
			new OpLabel(new Vector2(150f, 520f), new Vector2(300f, 30f), "No Mod Update Confirm", FLabelAlignment.Center, bigText: true),
			new OpLabel(60f, 460f, "Skip mod update confirm"),
			new OpCheckBox(onModUpdate, new Vector2(10f, 460f)) {
				description = "Skips the confirm dialog after updating the mods on the first load screen"
			},
			new OpLabel(60f, 430f, "Skip mod reload confirm"),
			new OpCheckBox(onModReload, new Vector2(10f, 430f)) {
				description = "Skips the confirm dialog after applying changes in the remix menu"
			},
			new OpLabel(120f, 400f, "Delay"),
			new OpUpdown(delay, new Vector2(10f, 395f), 100) {
				description = "Turn this up if the game closes before changes can be written"
			}
		];
		opTab.AddItems(elements);

	}

}

