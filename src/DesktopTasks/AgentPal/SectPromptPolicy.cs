// SPDX-License-Identifier: Apache-2.0
using System;

namespace AgentPal
{
    internal static class SectPromptPolicy
    {
        internal const string Discovery = "目录检查与上下文预算：先用内置 Glob/Read/Grep 定向读取适用的 CLAUDE.md、AGENTS.md 和明确相关文件；先确认操作系统、实际 shell 和命令是否存在，Windows 不能假设 ls/head 可用，不反复调用已失败的命令。禁止以 **/* 无差别递归扫描整个目录，特别是 Desktop、用户目录、下载目录或混有大型程序的目录。先只列工作目录第一层，再按任务选择子目录与文件类型；跳过 .git、node_modules、vendor、build、dist、缓存、二进制、SDK、CUDA/include 和第三方依赖树，除非任务明确要求检查其中内容。每次搜索限制结果数量，遇到超过 100 项或工具提示截断时立即收窄路径/模式，不把完整文件清单、头文件或日志堆入上下文。大文件分段读取；交接只给简短结论、文件路径与关键证据。需要使用某个被排除文件时可以定向读取，不得以排除规则遗漏实际任务。";
        internal static string Classify(string text)
        {
            string value = text ?? "";
            foreach (string phrase in new[] { "prompt is too long", "context_length_exceeded", "context length exceeded", "maximum context length", "context window exceeded", "input is too long", "request too large" })
                if (value.IndexOf(phrase, StringComparison.OrdinalIgnoreCase) >= 0) return "context-limit";
            return "";
        }
        internal static string FailureText(string kind)
        { return kind == "context-limit" ? UiLanguage.Text("上下文超限：请收窄扫描范围，使用精简交接重试。") : kind == "cli-error" ? UiLanguage.Text("Claude CLI 返回执行错误，请查看本轮记录。") : kind; }
        internal static string Excerpt(string text, int budget)
        {
            text = text ?? ""; if (budget <= 0) return ""; if (text.Length <= budget) return text;
            if (budget < 100) return UiText.TailCodeUnits(text, budget);
            string marker = "\n[交接内容已精简，完整记录保留在运行历史]\n";
            int head = Math.Min(600, (budget - marker.Length) / 3);
            if (head > 0 && Char.IsHighSurrogate(text[head - 1])) head--;
            return text.Substring(0, head) + marker + UiText.TailCodeUnits(text, budget - head - marker.Length);
        }
    }
}
