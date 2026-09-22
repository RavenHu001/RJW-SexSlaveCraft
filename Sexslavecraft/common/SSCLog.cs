using Verse;

// EN: This file is SSC's shared logging gate.
// EN: It routes verbose, important, warning, and error output through the current mod settings so call sites stay clean.
// CN: 这个文件是 SSC 的统一日志闸门。
// CN: 它会根据当前模组设置决定是否输出详细、重要、警告和错误日志，让调用点保持整洁。
namespace SexSlaveCraft
{
    public static class SSCLog
    {
        /// <summary>读取总日志闸门；设置尚未建立时保留启动诊断。</summary>
        public static bool AnyEnabled => SSCMod.settings == null || SSCMod.settings.enableSSCLogs;

        /// <summary>供高频调用点在字符串格式化之前判断详细日志是否开启，避免关闭日志仍产生无用分配。</summary>
        public static bool VerboseEnabled => AnyEnabled && (SSCMod.settings == null || SSCMod.settings.enableSSCVerboseLogs);

        /// <summary>输出受详细日志开关控制的流程追踪；调用点可先检查 VerboseEnabled 再构造诊断文本。</summary>
        public static void Verbose(string message)
        {
            // EN: Verbose is for high-frequency flow tracing like training ticks, ritual phases, and guard debugging.
            // CN: Verbose 用于高频流程追踪，比如调教 Tick、仪式阶段和保护规则调试。
            if (!VerboseEnabled) return;
            Log.Message(message);
        }

        /// <summary>通过总日志与重要日志闸门输出低频关键状态。</summary>
        public static void Important(string message)
        {
            // EN: Important logs are for low-frequency but meaningful state changes, such as ritual enslavement or compatibility fallback.
            // CN: Important 日志用于低频但关键的状态变化，比如仪式转奴或兼容回退。
            if (!AnyEnabled) return;
            if (SSCMod.settings != null && !SSCMod.settings.enableSSCImportantLogs) return;
            Log.Message(message);
        }

        /// <summary>以警告级别输出兼容回退等重要诊断，沿用重要日志开关。</summary>
        public static void WarningImportant(string message)
        {
            // EN: WarningImportant keeps the same low-frequency gate as Important, but escalates the output to a warning.
            // CN: WarningImportant 和 Important 共用同一套低频闸门，但会把输出提升成警告。
            if (!AnyEnabled) return;
            if (SSCMod.settings != null && !SSCMod.settings.enableSSCImportantLogs) return;
            Log.Warning(message);
        }

        /// <summary>始终输出错误，避免关闭普通日志时隐藏必须处理的故障。</summary>
        public static void Error(string message)
        {
            Log.Error(message);
        }
    }
}
