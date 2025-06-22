using System;

namespace Celeste.Mod.FlaksHelper;

public class FlaksHelperModule : EverestModule {
    public static FlaksHelperModule Instance { get; private set; }

    public override Type SettingsType => typeof(FlaksHelperModuleSettings);
    public static FlaksHelperModuleSettings Settings => (FlaksHelperModuleSettings) Instance._Settings;

    public override Type SessionType => typeof(FlaksHelperModuleSession);
    public static FlaksHelperModuleSession Session => (FlaksHelperModuleSession) Instance._Session;

    public override Type SaveDataType => typeof(FlaksHelperModuleSaveData);
    public static FlaksHelperModuleSaveData SaveData => (FlaksHelperModuleSaveData) Instance._SaveData;

    public FlaksHelperModule() {
        Instance = this;
#if DEBUG
        // debug builds use verbose logging
        Logger.SetLogLevel(nameof(FlaksHelperModule), LogLevel.Verbose);
#else
        // release builds use info logging to reduce spam in log files
        Logger.SetLogLevel(nameof(FlaksHelperModule), LogLevel.Info);
#endif
    }

    public override void Load() {
        // TODO: apply any hooks that should always be active
    }

    public override void Unload() {
        // TODO: unapply any hooks applied in Load()
    }
}