[English](GITHUB-PUBLISH.md)

# 在 GitHub 发布 agent-pal

目标仓库：<https://github.com/AHuaWinliver/agent-pal>。

本指南只描述发布步骤；源码整理过程不会替你创建仓库、提交、推送或创建 Release。

## 准备发布目录

优先使用 `dist/agent-pal-<version>-Source.zip` 的解压内容作为初次提交目录。它只包含公开文件及校验清单，不含个人设置、任务、聊天、旧素材或备份。

也可以直接提交当前源码根目录中的公开文件，但先检查 `.gitignore` 和 `git status`。不要从外层旧工作区整体上传，更不要提交 `build/`、`dist/`、`work/`、Claude 配置或 API 密钥。

运行检查和构建：

```powershell
.\tools\TestRepository.ps1
.\tests\release-source-test.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
.\tests\smoke-test.ps1
.\tests\drag-gait-test.ps1
.\tools\ExportOpenSource.ps1
```

`ExportOpenSource.ps1` 默认拒绝覆盖已存在的同名源码包。如需重复导出，请使用不同的 `-Destination` 文件名；先检查旧文件，不要误删原版。

## 首次上传

以下命令仅适合新建的本地仓库。如果远程已有提交，请先克隆远程、把整理后的公开文件合入并审阅差异；不要强制覆盖远程历史。

```powershell
git init
git branch -M main
git add .
git status
git diff --cached --stat
git commit -m "Prepare agent-pal release"
git remote add origin https://github.com/AHuaWinliver/agent-pal.git
git push -u origin main
```

`.gitignore` 已为必需构建脚本放行，包括演出预览和 README 素材生成器。推送后查看 Actions 的真实结果，不在 README 手工写“CI 已通过”。

## 建议的仓库展示信息

- **名称**：`agent-pal`
- **About**：`国风 Q 版 AI 桌面伴侣，让 Claude Code 陪你聊天、处理文件与完成定时任务。A lively Windows AI companion powered by your local Claude Code CLI.`
- **Topics**：`ai-companion`、`desktop-pet`、`claude-code`、`windows`、`csharp`、`winforms`、`productivity`、`virtual-pet`。
- **Social preview**：在仓库设置中上传 `docs/images/social-preview.png`。该图采用现有角色素材制作，没有引入外部 IP 图片。
- 主 README 为英文，`README.zh-CN.md` 为简体中文；所有展示图和动图采用仓库相对路径，不依赖本机绝对路径。

## 发布新版本

同步更新 `VERSION`、应用与安装器的程序集版本及变更日志。为通过验证的提交创建 `v<version>` 标签，在 GitHub Release 附上安装包、源码包和 SHA256 校验清单。发布说明以英文为主，并附简体中文。已有版本的附件保留原样。

Linux 发布需完成发行版矩阵及目标机的图形桌面验收。[发布检查清单](RELEASE-CHECKLIST.zh-CN.md)
