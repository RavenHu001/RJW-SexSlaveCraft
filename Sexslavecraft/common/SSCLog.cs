using Verse;

// EN: This file is SSC's shared logging gate.
// EN: It routes verbose, important, warning, and error output through the current mod settings so call sites stay clean.
// CN: 这个文件是 SSC 的统一日志闸门。
// CN: 它会根据当前模组设置决定是否输出详细、重要、警告和错误日志，让调用点保持整洁。
namespace SexSlaveCraft
{
    public static class SSCLog
    {
        public static bool AnyEnabled => SSCMod.settings == null || SSCMod.settings.enableSSCLogs;

        public static void Verbose(string message)
        {
            // EN: Verbose is for high-frequency flow tracing like training ticks, ritual phases, and guard debugging.
            // CN: Verbose 用于高频流程追踪，比如调教 Tick、仪式阶段和保护规则调试。
            if (!AnyEnabled) return;
            if (SSCMod.settings != null && !SSCMod.settings.enableSSCVerboseLogs) return;
            Log.Message(message);
        }

        public static void Important(string message)
        {
            // EN: Important logs are for low-frequency but meaningful state changes, such as ritual enslavement or compatibility fallback.
            // CN: Important 日志用于低频但关键的状态变化，比如仪式转奴或兼容回退。
            if (!AnyEnabled) return;
            if (SSCMod.settings != null && !SSCMod.settings.enableSSCImportantLogs) return;
            Log.Message(message);
        }

        public static void WarningImportant(string message)
        {
            // EN: WarningImportant keeps the same low-frequency gate as Important, but escalates the output to a warning.
            // CN: WarningImportant 和 Important 共用同一套低频闸门，但会把输出提升成警告。
            if (!AnyEnabled) return;
            if (SSCMod.settings != null && !SSCMod.settings.enableSSCImportantLogs) return;
            Log.Warning(message);
        }

        public static void Error(string message)
        {
            Log.Error(message);
        }
    }
}
