
# CryptoWidget - 加密货币实时行情悬浮卡片
# .NET 9 + WPF + Prism.Unity + Websocket.Client
#
# 常用:
#   make dev       - 开发：杀进程 + 构建(Debug) + 运行
#   make watch     - 热部署：启动后监听源码变化，自动重建并重启（按 Q 退出）
#   make bump      - 仅把版本号 +1（patch 位），不打包不提交
#   make dist      - 本地打包安装包（需本地 Inno Setup）：仅 publish -> 打包 -> package/
#                    【不升版本号】测试打包用；升版本请用 /publish-release 或 make release
#   make release   - 发布（云端出包）：升版本 + 写 notes + 提交 + 推送；GitHub 自动打包发 Release
#                    用法: make release NOTES="本次更新内容"
#
# 版本号单一真相源：CryptoWidget.Shell/CryptoWidget.Shell.csproj 里的 <Version>。
# Makefile 自动从这里读取，无需手工同步。

# 解决方案与主程序
SLN      := CryptoWidget.sln
SHELL_PRJ := CryptoWidget.Shell\CryptoWidget.Shell.csproj
APP_NAME := CryptoWidget.Shell.exe

# 输出目录（Debug 配置）
OUT_DIR  := CryptoWidget.Shell\bin\Debug\net9.0-windows
APP_PATH := $(OUT_DIR)\$(APP_NAME)

# 默认配置：Debug
CONFIG   ?= Debug

# 版本号：唯一真相源是 CryptoWidget.Shell/CryptoWidget.Shell.csproj 的 <Version>。
# 发布时由 bump_version.ps1 自动 +1，无需手动改这里。
VERSION  ?= 0.0.1

# Inno Setup 编译器路径（用于打包安装包）。
# 若已将 ISCC.exe 加入系统 PATH，可改为 ISCC ?= iscc
ISCC     ?= "D:\Inno Setup 7\ISCC.exe"

# 发布时默认升版本号的方式：patch（0.6.0 -> 0.6.1 ... 0.6.9 -> 0.7.0 进位）。可覆盖：make MAJOR=1 / make MINOR=1
BUMP_PART ?= patch

# 默认目标：热部署（直接 make 即进入监听重建模式，修改代码自动重启）
.DEFAULT_GOAL := watch

# 杀掉残留的 CryptoWidget 进程（避免锁文件导致构建失败 / 单实例 Mutex 抢占）
.PHONY: kill
kill:
	@echo "[kill] 清理残留 CryptoWidget 进程..."
	@taskkill /F /IM $(APP_NAME) 2>nul || echo "(无运行实例)"
	@echo "[kill] 完成"

# 还原 NuGet 依赖
.PHONY: restore
restore:
	@echo "[restore] 还原 NuGet 依赖..."
	dotnet restore $(SLN)

# 构建解决方案（Debug）
.PHONY: build
build: kill
	@echo "[build] 构建 $(SLN) (Config=$(CONFIG))..."
	dotnet build $(SLN) -c $(CONFIG) --nologo
	@echo "[build] 完成"

# 启动主程序（需先 build）
.PHONY: run
run:
	@echo "[run] 启动 $(APP_PATH)..."
	@if exist "$(APP_PATH)" ( \
		start "" "$(APP_PATH)"; \
	) else ( \
		echo "[run] 未找到 $(APP_PATH)，请先执行: make build"; \
	)

# 一键启动：杀进程 → 构建 → 运行（最常用）
.PHONY: dev
dev: build run
	@echo "[dev] 已启动 CryptoWidget"

# 热部署：启动后监听源码变化，自动重新构建并重启应用（按 Q 退出）
.PHONY: watch
watch:
	powershell -NoProfile -ExecutionPolicy Bypass -File scripts/dev_watch.ps1

# 清理构建产物（含 obj 缓存，解决奇怪的编译问题）
.PHONY: clean
clean: kill
	@echo "[clean] 清理 bin/obj..."
	@if exist "CryptoWidget.Shell\bin"    rmdir /S /Q "CryptoWidget.Shell\bin"
	@if exist "CryptoWidget.Shell\obj"    rmdir /S /Q "CryptoWidget.Shell\obj"
	@if exist "CryptoWidget.Services\bin" rmdir /S /Q "CryptoWidget.Services\bin"
	@if exist "CryptoWidget.Services\obj" rmdir /S /Q "CryptoWidget.Services\obj"
	@if exist "CryptoWidget.Common\bin"   rmdir /S /Q "CryptoWidget.Common\bin"
	@if exist "CryptoWidget.Common\obj"   rmdir /S /Q "CryptoWidget.Common\obj"
	@if exist "CryptoWidget.Models\bin"   rmdir /S /Q "CryptoWidget.Models\bin"
	@if exist "CryptoWidget.Models\obj"   rmdir /S /Q "CryptoWidget.Models\obj"
	@if exist "_publish"                  rmdir /S /Q "_publish"
	@echo "[clean] 完成"

