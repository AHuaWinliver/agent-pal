// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace AgentPal
{
    internal static class UiLanguage
    {
        private static string selected;
        internal static event Action Changed;
        internal static string Current
        {
            get
            {
                if (selected != null) return selected;
                try { selected = File.ReadAllText(StoragePath).Trim(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                if (selected != "en" && selected != "zh-CN") selected = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh" ? "zh-CN" : "en";
                return selected;
            }
        }
        internal static bool IsEnglish { get { return Current == "en"; } }
        private static string StoragePath
        {
            get
            {
                string test = Environment.GetEnvironmentVariable("AGENT_PAL_STATE_DIR");
                if (Environment.GetEnvironmentVariable("AGENT_PAL_TEST_MODE") == "1" && !String.IsNullOrEmpty(test) && Path.IsPathRooted(test)) return Path.Combine(test, "ui-language.txt");
#if AGENT_PAL_LINUX
                string root = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                if (String.IsNullOrEmpty(root) || !Path.IsPathRooted(root)) root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
                return Path.Combine(ExistingDataDirectory(root, "xianpal"), "ui-language.txt");
#else
                string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(ExistingDataDirectory(root, "XianPal"), "ui-language.txt");
#endif
            }
        }

        // Keep existing profiles in place so both upgrades and rollback see the
        // same settings. Fresh installations use the branded path.
        internal static string ExistingDataDirectory(string root, string legacyName)
        {
            string current = Path.Combine(root, "agent-pal");
            string legacy = Path.Combine(root, legacyName);
            return !Directory.Exists(current) && Directory.Exists(legacy) ? legacy : current;
        }

        internal static void Set(string value)
        {
            if (value != "en" && value != "zh-CN") throw new ArgumentException("Supported languages: en, zh-CN.");
            Directory.CreateDirectory(Path.GetDirectoryName(StoragePath));
            string temporary = StoragePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, value, new UTF8Encoding(false));
                if (File.Exists(StoragePath)) File.Replace(temporary, StoragePath, null); else File.Move(temporary, StoragePath);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            selected = value;
            if (Changed != null) Changed();
        }

        internal static string Text(string source)
        {
            if (String.IsNullOrEmpty(source)) return source ?? "";
            string translated;
            if (IsEnglish) return Translations.TryGetValue(source, out translated) ? translated : source;
            return Reverse.TryGetValue(source, out translated) ? translated : source;
        }

        internal static string Format(string source, params object[] values)
        { return String.Format(IsEnglish ? CultureInfo.GetCultureInfo("en") : CultureInfo.GetCultureInfo("zh-CN"), Text(source), values); }

        internal static bool IsCaption(string source)
        { return source != null && (Translations.ContainsKey(source) || Reverse.ContainsKey(source)); }

        private static Dictionary<string, string> BuildReverse()
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in Translations) if (!values.ContainsKey(pair.Value)) values.Add(pair.Value, pair.Key);
            return values;
        }

        private static readonly Dictionary<string, string> Translations = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "外部会话需已有联动桥接；此开关只接收事件，不安装 Hooks，也不修改 Claude 配置。", "External sessions need an existing hook bridge. This switch only accepts events; it never installs hooks or edits Claude settings." },
            { "伙伴管理", "Manage companions" }, { "切换伙伴", "Companions" }, { "使用此伙伴", "Use companion" }, { "删除已导入伙伴", "Remove" },
            { "当前", "Current" }, { "恢复默认", "Restore defaults" },
            { "提醒内容", "Reminder text" },
            { "小白龙", "Bailong" },
            { "打开 AgentPal", "Open AgentPal" }, { "正在完成安装…", "Finalizing installation…" },
            { "认识六位伙伴", "Meet your six pals" }, { "有伙伴的对话", "Conversation, with company" }, { "在任意文件夹指派任务", "Assign work from any folder" },
            { "任务、提醒与倒计时", "Tasks, reminders and countdowns" }, { "Agent 协作工作区", "An Agent workspace" },
            { "六位内置伙伴，共用一个工作空间。", "Six built-in companions. One shared workspace." },
            { "气泡对话、结构化回复，以及可展开的工具过程。", "Chat bubbles, structured replies and expandable tool activity." },
            { "选择伙伴或团队，输入任务，以当前文件夹开始工作。", "Choose a companion or team, enter a task, and use the current folder." },
            { "选择执行伙伴和星期、时间，任务与提醒均可随时修改。", "Pick a companion, choose days and times, and edit tasks or reminders later." },
            { "查看成员状态、任务依赖，以及成员之间的交接过程。", "Follow member status, task dependencies and recorded handoffs." },
            { "执行伙伴", "Companion" },
            { "伙伴专属配置", "Companion override" }, { "未找到 Claude 程序", "Claude executable not found" }, { "Claude 默认配置（未指定文件）", "Claude defaults (no settings file specified)" },
            { "添加提醒", "Add reminder" }, { "修改提醒", "Edit reminder" }, { "提醒已不存在，请重新选择。", "This reminder no longer exists. Select it again." },
            { "修改任务", "Edit task" }, { "选择任务后点击修改任务；执行结果在运行情况中查看。", "Select a task and choose Edit task. Results are in Run history." },
            { "轨迹配色…", "Trail colors…" }, { "轨迹配色", "Trail colors" }, { "已导入伙伴", "Imported companion" }, { "内置伙伴", "Built-in companion" },
            { "删除“{0}”的已导入资源？对话记录、任务与配置会保留。", "Delete the imported resources for “{0}”? Conversations, tasks and settings will be kept." },
            { "轨迹配色按伙伴保存，刷新资源或重启后仍然保留。", "Trail colors are saved for this companion and kept across resource refreshes and restarts." },
            { "内置伙伴受到保护，不能删除。可删除的已导入伙伴排列在列表末尾。", "Built-in companions cannot be deleted. Imported companions appear at the end of the list." },
            { "内置伙伴不能删除。", "Built-in companions cannot be deleted." },
            { "点击色块选择颜色；配色会独立保存到当前伙伴。", "Pick a color. Saved for this companion." },
            { "连接异常，正在重试", "Connection issue · retrying" }, { "执行失败", "Request failed" },
            { "Claude CLI 已启动，等待响应…", "Claude CLI started · waiting for a response…" },
            { "孙悟空", "Sun Wukong" }, { "行者悟空", "Wayfarer Wukong" },
            { "发送Agent Pal任务", "Send Agent Pal task" },
            { "青衡", "Qingheng" }, { "绯笺", "Feijian" }, { "苍芷", "Cangzhi" }, { "小墨", "Xiaomo" },
            { "默认设置", "Default settings" }, { "常规", "General" }, { "Agent", "Agents" }, { "外观", "Appearance" }, { "行为", "Behavior" },
            { "语言", "Language" }, { "全局默认", "Global defaults" }, { "沿用全局配置", "Inherit global settings" },
            { "工作目录", "Working directory" }, { "Claude 默认工作目录", "Default Claude working directory" },
            { "Claude 配置文件", "Claude settings file" }, { "Claude 可执行文件", "Claude executable" },
            { "浏览…", "Browse…" }, { "选择目录…", "Choose folder…" }, { "选择 JSON…", "Choose JSON…" },
            { "保存", "Save" }, { "取消", "Cancel" }, { "应用", "Apply" }, { "关闭", "Close" }, { "隐藏", "Hide" },
            { "保持置顶", "Keep on top" }, { "减少动画", "Reduce motion" }, { "开机自动启动", "Start at login" },
            { "贴边不透明度", "Docked opacity" }, { "光轨透明度", "Trail opacity" }, { "伙伴大小", "Companion size" },
            { "重置大小和位置", "Reset size and position" }, { "虚拟桌面显示范围", "Virtual desktop visibility" },
            { "仅在桌面 1 显示", "Desktop 1 only" }, { "在所有桌面显示", "All virtual desktops" },
            { "全屏跑动路线", "Roaming routes" }, { "内置随机曲线", "Built-in random curves" },
            { "Claude AI 路线池（一次生成24条）", "AI route pool (24 routes per request)" },
            { "AI 主动关怀", "Proactive care" }, { "允许主动说话与休息提醒", "Allow proactive messages and break reminders" },
            { "收集其他 Claude 会话（默认关闭）", "Monitor external Claude sessions (off by default)" },
            { "收集其他 Claude 会话", "Monitor other Claude sessions" }, { "自定义大小…", "Custom size…" },
            { "执行 Agent", "Run as Agent" }, { "全部 Agent 任务", "All Agents' tasks" }, { "已保存任务", "Saved tasks" },
            { "Claude Code 定时任务", "Claude Code scheduled tasks" }, { "Claude Code 定时任务…", "Claude Code scheduled tasks…" },
            { "定时任务", "Scheduled tasks" },
            { "执行任务", "Prompt" }, { "运行情况", "Run history" }, { "任务运行情况", "Task run history" },
            { "目录", "Folder" }, { "目录：", "Folder: " }, { "运行桌面", "Desktop" }, { "当前桌面", "Current desktop" }, { "桌面 {0}", "Desktop {0}" },
            { "设为新任务默认", "Use as default" }, { "虚拟桌面（Win+Tab），需提前创建", "Create virtual desktops first (Win+Tab)." },
            { "PowerShell 窗口", "PowerShell" }, { "终端窗口", "Terminal" }, { "CLI 交互·PS", "CLI · PowerShell" },
            { "CLI 交互·终端", "CLI · Terminal" }, { "Claude BG·关窗", "Claude BG · close window" },
            { "任务由本机 claude CLI 执行；电脑休眠期间不会唤醒，恢复后补执行到期任务。", "Uses your local Claude CLI. Missed tasks run after the computer wakes." },
            { "Claude 原生 Background：创建成功后关窗；阻塞等待后台完成，需已支持 --bg 的 CLI。", "Native Claude BG: closes the launcher after dispatch. Requires a CLI with --bg support." },
            { "原生 CLI 可继续追问及确认权限；阻塞模式等待此 CLI 会话退出，不自动关窗。", "Interactive CLI supports follow-ups and permissions. Runs stay active until the session exits." },
            { "状态", "Status" }, { "方式", "Mode" }, { "开始时间", "Started" }, { "完成时间", "Finished" }, { "耗时", "Duration" }, { "退出码", "Exit code" },
            { "运行情况  ·  双击记录查看输出", "Run history · Double-click a run to view output" }, { "保存修改", "Save changes" },
            { "打开原生后台会话", "Open background session" }, { "定时任务运行详情", "Task transcript" },
            { "在 Claude CLI 打开", "Open in Claude CLI" },
            { "后台输出", "Background output" }, { "后台运行", "Background output" }, { "原生 CLI 终端", "Native CLI terminal" },
            { "上次未完成则跳过", "Skip while previous run is active" }, { "允许并行", "Allow parallel runs" },
            { "阻塞模式", "Skip overlapping runs" }, { "并行模式", "Allow parallel runs" },
            { "每天", "Daily" }, { "每周", "Weekly" }, { "固定间隔", "Interval" }, { "每隔一段时间", "Interval" },
            { "周一至周五", "Monday–Friday" }, { "周六至周日", "Saturday–Sunday" }, { "自选星期", "Selected days" },
            { "选择星期…", "Choose days…" }, { "请选择至少一个星期。", "Select at least one day of the week." },
            { "确定", "OK" },
            { "重复", "Repeat" }, { "每{0}", "Every {0}" }, { "定时提醒", "Scheduled reminders" },
            { "提示框", "Popup" }, { "演出", "Performance" }, { "已停用", "Disabled" },
            { "周日", "Sunday" }, { "周一", "Monday" }, { "周二", "Tuesday" }, { "周三", "Wednesday" },
            { "周四", "Thursday" }, { "周五", "Friday" }, { "周六", "Saturday" },
            { "分钟", "Minutes" }, { "小时", "Hours" }, { "天", "Days" },
            { "添加任务", "Add task" }, { "保存任务", "Save task" }, { "新任务", "New task" },
            { "立即执行", "Run now" }, { "停用", "Disable" }, { "启用", "Enable" }, { "删除", "Delete" }, { "编辑", "Edit" },
            { "启用 / 停用", "Enable / disable" }, { "运行中", "Running" }, { "正在运行", "Running" },
            { "已完成", "Completed" }, { "失败", "Failed" }, { "已跳过", "Skipped" }, { "需要输入", "Input needed" }, { "就绪", "Ready" },
            { "对话", "Chat" }, { "发送", "Send" }, { "停止", "Stop" }, { "新会话", "New conversation" },
            { "会话 ▾", "History ▾" }, { "AgentPal · 对话", "AgentPal · Chat" }, { "正在回复", "Replying" },
            { "选择会话后直接回复…", "Select a conversation to reply…" }, { "回复所选 Claude 会话…", "Reply to this Claude conversation…" },
            { "该动态尚未取得会话 ID", "A session ID is not available yet" },
            { "新建文字对话    Win+Alt+P", "New conversation    Win+Alt+P" }, { "查看动态", "Activity" },
            { "切换形象", "Companions" }, { "打开 Claude Code CLI", "Open Claude Code CLI" },
            { "在 PowerShell 打开 Claude Code CLI", "Open Claude Code in PowerShell" },
            { "查看 Claude 动态", "Claude activity" }, { "Claude Code 动态", "Claude Code activity" },
            { "历史上的今天", "On this day" }, { "提醒与倒计时", "Reminders and countdowns" },
            { "提醒与倒计时…", "Reminders and countdowns…" }, { "每日提醒", "Daily reminders" },
            { "角色演绎", "Character performance" }, { "仅提示框", "Popup only" }, { "立即测试效果", "Preview effect" },
            { "添加", "Add" }, { "知道了", "Dismiss" }, { "稍后 5 分钟", "Snooze 5 minutes" },
            { "倒计时预设（分钟）", "Countdown presets (minutes)" }, { "开始自定义倒计时", "Start custom countdown" },
            { "5 分钟", "5 min" }, { "10 分钟", "10 min" }, { "15 分钟", "15 min" },
            { "30 分钟", "30 min" }, { "1 小时", "1 hr" }, { "2 小时", "2 hr" },
            { "角色全屏演出", "Performance" },
            { "{0} 分钟倒计时已开始\n{1} 到点", "{0}-minute timer started\nDue {1}" },
            { "{0} 到点", "Due {0}" }, { "剩余 {0}:{1:00}:{2:00}", "Left {0}:{1:00}:{2:00}" },
            { "剩余 {0:00}:{1:00}", "Left {0:00}:{1:00}" },
            { "没有进行中的倒计时", "No active countdown" }, { "示例：起来活动一下", "Example: take a movement break" },
            { "事件规划 / Agent 协作", "Events and Agent collaboration" }, { "事件规划 / Agent 协作…", "Events and Agent collaboration…" },
            { "Agent 工作区", "Agent workspace" }, { "成员与任务配置", "Members and tasks" }, { "事件规划", "Event planning" },
            { "交接记录", "Handoffs" }, { "打开角色资源目录", "Open character pack folder" },
            { "刷新外置角色", "Reload character packs" }, { "刷新外置角色 / 查看校验结果", "Reload character packs / validation" },
            { "刷新角色资源", "Refresh character packs" }, { "{0}（外置）", "{0} (custom)" },
            { "导入角色包…", "Import character pack…" }, { "角色包 (*.zip)|*.zip", "Character packs (*.zip)|*.zip" },
            { "正在检查 ZIP 角色包…", "Checking the ZIP character pack…" },
            { "安装并切换", "Install and switch" }, { "替换并切换", "Replace and switch" },
            { "已经安装了同一角色。继续将替换它的资源。", "This character is already installed. Continuing will replace its resources." },
            { "正在安装角色…", "Installing character…" }, { "无法准备角色包。", "Could not prepare the character pack." },
            { "导入角色合集", "Import character collection" }, { "角色", "Character" }, { "操作", "Action" },
            { "正在逐个校验合集中的角色…", "Validating every character in the collection…" },
            { "安装全部", "Install all" }, { "安装全部并替换", "Install all and replace existing" },
            { "替换已有角色", "Replace existing" }, { "新增角色", "Add character" },
            { "已校验 {0} 个角色。安装后当前形象不变。", "Validated {0} characters. Your current selection stays the same." },
            { "标记为替换的角色将覆盖原资源。", "Characters marked for replacement will overwrite their existing resources." },
            { "合集 ZIP 必须包含多个完整角色文件夹，不能包含额外文件或目录。", "The ZIP must contain complete character folders with no extra files or directories." },
            { "合集 ZIP 最多支持 16 个角色、512 MB。", "A collection can contain up to 16 characters and 512 MB of data." },
            { "安装后将超过 16 个外置角色，请先移出部分角色。", "Installing this collection would exceed 16 external characters. Remove some first." },
            { "请确认替换已有角色。", "Confirm replacement of existing characters first." },
            { "已安装的角色发生变化，请重新打开合集。", "Installed characters changed. Reopen the collection before installing." },
            { "无法完全恢复原角色，请检查保留的暂存目录：{0}", "Rollback could not complete. Check the preserved staging folders: {0}" },
            { "角色包中的角色 ID 无效或已被保留。", "The character ID is invalid or reserved." },
            { "ZIP 角色包过大。", "The ZIP character pack is too large." },
            { "ZIP 角色包中的文件过大。", "A file in the ZIP character pack is too large." },
            { "ZIP 角色包不完整。", "The ZIP character pack is incomplete." },
            { "ZIP 应包含一个完整角色。发现多余文件、重复路径或不支持的目录结构。", "The ZIP must contain one complete character. Extra files, duplicate paths, or an unsupported layout were found." },
            { "预览后资源已变化，请重新打开角色包。", "The resources changed after preview. Open the character pack again." },
            { "同名角色已存在，请重新预览后选择是否替换。", "A character with this ID already exists. Preview the pack again to replace it." },
            { "外置角色已达到 16 个，请先移出一个角色。", "The 16 custom character slots are full. Remove a character first." },
            { "无法恢复原角色，原文件仍在 {0}", "Could not restore the previous character. Its files are still at {0}" },
            { "角色已写入资源目录，但未能切换，请查看刷新结果。", "The character was written to the resource folder but could not be selected. Check the refresh results." },
            { "从图片创建…", "Create from images…" }, { "角色名", "Character name" },
            { "正面", "Front" }, { "左侧", "Left-facing" }, { "右侧", "Right-facing" },
            { "向左移动", "Moving left" }, { "向右移动", "Moving right" },
            { "选择图片…", "Choose image…" }, { "选择{0}图片", "Choose image · {0}" },
            { "下一步：预览", "Next: preview" },
            { "图片 (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg", "Images (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg" },
            { "选择同一双足角色的三张全身图，尽量露出手腕和鞋。支持透明或纯色背景的 PNG、JPEG。", "Choose three full-body views of the same biped, with visible wrists and shoes where possible. Use PNG or JPEG with a transparent or solid background." },
            { "正在制作角色动作…", "Creating character animations…" },
            { "飞行外观", "Flight artwork" }, { "飞行预览", "Flight preview" }, { "清除", "Clear" },
            { "玄翼", "Mystic wings" }, { "灵器", "Spirit vessel" },
            { "人物描述", "Character description" }, { "人物描述（可选）", "Character description (optional)" },
            { "人物描述最多 4000 个字符。", "Character descriptions can be up to 4,000 characters." },
            { "选择玄翼图片…", "Choose wings image…" }, { "选择灵器图片…", "Choose vessel image…" },
            { "恢复角色飞行外观", "Restore character flight artwork" }, { "可选 · 使用默认飞行外观", "Optional · Use default flight artwork" },
            { "请输入 1 到 20 个字符的角色名。", "Enter a character name of 1 to 20 characters." },
            { "请选择不超过 16 MB、最长边不超过 4096 像素的 PNG 或 JPEG。", "Choose a PNG or JPEG up to 16 MB and 4096 pixels on its longest edge." },
            { "图片中找不到完整的角色轮廓。", "The image does not contain a usable character silhouette." },
            { "请使用透明背景或纯色背景的全身图。", "Use a full-body view with a transparent or solid background." },
            { "生成的角色包过大，请使用细节较少的图片。", "The generated character pack is too large. Try images with less detail." },
            { "友好、好奇的桌面伙伴，回答清楚，给出有用的下一步。", "A friendly, curious desktop companion. Answer clearly and suggest a useful next step." },
            { "今天一起做点有趣的事吧。", "Let's try something interesting today." },
            { "我在这里。", "I'm right here." }, { "累了就休息一下。", "Take a break when you need one." },
            { "我们一步一步来。", "Let's take it one step at a time." }, { "好，准备出发。", "Ready when you are." },
            { "角色资源目录：", "Character pack folder:" }, { "外置角色资源", "Custom character packs" },
            { "可用外置角色：", "Available custom characters:" }, { "以下资源未能应用：", "Packs with validation errors:" },
            { "在“切换形象”中选择角色。", "Choose the character from the Companions menu." },
            { "请将包含 character.json 的完整角色文件夹复制到此目录，然后刷新角色资源。", "Copy the complete character folder containing character.json here, then refresh character packs." },
            { "显示伙伴", "Show companion" }, { "隐藏伙伴", "Hide companion" }, { "退出AgentPal", "Quit AgentPal" }, { "退出", "Quit" },
            { "跑一会儿", "Roam for a while" }, { "暂停 Claude 联动", "Pause Claude integration" },
            { "关于 AgentPal", "About AgentPal" }, { "Claude Code：已连接", "Claude Code: connected" },
            { "任务输出在「运行情况」查看", "Task output is available in Run history" },
            { "你", "You" }, { "角色大小", "Companion size" },
            { "AgentPal · Claude Code", "AgentPal · Claude Code" },
            { "休息提醒间隔（分钟）", "Break reminder interval (minutes)" },
            { "留空时继承全局值；全局留空时使用 Claude 默认配置。", "Leave a field empty to inherit the global value. Empty global fields use Claude's defaults." },
            { "留空时使用全局程序或 PATH", "Leave empty to use the global executable or PATH" },
            { "工作目录不存在。", "Working directory does not exist." }, { "设置未保存", "Settings were not saved" },
            { "安装目录过于宽泛，请选择独立应用目录。", "Choose a dedicated application folder, not a system or user root." },
            { "安装路径不能包含目录链接。", "The installation path cannot contain directory links." },
            { "此目录不属于本应用，请选择一个空目录。", "This folder contains unrelated files. Choose an empty application folder." },
            { "AgentPal安装程序", "AgentPal Setup" }, { "安装目录", "Installation folder" }, { "安装", "Install" },
            { "用于本机 Claude Code 工作流的动画桌面伴侣。", "An animated desktop companion for your local Claude Code workflow." },
            { "仅为当前用户安装，使用你自己的 Claude Code 配置；不捆绑模型或凭据。", "Installs for the current user. Uses your own Claude Code configuration; no model or credentials are bundled." },
            { "已检测到 Claude Code CLI。", "Claude Code CLI detected." },
            { "未检测到 Claude Code CLI，可在安装后配置。", "Claude Code CLI was not detected. You can configure it after installation." },
            { "正在安装AgentPal…", "Installing agent-pal…" }, { "安装失败。", "Installation failed." },
            { "正在准备安装…", "Preparing installation…" }, { "正在解压文件…", "Extracting files…" },
            { "正在校验动画资源…", "Validating animation resources…" }, { "正在检查已有文件…", "Checking existing files…" },
            { "正在安装已校验文件…", "Installing verified files…" }, { "正在验证安装后的程序…", "Verifying installed application…" },
            { "正在创建快捷方式并配置集成…", "Creating shortcuts and configuring integration…" },
            { "成员状态 · {0} 位", "Members · {0}" }, { "派发已暂停", "Dispatch paused" }, { "已交接", "Handed off" },
            { "成员状态", "Member status" },
            { "需要处理", "Needs input" },
            { "排队中", "Queued" },
            { "直接显示在 Windows 11 主菜单", "Show in the Windows 11 main menu" },
            { "Windows 11 主菜单扩展需要 64 位 Windows 11。", "The native main-menu extension requires 64-bit Windows 11." },
            { "缺少原生菜单扩展，请安装包含 shell 组件的新版安装包。", "The native menu component is missing. Install a build that includes the shell component." },
            { "原生菜单注册未完成，请查看系统授权或重试。", "Native menu registration did not finish. Check system approval and retry." },
            { "主菜单最多支持 {0} 个伙伴任务入口。", "The main menu supports up to {0} companion task entries." },
            { "Windows 11 主菜单需要安装 agent-pal 签名扩展。此预览使用专用测试证书，将仅加入本机“受信任的人”（不加入根证书），需要管理员授权。是否继续？", "The Windows 11 main menu requires agent-pal's signed extension. This preview uses a dedicated test certificate in this computer's Trusted People store, never Root. Administrator approval is required. Continue?" },
            { "正在注册并校验右键菜单…", "Registering and checking the context menu…" },
            { "主菜单扩展已注册，入口会直接显示在 Windows 11 菜单中。", "The native extension is registered for the Windows 11 main menu." },
            { "兼容菜单已注册。Windows 11 中位于“显示更多选项”。", "Compatibility entries are registered under Show more options on Windows 11." },
            { "菜单注册失败：", "Menu registration failed: " },
            { "菜单注册失败。点击查看详细原因。", "Menu registration failed. Click for details." },
            { "保存命令后会注册并验证所选菜单模式。", "Save to register and verify the selected menu mode." },
            { "主菜单扩展等待系统授权，请在默认设置中保存并更新快捷任务。", "The native menu needs system approval. Open Default settings and save/update folder quick tasks." },
            { "右键菜单尚未完成注册，请在默认设置中保存并更新快捷任务。", "Context-menu registration is incomplete. Open Default settings and save/update folder quick tasks." },
            { "原生菜单校验失败：显示内容与当前伙伴绑定不一致。", "Native menu check failed: the displayed command does not match the current companion binding." },
            { "原生菜单校验失败：入口未启用。", "Native menu check failed: the command is not enabled." },
            { "已移除右键菜单，命令定义已保留。", "Context-menu entries removed. Command definitions are retained." },
            { "已交给 {0}", "Handed off to {0}" }, { "暂无执行输出", "No output yet" },
            { "交接中", "Handing off" }, { "工位巡视", "Walking" }, { "翻阅报告", "Reading reports" },
            { "上下文超限：请收窄扫描范围，使用精简交接重试。", "Context limit reached. Narrow the search and retry with a compact handoff." },
            { "Claude CLI 返回执行错误，请查看本轮记录。", "Claude CLI returned an execution error. Open this run for details." },
            { "AgentPal已安装。请从伙伴菜单配置 Claude Code。", "agent-pal is installed. Open the companion menu to configure Claude Code." },
            { "桌面陪伴", "Desktop companionship" }, { "协调步态", "Coordinated movement" }, { "文件互动", "File interactions" },
            { "五位内置伙伴，拥有丰富的动画和表情。", "Five built-in companions with expressive animations." },
            { "金箍棒与筋斗云", "Staff and cloud" },
            { "角色特色演绎", "Character performances" },
            { "已内置：自然行走、挥棍转圈与筋斗云。", "Built in: walking, staff spinning, and a flying cloud." },
            { "Agent + Pal：能力与陪伴。\nAP 与 >_ 融为一个以终端为内核的小精灵。", "Agent + Pal: capability with companionship.\nAP and >_ form a little pal with a terminal at heart." },
            { "平滑的步伐、转向及专属角色动作。", "Smooth steps, turns, and character-specific motion." },
            { "拖入文件后，在对话中确认处理需求。", "Drop files, then confirm your request in chat." },
            { "提醒和互动采用角色特色动画。", "Distinct animations for reminders and interactions." },
            { "动态", "Activity" }, { "提醒", "Reminders" }, { "●  本机 CLI", "●  Local CLI" },
            { "询问 Claude Code", "Ask Claude Code" }, { "询问 Claude Code…", "Ask Claude Code…" },
            { "Enter 发送 · Shift+Enter 换行", "Enter to send · Shift+Enter for a new line" },
            { "选择一条动态查看 Claude 回复", "Select a conversation to view Claude's reply" },
            { "Claude Code 定时任务运行详情", "Claude Code task transcript" },
            { "当前没有倒计时", "No active countdown" }, { "到点后，当前 AgentPal会来到屏幕中间", "Your companion moves to the center of this screen when the timer ends." },
            { "倒计时状态异常", "Countdown unavailable" }, { "开始", "Start" }, { "自定义", "Custom" }, { "提醒我做什么…", "What should I remind you to do?" },
            { "我知道了", "Dismiss" }, { "等待 Claude 回复…", "Waiting for Claude…" },
            { "AgentPal · 事件规划与 Agent 协作", "AgentPal · Events and Agent collaboration" },
            { "AgentPal · 事件规划与 Agent 工作区", "AgentPal · Events and Agent workspace" },
            { "确认并执行协作", "Confirm and run" }, { "停止本轮", "Stop run" }, { "人物形象", "Character" },
            { "下表：任务与依赖", "Tasks and dependencies" },
            { "成员定义", "Team members" }, { "新增成员", "Add member" }, { "删除成员", "Remove member" },
            { "新增任务", "Add task" }, { "删除协作任务", "Remove task" },
            { "任务与依赖", "Tasks and dependencies" }, { "编辑成员", "Edit member" }, { "编辑任务", "Edit task" },
            { "执行方式", "Execution mode" }, { "自动（可修改工作区）", "Auto (workspace writes allowed)" },
            { "只读，可并行", "Read-only; parallel allowed" }, { "仅写隔离输出，可并行", "Isolated outputs; parallel allowed" },
            { "独占串行", "Exclusive serial" }, { "任务执行方式无效。", "Invalid task execution mode." },
            { "暂停派发", "Pause dispatch" }, { "继续派发", "Resume dispatch" }, { "重试失败步骤", "Retry failed steps" },
            { "已暂停派发", "Dispatch paused" }, { "上游步骤未成功", "Upstream step did not succeed" },
            { "等待并发工位", "Waiting for a free worker" }, { "等待工作目录或执行成员空闲", "Waiting for the workspace or member" },
            { "并发工位", "Worker slots" },
            { "执行过程 · {0} 条", "Process · {0} events" }, { "复制整条消息", "Copy original message" },
            { "右键快捷任务", "Folder quick tasks" }, { "管理右键快捷任务…", "Manage folder quick tasks…" },
            { "当前文件夹", "Current folder" }, { "任务命令", "Task command" },
            { "预览菜单", "Preview menu" }, { "这是菜单预览。请在目标文件夹右键来指派任务。", "This is a menu preview. Right-click the target folder to assign a task." },
            { "Ctrl+Enter 确定 · Enter 换行", "Ctrl+Enter to confirm · Enter for a new line" },
            { "在当前路径给{0}指派任务", "Assign a task to {0} here" }, { "给{0}指派任务", "Assign a task to {0}" },
            { "指派任务", "Assign task" },
            { "新增命令", "Add command" }, { "删除命令", "Remove command" }, { "快捷命令名称", "Command name" },
            { "预填任务（可留空，点击菜单后输入）", "Prefilled task (optional; editable after clicking)" },
            { "绑定伙伴（可多选，每位伙伴直接显示为一个菜单项）", "Companions (select one or more; each gets a direct menu item)" },
            { "桌面右键", "Desktop menu" }, { "文件夹与空白处右键", "Folder and background menus" },
            { "保存并更新右键菜单", "Save and update menus" }, { "移除右键菜单", "Remove menus" },
            { "右键当前文件夹，选择带伙伴缩略图的任务入口。输入任务并确认后，将打开该伙伴绑定的原生 Claude CLI。", "Right-click a folder and choose a companion with its thumbnail. Enter a task and confirm to open that companion's native Claude CLI." },
            { "使用该伙伴绑定的 Claude 程序和 settings，在下方目录打开原生 CLI。", "Opens native CLI here with this companion's Claude executable and settings." },
            { "右键菜单已更新。Windows 11 中可在“显示更多选项”中找到。", "Menus updated. On Windows 11, look under Show more options." },
            { "最多设置 20 条快捷命令。", "You can create up to 20 quick commands." },
            { "协作团队 · {0}", "Agent team · {0}" },
            { "运行批次", "Run batch" },
            { "绑定协作团队的快捷命令，预填任务最多 4000 字。", "Shortcuts bound to a team allow up to 4000 prefilled characters." },
            { "在当前路径交给团队 {0}", "Assign work to team {0} here" },
            { "绑定执行者：伙伴 / 协作团队（可多选）", "Assign to companions or saved teams (select one or more)" },
            { "右键当前文件夹，将任务交给伙伴或已保存的协作团队。伙伴打开原生 CLI；团队按已有职责和依赖执行，并在 Agent 工作空间中显示。", "Assign work from the current folder to a companion or saved team. Companions open native CLI; teams follow their saved roles and dependencies in the Agent workspace." },
            { "本次目标交给已有团队，所有成员使用当前目录。保留职责、任务和依赖，不修改团队模板。", "Runs the saved team in this folder. Uses its roles and dependencies; keeps the template unchanged." },
            { "协作团队绑定无效。", "The team binding is invalid." },
            { "团队任务请输入 1–4000 字。", "Enter a team task of 1–4000 characters." },
            { "此协作团队已删除或标识冲突，请重新绑定快捷命令。", "This team is missing or has a conflicting ID. Bind the shortcut again." },
            { "尚未收到团队启动确认，请先查看 Agent 执行记录，避免重复提交。", "Team startup has not been confirmed. Check Agent run history before submitting again." },
            { "团队快捷请求无效。", "The team shortcut request is invalid." },
            { "团队快捷请求已过期，请重新提交。", "This team request has expired. Submit it again." },
            { "此快捷命令或团队绑定已移除，请刷新右键菜单。", "This shortcut or team binding was removed. Refresh the context menu." },
            { "已有协作团队正在运行，请等待结束或先停止当前执行。", "A team is already running. Wait for it to finish or stop it first." },
            { "正在提交…", "Submitting…" },
            { "每条快捷命令需要名称和至少一位伙伴，预填任务最多 8000 字。", "Each command needs a name and at least one companion. Prefilled tasks can contain up to 8,000 characters." },
            { "快捷命令中的伙伴绑定无效。", "A companion binding is invalid." },
            { "请选择右键菜单位置并添加快捷命令。", "Choose a menu location and add a quick command." },
            { "当前工作目录无效。", "The current working directory is invalid." }, { "请输入任务，最多 8000 字。", "Enter a task of up to 8,000 characters." },
            { "快捷任务参数无效。", "Invalid quick-task arguments." },
            { "此快捷命令已停用或伙伴绑定已移除，请刷新右键菜单。", "This command was disabled or its companion binding was removed. Refresh the context menus." },
            { "已暂停派发；正在执行的步骤会继续完成。", "Dispatch is paused. Running steps will finish normally." },
            { "指定星期内间隔", "Weekly interval" }, { "当日重复间隔", "Repeat interval" },
            { "选中一行后点击编辑，可查看或修改完整内容。", "Select a row and click Edit to view or change the full details." },
            { "Agent 预设", "Agent preset" }, { "套用预设", "Apply preset" }, { "执行成员", "Assigned member" },
            { "青衡 · 规划 Agent / 统筹", "Qingheng · Planning Agent" }, { "绯笺 · 实施 Agent / 交付", "Feijian · Implementation Agent" },
            { "苍芷 · 审查 Agent / 独立审查", "Cangzhi · Review Agent" }, { "小墨 · 测试 Agent / 验证", "Xiaomo · Test Agent" },
            { "AgentPal · Agent 工作区", "AgentPal · Agent workspace" },
            { "先在“Agent 团队”中新建 Agent 团队并分配任务", "Create an Agent team and assign tasks in the Agent teams tab." },
            { "工作区背景资源缺失，请安装完整新版以修复。", "Workspace background is missing. Install the complete package to restore it." },
            { "依赖任务编号（逗号分隔）", "Dependency IDs (comma-separated)" },
            { "编号需唯一，由 1–32 位英文字母、数字或短横线组成。", "Use a unique ID of 1–32 letters, numbers, or hyphens." },
            { "请填写不重复的成员姓名。", "Enter a unique member name." }, { "请选择已存在的工作目录。", "Choose an existing working directory." },
            { "请填写成员职责。", "Enter the member's role." }, { "请填写任务内容。", "Enter the task instructions." }, { "请选择执行成员。", "Choose a member to run this task." },
            { "请先新建 Agent 团队。", "Create a team first." }, { "请先新增成员。", "Add a team member first." },
            { "请先选中成员行。", "Select a member row first." }, { "请先选中任务行。", "Select a task row first." },
            { "最多允许 {0} 位成员。", "A team can have up to {0} members." }, { "最多允许 {0} 项任务。", "A team can have up to {0} tasks." },
            { "任务 {0} 仍分配给此成员。请先重新分配或删除该任务。", "Task {0} is assigned to this member. Reassign or remove the task first." },
            { "任务 {0} 仍依赖此任务。请先移除该依赖。", "Task {0} depends on this task. Remove the dependency first." },
            { "点击工作区中的 Agent查看实时回复与交接摘要。执行记录会保存在本机。", "Select a companion in the hall to view live output and handoffs. Run history is stored locally." },
            { "共同目标 / 授权范围 / 验收要求（每位 Agent都能收到，不共享完整聊天历史）", "Shared goal, scope and acceptance criteria (sent to each agent; full chats stay separate)" },
            { "到点提醒，不自动执行。关联 Agent 团队仅作为执行入口；补发过期提醒一次。", "Events remind you to act. Team runs require confirmation. Overdue reminders are delivered once." },
            { "每位成员是独立 Claude CLI 会话。最多2个并发；目录相同或互相包含时串行。\n可点击工作目录单元格后使用下方按钮选目录；任务依赖填写编号，以逗号分隔。", "Each member uses a separate Claude CLI session. Up to two agents run at once; overlapping folders run sequentially.\nSelect a working-directory cell, then choose a folder below. Separate dependency IDs with commas." },
            { "创建四 Agent 团队", "Create four-agent team" }, { "执行 Agent 团队任务", "Run team" }, { "保存 Agent 团队配置", "Save team configuration" },
            { "取消运行", "Cancel run" }, { "配置Agent 团队名称、共同目标、成员职责、目录和任务依赖。", "Configure the team name, shared goal, member roles, folders and task dependencies." },
            { "创建一个Agent 团队并填写共同目标。", "Create a team and set a shared goal." },
            { "事件名称", "Event title" }, { "添加事件", "Add event" }, { "删除事件", "Delete event" }, { "保存事件", "Save events" },
            { "一次", "Once" }, { "每日", "Daily" }, { "已处理", "Handled" }, { "待提醒", "Upcoming" },
            { "删除定时任务", "Delete scheduled task" }, { "删除选中的任务和运行记录？", "Delete the selected task and its run history?" },
            { "定时任务未保存", "Task was not saved" }, { "任务工作目录", "Task working directory" },
            { "Claude 默认目录", "Default Claude folder" }, { "继承全局 / CLI 自身配置", "Inherit global settings or Claude's defaults" },
            { "并行", "Parallel" }, { "跳过重叠运行", "Skip overlap" }, { "受阻", "Blocked" },
            { "任务：", "Task:" }, { "Claude 回复：", "Claude reply:" }, { "执行中", "Running" },
            { "等待依赖 / 工位", "Waiting for dependencies" }, { "待命", "Ready" }, { "超时", "Timed out" }, { "已取消", "Cancelled" },
            { "上游失败，已阻断", "Blocked by upstream failure" }, { "上次运行中断", "Previous run interrupted" },
            { "点击成员查看任务、回复和交接数据 · 光点表示已记录的传递关系，不代表实时传输速度", "Select a companion to inspect output and handoffs. Moving markers show recorded connections, not live transfer speed." },
            { "Agent 团队", "Agent teams" }, { "运行记录", "Run records" },
            { "整次时限（分钟）", "Run limit (min)" }, { "单轮时限（分钟）", "Step limit (min)" },
            { "0 表示单轮不限时", "0 means no per-step limit" }, { "单轮不限时", "No per-step limit" },
            { "0 表示不限时", "0 means unlimited" }, { "整次不限时", "No overall run limit" },
            { "失败时由其他成员接手", "Hand off failed tasks to another member" },
            { "等待其他成员接手", "Waiting for another member to take over" },
            { "失败接手：{0} → {1}", "Recovery handoff: {0} → {1}" },
            { "需要用户处理", "Needs user input" },
            { "整次最多 {0} 分钟", "Run limit: {0} min" }, { "单轮最多 {0} 分钟", "Step limit: {0} min" },
            { "请填写有效的协作时限。", "Enter valid run and step time limits." },
            { "将调用本机 Claude 执行 {0} 项任务，可能读写以下目录并消耗模型额度。", "Claude will run {0} tasks locally. It may read or write the folders below and incur usage." },
            { "确认开始？", "Start the run?" },
            { "已达到整次协作时限。", "The run reached its time limit." }, { "已达到单轮时限。", "The step reached its time limit." },
            { "暂无运行记录", "No runs yet" }, { "等待分派任务…", "Waiting for task assignments…" },
            { "Agent 团队调度", "Task assignment" }, { "交给 {0} · {1}", "Assigned to {0} · {1}" },
            { "交接给 {0} · {1}", "Handoff to {0} · {1}" }, { "正在回复…", "Replying…" },
            { "成功", "Succeeded" }, { "未执行", "Skipped before launch" }, { "已中断", "Interrupted" },
            { "研发 Agent 团队", "Development team" }, { "审查 Agent 团队", "Review team" }, { "保存 Agent 团队", "Save team" },
            { "为选中成员选目录", "Choose member folder" }, { "套用到选中成员", "Apply role to member" },
            { "成员编号", "Member ID" }, { "姓名", "Name" }, { "职责 / 系统提示", "Role / instructions" },
            { "任务编号", "Task ID" }, { "依赖任务编号", "Dependency IDs" }, { "任务内容", "Task instructions" },
            { "事件", "Event" }, { "本地时间，如2026-11-05 09:00", "Local time, e.g. 2026-11-05 09:00" },
            { "重复：once/daily/weekly", "Repeat: once/daily/weekly" }, { "关联 Agent 团队名称（可空）", "Linked team (optional)" },
            { "启动agent-pal", "Launch agent-pal" }, { "安装完成。", "Installation complete." }, { "原安装已备份到：", "Previous installation saved at:" }
            ,{ "全屏漫游", "Full-screen roaming" }, { "AI 路线和主动发言会使用你配置的 Claude 服务，并可能产生用量。", "AI routes and proactive messages use your configured Claude service and may incur usage." }
        };
        private static readonly Dictionary<string, string> Reverse = BuildReverse();
    }
}