# 升版本号（非交互）：把 csproj 的 <Version> 自动 +1。
# 默认 patch 位；可用 make bump MAJOR=1 / make bump MINOR=1 切换。
# 版本号唯一真相源就是 csproj 的 <Version>，无需手动同步 Makefile。
.PHONY: bump
bump:
	powershell -NoProfile -ExecutionPolicy Bypass -File scripts/bump_version.ps1 $(BUMP_PART)

# 打包发布：自包含发布到临时目录 _publish（含 .NET 运行时，用户无需单独安装）
# 注意：这一步只是"原料"，真正对外发布的是下面的 dist 安装包。
.PHONY: publish
publish: kill
	@echo "[publish] 自包含发布到 _publish/ (win-x64)..."
	dotnet publish $(SHELL_PRJ) -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o _publish
	@echo "[publish] 完成 -> _publish/（中间产物，请勿直接发布）"

# 本地打包安装包（测试用，【不升版本号】）：publish -> Inno Setup 打包 -> 清理。
# 需要本地安装 Inno Setup（ISCC 路径见上方变量）。
# 仅生成 package/CryptoWidget-Setup-x.y.z.exe，不上传。升版本请用 /publish-release 或 make release。
.PHONY: dist
dist:
	dotnet publish $(SHELL_PRJ) -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o _publish
	powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build_installer.ps1 -Iscc $(ISCC)
	@echo "[dist] 安装包已生成 package/CryptoWidget-Setup-*.exe（本地，未发布，版本号未变）"

# 发布（云端出包）：升版本号 + 写 version.json notes + 提交 + 推送。
# 真正的打包与发 Release 由 GitHub 工作流(build.yml)完成。
# 用法: make release NOTES="本次更新内容"  （可选 PART=major|minor|patch，默认 patch）
.PHONY: release
release:
	powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release.ps1 -Notes "$(NOTES)" -Part $(BUMP_PART)

# 打开配置目录 %AppData%\CryptoWidget
.PHONY: config
config:
	@echo "[config] 打开配置目录..."
	@explorer "%APPDATA%\CryptoWidget"

# 打开日志目录
.PHONY: logs
logs:
	@echo "[logs] 打开日志目录..."
	@explorer "%APPDATA%\CryptoWidget\logs"

# 查看可用目标
.PHONY: help
help:
	@echo "CryptoWidget - 加密货币实时行情悬浮卡片"
	@echo.
	@echo "默认目标: make = make watch（热部署，Q 退出）"
	@echo.
	@echo "可用目标:"
	@echo "  dev        - 一键启动：杀进程 + 构建 + 运行（最常用）"
	@echo "  watch      - 热部署：启动后监听源码，修改自动重建重启（按 Q 退出）"
	@echo "  build      - 构建解决方案（Debug）"
	@echo "  run        - 启动主程序（需先 build）"
	@echo "  kill       - 杀掉残留 CryptoWidget 进程（修复构建权限问题）"
	@echo "  restore    - 还原 NuGet 依赖"
	@echo "  clean      - 清理 bin/obj（构建异常时使用）"
	@echo "  publish    - self-contained publish to _publish/ (intermediate, not an installer)"
	@echo "  bump       - bump version +1 (patch by default); use MAJOR=1 / MINOR=1 to switch"
	@echo "  dist       - local installer build (needs Inno Setup): publish -> package/CryptoWidget-Setup-x.y.z.exe (NO version bump)"
	@echo "  release    - publish via CI: bump + write notes + commit + push; GitHub builds & releases"
	@echo "  config     - open %AppData%\CryptoWidget config folder"
	@echo "  logs       - open log folder"
	@echo "  help       - show this help"
	@echo.
	@echo "Release: use 'make release NOTES=\"...\"' (bumps + pushes) or the /publish-release slash command."
	@echo "        'make dist' only builds a local installer WITHOUT bumping the version."
	@echo "Tip: MSB3021 (access denied) -> 'make kill' then 'make build'"
	@echo "Tip: no window after launch -> stale Mutex from prior instance; 'make kill' then retry"
	@echo "Tip: hot reload -> 'make watch' (rebuild & restart on source change, Q to quit)"
